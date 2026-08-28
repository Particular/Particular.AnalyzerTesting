namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

/// <summary>
/// A common base class used by <see cref="AnalyzerTest" /> and <see cref="CodeFixTest" />.
/// </summary>
public partial class BaseAnalyzerTest<TSelf> : BaseCompilationTest<TSelf> where TSelf : BaseAnalyzerTest<TSelf>
{
    private protected readonly List<(string Filename, string MarkupSource)> sources = [];
    private protected readonly List<(string Filename, string Expected)> expectedFixResults = [];
    private protected readonly List<CodeFixProvider> codeFixes = [];
    readonly List<string> commonUsings = [];

    private protected BaseAnalyzerTest(string? outputAssemblyName = null)
        : base(outputAssemblyName)
    {
    }

    /// <summary>
    /// To save typing in tests, specify common namespaces that should be prepended to all code sources in the test.
    /// </summary>
    public TSelf WithCommonUsings(params string[] namespaceNames)
    {
        commonUsings.AddRange(namespaceNames);
        return Self;
    }

    [GeneratedRegex(@"\r?\n", RegexOptions.Compiled)]
    private static partial Regex NewLineRegex();

    private protected Project CreateProject(IEnumerable<SourceFile> codeSources)
    {
        var parseOptions = new CSharpParseOptions(LangVersion)
            .WithFeatures(features);

        var project = new AdhocWorkspace()
            .AddProject(outputAssemblyName, LanguageNames.CSharp)
            .WithParseOptions(parseOptions)
            .WithCompilationOptions(new CSharpCompilationOptions(buildOutputType)
                .WithSyntaxTreeOptionsProvider(CreateSyntaxTreeOptionsProvider()))
            .AddMetadataReferences(References);

        foreach (var source in codeSources)
        {
            project = project.AddDocument(source.Filename, source.Source).Project;
        }

        return project;
    }

    private protected static async Task<Diagnostic[]> GetCompilerDiagnostics(Project project, CancellationToken cancellationToken = default)
    {
        var compilerDiagnostics = (await Task.WhenAll(project.Documents
                .Select(async doc =>
                {
                    var model = await doc.GetSemanticModelAsync(cancellationToken);
                    if (model is null)
                    {
                        return Enumerable.Empty<Diagnostic>();
                    }
                    return model
                        .GetDiagnostics(cancellationToken: cancellationToken)
                        .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                        .OrderBy(diagnostic => diagnostic.Location.SourceSpan)
                        .ThenBy(diagnostic => diagnostic.Id);
                })))
            .SelectMany(diagnostics => diagnostics)
            .ToArray();

        OutputCompilerDiagnostics(compilerDiagnostics);
        return compilerDiagnostics;
    }

    private protected async Task<AnalyzerDiagnosticsResult> GetAnalyzerDiagnostics(Compilation compilation, string[] ignoreDiagnosticIds, bool includeSeveritySuppressed, CancellationToken cancellationToken = default)
    {
        var optionsProvider = AnalyzerConfigOptionsFactory.CreateOptionsProvider(features, editorConfigOptions, editorConfigOptionsByFilename);
        var configuredProvider = CreateSyntaxTreeOptionsProvider();

        var analyzerTasks = analyzers
            .Select(analyzer => compilation.GetAnalyzerDiagnostics(analyzer, optionsProvider, configuredProvider, reportSuppressedDiagnostics: true, cancellationToken))
            .ToArray();

        await Task.WhenAll(analyzerTasks);

        var analyzerDiagnostics = analyzerTasks
            .SelectMany(t => t.Result)
            .Where(d => !ignoreDiagnosticIds.Contains(d.Id))
            .ToArray();

        OutputAnalyzerDiagnostics(analyzerDiagnostics);

        // Suppressed diagnostics (pragma, DiagnosticSuppressor) are reported with IsSuppressed set;
        // they are never visible but were not suppressed by a severity configuration.
        var visibleDiagnostics = analyzerDiagnostics.Where(d => !d.IsSuppressed).ToArray();

        if (!includeSeveritySuppressed)
        {
            return new AnalyzerDiagnosticsResult(visibleDiagnostics, []);
        }

        // Roslyn drops severity-suppressed diagnostics before reporting, even with
        // reportSuppressedDiagnostics enabled. Re-run with a neutral provider to observe what the
        // analyzer reported without severity filtering, so the two cases can be distinguished.
        // The neutral run also strips bulk severity keys from the analyzer config options, since
        // Roslyn applies them through the driver as well.
        var neutralProvider = SyntaxTreeOptionsProviderFactory.CreateNeutral();
        var neutralOptionsProvider = AnalyzerConfigOptionsFactory.CreateNeutralOptionsProvider(features, editorConfigOptions, editorConfigOptionsByFilename);
        var neutralTasks = analyzers
            .Select(analyzer => compilation.GetAnalyzerDiagnostics(analyzer, neutralOptionsProvider, neutralProvider, reportSuppressedDiagnostics: true, cancellationToken))
            .ToArray();

        await Task.WhenAll(neutralTasks);

        var severitySuppressedDiagnostics = neutralTasks
            .SelectMany(t => t.Result)
            .Where(d => !ignoreDiagnosticIds.Contains(d.Id))
            // Exclude diagnostics suppressed through pragma directives or DiagnosticSuppressors:
            // they are reported with IsSuppressed set and are not suppressed by a severity configuration.
            .Where(d => !d.IsSuppressed)
            .Where(d => !visibleDiagnostics.Any(v => v.Id == d.Id && v.Location.SourceSpan == d.Location.SourceSpan && v.Location.SourceTree?.FilePath == d.Location.SourceTree?.FilePath))
            .ToArray();

        return new AnalyzerDiagnosticsResult(visibleDiagnostics, severitySuppressedDiagnostics);
    }

    /// <summary>
    /// Resolve the expected effective severity of a diagnostic id for a source file, mirroring how
    /// Roslyn resolves severity: file-specific, then all-source, then global, then bulk category and
    /// all-analyzer configuration, then the descriptor default.
    /// </summary>
    private protected ReportDiagnostic ResolveExpectedSeverity(string filename, string diagnosticId, DiagnosticDescriptor? descriptor)
    {
        foreach (var (configuredFilename, fileSeverities) in diagnosticSeveritiesByFilename)
        {
            if (FilenameComparer.Matches(configuredFilename, filename) &&
                fileSeverities.TryGetValue(diagnosticId, out var fileSeverity))
            {
                return fileSeverity;
            }
        }

        if (diagnosticSeverities.TryGetValue(diagnosticId, out var allSourceSeverity))
        {
            return allSourceSeverity;
        }

        if (globalDiagnosticSeverities.TryGetValue(diagnosticId, out var globalSeverity))
        {
            return globalSeverity;
        }

        // Bulk configuration only applies to diagnostics enabled by default.
        if (descriptor is { IsEnabledByDefault: true })
        {
            var bulkOptions = new Dictionary<string, string>(editorConfigOptions);

            foreach (var (configuredFilename, fileOptions) in editorConfigOptionsByFilename)
            {
                if (FilenameComparer.Matches(configuredFilename, filename))
                {
                    foreach (var (key, value) in fileOptions)
                    {
                        bulkOptions[key] = value;
                    }
                }
            }

            if (TryGetBulkSeverity(bulkOptions, $"dotnet_analyzer_diagnostic.category-{descriptor.Category}.severity", out var bulkSeverity))
            {
                return bulkSeverity;
            }

            if (TryGetBulkSeverity(bulkOptions, "dotnet_analyzer_diagnostic.severity", out bulkSeverity))
            {
                return bulkSeverity;
            }
        }

        return ReportDiagnostic.Default;
    }

    static bool TryGetBulkSeverity(IReadOnlyDictionary<string, string> options, string key, out ReportDiagnostic severity)
    {
        if (options.TryGetValue(key, out var value) && TryParseSeverity(value, out severity))
        {
            return true;
        }

        severity = ReportDiagnostic.Default;
        return false;
    }

    // Mirrors AnalyzerConfigSet.TryParseSeverity.
    static bool TryParseSeverity(string value, out ReportDiagnostic severity)
    {
        if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            severity = ReportDiagnostic.Default;
            return true;
        }

        if (string.Equals(value, "error", StringComparison.OrdinalIgnoreCase))
        {
            severity = ReportDiagnostic.Error;
            return true;
        }

        if (string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase))
        {
            severity = ReportDiagnostic.Warn;
            return true;
        }

        if (string.Equals(value, "suggestion", StringComparison.OrdinalIgnoreCase))
        {
            severity = ReportDiagnostic.Info;
            return true;
        }

        if (string.Equals(value, "silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "refactoring", StringComparison.OrdinalIgnoreCase))
        {
            severity = ReportDiagnostic.Hidden;
            return true;
        }

        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            severity = ReportDiagnostic.Suppress;
            return true;
        }

        severity = ReportDiagnostic.Default;
        return false;
    }

    private protected static ReportDiagnostic MapDescriptorSeverity(DiagnosticDescriptor descriptor)
        => descriptor.IsEnabledByDefault
            ? MapSeverityToReport(descriptor.DefaultSeverity)
            : ReportDiagnostic.Suppress;

    static ReportDiagnostic MapSeverityToReport(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => ReportDiagnostic.Error,
            DiagnosticSeverity.Warning => ReportDiagnostic.Warn,
            DiagnosticSeverity.Info => ReportDiagnostic.Info,
            DiagnosticSeverity.Hidden => ReportDiagnostic.Hidden,
            _ => ReportDiagnostic.Hidden
        };

    private protected SourceFile CreateFile(string filename, string sourceCode, bool parseDiagnosticMarkup)
    {
        var code = new StringBuilder(sourceCode.Length + (commonUsings.Count * 20));

        if (commonUsings.Count > 0)
        {
            code.AppendLine("#pragma warning disable CS8019 // Unnecessary using directive");
            foreach (var use in commonUsings)
            {
                code.AppendLine($"using {use};");
            }

            code.AppendLine("#pragma warning restore CS8019");
            code.AppendLine();
        }

        if (!parseDiagnosticMarkup)
        {
            code.Append(sourceCode);
            return new SourceFile(filename, code.ToString(), []);
        }

        var markupSpans = new List<TextSpan>();
        var prefixOffset = code.Length;

        var remainingCode = sourceCode;
        var remainingCodeStart = 0;

        while (remainingCode.Length > 0)
        {
            var beforeAndAfterOpening = remainingCode.Split(["[|"], 2, StringSplitOptions.None);

            if (beforeAndAfterOpening.Length == 1)
            {
                _ = code.Append(beforeAndAfterOpening[0]);
                break;
            }

            var midAndAfterClosing = beforeAndAfterOpening[1].Split(["|]"], 2, StringSplitOptions.None);

            if (midAndAfterClosing.Length == 1)
            {
                throw new Exception("The markup code does not contain a closing '|]'");
            }

            var markupSpan = new TextSpan(prefixOffset + remainingCodeStart + beforeAndAfterOpening[0].Length, midAndAfterClosing[0].Length);

            _ = code.Append(beforeAndAfterOpening[0]).Append(midAndAfterClosing[0]);
            markupSpans.Add(markupSpan);

            remainingCode = midAndAfterClosing[1];
            remainingCodeStart += beforeAndAfterOpening[0].Length + markupSpan.Length;
        }

        return new SourceFile(filename, code.ToString(), [.. markupSpans]);
    }

    private protected static void OutputCode(IEnumerable<SourceFile> codeSources)
    {
        if (!AnalyzerTestFixtureState.VerboseLogging)
        {
            return;
        }

        foreach (var source in codeSources)
        {
            TestContext.Out.WriteLine($"// == {source.Filename} ===============================");
            var lines = NewLineRegex().Split(source.Source)
                .Select((line, index) => (line, index))
                .ToImmutableArray();
            var lineNumberSize = (lines.Length + 1).ToString().Length;
            var format = $$"""{0,{{lineNumberSize}}}: {1}""";

            foreach (var (line, index) in lines)
            {
                TestContext.Out.WriteLine(format, index + 1, line);
            }
        }
    }

    static void OutputCompilerDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        if (!AnalyzerTestFixtureState.VerboseLogging)
        {
            return;
        }

        TestContext.Out.WriteLine("Compiler diagnostics:");

        foreach (var diagnostic in diagnostics)
        {
            TestContext.Out.WriteLine($"  {diagnostic}");
        }
    }

    static void OutputAnalyzerDiagnostics(Diagnostic[] analyzerDiagnostics)
    {
        if (!AnalyzerTestFixtureState.VerboseLogging)
        {
            return;
        }

        TestContext.Out.WriteLine("Analyzer diagnostics:");

        foreach (var diagnostic in analyzerDiagnostics)
        {
            TestContext.Out.WriteLine($"  {diagnostic}");
        }
    }

    private protected record SourceFile(string Filename, string Source, TextSpan[] Spans);
    private protected record DiagnosticInfo(string Filename, TextSpan Span, string Id, DiagnosticSeverity Severity);
    private protected record AnalyzerDiagnosticsResult(Diagnostic[] Visible, Diagnostic[] SeveritySuppressed);
}

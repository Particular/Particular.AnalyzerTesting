#nullable enable
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
            .WithCompilationOptions(new CSharpCompilationOptions(buildOutputType))
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
                .Select(doc => doc.GetCompilerDiagnostics(cancellationToken))))
            .SelectMany(diagnostics => diagnostics)
            .ToArray();

        OutputCompilerDiagnostics(compilerDiagnostics);
        return compilerDiagnostics;
    }

    private protected async Task<Diagnostic[]> GetAnalyzerDiagnostics(Compilation? compilation, string[] ignoreDiagnosticIds, CancellationToken cancellationToken = default)
    {
        var analyzerTasks = analyzers
            .Select(analyzer => compilation.GetAnalyzerDiagnostics(analyzer, cancellationToken))
            .ToArray();

        await Task.WhenAll(analyzerTasks);

        var analyzerDiagnostics = analyzerTasks
            .SelectMany(t => t.Result)
            .Where(d => !ignoreDiagnosticIds.Contains(d.Id))
            .ToArray();

        OutputAnalyzerDiagnostics(analyzerDiagnostics);
        return analyzerDiagnostics;
    }

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
                TestContext.Out.WriteLine(string.Format(format, index + 1, line));
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
    private protected record DiagnosticInfo(string Filename, TextSpan Span, string Id);
}
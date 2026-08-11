namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

/// <summary>
/// Test a Roslyn analyzer using a fluent API. Start with <c>CodeFixTest.<see cref="ForAnalyzer" />&lt;TAnalyzer&gt;()</c>.
/// </summary>
public sealed class AnalyzerTest : BaseAnalyzerTest<AnalyzerTest>
{
    static Action<AnalyzerTest>? configureAllTests;

    AnalyzerTest(string? outputAssemblyName = null)
        : base(outputAssemblyName) =>
        configureAllTests?.Invoke(this);

    /// <summary>
    /// Configures all analyzer tests (including those built using <see cref="AnalyzerTest" /> and <see cref="AnalyzerTestFixture&lt;TAnalyzer&gt;" />
    /// in the project by storing a configuration action in a static variable. Use sparingly from within a <see cref="SetUpFixtureAttribute">SetUpFixture</see>.
    /// </summary>
    public static void ConfigureAllAnalyzerTests(Action<AnalyzerTest> configure)
        => configureAllTests = configure;

    /// <summary>
    /// Begin an analyzer test by specifying the analyzer that should be tested.
    /// </summary>
    public static AnalyzerTest ForAnalyzer<TAnalyzer>([CallerMemberName] string? outputAssemblyName = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
        => new AnalyzerTest(outputAssemblyName).WithAnalyzer<TAnalyzer>();

    /// <summary>
    /// Adds a code source to the test, which contains [|diagnostic markers|] to show where the the
    /// analyzer should report diagnostics (squiggles) in the code.
    /// </summary>
    public AnalyzerTest WithSource(string source, string? filename = null)
    {
        filename ??= $"CodeFile{sources.Count:00}.cs";
        sources.Add((filename, source));
        return this;
    }

    /// <summary>
    /// Assert that the analyzer detects the expected diagnostic ids.
    /// </summary>
    public Task AssertDiagnostics(params string[] expectedDiagnosticIds) => AssertDiagnostics(expectedDiagnosticIds, []);

    /// <summary>
    /// Assert that the analyzer detects the expected diagnostic ids. Diagnostics whose severity is
    /// configured to 'none' (for example through <c>WithDiagnosticSeverity</c>) are not considered
    /// reported, since they are not visible to the user.
    /// </summary>
    public async Task AssertDiagnostics(string[] expectedDiagnosticIds, string[] ignoreDiagnosticIds)
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var codeSources = sources.Select(s => CreateFile(s.Filename, s.MarkupSource, parseDiagnosticMarkup: true))
            .ToImmutableArray();
        OutputCode(codeSources);

        var project = CreateProject(codeSources);
        _ = await GetCompilerDiagnostics(project, cancellationToken);

        var compilation = await project.GetCompilationAsync(cancellationToken) ?? throw new Exception("Result of project compilation is null");
        compilation.Compile(!suppressCompilationErrors);

        var expectedDiagnostics = CreateExpectedDiagnostics(codeSources, expectedDiagnosticIds);

        var analyzerDiagnostics = await GetAnalyzerDiagnostics(compilation, ignoreDiagnosticIds, includeSeveritySuppressed: false, cancellationToken);

        var actualDiagnostics = analyzerDiagnostics.Visible
            .Select(diagnostic => new DiagnosticInfo(
                diagnostic.Location.SourceTree?.FilePath ?? "<null-file>",
                diagnostic.Location.SourceSpan,
                diagnostic.Id,
                diagnostic.Severity));

        try
        {
            Assert.That(actualDiagnostics, Is.EqualTo(expectedDiagnostics));
        }
        catch (AssertionException)
        {
            var withSuppression = await GetAnalyzerDiagnostics(compilation, ignoreDiagnosticIds, includeSeveritySuppressed: true, cancellationToken);

            var suppressedDiagnostics = withSuppression.SeveritySuppressed
                .Select(diagnostic => new DiagnosticInfo(
                    diagnostic.Location.SourceTree?.FilePath ?? "<null-file>",
                    diagnostic.Location.SourceSpan,
                    diagnostic.Id,
                    diagnostic.Severity));

            throw new AssertionException(BuildDiagnosticMismatchMessage(expectedDiagnostics, actualDiagnostics, suppressedDiagnostics));
        }
    }

    /// <summary>
    /// Assert that the analyzer reports the expected diagnostic ids at the [|…|]-marked locations,
    /// but that the diagnostics are suppressed by the configured severity (for example through
    /// <c>WithDiagnosticSeverity(id, ReportDiagnostic.Suppress)</c> or a bulk
    /// <c>dotnet_analyzer_diagnostic.severity</c> EditorConfig option).
    /// This distinguishes "the analyzer did not report a diagnostic" from "the analyzer reported it,
    /// but an .editorconfig-style severity configuration set its severity to none".
    /// Suppression through pragma directives or <c>DiagnosticSuppressor</c> is not treated as
    /// severity suppression. The analyzer must report the diagnostic when no severity is configured;
    /// a self-gating (opt-in) analyzer that only reports when explicitly enabled cannot be verified
    /// with this assertion.
    /// </summary>
    public Task AssertSuppressedDiagnostics(params string[] expectedDiagnosticIds)
        => AssertSuppressedDiagnostics(expectedDiagnosticIds, []);

    /// <summary>
    /// Assert that the analyzer reports the expected diagnostic ids at the [|…|]-marked locations,
    /// but that the diagnostics are suppressed by the configured severity.
    /// </summary>
    public async Task AssertSuppressedDiagnostics(string[] expectedDiagnosticIds, string[] ignoreDiagnosticIds)
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var codeSources = sources.Select(s => CreateFile(s.Filename, s.MarkupSource, parseDiagnosticMarkup: true))
            .ToImmutableArray();
        OutputCode(codeSources);

        var project = CreateProject(codeSources);
        _ = await GetCompilerDiagnostics(project, cancellationToken);

        var compilation = await project.GetCompilationAsync(cancellationToken) ?? throw new Exception("Result of project compilation is null");
        compilation.Compile(!suppressCompilationErrors);

        var analyzerDiagnostics = await GetAnalyzerDiagnostics(compilation, ignoreDiagnosticIds, includeSeveritySuppressed: true, cancellationToken);

        var expectedDiagnostics = CreateExpectedDiagnostics(codeSources, expectedDiagnosticIds);

        var actualDiagnostics = analyzerDiagnostics.SeveritySuppressed
            .Select(diagnostic => new DiagnosticInfo(
                diagnostic.Location.SourceTree?.FilePath ?? "<null-file>",
                diagnostic.Location.SourceSpan,
                diagnostic.Id,
                DiagnosticSeverity.Hidden));

        try
        {
            Assert.That(actualDiagnostics, Is.EqualTo(expectedDiagnostics));
        }
        catch (AssertionException)
        {
            var visibleDiagnostics = analyzerDiagnostics.Visible
                .Select(diagnostic => new DiagnosticInfo(
                    diagnostic.Location.SourceTree?.FilePath ?? "<null-file>",
                    diagnostic.Location.SourceSpan,
                    diagnostic.Id,
                    diagnostic.Severity));

            throw new AssertionException(BuildDiagnosticMismatchMessage(expectedDiagnostics, visibleDiagnostics, actualDiagnostics, suppressionAssertion: true));
        }
    }

    IEnumerable<DiagnosticInfo> CreateExpectedDiagnostics(ImmutableArray<SourceFile> codeSources, string[] expectedDiagnosticIds)
        => codeSources
            .SelectMany(src => src.Spans.Select(span => (src.Filename, span)))
            .SelectMany(src => expectedDiagnosticIds.Select(id => new DiagnosticInfo(src.Filename, src.span, id, ExpectedSeverity(src.Filename, id))));

    DiagnosticSeverity ExpectedSeverity(string filename, string diagnosticId)
    {
        var descriptor = analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics).FirstOrDefault(descriptor => descriptor.Id == diagnosticId);
        var configured = ResolveExpectedSeverity(filename, diagnosticId, descriptor);
        if (configured != ReportDiagnostic.Default)
        {
            return configured switch
            {
                ReportDiagnostic.Default => DiagnosticSeverity.Hidden,
                ReportDiagnostic.Error => DiagnosticSeverity.Error,
                ReportDiagnostic.Warn => DiagnosticSeverity.Warning,
                ReportDiagnostic.Info => DiagnosticSeverity.Info,
                ReportDiagnostic.Hidden => DiagnosticSeverity.Hidden,
                // Suppress: never visible, so the assertion fails.
                ReportDiagnostic.Suppress => DiagnosticSeverity.Hidden,
                _ => DiagnosticSeverity.Hidden
            };
        }

        // Unconfigured: a disabled-by-default descriptor is effectively suppressed.
        return descriptor is null
            ? DiagnosticSeverity.Hidden
            : MapDescriptorSeverity(descriptor) switch
            {
                ReportDiagnostic.Default => DiagnosticSeverity.Hidden,
                ReportDiagnostic.Error => DiagnosticSeverity.Error,
                ReportDiagnostic.Warn => DiagnosticSeverity.Warning,
                ReportDiagnostic.Info => DiagnosticSeverity.Info,
                ReportDiagnostic.Hidden => DiagnosticSeverity.Hidden,
                ReportDiagnostic.Suppress => DiagnosticSeverity.Hidden,
                _ => DiagnosticSeverity.Hidden
            };
    }

    static string BuildDiagnosticMismatchMessage(IEnumerable<DiagnosticInfo> expected, IEnumerable<DiagnosticInfo> actual, IEnumerable<DiagnosticInfo> severitySuppressed, bool suppressionAssertion = false)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        var suppressedArray = severitySuppressed.ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("The analyzer diagnostics did not match the expected diagnostics.");

        foreach (var expectedInfo in expectedArray)
        {
            var exactMatch = actualArray.FirstOrDefault(actualInfo => SameLocation(expectedInfo, actualInfo));
            if (exactMatch is not null)
            {
                if (exactMatch.Severity != expectedInfo.Severity)
                {
                    sb.AppendLine(expectedInfo.Severity == DiagnosticSeverity.Hidden
                        ? $"  {expectedInfo.Id} at {FormatLocation(expectedInfo)} was reported with severity {exactMatch.Severity} instead of being suppressed. Use AssertDiagnostics to assert visible diagnostics."
                        : $"  {expectedInfo.Id} at {FormatLocation(expectedInfo)} was reported with severity {exactMatch.Severity}, but the expected severity is {expectedInfo.Severity}. Configure the severity with WithDiagnosticSeverity or adjust the expected result.");
                }

                continue;
            }

            if (suppressedArray.Any(suppressedInfo => SameLocation(expectedInfo, suppressedInfo)))
            {
                sb.AppendLine($"  {expectedInfo.Id} at {FormatLocation(expectedInfo)} was reported by the analyzer, but is suppressed, most likely because its severity is configured to 'none'. Use AssertSuppressedDiagnostics to assert suppressed diagnostics, or remove the suppression.");
                continue;
            }

            sb.AppendLine(suppressionAssertion
                ? $"  {expectedInfo.Id} at {FormatLocation(expectedInfo)} was not reported by the analyzer even without a severity configuration, so it was not suppressed by severity either. Either it is not emitted at this location, or the analyzer is self-gating (opt-in) and only reports when explicitly enabled."
                : $"  {expectedInfo.Id} at {FormatLocation(expectedInfo)} was not reported by the analyzer.");
        }

        foreach (var actualInfo in actualArray)
        {
            if (!expectedArray.Any(expectedInfo => SameLocation(expectedInfo, actualInfo)))
            {
                sb.AppendLine($"  Unexpected diagnostic {actualInfo.Id} at {FormatLocation(actualInfo)} with severity {actualInfo.Severity}.");
            }
        }

        return sb.ToString();
    }

    static bool SameLocation(DiagnosticInfo left, DiagnosticInfo right)
        => left.Filename == right.Filename && left.Span == right.Span && left.Id == right.Id;

    static string FormatLocation(DiagnosticInfo diagnostic)
        => $"{diagnostic.Filename}({diagnostic.Span.Start},{diagnostic.Span.End})";
}

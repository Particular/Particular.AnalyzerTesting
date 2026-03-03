namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

/// <summary>
/// Test a Roslyn analyzer using a fluent API. Start with <c>CodeFixTest.<see cref="ForAnalyzer" />&lt;TAnalyzer&gt;()</c>.
/// </summary>
public sealed class AnalyzerTest : BaseAnalyzerTest<AnalyzerTest>
{
    static Action<AnalyzerTest>? configureAllTests;

    AnalyzerTest(string? outputAssemblyName = null)
        : base(outputAssemblyName)
    {
        configureAllTests?.Invoke(this);
    }

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
    /// Assert that the analyzer detects the expected diagnostic ids.
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

        var analyzerDiagnostics = await GetAnalyzerDiagnostics(compilation, ignoreDiagnosticIds, cancellationToken);

        var expectedDiagnostics = codeSources.SelectMany(src => src.Spans.Select(span => (src.Filename, span)))
            .SelectMany(src => expectedDiagnosticIds.Select(id => new DiagnosticInfo(src.Filename, src.span, id)));

        var actualDiagnostics = analyzerDiagnostics
            .Select(diagnostic => new DiagnosticInfo(diagnostic.Location.SourceTree?.FilePath ?? "<null-file>", diagnostic.Location.SourceSpan, diagnostic.Id));

        Assert.That(actualDiagnostics, Is.EqualTo(expectedDiagnostics));
    }
}
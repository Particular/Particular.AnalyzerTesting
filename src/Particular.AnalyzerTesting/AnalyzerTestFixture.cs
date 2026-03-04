namespace Particular.AnalyzerTesting;

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// A test fixture that runs multiple tests on source code using the same Roslyn analyzer.
/// </summary>
public class AnalyzerTestFixture<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// The C# version used to compile the test code, defaulting to the highest version for the Roslyn SDK you are using in the test. Override to use a different version.
    /// </summary>
    protected virtual LanguageVersion AnalyzerLanguageVersion { get; init; } = LangVersionHelper.LatestForCurrentRoslynSdk;

    /// <summary>
    /// Override in a test fixture to apply configuration to every test in the fixture.
    /// </summary>
    /// <param name="test"></param>
    protected virtual void ConfigureFixtureTests(AnalyzerTest test) { }

    /// <summary>
    /// Assert that the code raises the expected diagnostic in the locations specified by the [|…|] markup, or raises no diagnostics
    /// if the <c>expectedDiagnsoticId</c> is null.
    /// </summary>
    protected Task Assert(string markupCode, string? expectedDiagnosticId = null, [CallerMemberName] string? outputAssemblyName = null)
        => Assert(markupCode, expectedDiagnosticId is null ? [] : [expectedDiagnosticId], [], true, outputAssemblyName);

    /// <summary>
    /// Assert that the code raises the expected diagnostic in the locations specified by the [|…|] markup.
    /// </summary>
    protected Task Assert(string markupCode, string[] expectedDiagnosticIds, [CallerMemberName] string? outputAssemblyName = null)
        => Assert(markupCode, expectedDiagnosticIds, [], true, outputAssemblyName);

    /// <summary>
    /// Assert that the code raises the expected diagnostic in the locations specified by the [|…|] markup.
    /// </summary>
    protected Task Assert(string markupCode, string[] expectedDiagnosticIds, string[] ignoreDiagnosticIds, [CallerMemberName] string? outputAssemblyName = null)
        => Assert(markupCode, expectedDiagnosticIds, ignoreDiagnosticIds, true, outputAssemblyName);

    /// <summary>
    /// Assert that the code raises the expected diagnostic in the locations specified by the [|…|] markup.
    /// </summary>
    protected Task Assert(string markupCode, string[] expectedDiagnosticIds, string[] ignoreDiagnosticIds, bool mustCompile, [CallerMemberName] string? outputAssemblyName = null)
    {
        var test = AnalyzerTest.ForAnalyzer<TAnalyzer>(outputAssemblyName ?? "TestProject")
            .WithLangVersion(AnalyzerLanguageVersion);

        if (!mustCompile)
        {
            test.SuppressCompilationErrors();
        }

        ConfigureFixtureTests(test);

        foreach (var file in MarkupSplitter.SplitMarkup(markupCode))
        {
            test.WithSource(file.Content, file.Filename);
        }

        return test.AssertDiagnostics(expectedDiagnosticIds, ignoreDiagnosticIds);
    }
}

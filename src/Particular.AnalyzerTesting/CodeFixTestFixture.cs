namespace Particular.AnalyzerTesting;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

/// <summary>
/// A test fixture that runs multiple tests on source code using a Roslyn analyzer and code fix to ensure that the changes
/// made to the source code match the expected results.
/// </summary>
public abstract class CodeFixTestFixture<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>
    /// Override in a test fixture to change the LanguageVersion used to compile the tests.
    /// </summary>
    public virtual LanguageVersion AnalyzerLanguageVersion { get; } = LanguageVersion.CSharp14;

    /// <summary>
    /// Override in a test fixture to apply configuration to every test in the fixture.
    /// </summary>
    /// <param name="test"></param>
    protected virtual void ConfigureFixtureTests(CodeFixTest test) { }

    /// <summary>
    /// Assert that the code fix applied to the original code creates the expected output.
    /// </summary>
    protected async Task Assert(string original, string expected, bool mustCompile = true)
    {
        var originalFiles = MarkupSplitter.SplitMarkup(original).ToDictionary(f => f.Filename);
        var fixedFiles = MarkupSplitter.SplitMarkup(expected).ToDictionary(f => f.Filename);

        NUnit.Framework.Assert.That(originalFiles.Keys, Is.EquivalentTo(fixedFiles.Keys));

        var test = CodeFixTest.ForAnalyzer<TAnalyzer>("TestProject")
            .WithCodeFix<TCodeFix>()
            .WithLangVersion(AnalyzerLanguageVersion);

        if (!mustCompile)
        {
            test.SuppressCompilationErrors();
        }

        ConfigureFixtureTests(test);

        foreach (var file in originalFiles.Values)
        {
            var expectedFile = fixedFiles[file.Filename];
            test.WithSource(file.Content, expectedFile.Content, file.Filename);
        }

        await test.AssertCodeFixes();
    }
}
#nullable enable
namespace Particular.AnalyzerTesting;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using NUnit.Framework;

/// <summary>
/// Test a Roslyn code fix using a fluent API. Start with <code>CodeFixTest.<see cref="ForAnalyzer" /></code>
/// </summary>
public sealed class CodeFixTest : BaseAnalyzerTest<CodeFixTest>
{
    CodeFixTest(string? outputAssemblyName = null)
        : base(outputAssemblyName)
    {
    }

    /// <summary>
    /// Begin a code fix test by specifying the analyzer that should be tested.
    /// </summary>
    public static CodeFixTest ForAnalyzer<TAnalyzer>([CallerMemberName] string? outputAssemblyName = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
        => new CodeFixTest(outputAssemblyName).WithAnalyzer<TAnalyzer>();

    /// <summary>
    /// Specify a Code Fix to make changes to the source.
    /// </summary>
    public CodeFixTest WithCodeFix<TCodeFix>() where TCodeFix : CodeFixProvider, new()
    {
        codeFixes.Add(new TCodeFix());
        return this;
    }

    /// <summary>
    /// Specify a source file and how it should look after the Code Fix is applied.
    /// </summary>
    public CodeFixTest WithSource(string source, string expectedResult, string? filename = null)
    {
        filename ??= $"CodeFile{sources.Count:00}.cs";
        sources.Add((filename, source));
        expectedFixResults.Add((filename, expectedResult));
        return this;
    }

    /// <summary>
    /// Apply the code fixes and assert that the expected results match the modified source code.
    /// </summary>
    public async Task AssertCodeFixes()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var codeSources = sources.Select(s => CreateFile(s.Filename, s.MarkupSource, parseDiagnosticMarkup: false))
            .ToImmutableArray();
        OutputCode(codeSources);

        var expectedResults = expectedFixResults.Select(s => CreateFile(s.Filename, s.Expected, parseDiagnosticMarkup: false))
            .ToImmutableArray();

        var currentSources = codeSources.ToArray();

        while (true)
        {
            var project = CreateProject(currentSources);
            var compilerDiagnostics = await GetCompilerDiagnostics(project, cancellationToken);

            var compilation = await project.GetCompilationAsync(cancellationToken);
            compilation.Compile(!suppressCompilationErrors);

            var analyzerDiagnostics = await GetAnalyzerDiagnostics(compilation, [], cancellationToken);

            if (analyzerDiagnostics.Length == 0)
            {
                break;
            }

            var actions = await GetCodeFixActions(project, analyzerDiagnostics, cancellationToken);
            if (actions.Length == 0)
            {
                break;
            }
            var actionsByDocumentName = actions.ToLookup(a => a.Document.Name);

            List<SourceFile> updatedSources = [];
            foreach (var projectDocument in project.Documents)
            {
                var document = projectDocument;
                var docActions = actionsByDocumentName[document.Name];
                // TODO: Replace Take(1) with First() and remove loop if I keep this
                foreach (var action in docActions.Take(1))
                {
                    var operations = await action.Action.GetOperationsAsync(cancellationToken);
                    var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
                    document = solution.GetDocument(document!.Id);
                    var tmpSrc = await GetUpdatedCode(document!, cancellationToken);
                    _ = tmpSrc;
                }

                if (document is not null)
                {
                    var updatedSource = await GetUpdatedCode(document, cancellationToken);
                    updatedSources.Add(new(projectDocument.Name, updatedSource, []));
                }
            }

            currentSources = updatedSources.ToArray();
        }

        var updatedSourcesByFilename = currentSources.ToDictionary(s => s.Filename);
        foreach (var expectedSource in expectedResults)
        {
            var updated = updatedSourcesByFilename.GetValueOrDefault(expectedSource.Filename);
            Assert.That(updated, Is.Not.Null, $"No updated code for source filename {expectedSource.Filename}");
            Assert.That(updated!.Source, Is.EqualTo(expectedSource.Source).IgnoreLineEndingFormat);
        }
    }

    async Task<(Document Document, CodeAction Action)[]> GetCodeFixActions(Project project, Diagnostic[] diagnostics, CancellationToken cancellationToken)
    {
        var diagnosticsByFile = diagnostics.ToLookup(d => d.Location.SourceTree!.FilePath);
        var fixesById = codeFixes.SelectMany(fix => fix.FixableDiagnosticIds.Select(id => (id, fix)))
            .ToLookup(f => f.id, f => f.fix);

        var actions = new List<(Document Document, CodeAction Action)>();

        foreach (var document in project.Documents)
        {
            foreach (var diagnostic in diagnosticsByFile[document.Name])
            {
                foreach (var fixProvider in fixesById[diagnostic.Id])
                {
                    var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add((document, action)), cancellationToken);
                    await fixProvider.RegisterCodeFixesAsync(context);
                }
            }
        }

        return [.. actions];
    }

    async Task<string> GetUpdatedCode(Document document, CancellationToken cancellationToken)
    {
        var simplifiedDoc = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken);
        var root = await simplifiedDoc.GetSyntaxRootAsync(cancellationToken);
        root = Formatter.Format(root!, Formatter.Annotation, simplifiedDoc.Project.Solution.Workspace, cancellationToken: cancellationToken);
        return root.GetText().ToString();
    }
}
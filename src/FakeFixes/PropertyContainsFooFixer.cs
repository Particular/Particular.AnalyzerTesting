namespace FakeFixes;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

[Shared]
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertyContainsFooFixer))]

public class PropertyContainsFooFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticIds.IdentifierContainsFoo];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;


    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Fixer only fixes one diagnostic so we can assume only one
        var diagnostic = context.Diagnostics.First();

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root?.FindNode(diagnostic.Location.SourceSpan) is not PropertyDeclarationSyntax propertySyntax)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
        {
            return;
        }

        var newName = propertySymbol.Name.Replace("Foo", "Bar");

        var codeAction = CodeAction.Create(
            title: "Rename Foo",
            createChangedSolution: ct => RenameFooToBar(context.Document, propertySymbol, newName, ct),
            equivalenceKey: nameof(RenameFooToBar));

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    public async Task<Solution> RenameFooToBar(Document document, ISymbol propertySymbol, string newName, CancellationToken cancellationToken = default)
    {
        var originalSolution = document.Project.Solution;
        var renameOptions = new SymbolRenameOptions();

        var newSolution = await Renamer.RenameSymbolAsync(originalSolution, propertySymbol, renameOptions, newName, cancellationToken)
            .ConfigureAwait(false);

        return newSolution;
    }
}
namespace FakeAnalyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

[Generator(LanguageNames.CSharp)]
public sealed class DiagnosticEmittingSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntaxTrees = context.CompilationProvider.SelectMany(static (compilation, _) => compilation.SyntaxTrees)
            .WithTrackingName(TrackingNames.SyntaxTrees);

        context.RegisterSourceOutput(syntaxTrees, static (productionContext, tree) =>
        {
            productionContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratorReported,
                Location.Create(tree, new TextSpan(0, 0)),
                tree.FilePath));
        });
    }

    internal static class TrackingNames
    {
        public const string SyntaxTrees = nameof(SyntaxTrees);

        public static string[] All => [SyntaxTrees];
    }
}

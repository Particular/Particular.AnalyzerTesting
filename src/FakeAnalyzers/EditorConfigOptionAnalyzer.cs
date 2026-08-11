namespace FakeAnalyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EditorConfigOptionAnalyzer : DiagnosticAnalyzer
{
    const string OptionName = "nservicebus_enable_message_overload_migration_diagnostics";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.EditorConfigOptionEnabled];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var optionsProvider = context.Options.AnalyzerConfigOptionsProvider;
        if (!optionsProvider.GetOptions(context.Node.SyntaxTree).TryGetValue(OptionName, out var value) || value != "true" || optionsProvider.GlobalOptions.TryGetValue(OptionName, out _))
        {
            return;
        }

        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.EditorConfigOptionEnabled, classDeclaration.Identifier.GetLocation(), classDeclaration.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }
}

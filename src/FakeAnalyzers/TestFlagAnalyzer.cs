namespace FakeAnalyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestFlagAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.TestFlagEnabled];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(startContext =>
        {
            if (!startContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.TestFlag", out var value) || value != "enabled")
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        });
    }

    static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.TestFlagEnabled, classDeclaration.Identifier.GetLocation(), classDeclaration.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }
}
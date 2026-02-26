namespace FakeAnalyzers;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PropertyContainsFooAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.PropertyContainsFoo];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not PropertyDeclarationSyntax propSyntax)
        {
            return;
        }

        if (propSyntax.Identifier.Text.Contains("Foo", StringComparison.OrdinalIgnoreCase))
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.PropertyContainsFoo, propSyntax.Identifier.GetLocation(), propSyntax.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
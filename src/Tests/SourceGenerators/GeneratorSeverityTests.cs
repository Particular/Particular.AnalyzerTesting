namespace Tests;

using System.Linq;
using FakeAnalyzers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class GeneratorSeverityTests
{
    const string SimpleSource = """
                                public class MyClass
                                {
                                }
                                """;

    const string SourceWithFooProperty = """
                                         public class MyClass
                                         {
                                             public string Foo { get; set; }
                                         }
                                         """;

    [Test]
    public void DefaultSeverityIsError()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<DiagnosticEmittingSourceGenerator>()
            .WithSource(SimpleSource)
            .SuppressDiagnosticErrors()
            .Run();

        var diagnostic = result.GeneratorDiagnostics.Single(d => d.Id == DiagnosticIds.GeneratorReported);
        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
    }

    [Test]
    public void SuppressedGeneratorDiagnosticIsNotReported()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<DiagnosticEmittingSourceGenerator>()
            .WithSource(SimpleSource)
            .WithDiagnosticSeverity(DiagnosticIds.GeneratorReported, ReportDiagnostic.Suppress)
            .Run();

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Not.Contain(DiagnosticIds.GeneratorReported));
    }

    [Test]
    public void GeneratorDiagnosticReportedAsWarning()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<DiagnosticEmittingSourceGenerator>()
            .WithSource(SimpleSource)
            .WithDiagnosticSeverity(DiagnosticIds.GeneratorReported, ReportDiagnostic.Warn)
            .Run();

        var diagnostic = result.GeneratorDiagnostics.Single(d => d.Id == DiagnosticIds.GeneratorReported);
        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    }

    [Test]
    public void SuppressedAnalyzerDiagnosticIsNotReported()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<DiagnosticEmittingSourceGenerator>()
            .WithAnalyzer<PropertyContainsFooAnalyzer>()
            .WithSource(SourceWithFooProperty)
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress)
            .SuppressDiagnosticErrors()
            .Run();

        Assert.That(result.AnalyzerDiagnostics.Select(d => d.Id), Does.Not.Contain(DiagnosticIds.IdentifierContainsFoo));
    }

    [Test]
    public void AttachedAnalyzerReportsDiagnosticByDefault()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<DiagnosticEmittingSourceGenerator>()
            .WithAnalyzer<PropertyContainsFooAnalyzer>()
            .WithSource(SourceWithFooProperty)
            .SuppressDiagnosticErrors()
            .SuppressCompilationErrors()
            .Run();

        Assert.That(result.AnalyzerDiagnostics.Select(d => d.Id), Contains.Item(DiagnosticIds.IdentifierContainsFoo));
    }
}

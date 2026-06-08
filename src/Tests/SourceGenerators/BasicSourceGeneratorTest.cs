namespace Tests.SourceGenerators;

using System.Linq;
using System.Threading.Tasks;
using FakeAnalyzers;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class BasicSourceGeneratorTest
{
    const string Source = $$"""
                           using System;
                           
                           [AttributeUsage(AttributeTargets.All)]
                           public class MarkerAttribute : Attribute { }
                           
                           [Marker]
                           public class Hello
                           {
                               [Marker]
                               private string there = "foo";
                               
                               [Marker]
                               public DateTime Enjoy { get; set; }
                               
                               public void Use()
                               {
                                   _ = the;
                                   _ = there;
                               }
                               
                               [Marker]
                               private string the;
                               
                               public void DoArguments([Marker] string test, [Marker] Hello results)
                               {
                                  the = test;
                               }
                           }

                           """;

    [Test]
    public async Task BasicTest()
    {
        SourceGeneratorTest.ForIncrementalGenerator<SimpleSourceGenerator>()
            .WithSource(Source)
            .Run()
            .Approve()
            .ToConsole()
            .AssertRunsAreEqual()
            .OutputSteps();
    }

    [Test]
    public void AnalyzerSeesPropertyDuringSourceGeneratorRun()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<SimpleSourceGenerator>()
            .WithAnalyzer<TestFlagAnalyzer>()
            .WithProperty("build_property.TestFlag", "enabled")
            .SuppressCompilationErrors()
            .WithSource(Source)
            .Run();

        Assert.That(result.AnalyzerDiagnostics.Select(diagnostic => diagnostic.Id), Contains.Item(DiagnosticIds.TestFlagEnabled));
    }
}

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

    [Test]
    public void SourceGeneratorSeesEditorConfigOptionsForEachSourceFile()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<EditorConfigOptionSourceGenerator>(["CompilationAndOptions"])
            .WithEditorConfigOption("test_all_source_option", "all-sources")
            .WithEditorConfigOption("test_filename_option", "selected-only", "Selected.cs")
            .WithSource("public class Selected { }", "Selected.cs")
            .WithSource("public class Other { }", "Other.cs")
            .Run();

        var output = result.GetCompilationOutput();
        Assert.That(output, Does.Contain("// Selected.cs: all-source=all-sources; filename=selected-only"));
        Assert.That(output, Does.Contain("// Other.cs: all-source=all-sources; filename=<not-set>"));
    }
}

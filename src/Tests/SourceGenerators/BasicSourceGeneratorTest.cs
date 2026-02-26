namespace Tests.SourceGenerators;

using System.Threading.Tasks;
using FakeAnalyzers;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class BasicSourceGeneratorTest
{
    [Test]
    public async Task BasicTest()
    {
        var source = $$"""
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

        SourceGeneratorTest.ForIncrementalGenerator<SimpleSourceGenerator>()
            .WithSource(source)
            .Approve()
            .ToConsole()
            .AssertRunsAreEqual()
            .OutputSteps();
    }
}
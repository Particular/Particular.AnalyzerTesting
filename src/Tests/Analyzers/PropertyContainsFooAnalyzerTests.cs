namespace Tests;

using System;
using System.Threading.Tasks;
using Particular.AnalyzerTesting;
using FakeAnalyzers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

public class PropertyContainsFooAnalyzerTests
{
    const string Code = """
                        public class MyFoo
                        {
                            public string [|Foo1|] { get; set; }
                            public string [|Foo2|] { get; set; }
                            public string [|Foo3|] { get; set; }
                            public string [|Foo4|] { get; set; }
                            public string [|Foo5|] { get; set; }
                            public string [|Foo6|] { get; set; }
                            
                            public void Method()
                            {
                                Foo1 = "Bar";
                                Foo2 = "Bar";
                                Foo3 = "Bar";
                                Foo4 = "Bar";
                                Foo5 = "Bar";
                                Foo6 = "Bar";
                            }
                        }
                        """;

    [Test]
    public Task Simple() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>("FooProject")
            .WithCommonUsings("System")
            .WithSource(Code, "Code.cs")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public void ThrowAsExe()
    {
        Assert.ThrowsAsync<Exception>(async () =>
        {
            await AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>("FooProject")
                .WithCommonUsings("System")
                .BuildAs(OutputKind.ConsoleApplication)
                .WithSource(Code, "Code.cs")
                .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);
        });
    }
}
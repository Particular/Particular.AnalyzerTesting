namespace Tests.Fixers;

using System.Threading.Tasks;
using FakeAnalyzers;
using FakeFixes;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class PropertyContainsFooFixerTests : CodeFixTestFixture<PropertyContainsFooAnalyzer, PropertyContainsFooFixer>
{
    const string code = """
                        public class MyFoo
                        {
                            public string Foo1 { get; set; }
                            public string Foo2 { get; set; }
                            public string Foo3 { get; set; }
                            public string Foo4 { get; set; }
                            public string Foo5 { get; set; }
                            public string Foo6 { get; set; }
                            
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

    const string expected = """
                            public class MyFoo
                            {
                                public string Bar1 { get; set; }
                                public string Bar2 { get; set; }
                                public string Bar3 { get; set; }
                                public string Bar4 { get; set; }
                                public string Bar5 { get; set; }
                                public string Bar6 { get; set; }
                                
                                public void Method()
                                {
                                    Bar1 = "Bar";
                                    Bar2 = "Bar";
                                    Bar3 = "Bar";
                                    Bar4 = "Bar";
                                    Bar5 = "Bar";
                                    Bar6 = "Bar";
                                }
                            }
                            """;

    protected override void ConfigureFixtureTests(CodeFixTest test) =>
        // Just to call it somewhere
        test.WithProperty("TotallyIgnored", "");

    [Test]
    public Task FixUsingBaseApi() => CodeFixTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
        .WithCodeFix<PropertyContainsFooFixer>()
        .WithSource(code, expected, "Code.cs")
        .AssertCodeFixes();

    [Test]
    public Task FixUsingTestFixtureAssert() => Assert(code, expected);
}
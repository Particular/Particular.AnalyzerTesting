namespace Tests.Fixers;

using System.Threading.Tasks;
using FakeAnalyzers;
using FakeFixes;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class SuppressedDiagnosticFixerTests
{
    const string code = """
                        public class MyFoo
                        {
                            public string Foo1 { get; set; }
                            public string Foo2 { get; set; }
                        }
                        """;

    const string expected = """
                            public class MyFoo
                            {
                                public string Bar1 { get; set; }
                                public string Bar2 { get; set; }
                            }
                            """;

    const string pragmaSuppressedCode = """
                                        #pragma warning disable FAKE0003
                                        public class MyFoo
                                        {
                                            public string Foo1 { get; set; }
                                            public string Foo2 { get; set; }
                                        }
                                        """;

    [Test]
    public Task FixIsAppliedWhenDiagnosticIsNotSuppressed() => CodeFixTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
        .WithCodeFix<PropertyContainsFooFixer>()
        .WithSource(code, expected, "Code.cs")
        .AssertCodeFixes();

    [Test]
    public Task FixIsNotAppliedWhenDiagnosticSeverityIsSuppressed() => CodeFixTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
        .WithCodeFix<PropertyContainsFooFixer>()
        .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress)
        .WithSource(code, code, "Code.cs")
        .AssertCodeFixes();

    [Test]
    public Task FixIsNotAppliedWhenDiagnosticIsPragmaSuppressed() => CodeFixTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
        .WithCodeFix<PropertyContainsFooFixer>()
        .WithSource(pragmaSuppressedCode, pragmaSuppressedCode, "Code.cs")
        .AssertCodeFixes();

    [Test]
    public Task FixIsAppliedOnlyToFileWithoutSeveritySuppression()
    {
        const string one = """
                          public class One
                          {
                              public string Foo1 { get; set; }
                          }
                          """;
        const string two = """
                          public class Two
                          {
                              public string Foo1 { get; set; }
                          }
                          """;
        const string fixedTwo = """
                                public class Two
                                {
                                    public string Bar1 { get; set; }
                                }
                                """;
        return CodeFixTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithCodeFix<PropertyContainsFooFixer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress, "One.cs")
            .WithSource(one, one, "One.cs")
            .WithSource(two, fixedTwo, "Two.cs")
            .AssertCodeFixes();
    }
}

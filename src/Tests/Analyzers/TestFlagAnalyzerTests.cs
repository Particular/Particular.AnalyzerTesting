namespace Tests;

using System.Threading.Tasks;
using FakeAnalyzers;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class TestFlagAnalyzerTests
{
    const string Code = """
                        public class [|MyClass|]
                        {
                        }
                        """;

    [Test]
    public Task ReportsDiagnosticWhenPropertyIsEnabled() =>
        AnalyzerTest.ForAnalyzer<TestFlagAnalyzer>()
            .WithProperty("build_property.TestFlag", "enabled")
            .WithSource(Code)
            .AssertDiagnostics(DiagnosticIds.TestFlagEnabled);
}
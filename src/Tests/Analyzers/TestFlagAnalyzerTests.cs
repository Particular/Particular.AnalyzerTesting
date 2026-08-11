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

    [Test]
    public Task ReportsDiagnosticWhenEditorConfigOptionIsEnabledForAllSources() =>
        AnalyzerTest.ForAnalyzer<EditorConfigOptionAnalyzer>()
            .WithEditorConfigOption("nservicebus_enable_message_overload_migration_diagnostics", "true")
            .WithSource("public class [|First|] { }", "First.cs")
            .WithSource("public class [|Second|] { }", "Second.cs")
            .AssertDiagnostics(DiagnosticIds.EditorConfigOptionEnabled);

    [Test]
    public Task ReportsDiagnosticOnlyForEditorConfigOptionSourceFile() =>
        AnalyzerTest.ForAnalyzer<EditorConfigOptionAnalyzer>()
            .WithEditorConfigOption("nservicebus_enable_message_overload_migration_diagnostics", "true", "Selected.cs")
            .WithSource("public class [|Selected|] { }", "Selected.cs")
            .WithSource("public class Other { }", "Other.cs")
            .AssertDiagnostics(DiagnosticIds.EditorConfigOptionEnabled);
}
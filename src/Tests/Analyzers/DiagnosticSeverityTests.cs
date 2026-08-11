namespace Tests;

using System.Threading.Tasks;
using FakeAnalyzers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class DiagnosticSeverityTests
{
    [Test]
    public Task SeverityChangeMakesDiagnosticVisibleAsWarning() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Warn)
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task SuppressedDiagnosticIsStillReportedByAnalyzer() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress)
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task NothingIsVisibleWhenDiagnosticIsSuppressed() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress)
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics();

    [Test]
    public void AssertSuppressedDiagnosticsThrowsWhenDiagnosticWasNotReported()
    {
        Assert.ThrowsAsync<AssertionException>(async () =>
        {
            await AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
                .WithSource("public class MyClass { public string [|Bar|] { get; set; } }")
                .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);
        });
    }

    [Test]
    public Task FileSpecificSeverityOverridesAllSourceSeverity() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress)
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Warn, "FileA.cs")
            .WithSource("public class MyClass { public string [|FooA|] { get; set; } }", "FileA.cs")
            .WithSource("public class Other { public string FooB { get; set; } }", "FileB.cs")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task AllSourceSeverityOverridesGlobalSeverity() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithGlobalDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Warn)
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress)
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics();

    [Test]
    public Task GlobalSeverityAppliesWhenNoTreeLevelSeverityIsConfigured() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithGlobalDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Warn)
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task FileSpecificSeverityMatchesFilenameCaseInsensitively() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress, "src/Foo.cs")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }", "src/FOO.cs")
            .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task FileSpecificSeverityTreatsDirectorySeparatorsAsEqual() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Suppress, "dir/File.cs")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }", "dir\\File.cs")
            .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public void PragmaSuppressedDiagnosticIsNotReportedButSuppressed()
    {
        // A pragma-suppressed diagnostic is not suppressed by a severity configuration, so
        // AssertSuppressedDiagnostics must not treat it as one.
        Assert.ThrowsAsync<AssertionException>(async () =>
        {
            await AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
                .WithSource("#pragma warning disable FAKE0003\npublic class MyClass { public string [|FooBar|] { get; set; } }")
                .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);
        });
    }

    [Test]
    public Task PragmaSuppressedDiagnosticIsNotVisible() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithSource("#pragma warning disable FAKE0003\npublic class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics();

    [Test]
    public Task WarnSeverityMatchesNormalizedFilename() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Warn, "src/FOO.cs")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }", "src/foo.cs")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task BulkCategorySeveritySetsExpectedSeverity() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.category-Code.severity", "warning")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task BulkCategorySeveritySuppressesDiagnostic() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.category-Code.severity", "none")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task BulkAllAnalyzerSeveritySuppressesDiagnostic() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.severity", "none")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertSuppressedDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task BulkCategorySeverityWinsOverBulkAllAnalyzerSeverity() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.category-Code.severity", "warning")
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.severity", "none")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public Task BulkSeverityNoneHidesDiagnostic() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.severity", "none")
            .WithSource("public class MyClass { public string FooBar { get; set; } }")
            .AssertDiagnostics();

    [Test]
    public Task ExplicitPerRuleDefaultSeverityBlocksBulkSeverity() =>
        AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticIds.IdentifierContainsFoo, ReportDiagnostic.Default)
            .WithEditorConfigOption("dotnet_analyzer_diagnostic.category-Code.severity", "none")
            .WithSource("public class MyClass { public string [|FooBar|] { get; set; } }")
            .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);

    [Test]
    public void SelfGatedDiagnosticIsNotTreatableAsSeveritySuppressed()
    {
        Assert.ThrowsAsync<AssertionException>(async () =>
        {
            await AnalyzerTest.ForAnalyzer<EditorConfigOptionAnalyzer>()
                .WithEditorConfigOption("nservicebus_enable_message_overload_migration_diagnostics", "true")
                .WithSource("public class [|MyClass|] { }")
                .AssertSuppressedDiagnostics(DiagnosticIds.EditorConfigOptionEnabled);
        });
    }

    [Test]
    public Task SeverityConfigurationSuppressesSelfGatedDiagnostic() =>
        AnalyzerTest.ForAnalyzer<EditorConfigOptionAnalyzer>()
            .WithEditorConfigOption("nservicebus_enable_message_overload_migration_diagnostics", "true")
            .WithDiagnosticSeverity(DiagnosticIds.EditorConfigOptionEnabled, ReportDiagnostic.Suppress)
            .WithSource("public class [|MyClass|] { }")
            .AssertSuppressedDiagnostics(DiagnosticIds.EditorConfigOptionEnabled);

    [Test]
    public void AssertSuppressedDiagnosticsExplainsSelfGatingAnalyzer()
    {
        var exception = Assert.ThrowsAsync<AssertionException>(async () =>
        {
            await AnalyzerTest.ForAnalyzer<EditorConfigOptionAnalyzer>()
                .WithSource("public class [|MyClass|] { }")
                .AssertSuppressedDiagnostics(DiagnosticIds.EditorConfigOptionEnabled);
        });

        Assert.That(exception!.Message, Does.Contain("self-gating"));
    }
}

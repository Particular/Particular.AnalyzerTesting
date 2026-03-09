# Particular.AnalyzerTesting

This package contains tools for testing Roslyn analyzers, code fixes, and source generators that are used by the team at [Particular Software](https://particular.net).

## CI

This project's CI is in GitHub Actions. All pull requests are built and tested by the CI.

## Deployment

Tagged versions are automatically pushed to [feedz.io](https://feedz.io/org/particular-software/repository/packages/packages/Particular.AnalyzerTesting). We do not push the package to NuGet.

## Adding to a project

Add the package to your test project's `.csproj` file alongside the `Microsoft.CodeAnalysis.CSharp.Workspaces` package.

```xml
<ItemGroup>
  <PackageReference Include="Particular.AnalyzerTesting" Version="..." />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.0.0" />
</ItemGroup>
```

## Testing multiple Roslyn versions

Roslyn analyzers and code fixes must work correctly across all versions of Roslyn that your users may have installed. To verify this, create multiple test projects that reference the same shared test files but use different versions of `Microsoft.CodeAnalysis.CSharp.Workspaces`.

Define a shared `$(RoslynPackageVersion)` property in the project's `src/Custom.Build.props` file. This makes it easy to manage the minimum supported Roslyn version in one place, and to create additional test projects that target the latest Roslyn version.

```xml
<!-- Custom.Build.props -->
<Project>
  <PropertyGroup>
    <RoslynPackageVersion>4.14.0</RoslynPackageVersion>
  </PropertyGroup>
</Project>
```

> **Remarks:** The `Particular.AnalyzerTesting` package itself uses `PrivateAssets="All"` on its own Roslyn dependency, so the version of Roslyn used at test time is entirely controlled by the `Microsoft.CodeAnalysis.CSharp.Workspaces` version you specify in your test project. The same applies to `NUnit` and `Particular.Approvals`. 

A common pattern is to have a project that uses the minimum supported Roslyn version and another that uses the most recent version:

```
src/
  MyAnalyzer/
  Tests.MinRoslynVersion/
    Tests.MinRoslynVersion.csproj   ← uses $(RoslynPackageVersion)
  Tests.CurrentRoslynVersion/
    Tests.CurrentRoslynVersion.csproj  ← uses the latest Roslyn version explicitly
  SharedTests/
    MyAnalyzerTests.cs              ← shared by both test projects
```

The minimum-version project uses the `$(RoslynPackageVersion)` property:

```xml
<!-- Tests.MinRoslynVersion/Tests.MinRoslynVersion.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="$(RoslynPackageVersion)" />
  <PackageReference Include="Particular.AnalyzerTesting" Version="..." />
</ItemGroup>

<ItemGroup>
  <Compile Include="..\SharedTests\**\*.cs" LinkBase="Shared" />
</ItemGroup>
```

The current-version project pins to a specific newer Roslyn package:

```xml
<!-- Tests.CurrentRoslynVersion/Tests.CurrentRoslynVersion.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.0.0" />
  <PackageReference Include="Particular.AnalyzerTesting" Version="..." />
</ItemGroup>

<ItemGroup>
  <Compile Include="..\SharedTests\**\*.cs" LinkBase="Shared" />
</ItemGroup>
```

Both test projects compile and run exactly the same tests, so any behavior differences between Roslyn versions are immediately detected.

## Testing analyzers

### AnalyzerTestFixture

`AnalyzerTestFixture<TAnalyzer>` is a base class for test fixtures that run multiple tests against the same analyzer. Inherit from it and override `ConfigureFixtureTests` to apply common setup (such as additional source files or common `using` directives) to every test in the fixture.

Use `[|…|]` markup in the source code string to indicate the exact locations where the analyzer should report a diagnostic.

Multiple source files can be specified in a single string by separating them with a line of 5 or more dashes (`-----`). Files can be named by putting a `// Filename.cs` comment as the first line of each section:

```csharp
const string code = """
    // File1.cs
    public class Foo { }
    -----
    // File2.cs
    public class Bar { }
    """;
```

```csharp
public class DateTimeNowAnalyzerTests : AnalyzerTestFixture<DateTimeNowAnalyzer>
{
    protected override void ConfigureFixtureTests(AnalyzerTest test)
    {
        test.WithCommonUsings("System", "System.Threading.Tasks");
    }

    [Test]
    public Task ReportsDiagnosticOnDateTimeNow()
    {
        const string code = """
            public class Foo
            {
                public void Bar()
                {
                    var t = [|DateTime.Now|];
                }
            }
            """;

        return Assert(code, DiagnosticIds.NowUsedInsteadOfUtcNow);
    }

    [Test]
    public Task NoDiagnosticOnDateTimeUtcNow()
    {
        const string code = """
            public class Foo
            {
                public void Bar()
                {
                    var t = DateTime.UtcNow;
                }
            }
            """;

        return Assert(code);
    }
}
```

To configure all analyzer tests in a project (e.g., to add common reference assemblies or source files), use `AnalyzerTest.ConfigureAllAnalyzerTests` from an NUnit `[SetUpFixture]`:

```csharp
[SetUpFixture]
public class SetUpFixture
{
    [OneTimeSetUp]
    public void OneTimeSetup() => AnalyzerTest.ConfigureAllAnalyzerTests(test =>
    {
        test.WithCommonUsings("System", "System.Threading.Tasks");
    });
}
```

### AnalyzerTest

For more control, use the `AnalyzerTest` fluent API directly. Start with `AnalyzerTest.ForAnalyzer<TAnalyzer>()` and chain the configuration methods before calling `AssertDiagnostics`.

```csharp
[Test]
public Task Simple() =>
    AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
        .WithCommonUsings("System")
        .WithSource("""
            public class MyFoo
            {
                public string [|Foo1|] { get; set; }
                public string [|Foo2|] { get; set; }
            }
            """, "Code.cs")
        .AssertDiagnostics(DiagnosticIds.IdentifierContainsFoo);
```

Multiple source files can be added with separate `WithSource` calls.

**`AnalyzerTest` fluent methods**

| Method | Description |
|---|---|
| `ForAnalyzer<TAnalyzer>()` | Start a test for the specified analyzer. |
| `WithSource(source, filename)` | Add a source file to compile. Use `[|…|]` markup to indicate expected diagnostic locations. |
| `WithCommonUsings(namespaces)` | Prepend `using` directives to all source files, reducing noise in test code. |
| `WithLangVersion(langVersion)` | Set the C# language version for the compilation (defaults to the highest version supported by the Roslyn SDK in use). |
| `AddReferences(references)` | Add metadata references to the compilation. |
| `BuildAs(outputKind)` | Change the compilation output kind (defaults to `DynamicallyLinkedLibrary`). |
| `SuppressCompilationErrors()` | Ignore compilation errors, useful when testing analyzers that run on code that does not compile. |
| `WithInterceptorNamespace(ns)` | Add an interceptors namespace feature flag to the compilation. |
| `WithProperty(name, value)` | Add an arbitrary MSBuild-style build property (feature flag) to the compilation. |
| `AssertDiagnostics(expectedDiagnosticIds)` | Run the analyzer and assert that the diagnostics match the `[|…|]`-marked locations. |

## Testing code fixes

### CodeFixTestFixture

`CodeFixTestFixture<TAnalyzer, TCodeFix>` is a base class for test fixtures that verify code fixes. Inherit from it, then call `Assert(original, expected)` to verify that the code fix transforms the original source into the expected output.

```csharp
public class PropertyContainsFooFixerTests : CodeFixTestFixture<PropertyContainsFooAnalyzer, PropertyContainsFooFixer>
{
    const string original = """
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

    [Test]
    public Task FixesProperties() => Assert(original, expected);
}
```

Override `ConfigureFixtureTests` to apply shared configuration (such as additional references) to every test in the fixture.

### CodeFixTest

Use the `CodeFixTest` fluent API when you need more control over the test setup, for example when working with multiple source files.

```csharp
[Test]
public Task FixUsingBaseApi() =>
    CodeFixTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
        .WithCodeFix<PropertyContainsFooFixer>()
        .WithSource(original, expected, "Code.cs")
        .AssertCodeFixes();
```

To configure all code fix tests in a project, use `CodeFixTest.ConfigureAllCodeFixTests` from a `[SetUpFixture]`.

**`CodeFixTest` fluent methods**

| Method | Description |
|---|---|
| `ForAnalyzer<TAnalyzer>()` | Start a test for the specified analyzer. |
| `WithCodeFix<TCodeFix>()` | Specify the code fix provider to apply. |
| `WithSource(source, expectedResult, filename)` | Add a source file and the expected content after the code fix is applied. |
| `WithCommonUsings(namespaces)` | Prepend `using` directives to all source files. |
| `WithLangVersion(langVersion)` | Set the C# language version for the compilation. |
| `AddReferences(references)` | Add metadata references to the compilation. |
| `BuildAs(outputKind)` | Change the compilation output kind. |
| `SuppressCompilationErrors()` | Ignore compilation errors. |
| `WithInterceptorNamespace(ns)` | Add an interceptors namespace feature flag to the compilation. |
| `WithProperty(name, value)` | Add an arbitrary build property to the compilation. |
| `AssertCodeFixes()` | Apply code fixes iteratively and assert that the final source matches the expected output. |

## Testing source generators

### SourceGeneratorTest

Use `SourceGeneratorTest` to test incremental and non-incremental Roslyn source generators. Start with `ForIncrementalGenerator<TGenerator>()` or `ForSourceGenerator<TGenerator>()`, then chain configuration and assertion methods.

The primary assertion method is `Approve()`, which uses [Particular.Approvals](https://github.com/Particular/Particular.Approvals) to snapshot-test the generated output. On the first run it creates a `.received.txt` file, which must be renamed to `.approved.txt` and committed. Subsequent runs compare the received file against the approved file.

```csharp
[Test]
public async Task BasicTest()
{
    var source = """
        using System;

        [AttributeUsage(AttributeTargets.All)]
        public class MarkerAttribute : Attribute { }

        [Marker]
        public class Hello
        {
            [Marker]
            private string there = "foo";
        }
        """;

    SourceGeneratorTest.ForIncrementalGenerator<MySourceGenerator>()
        .WithSource(source)
        .Approve()
        .AssertRunsAreEqual();
}
```

To configure all source generator tests in a project, use `SourceGeneratorTest.ConfigureAllSourceGeneratorTests` from a `[SetUpFixture]`.

**`SourceGeneratorTest` fluent methods**

| Method | Description |
|---|---|
| `ForIncrementalGenerator<TGenerator>(stages)` | Start a test for an `IIncrementalGenerator`. Optionally specify tracking stage names used by `AssertRunsAreEqual`. |
| `ForSourceGenerator<TGenerator>()` | Start a test for a non-incremental `ISourceGenerator`. |
| `WithSource(source, filename)` | Add a source file to compile. |
| `WithIncrementalGenerator<TGenerator>(stages)` | Add an additional incremental source generator to the compilation. |
| `WithSourceGenerator<TGenerator>()` | Add an additional non-incremental source generator to the compilation. |
| `WithSuppressor<TSuppressor>()` | Add a `DiagnosticSuppressor` to the compilation. |
| `WithScenarioName(name)` | Set a scenario name passed to `Approver.Verify`, useful when one test method covers multiple cases. |
| `WithLangVersion(langVersion)` | Set the C# language version for the compilation. |
| `AddReferences(references)` | Add metadata references to the compilation. |
| `BuildAs(outputKind)` | Change the compilation output kind. |
| `SuppressCompilationErrors()` | Ignore compilation warnings and errors. |
| `SuppressDiagnosticErrors()` | Ignore errors raised by the source generator itself. |
| `WithInterceptorNamespace(ns)` | Add an interceptors namespace feature flag to the compilation. |
| `WithProperty(name, value)` | Add an arbitrary build property to the compilation. |
| `Run()` | Run the source generator without running an approval test. |
| `Approve(scrubber)` | Run the generator (if not already run) and perform an approval test on the generated output. |
| `ShouldNotGenerateCode()` | Assert that the source generator produces no output for the given sources. |
| `ToConsole()` | Write the generated output to the test console. Useful during initial development. |
| `AssertRunsAreEqual()` | Run the generator twice and assert that the incremental generator used cached results in the second run, verifying memoization correctness. |
| `GetCompilationOutput(withLineNumbers)` | Return the full generated output as a string. |
| `OutputSteps(specificStages)` | Write incremental generator step information to the test console for debugging. |

## Further Examples

The following repositories contain real-world tests built with `Particular.AnalyzerTesting` and can serve as reference implementations:

- [Particular.Analyzers](https://github.com/Particular/Particular.Analyzers/tree/master/src/SharedTests) – analyzer tests shared across multiple Roslyn version test projects
- [NServiceBus](https://github.com/Particular/NServiceBus/tree/master/src/NServiceBus.Core.Analyzer.Tests.Roslyn5) – analyzer, code fix, and source generator tests for the NServiceBus core analyzer
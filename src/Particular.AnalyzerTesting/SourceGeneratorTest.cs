namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

/// <summary>
/// A tool for testing Roslyn source generators.
/// </summary>
public sealed class SourceGeneratorTest : BaseCompilationTest<SourceGeneratorTest>
{
    readonly List<(string Filename, string Source)> sources = [];
    readonly List<ISourceGenerator> generators = [];
    readonly List<DiagnosticSuppressor> suppressors = [];
    string? scenarioName;
    Compilation? initialCompilation;
    SourceGeneratorBuild? build;
    ImmutableArray<Diagnostic> compilationDiagnostics;
    GeneratorTestOutput outputType;
    readonly Dictionary<string, HashSet<string>> generatorStages = [];
    bool suppressDiagnosticErrors;

    static readonly Type WrapperType = typeof(ISourceGenerator).Assembly.GetType("Microsoft.CodeAnalysis.IncrementalGeneratorWrapper", throwOnError: true)!;
    static readonly MethodInfo GeneratorPropertyGetter = WrapperType.GetProperty("Generator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetMethod!;
    static Action<SourceGeneratorTest>? configureAllTests;

    SourceGeneratorTest(string? outputAssemblyName = null)
        : base(outputAssemblyName) =>
        configureAllTests?.Invoke(this);

    /// <summary>
    /// Configures all source generator tests in the project by storing a configuration action in a static variable.
    /// Use sparingly from within a <see cref="SetUpFixtureAttribute">SetUpFixture</see>.
    /// </summary>
    public static void ConfigureAllSourceGeneratorTests(Action<SourceGeneratorTest> configure)
        => configureAllTests = configure;

    /// <summary>
    /// Start a test for a non-incremental source generator.
    /// </summary>
    public static SourceGeneratorTest ForSourceGenerator<TGenerator>([CallerMemberName] string? outputAssemblyName = null)
        where TGenerator : ISourceGenerator, new()
        => new SourceGeneratorTest(outputAssemblyName).WithSourceGenerator<TGenerator>();

    /// <summary>
    /// Start a test for an incremental source generator.
    /// </summary>
    public static SourceGeneratorTest ForIncrementalGenerator<TGenerator>(string[]? stages = null, [CallerMemberName] string? outputAssemblyName = null)
        where TGenerator : IIncrementalGenerator, new()
        => new SourceGeneratorTest(outputAssemblyName).WithIncrementalGenerator<TGenerator>(stages ?? []);

    /// <summary>
    /// Add a source file to the project.
    /// </summary>
    public SourceGeneratorTest WithSource(string source, string? filename = null)
    {
        filename ??= $"Source{sources.Count:00}.cs";
        sources.Add((filename, source));
        return this;
    }

    /// <summary>
    /// Specify a scenario name that is passed to Approver.Verify for tests that have multiple test cases.
    /// </summary>
    public SourceGeneratorTest WithScenarioName(string name)
    {
        scenarioName = name;
        return this;
    }

    /// <summary>
    /// Add an incremental source generator (<see cref="IIncrementalGenerator" />) to the compilation.
    /// </summary>
    public SourceGeneratorTest WithIncrementalGenerator<TGenerator>(params string[] stages) where TGenerator : IIncrementalGenerator, new()
    {
        if (stages.Length == 0)
        {
            if (TryGetTrackingNames(typeof(TGenerator), out var trackingNames))
            {
                stages = [.. trackingNames];
            }
            else
            {
                throw new Exception("""
                                    To test an incremental generator, either:
                                      1. Provide explicit tracking stage names to the `stages` parameter of WithIncrementalGenerator<TGenerator> that are unique to the specific test.
                                      2. The source generator should have an `internal static class TrackingNames` that the test can discover via reflection, where each stage name is identified by a `public const string`, and there is a property `public static string[] All => [Stage1, Stage2]` that returns all stage names. 
                                    """);
            }
        }

        generatorStages.Add(typeof(TGenerator).FullName!, new HashSet<string>(stages, StringComparer.OrdinalIgnoreCase));
        generators.Add(new TGenerator().AsSourceGenerator());
        return this;
    }

    /// <summary>
    /// Add a non-incremental source generator (<see cref="ISourceGenerator" />) to the compilation.
    /// </summary>
    public SourceGeneratorTest WithSourceGenerator<TGenerator>() where TGenerator : ISourceGenerator, new()
    {
        generators.Add(new TGenerator());
        return this;
    }

    /// <summary>
    /// Add a Roslyn diagnostic suppressor to the compilation.
    /// </summary>
    public SourceGeneratorTest WithSuppressor<TSuppressor>() where TSuppressor : DiagnosticSuppressor, new()
    {
        suppressors.Add(new TSuppressor());
        return this;
    }

    /// <summary>
    /// Do not fail the test if Roslyn diagnostics raise errors.
    /// </summary>
    public SourceGeneratorTest SuppressDiagnosticErrors()
    {
        suppressDiagnosticErrors = true;
        return this;
    }

    /// <summary>
    /// A design-time tool to control the console output of the test to make debugging easier.
    /// </summary>
    /// <param name="output"></param>
    /// <returns></returns>
    public SourceGeneratorTest ControlOutput(GeneratorTestOutput output)
    {
        outputType = output;
        return this;
    }

    /// <summary>
    /// Run the source generator test and return the results.
    /// </summary>
    public SourceGeneratorTestResult Run()
    {
        if (build is not null)
        {
            return CreateResult();
        }

        if (generators.Count == 0)
        {
            throw new Exception("No generators added");
        }

        var parseOptions = new CSharpParseOptions(LangVersion)
            .WithFeatures(features);

        var syntaxTrees = sources
            .Select(src =>
            {
                var tree = CSharpSyntaxTree.ParseText(src.Source, path: src.Filename);
                return tree.WithRootAndOptions(tree.GetRoot(), parseOptions);
            });

        var driverOpts = new GeneratorDriverOptions(
            disabledOutputs: IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);

        var optsProvider = new OptionsProvider(new DictionaryAnalyzerOptions(features));

        var driver = CSharpGeneratorDriver.Create(generators,
            driverOptions: driverOpts,
            optionsProvider: optsProvider,
            parseOptions: parseOptions);

        var compileOpts = new CSharpCompilationOptions(buildOutputType);

        initialCompilation = CSharpCompilation.Create(outputAssemblyName, syntaxTrees, References, compileOpts);

        ImmutableArray<DiagnosticAnalyzer> analyzersToUse = analyzers.Count > 0 ? [.. analyzers] : [new NoOpAnalyzer()];
        ImmutableArray<DiagnosticSuppressor> suppressorsToUse = suppressors.Count > 0 ? [.. suppressors] : [];
        build = new SourceGeneratorBuild(initialCompilation, driver, analyzersToUse, suppressorsToUse);

        try
        {
            if (!suppressDiagnosticErrors)
            {
                Assert.That(build.GeneratorDiagnostics, Has.None.Matches<Diagnostic>(d => d.Severity >= DiagnosticSeverity.Error));
            }

            compilationDiagnostics = build.OutputCompilation.GetAllDiagnosticsAsync().GetAwaiter().GetResult();

            if (!suppressCompilationErrors)
            {
                Assert.That(compilationDiagnostics, Has.None.Matches<Diagnostic>(d => d.Severity >= DiagnosticSeverity.Warning));
            }

            return CreateResult();
        }
        catch (AssertionException)
        {
            var result = CreateResult();
            _ = result.ToConsole();
            throw;
        }

        SourceGeneratorTestResult CreateResult() => new(build, compilationDiagnostics, scenarioName, outputType, generatorStages);
    }

    internal static Type GetUnderlyingGeneratorType(ISourceGenerator generator)
    {
        var generatorType = generator.GetType();
        if (generatorType != WrapperType)
        {
            return generatorType;
        }

        var innerGenerator = GeneratorPropertyGetter.Invoke(generator, []);
        if (innerGenerator is not null)
        {
            generatorType = innerGenerator.GetType();
        }
        return generatorType;
    }

    static bool TryGetTrackingNames(Type generatorType, out IReadOnlyCollection<string> names)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var trackingNamesType = generatorType.GetNestedType("TrackingNames", BindingFlags.Public | BindingFlags.NonPublic);
        if (trackingNamesType is null)
        {
            names = [];
            return false;
        }

        object? value = null;
        var property = trackingNamesType.GetProperty("All", Flags);
        if (property is not null)
        {
            value = property.GetValue(null);
        }
        else
        {
            var field = trackingNamesType.GetField("All", Flags);
            if (field is not null)
            {
                value = field.GetValue(null);
            }
        }

        if (value is IEnumerable<string> enumerable)
        {
            names = [.. enumerable];
            return names.Count > 0;
        }

        names = [];
        return false;
    }

    class OptionsProvider(AnalyzerConfigOptions options) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
        public override AnalyzerConfigOptions GlobalOptions => options;
    }

    internal sealed class DictionaryAnalyzerOptions(Dictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public static DictionaryAnalyzerOptions Empty { get; } = new([]);

        public override bool TryGetValue(string key, out string value)
            => properties.TryGetValue(key, out value!);
    }
}

/// <summary>
/// Options for the output of a source generator either for the console or for an approval test.
/// </summary>
public enum GeneratorTestOutput
{
    /// <summary>
    /// Output only the code generated by the source generator(s).
    /// </summary>
    GeneratedOnly = 0,
    /// <summary>
    /// Output only the source code.
    /// </summary>
    SourceOnly = 1,
    /// <summary>
    /// Output original source code along with code generated by the source generator.
    /// </summary>
    All = 2
}

#pragma warning disable RS1001 // Missing DiagnosticAnalyzer attribute - but we don't actually want it to be "found"
class NoOpAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(static context => { }, SyntaxKind.ModuleKeyword);
    }
}
#pragma warning restore RS1001
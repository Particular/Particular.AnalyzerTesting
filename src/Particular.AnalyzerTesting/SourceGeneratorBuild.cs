namespace Particular.AnalyzerTesting;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

class SourceGeneratorBuild
{
    readonly Compilation initialCompilation;
    readonly GeneratorDriver driver;
    readonly ImmutableArray<DiagnosticAnalyzer> analyzers;
    readonly ImmutableArray<DiagnosticSuppressor> suppressors;

    public SourceGeneratorBuild(Compilation initialCompilation, GeneratorDriver driver, ImmutableArray<DiagnosticAnalyzer> analyzers, ImmutableArray<DiagnosticSuppressor> suppressors)
    {
        this.initialCompilation = initialCompilation;
        this.driver = driver.RunGeneratorsAndUpdateCompilation(initialCompilation, out var outputCompilation, out var generatorDiagnostics);
        this.analyzers = analyzers;
        this.suppressors = suppressors;

        RunResult = this.driver.GetRunResult();

        var allAnalyzers = analyzers.Concat(suppressors).ToImmutableArray();
        OutputCompilation = outputCompilation.WithAnalyzers(allAnalyzers);
        GeneratorDiagnostics = generatorDiagnostics;
    }

    public CompilationWithAnalyzers OutputCompilation { get; }
    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }
    public GeneratorDriverRunResult RunResult { get; }

    public SourceGeneratorBuild Clone()
    {
        var cloneCompilation = initialCompilation.Clone();
        return new SourceGeneratorBuild(cloneCompilation, driver, analyzers, suppressors);
    }
}
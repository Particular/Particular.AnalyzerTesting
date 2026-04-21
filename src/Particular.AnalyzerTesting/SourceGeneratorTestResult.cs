namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Approvals;

/// <summary>
/// The result of running a <see cref="SourceGeneratorTest" />, providing access to diagnostics and output.
/// </summary>
public sealed partial class SourceGeneratorTestResult
{
    readonly SourceGeneratorBuild build;
    readonly ImmutableArray<Diagnostic> compilationDiagnostics;
    readonly string? scenarioName;
    readonly GeneratorTestOutput outputType;
    readonly Dictionary<string, HashSet<string>> generatorStages;
    bool wroteToConsole;
    ImmutableArray<Diagnostic>? analyzerDiagnostics;

    static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal SourceGeneratorTestResult(
        SourceGeneratorBuild build,
        ImmutableArray<Diagnostic> compilationDiagnostics,
        string? scenarioName,
        GeneratorTestOutput outputType,
        Dictionary<string, HashSet<string>> generatorStages)
    {
        this.build = build;
        this.compilationDiagnostics = compilationDiagnostics;
        this.scenarioName = scenarioName;
        this.outputType = outputType;
        this.generatorStages = generatorStages;
    }

    [field: AllowNull, MaybeNull]
    SourceGeneratorBuild ClonedBuild => field ??= build.Clone();

    /// <summary>
    /// Diagnostics reported by the source generator during execution.
    /// </summary>
    public ImmutableArray<Diagnostic> GeneratorDiagnostics => build.GeneratorDiagnostics;

    /// <summary>
    /// Diagnostics reported by analyzers configured on the source generator test after execution.
    /// </summary>
    public ImmutableArray<Diagnostic> AnalyzerDiagnostics
    {
        get
        {
            analyzerDiagnostics ??= build.OutputCompilation.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
            return analyzerDiagnostics.Value;
        }
    }

    /// <summary>
    /// Get the compilation output of the source generator as a string.
    /// </summary>
    public string GetCompilationOutput(bool withLineNumbers = false)
    {
        var sb = new StringBuilder();

        void WriteHeading(string heading)
        {
            if (sb.Length > 0)
            {
                _ = sb.AppendLine();
            }

            var start = $"// == {heading} ==";

            _ = sb.Append(start);

            for (var i = 0; i < 120 - start.Length; i++)
            {
                _ = sb.Append('=');
            }

            _ = sb.AppendLine();
        }

        if (GeneratorDiagnostics.Any())
        {
            WriteHeading("Generator Diagnostics");
            foreach (var diagnostic in GeneratorDiagnostics.Order(DiagnosticSortComparer.Instance))
            {
                var diagnosticString = NormalizeDiagnosticString(diagnostic);
                _ = sb.AppendLine(diagnosticString);
            }
        }

        if (compilationDiagnostics.Any())
        {
            WriteHeading("Compilation Diagnostics");
            foreach (var diagnostic in compilationDiagnostics.Order(DiagnosticSortComparer.Instance))
            {
                var diagnosticString = NormalizeDiagnosticString(diagnostic);
                _ = sb.AppendLine(diagnosticString);
            }
        }

        foreach (var syntaxTree in FilteredSyntaxTrees())
        {
            WriteHeading(syntaxTree.FilePath.Replace('\\', '/'));

            if (withLineNumbers)
            {
                var lines = syntaxTree.GetText().Lines;
                var padSize = lines.Count.ToString().Length;
                foreach (var line in lines)
                {
                    _ = sb.AppendLine($"{(line.LineNumber + 1).ToString().PadLeft(padSize)}: {line.Text?.GetSubText(line.Span)}");
                }
            }
            else
            {
                sb.AppendLine(syntaxTree.ToString());
            }
        }

        return sb.ToString().TrimEnd();
    }

    static string NormalizeDiagnosticString(Diagnostic diagnostic)
    {
        var diagnosticString = diagnostic.ToString();
        return IsWindows switch
        {
            true => diagnosticString.Replace("\\", "/"),
            _ => diagnosticString
        };
    }

    /// <summary>
    /// Run the source generator (if it hasn't been run already) and run an approval test on the results.
    /// </summary>
    public SourceGeneratorTestResult Approve(Func<string, string>? scrubber = null, [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerMemberName = null)
    {
        _ = ToConsole();

        var output = GetCompilationOutput();
        var toApprove = ScrubPlatformSpecificInterceptorData().Replace(output, m => m.Value.Replace(m.Groups["InterceptData"].Value, "{PLATFORM-SPECIFIC-BASE64-DATA}"));
        toApprove = ScrubVersionSpecificAttributeData().Replace(toApprove, m => m.Value.Replace(m.Groups["AssemblyName"].Value, "NService.Core.Analyzer.Tests")
            .Replace(m.Groups["Version"].Value, "1.0.0"));
        Approver.Verify(toApprove, scrubber, scenarioName, callerFilePath, callerMemberName);
        return this;
    }

    /// <summary>
    /// Assert that the source generator should not generate any new code given the current sources.
    /// </summary>
    public SourceGeneratorTestResult ShouldNotGenerateCode()
    {
        var generatedOutputs = build.OutputCompilation.Compilation.SyntaxTrees
            .Where(tree => tree.FilePath.EndsWith(".g.cs"))
            .ToImmutableArray();

        Assert.That(generatedOutputs, Is.Empty);
        return this;
    }

    [GeneratedRegex("""System\.Runtime\.CompilerServices\.InterceptsLocationAttribute\(1, "(?<InterceptData>[A-Za-z0-9+=/]+)"\)""", RegexOptions.Compiled | RegexOptions.NonBacktracking)]
    private static partial Regex ScrubPlatformSpecificInterceptorData();

    [GeneratedRegex("""System\.CodeDom\.Compiler\.GeneratedCodeAttribute\("(?<AssemblyName>[^"]+)",\s*"(?<Version>[^"]+)"\)""", RegexOptions.Compiled | RegexOptions.NonBacktracking)]
    private static partial Regex ScrubVersionSpecificAttributeData();

    /// <summary>
    /// Run the source generator and write the results to the Console. Most useful for initial development of a source generator.
    /// </summary>
    /// <returns></returns>
    public SourceGeneratorTestResult ToConsole()
    {
        if (wroteToConsole)
        {
            return this;
        }

        if (AnalyzerTestFixtureState.VerboseLogging)
        {
            var output = GetCompilationOutput(true);
            TestContext.Out.WriteLine(output);
            wroteToConsole = true;
        }

        return this;
    }

    /// <summary>
    /// Assert that duplicate runs of the source generator used cached outputs to ensure performance.
    /// </summary>
    public SourceGeneratorTestResult AssertRunsAreEqual()
    {
        if (generatorStages.Count == 0)
        {
            throw new Exception("""
                                Must add generator stages first. Either:
                                  1. Provide explicit tracking stage names to WithIncrementalGenerator<TGenerator> on the SourceGeneratorTest.
                                  2. The source generator should have an internal static class TrackingNames with a public static string[] All property returning all stage names.
                                """);
        }

        var trackedSteps1 = GetTracked(build.RunResult);
        var trackedSteps2 = GetTracked(ClonedBuild.RunResult);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trackedSteps1, Is.Not.Empty);
            Assert.That(trackedSteps2, Has.Count.EqualTo(trackedSteps1.Count));
            Assert.That(trackedSteps1.Keys, Is.EquivalentTo(trackedSteps2.Keys));
        }

        foreach (var stepsPerGenerator in trackedSteps1)
        {
            using (Assert.EnterMultipleScope())
            {
                foreach ((string key, ImmutableArray<IncrementalGeneratorRunStep> runStep1) in stepsPerGenerator.Value)
                {
                    var runStep2 = trackedSteps2[stepsPerGenerator.Key][key];
                    AssertStepsAreEqual(key, runStep1, runStep2);
                }
            }
        }

        return this;

        Dictionary<string, Dictionary<string, ImmutableArray<IncrementalGeneratorRunStep>>> GetTracked(GeneratorDriverRunResult result) =>
            result.Results.GroupBy(x => SourceGeneratorTest.GetUnderlyingGeneratorType(x.Generator).FullName!)
                .ToDictionary(g => g.Key, g =>
                    g.SelectMany(r => r.TrackedSteps.Where(step => generatorStages[g.Key].Contains(step.Key))).ToDictionary());
    }

    static void AssertStepsAreEqual(string trackingName, ImmutableArray<IncrementalGeneratorRunStep> steps1, ImmutableArray<IncrementalGeneratorRunStep> steps2)
    {
        Assert.That(steps1, Has.Length.EqualTo(steps2.Length));

        for (var i = 0; i < steps1.Length; i++)
        {
            var step1 = steps1[i];
            var step2 = steps2[i];

            var out1 = step1.Outputs.Select(o => o.Value).ToArray();
            var out2 = step2.Outputs.Select(o => o.Value).ToArray();

            Assert.That(out1, Is.EqualTo(out2).UsingPropertiesComparer(), $"Step '{trackingName}' outputs are not the same between runs, but should be cacheable results.");

            var outputReasons = step2.Outputs.Select(o => o.Reason).ToArray();
            var badReasons = outputReasons.Where(reason => reason is not IncrementalStepRunReason.Cached and not IncrementalStepRunReason.Unchanged).ToArray();

            Assert.That(badReasons, Is.Empty, $"Step '{trackingName}' outputs contain reasons: {string.Join(',', badReasons)}. Should all be Cached or Unchanged to be memoizable.");
        }
    }

    /// <summary>
    /// Output the steps of a source generator to the console for debugging purposes.
    /// </summary>
    public SourceGeneratorTestResult OutputSteps(params ReadOnlySpan<string> specificStages)
    {
        foreach (var result in ClonedBuild.RunResult.Results)
        {
            var generatorType = SourceGeneratorTest.GetUnderlyingGeneratorType(result.Generator);
            TestContext.Out.WriteLine($"## {generatorType.Name} Results");
            TestContext.Out.WriteLine();

            foreach (var stepName in specificStages.Length != 0 ? specificStages : generatorStages[generatorType.FullName!].ToArray())
            {
                var namedStep = result.TrackedSteps[stepName];
                var outputs = namedStep.SelectMany(runStep => runStep.Outputs).ToArray();
                var outputCount = outputs.Length;
                var reasons = outputs.Select(o => o.Reason).GroupBy(reason => reason)
                    .Select(g => $"{g.Count()} {g.Key}")
                    .ToArray();

                TestContext.Out.WriteLine($"Step {stepName} -  {outputCount} total outputs, {string.Join(", ", reasons)}");

                foreach (var output in outputs)
                {
                    TestContext.Out.WriteLine($"- [{output.Reason}] {output.Value}");
                }

                TestContext.Out.WriteLine();
            }
        }

        return this;
    }

    IEnumerable<SyntaxTree> FilteredSyntaxTrees() =>
        outputType switch
        {
            GeneratorTestOutput.All => build.OutputCompilation.Compilation.SyntaxTrees,
            GeneratorTestOutput.GeneratedOnly => build.OutputCompilation.Compilation.SyntaxTrees.Where(t => t.FilePath.EndsWith(".g.cs")),
            GeneratorTestOutput.SourceOnly => build.OutputCompilation.Compilation.SyntaxTrees.Where(t => !t.FilePath.EndsWith(".g.cs")),
            _ => throw new ArgumentOutOfRangeException()
        };
}
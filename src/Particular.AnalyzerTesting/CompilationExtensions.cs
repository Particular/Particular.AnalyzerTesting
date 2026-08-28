namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

static class CompilationExtensions
{
    extension(Compilation compilation)
    {
        public void Compile(bool throwOnFailure = true)
        {
            using var peStream = new MemoryStream();
            var emitResult = compilation.Emit(peStream);

            if (emitResult.Success)
            {
                return;
            }

            if (throwOnFailure)
            {
                throw new Exception("Compilation failed.");
            }

            Debug.WriteLine("Compilation failed.");
        }

        public async Task<IEnumerable<Diagnostic>> GetAnalyzerDiagnostics(
            DiagnosticAnalyzer analyzer,
            AnalyzerConfigOptionsProvider optionsProvider,
            SyntaxTreeOptionsProvider syntaxTreeOptionsProvider,
            bool reportSuppressedDiagnostics,
            CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();

            var analysisOptions = new CompilationWithAnalyzersOptions(
                new AnalyzerOptions([], optionsProvider),
                (exception, _, __) => exceptions.Add(exception),
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: reportSuppressedDiagnostics);

            // Swap in the requested severity provider. The compilation is created by the test with
            // the configured provider, but the neutral provider is used to observe what analyzers
            // report before Roslyn's severity filtering kicks in.
            var compilationWithSeverity = compilation.WithOptions(
                compilation.Options.WithSyntaxTreeOptionsProvider(syntaxTreeOptionsProvider));

            var diagnostics = await compilationWithSeverity
                .WithAnalyzers([analyzer], analysisOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken);

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }

            return diagnostics.Distinct()
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan)
                .ThenBy(diagnostic => diagnostic.Id);
        }
    }
}

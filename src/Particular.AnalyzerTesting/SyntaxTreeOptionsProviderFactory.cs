namespace Particular.AnalyzerTesting;

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

/// <summary>
/// Creates <see cref="SyntaxTreeOptionsProvider" /> implementations that back the diagnostic severity
/// configuration made through <c>WithDiagnosticSeverity</c> and <c>WithGlobalDiagnosticSeverity</c>.
/// Severity resolution mirrors Roslyn: file-specific beats all-source, tree-level beats global,
/// otherwise the descriptor default applies.
/// </summary>
static class SyntaxTreeOptionsProviderFactory
{
    public static SyntaxTreeOptionsProvider Create(
        IReadOnlyDictionary<string, ReportDiagnostic> allSourceSeverities,
        IReadOnlyDictionary<string, Dictionary<string, ReportDiagnostic>>? filenameSeverities = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? globalSeverities = null)
        => new OptionsProvider(
            allSourceSeverities,
            filenameSeverities ?? new Dictionary<string, Dictionary<string, ReportDiagnostic>>(),
            globalSeverities ?? new Dictionary<string, ReportDiagnostic>());

    /// <summary>
    /// A provider that applies no severity configuration, used to observe what analyzers report
    /// before Roslyn's severity filtering kicks in.
    /// </summary>
    public static SyntaxTreeOptionsProvider CreateNeutral()
        => new OptionsProvider(
            new Dictionary<string, ReportDiagnostic>(),
            new Dictionary<string, Dictionary<string, ReportDiagnostic>>(),
            new Dictionary<string, ReportDiagnostic>());

    sealed class OptionsProvider(
        IReadOnlyDictionary<string, ReportDiagnostic> allSourceSeverities,
        IReadOnlyDictionary<string, Dictionary<string, ReportDiagnostic>> filenameSeverities,
        IReadOnlyDictionary<string, ReportDiagnostic> globalSeverities) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken = default) => GeneratedKind.NotGenerated;

#pragma warning disable PS0003 // Make the CancellationToken parameter optional - the base member signature must be matched
        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            if (TryGetFileSpecificSeverity(tree.FilePath, diagnosticId, out severity))
            {
                return true;
            }

            if (allSourceSeverities.TryGetValue(diagnosticId, out severity))
            {
                return true;
            }

            severity = ReportDiagnostic.Default;
            return false;
        }

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            if (globalSeverities.TryGetValue(diagnosticId, out severity))
            {
                return true;
            }

            severity = ReportDiagnostic.Default;
            return false;
        }
#pragma warning restore PS0003

        bool TryGetFileSpecificSeverity(string treePath, string diagnosticId, out ReportDiagnostic severity)
        {
            foreach (var (filename, severities) in filenameSeverities)
            {
                if (FilenameComparer.Matches(filename, treePath) && severities.TryGetValue(diagnosticId, out severity))
                {
                    return true;
                }
            }

            severity = ReportDiagnostic.Default;
            return false;
        }
    }
}

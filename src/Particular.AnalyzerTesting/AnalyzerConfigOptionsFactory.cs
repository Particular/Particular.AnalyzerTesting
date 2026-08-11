namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

static class AnalyzerConfigOptionsFactory
{
    public static AnalyzerConfigOptionsProvider CreateOptionsProvider(
        IReadOnlyDictionary<string, string> globalProperties,
        IReadOnlyDictionary<string, string>? sourceProperties = null,
        IReadOnlyDictionary<string, Dictionary<string, string>>? sourceFileProperties = null)
        => new OptionsProvider(globalProperties, sourceProperties ?? new Dictionary<string, string>(), sourceFileProperties ?? new Dictionary<string, Dictionary<string, string>>(), excludeBulkSeverityKeys: false);

    /// <summary>
    /// Like <see cref="CreateOptionsProvider" />, but the syntax-tree options omit bulk analyzer
    /// severity keys (<c>dotnet_analyzer_diagnostic.severity</c> and
    /// <c>dotnet_analyzer_diagnostic.category-…severity</c>). Used for the neutral run that observes
    /// what analyzers report before Roslyn's severity filtering kicks in.
    /// </summary>
    public static AnalyzerConfigOptionsProvider CreateNeutralOptionsProvider(
        IReadOnlyDictionary<string, string> globalProperties,
        IReadOnlyDictionary<string, string>? sourceProperties = null,
        IReadOnlyDictionary<string, Dictionary<string, string>>? sourceFileProperties = null)
        => new OptionsProvider(globalProperties, sourceProperties ?? new Dictionary<string, string>(), sourceFileProperties ?? new Dictionary<string, Dictionary<string, string>>(), excludeBulkSeverityKeys: true);

    sealed class OptionsProvider(
        IReadOnlyDictionary<string, string> globalProperties,
        IReadOnlyDictionary<string, string> sourceProperties,
        IReadOnlyDictionary<string, Dictionary<string, string>> sourceFileProperties,
        bool excludeBulkSeverityKeys) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            var properties = new Dictionary<string, string>(globalProperties);
            AddProperties(properties, sourceProperties);

            foreach (var (filename, fileProperties) in sourceFileProperties)
            {
                if (FilenameComparer.Matches(filename, tree.FilePath))
                {
                    AddProperties(properties, fileProperties);
                }
            }

            return new DictionaryAnalyzerConfigOptions(properties);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => new DictionaryAnalyzerConfigOptions(globalProperties);

        public override AnalyzerConfigOptions GlobalOptions { get; } = new DictionaryAnalyzerConfigOptions(globalProperties);

        void AddProperties(Dictionary<string, string> destination, IReadOnlyDictionary<string, string> source)
        {
            foreach (var (key, value) in source)
            {
                if (excludeBulkSeverityKeys && IsBulkSeverityKey(key))
                {
                    continue;
                }

                destination[key] = value;
            }
        }

        static bool IsBulkSeverityKey(string key)
            => key.StartsWith("dotnet_analyzer_diagnostic.", StringComparison.Ordinal) &&
               key.EndsWith(".severity", StringComparison.Ordinal);
    }

    sealed class DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
            => properties.TryGetValue(key, out value!);
    }
}
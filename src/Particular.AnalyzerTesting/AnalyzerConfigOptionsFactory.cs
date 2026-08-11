namespace Particular.AnalyzerTesting;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

static class AnalyzerConfigOptionsFactory
{
    public static AnalyzerConfigOptionsProvider CreateOptionsProvider(
        IReadOnlyDictionary<string, string> globalProperties,
        IReadOnlyDictionary<string, string>? sourceProperties = null,
        IReadOnlyDictionary<string, Dictionary<string, string>>? sourceFileProperties = null)
        => new OptionsProvider(globalProperties, sourceProperties ?? new Dictionary<string, string>(), sourceFileProperties ?? new Dictionary<string, Dictionary<string, string>>());

    public static AnalyzerOptions CreateAnalyzerOptions(
        IReadOnlyDictionary<string, string> globalProperties,
        IReadOnlyDictionary<string, string>? sourceProperties = null,
        IReadOnlyDictionary<string, Dictionary<string, string>>? sourceFileProperties = null)
        => new([], CreateOptionsProvider(globalProperties, sourceProperties, sourceFileProperties));

    sealed class OptionsProvider(
        IReadOnlyDictionary<string, string> globalProperties,
        IReadOnlyDictionary<string, string> sourceProperties,
        IReadOnlyDictionary<string, Dictionary<string, string>> sourceFileProperties) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            var properties = new Dictionary<string, string>(globalProperties);
            AddProperties(properties, sourceProperties);

            if (sourceFileProperties.TryGetValue(tree.FilePath, out var fileProperties))
            {
                AddProperties(properties, fileProperties);
            }

            return new DictionaryAnalyzerConfigOptions(properties);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => new DictionaryAnalyzerConfigOptions(globalProperties);

        public override AnalyzerConfigOptions GlobalOptions { get; } = new DictionaryAnalyzerConfigOptions(globalProperties);

        static void AddProperties(Dictionary<string, string> destination, IReadOnlyDictionary<string, string> source)
        {
            foreach (var (key, value) in source)
            {
                destination[key] = value;
            }
        }
    }

    sealed class DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
            => properties.TryGetValue(key, out value!);
    }
}
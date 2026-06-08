namespace Particular.AnalyzerTesting;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

static class AnalyzerConfigOptionsFactory
{
    public static AnalyzerConfigOptionsProvider CreateOptionsProvider(IReadOnlyDictionary<string, string> properties)
        => new OptionsProvider(new DictionaryAnalyzerConfigOptions(properties));

    public static AnalyzerOptions CreateAnalyzerOptions(IReadOnlyDictionary<string, string> properties)
        => new([], CreateOptionsProvider(properties));

    sealed class OptionsProvider(AnalyzerConfigOptions options) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
        public override AnalyzerConfigOptions GlobalOptions => options;
    }

    sealed class DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
            => properties.TryGetValue(key, out value!);
    }
}
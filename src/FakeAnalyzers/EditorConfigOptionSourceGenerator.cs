namespace FakeAnalyzers;

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[Generator(LanguageNames.CSharp)]
public sealed class EditorConfigOptionSourceGenerator : IIncrementalGenerator
{
    const string AllSourceOption = "test_all_source_option";
    const string FilenameOption = "test_filename_option";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationAndOptions = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider);
        context.RegisterSourceOutput(compilationAndOptions, static (productionContext, input) =>
        {
            var output = new StringBuilder();

            foreach (var tree in input.Left.SyntaxTrees)
            {
                var options = input.Right.GetOptions(tree);
                var allSourceValue = GetOptionValue(options, AllSourceOption);
                var filenameValue = GetOptionValue(options, FilenameOption);
                output.AppendLine($"// {tree.FilePath}: all-source={allSourceValue}; filename={filenameValue}");
            }

            productionContext.AddSource("EditorConfigOptions.g.cs", output.ToString());
        });
    }

    static string GetOptionValue(AnalyzerConfigOptions options, string name)
        => options.TryGetValue(name, out var value) ? value : "<not-set>";
}

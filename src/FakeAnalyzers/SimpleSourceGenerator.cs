namespace FakeAnalyzers;

using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

[Generator(LanguageNames.CSharp)]
public class SimpleSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var marked = context.SyntaxProvider
            .ForAttributeWithMetadataName("MarkerAttribute",
                predicate: (node, _) => true,
                transform: Parse)
            .Where(static spec => spec.HasValue)
            .Select(static (spec, _) => spec!.Value)
            .WithTrackingName(TrackingNames.MarkedItemSpecs);

        var collected = marked.Collect()
            .WithTrackingName(TrackingNames.CollectedSpec);

        context.RegisterSourceOutput(collected, static (productionContext, spec) =>
        {
            var b = new StringBuilder()
                .AppendLine("// Unnecessary using to generate CS8019 diagnostic and ensure diagnostic output slashes are always / and not \\")
                .AppendLine("using System;")
                .AppendLine()
                .AppendLine("// Marked Items:");

            foreach (var item in spec)
            {
                b.AppendLine($"//   - {item.Name}");
            }

            productionContext.AddSource("MarkedItemsInComment.g.cs", b.ToString());
        });
    }

    static MarkedItemSpec? Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        return new MarkedItemSpec(context.TargetSymbol.Name);
    }

    record struct MarkedItemSpec(string Name);

    internal static class TrackingNames
    {
        public const string MarkedItemSpecs = nameof(MarkedItemSpecs);
        public const string CollectedSpec = nameof(CollectedSpec);

        public static string[] All => [MarkedItemSpecs, CollectedSpec];
    }
}
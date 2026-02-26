namespace Particular.Analyzers
{
    using Microsoft.CodeAnalysis;

    public static class SuppressionDescriptors
    {
        public static readonly SuppressionDescriptor FakeSuppression = new(
            SuppressionIds.FakeSuppression,
            "IDE0060",
            "Fake suppression.");
    }
}

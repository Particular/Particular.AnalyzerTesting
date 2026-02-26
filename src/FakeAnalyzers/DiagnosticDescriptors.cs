namespace FakeAnalyzers
{
    using Microsoft.CodeAnalysis;

    public static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor NowUsedInsteadOfUtcNow = new(
            id: DiagnosticIds.NowUsedInsteadOfUtcNow,
            title: "DateTime.UtcNow or DateTimeOffset.UtcNow should be used instead of DateTime.Now and DateTimeOffset.Now, unless the value is being used for displaying the current date-time in a user's local time zone",
            messageFormat: "Use {0}.UtcNow instead of {0}.Now",
            category: "Code",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AsyncVoid = new(
            id: DiagnosticIds.AsyncVoid,
            title: "Methods should not be declared async void",
            messageFormat: "An `async void` method is almost always a mistake as nothing can be returned to await. Should only be used for event delegates, in which case this rule should be disabled in that instance.",
            category: "Code",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor PropertyContainsFoo = new(
            id: DiagnosticIds.IdentifierContainsFoo,
            title: "Identifiers should not contain Foo",
            messageFormat: "You named something '{0}' which contains Foo. How dare you.",
            category: "Code",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}

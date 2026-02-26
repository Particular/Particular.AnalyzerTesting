namespace Particular.Analyzers;

#if FIXES
static class DiagnosticIds
#else
public static class DiagnosticIds
#endif
{
    public const string NowUsedInsteadOfUtcNow = "FAKE0001";
    public const string AsyncVoid = "FAKE0002";
    public const string IdentifierContainsFoo = "FAKE0003";
}
namespace Particular.AnalyzerTesting;

using System;

/// <summary>
/// Defines how configured filenames are matched against syntax tree file paths for file-scoped
/// EditorConfig options and diagnostic severities. The contract is deterministic across platforms:
/// <c>\</c> and <c>/</c> are equivalent, comparison is case-insensitive on every platform, and
/// matching is exact on the full path (a bare <c>Foo.cs</c> does not match <c>src/Foo.cs</c>).
/// </summary>
static class FilenameComparer
{
    public static bool Matches(string configuredFilename, string syntaxTreePath)
        => string.Equals(Normalize(configuredFilename), Normalize(syntaxTreePath), StringComparison.OrdinalIgnoreCase);

    static string Normalize(string path) => path.Replace('\\', '/');
}

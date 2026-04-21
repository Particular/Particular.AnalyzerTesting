namespace Particular.AnalyzerTesting;

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

sealed class DiagnosticSortComparer : IComparer<Diagnostic>
{
    public static DiagnosticSortComparer Instance { get; } = new();

    public int Compare(Diagnostic? x, Diagnostic? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var xLoc = x.Location;
        var yLoc = y.Location;

        var xInSource = xLoc.IsInSource;
        var yInSource = yLoc.IsInSource;

        var c = yInSource.CompareTo(xInSource);
        if (c != 0)
        {
            return c;
        }

        if (xInSource && yInSource)
        {
            var xPath = NormalizePath(xLoc!.SourceTree?.FilePath);
            var yPath = NormalizePath(yLoc!.SourceTree?.FilePath);

            c = StringComparer.Ordinal.Compare(xPath, yPath);
            if (c != 0)
            {
                return c;
            }

            var xSpan = xLoc.SourceSpan;
            var ySpan = yLoc.SourceSpan;

            c = xSpan.Start.CompareTo(ySpan.Start);
            if (c != 0)
            {
                return c;
            }

            c = xSpan.Length.CompareTo(ySpan.Length);
            if (c != 0)
            {
                return c;
            }
        }

        c = StringComparer.Ordinal.Compare(x.Id, y.Id);
        if (c != 0)
        {
            return c;
        }

        c = x.Severity.CompareTo(y.Severity);
        if (c != 0)
        {
            return c;
        }

        c = StringComparer.Ordinal.Compare(x.GetMessage(), y.GetMessage());
        return c != 0 ? c :
            StringComparer.Ordinal.Compare(x.ToString(), y.ToString());

        static string NormalizePath(string? path) => string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/');
    }
}
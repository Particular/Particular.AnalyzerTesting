namespace Particular.AnalyzerTesting;

using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

static class LangVersionHelper
{
    public static readonly LanguageVersion LatestForCurrentRoslynSdk = Enum.GetValues<LanguageVersion>()
        .Where(value => value.ToString().StartsWith("CSharp"))
        .OrderByDescending(value => value)
        .First();
}
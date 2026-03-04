namespace Tests;

using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Particular.AnalyzerTesting;

public class LangVersionTest
{
    [Test]
    public void TestLangVersionHelper()
    {
        var actual = LangVersionHelper.LatestForCurrentRoslynSdk;

        // C# 13 is the correct value for Roslyn 4, should increment based on the Roslyn SDK used by the consumer
        Assert.That(actual, Is.EqualTo(LanguageVersion.CSharp13));
    }
}
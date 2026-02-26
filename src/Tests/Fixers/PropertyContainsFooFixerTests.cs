namespace Tests.Fixers;

using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Analyzers;
using Particular.AnalyzerTesting;

public class PropertyContainsFooFixerTests
{
    [Test]
    public async Task Fix()
    {
        var code = """
                   public class MyFoo
                   {
                       public string Foo1 { get; set; }
                       public string Foo2 { get; set; }
                       public string Foo3 { get; set; }
                       public string Foo4 { get; set; }
                       public string Foo5 { get; set; }
                       public string Foo6 { get; set; }
                       
                       public void Method()
                       {
                           Foo1 = "Bar";
                           Foo2 = "Bar";
                           Foo3 = "Bar";
                           Foo4 = "Bar";
                           Foo5 = "Bar";
                           Foo6 = "Bar";
                       }
                   }
                   """;

        var expected = """
                       public class MyFoo
                       {
                           public string Bar1 { get; set; }
                           public string Bar2 { get; set; }
                           public string Bar3 { get; set; }
                           public string Bar4 { get; set; }
                           public string Bar5 { get; set; }
                           public string Bar6 { get; set; }
                           
                           public void Method()
                           {
                               Bar1 = "Bar";
                               Bar2 = "Bar";
                               Bar3 = "Bar";
                               Bar4 = "Bar";
                               Bar5 = "Bar";
                               Bar6 = "Bar";
                           }
                       }
                       """;
        await AnalyzerTest.ForAnalyzer<PropertyContainsFooAnalyzer>()
            .WithCodeFix<PropertyContainsFooFixer>()
            .WithCodeFixSource(code, expected, "Code.cs")
            .AssertCodeFixes();
    }
}
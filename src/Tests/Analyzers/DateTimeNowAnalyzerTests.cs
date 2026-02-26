namespace Particular.Analyzers.Tests
{
    using System.Threading.Tasks;
    using AnalyzerTesting;
    using NUnit.Framework;

    public class DateTimeNowAnalyzerTests : AnalyzerTestFixture<DateTimeNowAnalyzer>
    {
        protected override void ConfigureFixtureTests(AnalyzerTest test)
        {
            test.WithSource("""
                            namespace NServiceBus
                            {
                               interface ICancellableContext { }
                               class CancellableContext : ICancellableContext { }
                               interface IMessage { }
                            }
                            """, "ExternalTypes.cs");

            test.WithCommonUsings("System", "System.Threading", "System.Threading.Tasks", "NServiceBus");
        }

        [Test]
        public Task SimpleTest()
        {
            const string code = @"
public class Foo
{
    public void Bar()
    {
        var t1 = [|DateTime.Now|];
        var t2 = [|DateTimeOffset.Now|];
        var t3 = this.Now;
        var t4 = new Foo().Now;

        var other = new Foo();
        var t5 = other.Now;

        Use([|DateTime.Now|]);
        Use([|DateTimeOffset.Now|]);
    }
    public int Now { get; }
    public void Use(DateTime dt) {}
    public void Use(DateTimeOffset dto) {}
}"
;
            return Assert(code, DiagnosticIds.NowUsedInsteadOfUtcNow);
        }
    }
}

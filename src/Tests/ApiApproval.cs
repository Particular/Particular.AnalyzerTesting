namespace Tests;

using NUnit.Framework;
using Particular.AnalyzerTesting;
using Particular.Approvals;
using PublicApiGenerator;

public class ApiApproval
{
    [Test]
    public void ApproveApi()
    {
        var publicApi = typeof(AnalyzerTest).Assembly.GeneratePublicApi(new ApiGeneratorOptions()
        {
            ExcludeAttributes = ["System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute"]
        });

        Approver.Verify(publicApi);
    }
}
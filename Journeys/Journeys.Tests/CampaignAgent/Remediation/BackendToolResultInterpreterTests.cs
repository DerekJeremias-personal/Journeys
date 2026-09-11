using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class BackendToolResultInterpreterTests
{
    [Fact]
    public void TryParse_failure_with_errors_map()
    {
        var json = """{"errors":{"attributes[1].symbol":"Symbol must not exceed 100 characters."},"message":"Validation failed"}""";
        var ok = BackendToolResultInterpreter.TryParse("SaveModel", json, out var failure);
        Assert.True(ok);
        Assert.Equal(BackendToolFailureParseStatus.Success, failure!.ParseStatus);
        Assert.Contains("attributes[1].symbol", failure.ValidationErrors.Keys);
    }

    [Fact]
    public void TryParse_failure_with_validationErrors_camelCase()
    {
        var json = """{"validationErrors":{"modelMetaData.NaturalKeySymbols":"Invalid JSON"},"message":"Bad request"}""";
        var ok = BackendToolResultInterpreter.TryParse("save_model", json, out var failure);
        Assert.True(ok);
        Assert.Single(failure!.ValidationErrors);
    }

    [Fact]
    public void TryParse_not_failure_on_success_payload()
    {
        var ok = BackendToolResultInterpreter.TryParse("SaveModel", """{"id":"abc"}""", out var failure);
        Assert.False(ok);
        Assert.Equal(BackendToolFailureParseStatus.NotFailure, failure!.ParseStatus);
    }
}

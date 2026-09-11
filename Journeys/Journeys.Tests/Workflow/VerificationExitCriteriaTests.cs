using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class VerificationExitCriteriaTests
{
    private readonly VerificationExitCriteria _criteria = new();

    [Fact]
    public void IsMet_false_when_verification_record_empty()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        Assert.False(_criteria.IsMet(s));
    }

    [Fact]
    public void IsMet_false_when_verification_record_is_error_json()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.VerificationRecord = """{"errors":{"processEvent":"HttpRequestException: timeout"}}""";
        Assert.False(_criteria.IsMet(s));
    }

    [Fact]
    public void IsMet_false_when_verification_record_is_mcp_opaque_error()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.VerificationRecord =
            """{"content":[{"type":"text","text":"An error occurred invoking 'process_event'."}],"isError":true}""";
        Assert.False(_criteria.IsMet(s));
    }

    [Fact]
    public void IsMet_true_when_verification_record_is_successful_process_event()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.VerificationRecord = """{"evaluatedCampaigns":["c1"],"status":"processed"}""";
        Assert.True(_criteria.IsMet(s));
    }
}

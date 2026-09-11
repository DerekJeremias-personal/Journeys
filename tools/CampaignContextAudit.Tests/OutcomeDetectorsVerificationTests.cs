using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using CampaignContextAudit.Transcript;
using Xunit;

namespace CampaignContextAudit.Tests;

public class OutcomeDetectorsVerificationTests
{
    [Fact]
    public void DetectVerificationDeferred_when_two_test_intents_and_no_process_event()
    {
        var rows = new List<AgentMessageDoc>
        {
            User(1, "please test with test_exp_01"),
            User(5, "run a test now please"),
        };
        var timeline = new List<ToolEvent>
        {
            new(2, "get_account", "call-1", 100)
        };

        var findings = OutcomeDetectors.DetectVerificationDeferred(rows, timeline);

        Assert.Single(findings);
        Assert.Equal("VERIFICATION_DEFERRED", findings[0].Code);
    }

    [Fact]
    public void DetectVerificationDeferred_false_when_process_event_ran()
    {
        var rows = new List<AgentMessageDoc>
        {
            User(1, "test with test_exp_01"),
            User(5, "run a test"),
        };
        var timeline = new List<ToolEvent>
        {
            new(3, "process_event", "call-1", 100)
        };

        var findings = OutcomeDetectors.DetectVerificationDeferred(rows, timeline);

        Assert.Empty(findings);
    }

    [Fact]
    public void DetectVerificationBlockedNoAllowlist_when_flag_set()
    {
        var workflow = """{"verificationBlockedNoAllowlist":true}""";
        var rows = new List<AgentMessageDoc> { User(1, "test with test_exp_01") };

        var findings = OutcomeDetectors.DetectVerificationBlockedNoAllowlist(workflow, rows);

        Assert.Single(findings);
        Assert.Equal("VERIFICATION_BLOCKED_NO_ALLOWLIST", findings[0].Code);
    }

    private static AgentMessageDoc User(long seq, string text) => new()
    {
        Sequence = seq,
        Role = "user",
        Content = text
    };
}

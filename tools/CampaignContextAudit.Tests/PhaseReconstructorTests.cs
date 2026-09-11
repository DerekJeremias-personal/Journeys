using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class PhaseReconstructorTests
{
    private static ToolEvent Ev(long seq, string name) => new(seq, name, $"c{seq}", 0);

    [Fact]
    public void Infers_monotonic_phase_floor_from_tools_before_each_user_turn()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 0, Role = "user" },   // no tools yet
            new() { Sequence = 1, Role = "assistant", Content = "x" },
            new() { Sequence = 2, Role = "user" },   // after a save_model at seq 1? no—tool at seq 3
            new() { Sequence = 4, Role = "user" },
        };
        var timeline = new[] { Ev(1, "save_model"), Ev(3, "upsert_campaign") };

        var map = PhaseReconstructor.PhaseByUserTurn(rows, timeline);

        Assert.Equal("DataAnalysis", map[0]);     // no tool with seq <= 0
        Assert.Equal("EventModels", map[2]);      // save_model (seq1) <= 2
        Assert.Equal("CampaignSetup", map[4]);    // upsert_campaign (seq3) <= 4
    }

    [Fact]
    public void Maps_validate_campaign_to_campaign_journey_phase()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 0, Role = "user" },
            new() { Sequence = 2, Role = "user" },
        };
        var timeline = new[] { Ev(1, "validate_campaign") };

        var map = PhaseReconstructor.PhaseByUserTurn(rows, timeline);

        Assert.Equal("CampaignJourney", map[2]);
    }

    [Fact]
    public void Override_forces_fixed_phase_for_all_turns()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 0, Role = "user" },
            new() { Sequence = 2, Role = "user" },
        };
        var timeline = new[] { Ev(1, "save_model") };

        var map = PhaseReconstructor.PhaseByUserTurn(rows, timeline, overridePhase: "Verification");

        Assert.Equal("Verification", map[0]);
        Assert.Equal("Verification", map[2]);
    }
}

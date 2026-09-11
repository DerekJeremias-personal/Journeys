using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class GovernanceLoadSetTests
{
    [Fact]
    public void LoadedEveryTurn_includes_core_four()
    {
        var set = GovernanceLoadSet.LoadedEveryTurn;
        Assert.Contains("SystemPrompt.txt", set);
        Assert.Contains("SharedAgentToolingGovernance.txt", set);
        Assert.Contains("CampaignGovernanceCore.txt", set);
        Assert.Contains("WorkflowCoachChecklistGovernance.txt", set);
    }

    [Fact]
    public void CampaignBuild_supplements_match_composer()
    {
        var supplements = GovernanceLoadSet.SupplementsForSkill("CampaignBuild");
        Assert.Equal(
            new[]
            {
                "PointAccountModelGovernance.txt",
                "PointAccountJourneyCheatSheetGovernance.txt",
                "RulesEnginePatternGovernance.txt"
            },
            supplements);
    }

    [Fact]
    public void Journey_phase_file_is_orphan_not_loaded()
    {
        Assert.False(GovernanceLoadSet.IsLoadedOrSupplement("WorkflowPhaseCampaignJourneyGovernance.txt"));
    }
}

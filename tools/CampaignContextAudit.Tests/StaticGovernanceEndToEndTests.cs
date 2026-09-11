using CampaignContextAudit;
using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class StaticGovernanceEndToEndTests
{
    [Fact]
    public async Task Static_mode_writes_markdown()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cca-static-{Guid.NewGuid():N}");
        var gov = Path.Combine(root, "gov");
        var outDir = Path.Combine(root, "out");
        Directory.CreateDirectory(gov);
        Directory.CreateDirectory(outDir);

        File.WriteAllText(Path.Combine(gov, "SystemPrompt.txt"), "persona");
        File.WriteAllText(Path.Combine(gov, "SharedAgentToolingGovernance.txt"), "shared");
        File.WriteAllText(Path.Combine(gov, "CampaignGovernanceCore.txt"), "core");
        File.WriteAllText(Path.Combine(gov, "WorkflowCoachChecklistGovernance.txt"), "coach");
        File.WriteAllText(Path.Combine(gov, "WorkflowPhaseDataAnalysisObjectiveGovernance.txt"), "- Brief objective");
        File.WriteAllText(Path.Combine(gov, "WorkflowPhaseCampaignJourneyGovernance.txt"),
            "DEPRECATED\n\n" + new string('x', 300));

        var sourceRoot = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceRoot);

        var exit = await Program.Main([
            "--static-governance",
            "--governance", gov,
            "--source-root", sourceRoot,
            "--out", outDir,
            "--no-warehouse"]);

        Assert.Equal(0, exit);
        Assert.Single(Directory.GetFiles(outDir, "*-governance-static-audit.md"));
    }
}

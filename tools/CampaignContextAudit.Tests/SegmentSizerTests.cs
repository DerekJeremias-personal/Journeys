using System.IO;
using CampaignContextAudit.Budget;
using Xunit;

namespace CampaignContextAudit.Tests;

public class SegmentSizerTests
{
    [Fact]
    public void Sizes_stable_segments_from_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SystemPrompt.txt"), new string('p', 100));
        File.WriteAllText(Path.Combine(dir, "SharedAgentToolingGovernance.txt"), new string('s', 200));
        File.WriteAllText(Path.Combine(dir, "CampaignGovernanceCore.txt"), new string('c', 300));
        File.WriteAllText(Path.Combine(dir, "WorkflowPhaseEventModelsGovernance.txt"), new string('e', 400));

        var sizer = new SegmentSizer(dir);
        var sizes = sizer.StableSizesForPhase("EventModels");

        Assert.Equal(100, sizes.PersonaChars);
        Assert.Equal(200, sizes.SharedChars);
        Assert.Equal(300, sizes.CoreChars);
        Assert.Equal(400, sizes.PhaseChars);
        Assert.Equal(1000, sizes.TotalStableChars);
    }
}

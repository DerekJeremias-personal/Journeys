using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class BuildSubStepGovernanceResolverTests
{
    private static readonly string GovernanceDir = JourneysApiContentPaths.CampaignAgentDir;

    [Fact]
    public void Extract_TierLadder_returns_marked_section_only()
    {
        var input = "<!-- episode:TierLadder -->\nTIER\n<!-- /episode -->\n<!-- episode:SimpleEarn -->\nEARN\n<!-- /episode -->";
        var slice = GovernanceEpisodeMarkerParser.Extract(input, RuleEpisode.TierLadder);
        Assert.Contains("TIER", slice);
        Assert.DoesNotContain("EARN", slice);
    }

    [Fact]
    public void Resolve_journeyRequired_includes_campaign_dto_shape()
    {
        var pkg = new BuildContextPackage(
            CampaignBuildSubStep.JourneyRequired,
            RuleEpisode.TierLadder,
            null,
            "tier-navigation-point-balance",
            false);
        var slices = BuildSubStepGovernanceResolver.Resolve(pkg, LoadTestCorpus());
        Assert.Contains(slices, s => s.Contains("events is an array of event payload model id STRINGS", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_patRequired_omits_campaign_dto_shape()
    {
        var pkg = new BuildContextPackage(
            CampaignBuildSubStep.PatRequired,
            RuleEpisode.TierLadder,
            null,
            null,
            true);
        var slices = BuildSubStepGovernanceResolver.Resolve(pkg, LoadTestCorpus());
        Assert.DoesNotContain(slices, s => s.Contains("CAMPAIGN DTO CONTAINER SHAPE", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> LoadTestCorpus()
    {
        var names = new[]
        {
            "PointAccountModelGovernance.txt",
            "PointAccountJourneyCheatSheetGovernance.txt",
            "RulesEnginePatternGovernance.txt",
            "JsonCasingContractGovernance.txt",
            "CampaignDtoShapeGovernance.txt",
            "ProcessEventPayloadTypesGovernance.txt"
        };

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var path = Path.Combine(GovernanceDir, name);
            if (File.Exists(path))
                map[name] = File.ReadAllText(path);
        }

        return map;
    }
}

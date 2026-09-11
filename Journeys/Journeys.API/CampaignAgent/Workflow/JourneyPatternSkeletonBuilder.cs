using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

public static class JourneyPatternSkeletonBuilder
{
    public static string? TryBuild(string patternId, CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(patternId))
            return null;

        var pattern = RulesEnginePatternRecipes.Build().Patterns
            .FirstOrDefault(p => string.Equals(p.Id, patternId, StringComparison.OrdinalIgnoreCase));
        if (pattern == null)
            return null;

        var raw = pattern.MinimalSkeleton.GetRawText();
        var tqpId = ResolveTierQualificationPatId(state);
        if (string.IsNullOrWhiteSpace(tqpId))
            return raw;

        return raw.Replace("<tqp-pat-id>", tqpId, StringComparison.Ordinal);
    }

    internal static string? ResolveTierQualificationPatId(CampaignWorkflowState state)
    {
        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        var tierPat = manifest.Items.FirstOrDefault(item =>
            string.Equals(item.Role, "tierQualification", StringComparison.OrdinalIgnoreCase));
        return tierPat?.Id;
    }
}

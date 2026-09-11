using Journeys.Core.Models;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

public static class BuildSubStepGovernanceResolver
{
    public static IReadOnlyList<string> Resolve(
        BuildContextPackage package,
        IReadOnlyDictionary<string, string> governanceFiles)
    {
        var slices = new List<string>();

        switch (package.SubStep)
        {
            case CampaignBuildSubStep.PatRequired:
                AppendIfPresent(slices, governanceFiles, CampaignWorkflowPhaseGovernanceFiles.PointAccountModelFileName);
                AppendJsonCasingL1(slices, governanceFiles);
                break;

            case CampaignBuildSubStep.JourneyRequired:
                AppendJsonCasingFull(slices, governanceFiles);
                AppendIfPresent(slices, governanceFiles, CampaignWorkflowPhaseGovernanceFiles.CampaignDtoShapeFileName);
                AppendEpisodePatternSlice(slices, governanceFiles, package.Episode);
                AppendIfPresent(slices, governanceFiles, CampaignWorkflowPhaseGovernanceFiles.PointAccountJourneyCheatSheetFileName);
                break;

            case CampaignBuildSubStep.BuildComplete:
                AppendIfPresent(slices, governanceFiles, CampaignWorkflowPhaseGovernanceFiles.ProcessEventPayloadTypesFileName);
                break;

            case CampaignBuildSubStep.ShellOptional:
                AppendJsonCasingL1(slices, governanceFiles);
                break;
        }

        return slices;
    }

    public static IReadOnlyList<string> ResolveVerificationHandoff(IReadOnlyDictionary<string, string> governanceFiles)
    {
        var slices = new List<string>();
        AppendIfPresent(slices, governanceFiles, CampaignWorkflowPhaseGovernanceFiles.ProcessEventPayloadTypesFileName);
        return slices;
    }

    private static void AppendEpisodePatternSlice(
        List<string> slices,
        IReadOnlyDictionary<string, string> governanceFiles,
        RuleEpisode episode)
    {
        if (!governanceFiles.TryGetValue(CampaignWorkflowPhaseGovernanceFiles.RulesEnginePatternFileName, out var patternContent))
            return;

        var episodeSlice = GovernanceEpisodeMarkerParser.Extract(patternContent, episode);
        if (!string.IsNullOrWhiteSpace(episodeSlice))
        {
            slices.Add(CampaignWorkflowPhaseGovernanceFiles.RulesEnginePatternSectionTitle
                         + "\n---\n"
                         + episodeSlice.Trim());
            return;
        }

        if (episode == RuleEpisode.Generic)
        {
            var genericSlice = GovernanceEpisodeMarkerParser.Extract(patternContent, RuleEpisode.Generic);
            if (!string.IsNullOrWhiteSpace(genericSlice))
            {
                slices.Add(CampaignWorkflowPhaseGovernanceFiles.RulesEnginePatternSectionTitle
                             + "\n---\n"
                             + genericSlice.Trim());
            }
        }
    }

    private static void AppendJsonCasingL1(List<string> slices, IReadOnlyDictionary<string, string> governanceFiles)
    {
        if (!governanceFiles.TryGetValue(CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractFileName, out var content))
            return;

        var l1End = content.IndexOf("L2 —", StringComparison.Ordinal);
        var slice = l1End > 0 ? content[..l1End].Trim() : content.Trim();
        if (slice.Length > 0)
            slices.Add(CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractSectionTitle + "\n---\n" + slice);
    }

    private static void AppendJsonCasingFull(List<string> slices, IReadOnlyDictionary<string, string> governanceFiles)
    {
        AppendIfPresent(
            slices,
            governanceFiles,
            CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractFileName,
            CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractSectionTitle);
    }

    private static void AppendIfPresent(
        List<string> slices,
        IReadOnlyDictionary<string, string> governanceFiles,
        string fileName,
        string? sectionTitle = null)
    {
        if (!governanceFiles.TryGetValue(fileName, out var content) || string.IsNullOrWhiteSpace(content))
            return;

        var title = sectionTitle ?? Path.GetFileNameWithoutExtension(fileName).Replace("Governance", "", StringComparison.Ordinal);
        slices.Add(title + "\n---\n" + content.Trim());
    }
}

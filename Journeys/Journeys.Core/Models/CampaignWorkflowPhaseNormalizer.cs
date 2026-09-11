using Journeys.Core.Models;

namespace Journeys.Core.Models;

public static class CampaignWorkflowPhaseNormalizer
{
    public static CampaignWorkflowPhase Normalize(CampaignWorkflowPhase phase) =>
        phase switch
        {
            CampaignWorkflowPhase.CampaignSetup => CampaignWorkflowPhase.CampaignBuild,
            CampaignWorkflowPhase.PointAccountTypes => CampaignWorkflowPhase.CampaignBuild,
            CampaignWorkflowPhase.CampaignJourney => CampaignWorkflowPhase.CampaignBuild,
            _ => phase
        };

    public static bool IsBuildPhase(CampaignWorkflowPhase phase) =>
        phase is CampaignWorkflowPhase.CampaignBuild
            or CampaignWorkflowPhase.CampaignSetup
            or CampaignWorkflowPhase.PointAccountTypes
            or CampaignWorkflowPhase.CampaignJourney;
}

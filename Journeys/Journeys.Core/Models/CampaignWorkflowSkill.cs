namespace Journeys.Core.Models;

/// <summary>
/// High-level campaign agent skills (four-skill model). Distinct from legacy
/// <see cref="CampaignWorkflowPhase"/> which included separate Setup/PAT/Journey phases.
/// </summary>
public enum CampaignWorkflowSkill
{
    Brief = 0,
    EventModels = 1,
    CampaignBuild = 2,
    Verification = 3
}

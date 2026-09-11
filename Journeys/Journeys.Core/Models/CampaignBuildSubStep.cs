namespace Journeys.Core.Models;

/// <summary>
/// Artifact-gated sub-steps within the CampaignBuild skill (not user-facing phases).
/// Shell vs PAT order is flexible; PAT-before-journey is enforced separately.
/// </summary>
public enum CampaignBuildSubStep
{
    ShellOptional,
    PatRequired,
    JourneyRequired,
    BuildComplete
}

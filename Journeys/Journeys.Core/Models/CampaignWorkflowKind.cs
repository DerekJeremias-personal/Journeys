namespace Journeys.Core.Models;

/// <summary>
/// Whether the program primarily relies on event ingress vs tag/account eligibility.
/// </summary>
public enum CampaignWorkflowKind
{
    EventDriven = 0,
    TagFirst = 1
}

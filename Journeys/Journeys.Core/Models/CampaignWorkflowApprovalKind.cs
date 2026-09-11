namespace Journeys.Core.Models;

/// <summary>Human approval gate before advancing from data analysis or journey phases, or selecting an event model.</summary>
public enum CampaignWorkflowApprovalKind
{
    None = 0,
    DesignBrief = 1,
    Journey = 2,
    EventModelSelection = 3,
    LivePromotion = 4
}

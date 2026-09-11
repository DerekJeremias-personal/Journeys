namespace Journeys.Core.Models;

/// <summary>
/// Campaign authoring phase for HTTP campaign agent workflow orchestration.
/// </summary>
public enum CampaignWorkflowPhase
{
    /// <summary>Warehouse MCP analysis → design brief.</summary>
    DataAnalysis = 0,

    /// <summary>Backend event payload + wrapper models.</summary>
    EventModels = 1,

    /// <summary>Draft campaign shell (Events, dates, status).</summary>
    CampaignSetup = 2,

    /// <summary>Point account types before journey outcomes.</summary>
    PointAccountTypes = 3,

    /// <summary>Journey tree, rules, navigation, outcomes.</summary>
    CampaignJourney = 4,

    /// <summary>Assistant context + ProcessEvent / verification.</summary>
    Verification = 5,

    /// <summary>Workflow complete for automation.</summary>
    Done = 6,

    /// <summary>Shell placeholder + PAT upserts + journey authoring (replaces Setup/PAT/Journey).</summary>
    CampaignBuild = 7
}

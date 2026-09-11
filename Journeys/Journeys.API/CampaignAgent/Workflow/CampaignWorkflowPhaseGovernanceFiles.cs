using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Maps workflow phases to governance text files under CampaignAgent/.
/// </summary>
public static class CampaignWorkflowPhaseGovernanceFiles
{
    public const string CoreFileName = "CampaignGovernanceCore.txt";
    public const string JsonCasingContractFileName = "JsonCasingContractGovernance.txt";
    public const string JsonCasingContractSectionTitle = "JSON CASING CONTRACT (mandatory)";
    public const string SharedFileName = "SharedAgentToolingGovernance.txt";
    public const string CoachChecklistFileName = "WorkflowCoachChecklistGovernance.txt";
    public const string PointAccountModelFileName = "PointAccountModelGovernance.txt";
    public const string PointAccountJourneyCheatSheetFileName = "PointAccountJourneyCheatSheetGovernance.txt";
    public const string RulesEnginePatternFileName = "RulesEnginePatternGovernance.txt";
    public const string RulesEnginePatternVerificationCheatSheetFileName = "RulesEnginePatternVerificationCheatSheet.txt";
    public const string CampaignDtoShapeFileName = "CampaignDtoShapeGovernance.txt";
    public const string ProcessEventPayloadTypesFileName = "ProcessEventPayloadTypesGovernance.txt";
    public const string PointAccountModelSectionTitle = "POINT ACCOUNT MODEL";
    public const string RulesEnginePatternSectionTitle = "RULES ENGINE PATTERNS";

    public static string GetCoachChecklistSectionTitle() => "WORKFLOW COACH (mandatory)";

    /// <summary>
    /// Objective mode swaps the DataAnalysis warehouse playbook for the interview playbook
    /// when data-warehouse tools are disabled.
    /// </summary>
    public static string GetPhaseFileName(CampaignWorkflowPhase phase, bool dataWarehouseEnabled)
    {
        if (phase == CampaignWorkflowPhase.DataAnalysis && !dataWarehouseEnabled)
            return "WorkflowPhaseDataAnalysisObjectiveGovernance.txt";
        return GetPhaseFileName(phase);
    }

    public static string GetPhaseFileName(CampaignWorkflowPhase phase) =>
        phase switch
        {
            CampaignWorkflowPhase.DataAnalysis => "WorkflowPhaseDataAnalysisGovernance.txt",
            CampaignWorkflowPhase.EventModels => "WorkflowPhaseEventModelsGovernance.txt",
            CampaignWorkflowPhase.CampaignBuild => "WorkflowPhaseCampaignBuildGovernance.txt",
            CampaignWorkflowPhase.CampaignSetup => "WorkflowPhaseCampaignBuildGovernance.txt",
            CampaignWorkflowPhase.PointAccountTypes => "WorkflowPhaseCampaignBuildGovernance.txt",
            CampaignWorkflowPhase.CampaignJourney => "WorkflowPhaseCampaignBuildGovernance.txt",
            CampaignWorkflowPhase.Verification => "WorkflowPhaseVerificationGovernance.txt",
            CampaignWorkflowPhase.Done => "WorkflowPhaseVerificationGovernance.txt",
            _ => "WorkflowPhaseDataAnalysisGovernance.txt"
        };

    public static string GetPhaseSectionTitle(CampaignWorkflowPhase phase) =>
        phase switch
        {
            CampaignWorkflowPhase.DataAnalysis => "DATA ANALYSIS (workflow phase)",
            CampaignWorkflowPhase.EventModels => "EVENT MODELS (workflow phase)",
            CampaignWorkflowPhase.CampaignBuild => "CAMPAIGN BUILD (workflow phase)",
            CampaignWorkflowPhase.CampaignSetup => "CAMPAIGN BUILD (workflow phase)",
            CampaignWorkflowPhase.PointAccountTypes => "CAMPAIGN BUILD (workflow phase)",
            CampaignWorkflowPhase.CampaignJourney => "CAMPAIGN BUILD (workflow phase)",
            CampaignWorkflowPhase.Verification or CampaignWorkflowPhase.Done => "VERIFICATION (workflow phase)",
            _ => "WORKFLOW PHASE"
        };
}

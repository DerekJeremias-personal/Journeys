namespace CampaignContextAudit.Budget;

public static class GovernancePhaseMap
{
    public const string CoreFileName = "CampaignGovernanceCore.txt";
    public const string SharedFileName = "SharedAgentToolingGovernance.txt";
    public const string PersonaFileName = "SystemPrompt.txt";

    public static string NormalizePhase(string? phase) => (phase ?? "DataAnalysis").Trim() switch
    {
        "CampaignAndPat" => "CampaignSetup",
        "Verify" => "Verification",
        var p when string.IsNullOrEmpty(p) => "DataAnalysis",
        var p => p
    };

    // dataWarehouseEnabled defaults true to match production DataWarehouseProxyOptions in the assessed env;
    // override via CLI if the assessed deployment had it disabled.
    public static string GetPhaseFileName(string? phase, bool dataWarehouseEnabled = true)
    {
        var p = NormalizePhase(phase);
        if (p == "DataAnalysis" && !dataWarehouseEnabled)
            return "WorkflowPhaseDataAnalysisObjectiveGovernance.txt";
        return p switch
        {
            "DataAnalysis" => "WorkflowPhaseDataAnalysisGovernance.txt",
            "EventModels" => "WorkflowPhaseEventModelsGovernance.txt",
            "CampaignBuild" => "WorkflowPhaseCampaignBuildGovernance.txt",
            "CampaignSetup" => "WorkflowPhaseCampaignBuildGovernance.txt",
            "PointAccountTypes" => "WorkflowPhaseCampaignBuildGovernance.txt",
            "CampaignJourney" => "WorkflowPhaseCampaignBuildGovernance.txt",
            "Verification" => "WorkflowPhaseVerificationGovernance.txt",
            "Done" => "WorkflowPhaseVerificationGovernance.txt",
            _ => "WorkflowPhaseDataAnalysisGovernance.txt"
        };
    }
}

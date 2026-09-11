namespace CampaignContextAudit.Governance;

public static class GovernanceLoadSet
{
    public static readonly HashSet<string> LoadedEveryTurn = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemPrompt.txt",
        "SharedAgentToolingGovernance.txt",
        "CampaignGovernanceCore.txt",
        "WorkflowCoachChecklistGovernance.txt"
    };

    private static readonly Dictionary<string, string> SkillPhaseFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Brief"] = "WorkflowPhaseDataAnalysisGovernance.txt",
        ["BriefObjective"] = "WorkflowPhaseDataAnalysisObjectiveGovernance.txt",
        ["EventModels"] = "WorkflowPhaseEventModelsGovernance.txt",
        ["CampaignBuild"] = "WorkflowPhaseCampaignBuildGovernance.txt",
        ["Verification"] = "WorkflowPhaseVerificationGovernance.txt"
    };

    private static readonly Dictionary<string, string[]> SkillSupplements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CampaignBuild"] =
        [
            "PointAccountModelGovernance.txt",
            "PointAccountJourneyCheatSheetGovernance.txt",
            "RulesEnginePatternGovernance.txt"
        ],
        ["Verification"] =
        [
            "PointAccountJourneyCheatSheetGovernance.txt",
            "RulesEnginePatternVerificationCheatSheet.txt"
        ]
    };

    public static IReadOnlyList<string> AllKnownSkills() =>
        ["Brief", "EventModels", "CampaignBuild", "Verification"];

    public static string PhaseFileForSkill(string skill, bool dataWarehouseEnabled) =>
        skill == "Brief" && !dataWarehouseEnabled
            ? SkillPhaseFiles["BriefObjective"]
            : SkillPhaseFiles.GetValueOrDefault(skill, SkillPhaseFiles["Brief"]);

    public static IReadOnlyList<string> SupplementsForSkill(string skill) =>
        SkillSupplements.TryGetValue(skill, out var s) ? s : Array.Empty<string>();

    public static bool IsLoadedOrSupplement(string fileName)
    {
        if (LoadedEveryTurn.Contains(fileName))
            return true;
        if (SkillPhaseFiles.Values.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            return true;
        return SkillSupplements.Values.SelectMany(x => x).Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> SkillsForFile(string fileName, bool dataWarehouseEnabled)
    {
        var skills = new List<string>();
        foreach (var skill in AllKnownSkills())
        {
            if (PhaseFileForSkill(skill, dataWarehouseEnabled).Equals(fileName, StringComparison.OrdinalIgnoreCase)
                || SupplementsForSkill(skill).Contains(fileName, StringComparer.OrdinalIgnoreCase))
                skills.Add(skill);
        }

        if (LoadedEveryTurn.Contains(fileName))
            skills.AddRange(AllKnownSkills());

        return skills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

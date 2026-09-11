namespace CampaignContextAudit.Governance;

public enum GovernanceFileClassification
{
    LoadedEveryTurn,
    Phase,
    Supplement,
    Orphan,
    LegacyStub
}

public enum GuidanceLayer
{
    Persona,
    SharedTooling,
    Core,
    CoachChecklist,
    Phase,
    Supplement,
    Registry,
    Coach,
    Remediation,
    SalientFacts,
    Doc
}

public enum GovernanceFindingSeverity
{
    Informational,
    Wasteful,
    Degrading,
    Blocking
}

public sealed record GovernanceFileEntry(
    string FileName,
    GovernanceFileClassification Classification,
    int LineCount,
    int CharCount,
    IReadOnlyList<string> LoadedForSkills);

public sealed record NormalizedRule(
    string Hash,
    string Excerpt,
    GuidanceLayer Layer,
    string Source);

public sealed record GovernanceFinding(
    string Code,
    GovernanceFindingSeverity Severity,
    IReadOnlyList<string> Sources,
    string Summary,
    string? Skill = null,
    bool TraceBoosted = false);

public sealed record SkillCompetencyProfile(
    string Skill,
    int StableChars,
    int LayerCount,
    double RedundancyIndex,
    double SpecificityScore,
    double CoachDependency,
    string CompetencyBand);

public sealed record StaticGovernanceOptions(
    string GovernanceDir,
    string SourceRoot,
    bool DataWarehouseEnabled,
    string? CorrelateFindingsDir);

public sealed record StaticGovernanceReport(
    IReadOnlyList<GovernanceFileEntry> Corpus,
    IReadOnlyList<GovernanceFinding> Findings,
    IReadOnlyList<SkillCompetencyProfile> SkillProfiles,
    IReadOnlyList<RemediationItem> Remediation,
    IReadOnlyDictionary<string, int> CorpusCounts,
    IReadOnlyList<DuplicationClusterSummary> DuplicationClusters,
    bool TraceCorrelationRun);

public sealed record RemediationItem(
    int Rank,
    string Action,
    IReadOnlyList<string> FindingCodes);

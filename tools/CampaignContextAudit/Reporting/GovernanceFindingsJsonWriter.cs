using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignContextAudit.Governance;

namespace CampaignContextAudit.Reporting;

public static class GovernanceFindingsJsonWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static void Write(string path, StaticGovernanceReport report, string governanceDir, string sourceRoot)
    {
        var payload = new
        {
            schemaVersion = 1,
            generatedAt = DateTimeOffset.UtcNow,
            governanceDir,
            sourceRoot,
            corpus = new
            {
                loaded = report.CorpusCounts.GetValueOrDefault(nameof(GovernanceFileClassification.LoadedEveryTurn)),
                phase = report.CorpusCounts.GetValueOrDefault(nameof(GovernanceFileClassification.Phase)),
                supplement = report.CorpusCounts.GetValueOrDefault(nameof(GovernanceFileClassification.Supplement)),
                orphan = report.CorpusCounts.GetValueOrDefault(nameof(GovernanceFileClassification.Orphan)),
                legacyStub = report.CorpusCounts.GetValueOrDefault(nameof(GovernanceFileClassification.LegacyStub))
            },
            skillProfiles = report.SkillProfiles.Select(p => new
            {
                skill = p.Skill,
                stableChars = p.StableChars,
                layerCount = p.LayerCount,
                redundancyIndex = p.RedundancyIndex,
                specificityScore = p.SpecificityScore,
                coachDependency = p.CoachDependency,
                competencyBand = p.CompetencyBand
            }),
            findings = report.Findings.Select(f => new
            {
                code = f.Code,
                severity = f.Severity.ToString().ToLowerInvariant(),
                sources = f.Sources,
                summary = f.Summary,
                skill = f.Skill,
                traceBoosted = f.TraceBoosted
            }),
            remediation = report.Remediation.Select(r => new
            {
                rank = r.Rank,
                action = r.Action,
                findingCodes = r.FindingCodes
            }),
            traceCorrelationRun = report.TraceCorrelationRun
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOpts));
    }
}

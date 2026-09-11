using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using CampaignContextAudit.Transcript;

namespace CampaignContextAudit.Reporting;

public static class FindingsJsonWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static AuditReportJson BuildReport(
        LoadedTranscript transcript,
        WorkflowSnapshot? snapshot,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<TurnBudget> turns,
        PerformanceSummaryJson? performance,
        RubricScorecard? rubric = null,
        DeliveryScorecard? delivery = null)
    {
        var workflow = transcript.WorkflowRows.LastOrDefault();
        var conversationId = transcript.ChatRows.Concat(transcript.WorkflowRows)
            .Select(r => r.ConversationId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? "unknown";

        return new AuditReportJson
        {
            SchemaVersion = 3,
            SourceFile = Path.GetFileName(transcript.SourcePath),
            ConversationId = conversationId,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            OutcomeSummary = BuildOutcomeSummary(snapshot, workflow),
            Findings = findings.Select(f => new FindingJson
            {
                Code = f.Code,
                Severity = f.Severity,
                CitedSequences = f.CitedSequences,
                Summary = f.Summary
            }).ToList(),
            BudgetByTurn = turns.Select(t => new BudgetTurnJson
            {
                UserSequence = t.UserSequence,
                Phase = t.Phase,
                StableChars = t.StableChars,
                HistoryChars = t.HistoryChars,
                SessionChars = t.SessionChars,
                TotalChars = t.TotalChars
            }).ToList(),
            PerformanceSummary = performance,
            Delivery = delivery is null ? null : new DeliveryScorecardJson
            {
                Grade = delivery.Grade,
                Score = delivery.Score,
                Rationale = delivery.Rationale,
                EffectiveWallMs = delivery.EffectiveWallMs,
                HighestReached = delivery.HighestReached,
                ExpectedMinimum = delivery.ExpectedMinimum,
                Milestones = delivery.Milestones.Select(m => new MilestoneJson
                {
                    Id = m.Id,
                    Label = m.Label,
                    Reached = m.Reached
                }).ToList()
            },
            Rubric = rubric?.Dimensions.Select(d => new RubricDimensionJson
            {
                DimensionId = d.DimensionId,
                Dimension = d.Dimension,
                Lens = d.Lens,
                Grade = d.Grade,
                Score = d.Score,
                Rationale = d.Rationale
            }).ToList() ?? [],
            OverallCompetency = rubric is null
                ? null
                : new OverallGradeJson
                {
                    Grade = rubric.Competency.Grade,
                    Score = rubric.Competency.Score,
                    UncappedGrade = rubric.UncappedCompetency?.Grade,
                    UncappedScore = rubric.UncappedCompetency?.Score
                },
            OverallEfficiency = rubric is null
                ? null
                : new OverallGradeJson { Grade = rubric.Efficiency.Grade, Score = rubric.Efficiency.Score }
        };
    }

    public static void Write(string path, AuditReportJson report) =>
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOpts));

    public static AuditReportJson Read(string path) =>
        JsonSerializer.Deserialize<AuditReportJson>(File.ReadAllText(path), JsonOpts)
        ?? throw new InvalidOperationException($"Could not deserialize findings JSON: {path}");

    private static OutcomeSummary BuildOutcomeSummary(WorkflowSnapshot? snapshot, AgentMessageDoc? workflowRow)
    {
        if (snapshot is null)
        {
            return new OutcomeSummary
            {
                WorkflowPhase = workflowRow?.WorkflowPhase,
                LinkedCampaignId = TryReadLinkedCampaignId(workflowRow?.Content)
            };
        }

        return new OutcomeSummary
        {
            CreationComplete = snapshot.CreationComplete,
            JourneyRuleSetCount = snapshot.JourneyRuleSetCount,
            WorkflowPhase = snapshot.WorkflowPhase,
            LinkedCampaignId = TryReadLinkedCampaignId(workflowRow?.Content)
        };
    }

    private static string? TryReadLinkedCampaignId(string? workflowContent)
    {
        if (string.IsNullOrWhiteSpace(workflowContent)) return null;
        try
        {
            using var doc = JsonDocument.Parse(workflowContent);
            var root = doc.RootElement;
            if (root.TryGetProperty("creationSnapshot", out var cs)
                && cs.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(cs.GetString()))
            {
                using var inner = JsonDocument.Parse(cs.GetString()!);
                if (inner.RootElement.TryGetProperty("campaignId", out var cid))
                    return cid.GetString();
            }

            if (root.TryGetProperty("campaignShellRef", out var shell)
                && shell.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(shell.GetString()))
            {
                using var inner = JsonDocument.Parse(shell.GetString()!);
                if (inner.RootElement.TryGetProperty("campaignId", out var cid))
                    return cid.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore malformed workflow content
        }

        return null;
    }
}

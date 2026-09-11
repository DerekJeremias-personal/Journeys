using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public sealed record Finding(string Code, string Severity, string Summary, IReadOnlyList<string> CitedSequences);

public static class CompetencyDetectors
{
    public static IReadOnlyList<Finding> RedundantDiscovery(IReadOnlyList<ToolEvent> timeline)
    {
        var findings = new List<Finding>();
        var discovery = timeline.Where(e => ToolCallTimeline.DiscoveryTools.Contains(e.ToolName)).ToList();
        for (int i = 1; i < discovery.Count; i++)
        {
            var prev = discovery[i - 1];
            var cur = discovery[i];
            // Two discovery reads with no mutation between them = likely redundant retrieval.
            var mutatedBetween = timeline.Any(e =>
                e.Sequence > prev.Sequence && e.Sequence < cur.Sequence
                && ToolCallTimeline.ModelMutationTools.Contains(e.ToolName));
            if (!mutatedBetween)
                findings.Add(new Finding("REDUNDANT_DISCOVERY", "wasteful",
                    $"Back-to-back discovery reads ({prev.ToolName} then {cur.ToolName}) with no intervening mutation.",
                    new[] { prev.Sequence.ToString(), cur.Sequence.ToString() }));
        }
        return findings;
    }

    public static IReadOnlyList<Finding> RediscoveryAfterCreation(IReadOnlyList<ToolEvent> timeline)
    {
        var findings = new List<Finding>();
        var lastCreation = timeline.Where(e => ToolCallTimeline.ModelMutationTools.Contains(e.ToolName))
            .Select(e => (long?)e.Sequence).LastOrDefault();
        if (lastCreation is null) return findings;
        foreach (var e in timeline.Where(e => ToolCallTimeline.DiscoveryTools.Contains(e.ToolName) && e.Sequence > lastCreation))
            findings.Add(new Finding("REDISCOVERY_AFTER_CREATION", "intent-breaking",
                $"Discovery read ({e.ToolName}) after the agent already created models (last creation at seq {lastCreation}); possible working-memory loss.",
                new[] { e.Sequence.ToString() }));
        return findings;
    }

    public static IReadOnlyList<Finding> TopBloat(IReadOnlyList<ToolEvent> timeline, int top = 5) =>
        timeline.Where(e => e.ResultChars > 0 && !e.IsDigested)
            .OrderByDescending(e => e.ResultChars)
            .Take(top)
            .Select(e => new Finding("TOOL_RESULT_BLOAT", "degrading",
                $"{e.ToolName} result is {e.ResultChars:N0} chars, persisted verbatim into history.",
                new[] { e.Sequence.ToString() }))
            .ToList();

    public static IReadOnlyList<Finding> WorkflowStateDrift(
        IReadOnlyList<AgentMessageDoc> workflowRows,
        IReadOnlyList<ToolEvent> timeline,
        WorkflowSnapshot? snapshot = null)
    {
        var findings = new List<Finding>();
        var latest = workflowRows.OrderByDescending(w => w.Sequence).FirstOrDefault();
        var saveModelUsed = timeline.Any(e => string.Equals(e.ToolName, "save_model", StringComparison.OrdinalIgnoreCase));
        if (saveModelUsed && latest is { WorkflowModelGatePassed: false or null })
            findings.Add(new Finding("WORKFLOW_STATE_DRIFT", "degrading",
                "Models were created via save_model but the workflow row shows workflowModelGatePassed != true; narrative/state drift.",
                new[] { latest?.Sequence.ToString() ?? "n/a" }));

        if (snapshot is { CreationComplete: false }
            && timeline.Any(e => string.Equals(e.ToolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)))
            findings.Add(new Finding("WORKFLOW_STATE_DRIFT", "degrading",
                "upsert_campaign was invoked but creationSnapshot.creationComplete is still false; workflow snapshot drift.",
                new[] { latest?.Sequence.ToString() ?? "n/a" }));

        return findings;
    }
}

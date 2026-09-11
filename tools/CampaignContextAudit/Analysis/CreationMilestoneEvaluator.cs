using System.Text.Json;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public static class CreationMilestoneEvaluator
{
    private static readonly HashSet<string> EventModelTools =
        new(StringComparer.OrdinalIgnoreCase) { "list_models", "get_all_models", "get_model", "get_example_model" };

    private static readonly HashSet<string> JourneyPhaseTools =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "validate_campaign", "upsert_campaign", "get_campaign_assistant_context"
        };

    public static CreationMilestoneEvaluation Evaluate(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline,
        string? workflowContent)
    {
        var toolNames = timeline.Select(t => t.ToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var linkedCampaignId = TryReadLinkedCampaignId(workflowContent);

        var designBrief = HasDesignBrief(workflowContent, toolNames);
        var eventModels = HasEventModels(snapshot, workflowContent, toolNames);
        var campaignSetup = !string.IsNullOrWhiteSpace(linkedCampaignId);
        var pointAccountTypes = snapshot?.PatManifestCount > 0;
        var campaignJourney = HasCampaignJourney(snapshot, chatRows);
        var verification = snapshot?.VerificationPatCount > 0 || toolNames.Contains("process_event");
        var done = snapshot?.CreationComplete == true;

        var milestones = new List<MilestoneResult>
        {
            new(CreationMilestoneIds.DesignBrief, "Design brief", designBrief),
            new(CreationMilestoneIds.EventModels, "Event models", eventModels),
            new(CreationMilestoneIds.CampaignSetup, "Campaign shell linked", campaignSetup),
            new(CreationMilestoneIds.PointAccountTypes, "Point account types", pointAccountTypes),
            new(CreationMilestoneIds.CampaignJourney, "Journey saved", campaignJourney),
            new(CreationMilestoneIds.Verification, "Verification", verification),
            new(CreationMilestoneIds.Done, "Creation complete", done),
        };

        string? highestReached = null;
        foreach (var id in CreationMilestoneIds.Ordered)
        {
            if (milestones.First(m => m.Id == id).Reached)
                highestReached = id;
        }

        var expectedMinimum = InferExpectedMinimum(snapshot, toolNames, linkedCampaignId);
        var hasGap = Ordinal(highestReached) < Ordinal(expectedMinimum);

        return new CreationMilestoneEvaluation(milestones, highestReached, expectedMinimum, hasGap);
    }

    private static string? InferExpectedMinimum(
        WorkflowSnapshot? snapshot,
        HashSet<string> toolNames,
        string? linkedCampaignId)
    {
        if (toolNames.Contains("validate_campaign") || toolNames.Contains("upsert_campaign"))
            return CreationMilestoneIds.CampaignJourney;

        if (snapshot?.PatManifestCount > 0
            || toolNames.Contains("upsert_point_account_type")
            || toolNames.Contains("validate_point_account_type"))
            return CreationMilestoneIds.PointAccountTypes;

        if (!string.IsNullOrWhiteSpace(linkedCampaignId))
            return CreationMilestoneIds.CampaignSetup;

        if (EventModelTools.Any(toolNames.Contains))
            return CreationMilestoneIds.EventModels;

        if (JourneyPhaseTools.Any(toolNames.Contains))
            return CreationMilestoneIds.CampaignJourney;

        return null;
    }

    private static bool HasDesignBrief(string? workflowContent, HashSet<string> toolNames)
    {
        if (toolNames.Contains("propose_campaign_design")) return true;
        if (string.IsNullOrWhiteSpace(workflowContent)) return false;

        try
        {
            using var doc = JsonDocument.Parse(workflowContent);
            var root = doc.RootElement;
            if (ReadBool(root, "campaignDesignBriefProposed") == true) return true;
            if (root.TryGetProperty("campaignDesignBrief", out var brief)
                && brief.ValueKind != JsonValueKind.Null
                && brief.ValueKind != JsonValueKind.Undefined)
            {
                if (brief.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(brief.GetString()))
                    return true;
                if (brief.ValueKind == JsonValueKind.Object && brief.EnumerateObject().Any())
                    return true;
            }
        }
        catch (JsonException)
        {
            // ignore malformed workflow content
        }

        return false;
    }

    private static bool HasEventModels(WorkflowSnapshot? snapshot, string? workflowContent, HashSet<string> toolNames)
    {
        if (snapshot?.FetchFailed == true) return false;

        if (!string.IsNullOrWhiteSpace(workflowContent))
        {
            try
            {
                using var doc = JsonDocument.Parse(workflowContent);
                var root = doc.RootElement;
                if (ReadBool(root, "eventModelCandidates", "fetchFailed") == true) return false;
                if (HasNonEmptyString(root, "selectedEventModelId")) return true;
                if (HasNonEmptyString(root, "eventModelRef")) return true;
            }
            catch (JsonException)
            {
                // ignore malformed workflow content
            }
        }

        return EventModelTools.Any(toolNames.Contains) && snapshot?.FetchFailed != true;
    }

    private static bool HasCampaignJourney(WorkflowSnapshot? snapshot, IReadOnlyList<AgentMessageDoc> chatRows)
    {
        if (snapshot?.JourneyRuleSetCount is > 0) return true;

        var callIdToTool = BuildCallIdToToolName(chatRows);
        foreach (var row in chatRows.Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase)))
        {
            var toolName = ResolveToolName(row, callIdToTool);
            if (!string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)) continue;
            if (ToolOutcomeClassifier.Classify(row) == ToolOutcomeKind.MutationDigest
                && TryReadRuleSetCount(row.ToolResultJson) > 0)
                return true;
        }

        return false;
    }

    private static int Ordinal(string? milestoneId)
    {
        if (milestoneId is null) return -1;
        var idx = Array.IndexOf(CreationMilestoneIds.Ordered, milestoneId);
        return idx < 0 ? -1 : idx;
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

    private static bool? ReadBool(JsonElement root, params string[] path)
    {
        if (!TryNavigate(root, out var el, path)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool HasNonEmptyString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el)) return false;
        return el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString());
    }

    private static bool TryNavigate(JsonElement root, out JsonElement current, params string[] path)
    {
        current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return false;
        }
        return true;
    }

    private static Dictionary<string, string> BuildCallIdToToolName(IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in chatRows.Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var call in Transcript.MeaiEnvelope.ExtractFunctionCalls(row.Content))
            {
                if (!string.IsNullOrWhiteSpace(call.CallId))
                    map[call.CallId] = call.Name;
            }
        }
        return map;
    }

    private static string? ResolveToolName(AgentMessageDoc row, IReadOnlyDictionary<string, string> callIdToTool)
    {
        if (!string.IsNullOrWhiteSpace(row.ToolName)) return row.ToolName;
        if (string.IsNullOrWhiteSpace(row.ToolCallId)) return null;
        return callIdToTool.TryGetValue(row.ToolCallId, out var name) ? name : null;
    }

    private static int TryReadRuleSetCount(string? toolResultJson)
    {
        var body = toolResultJson ?? "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                using var inner = JsonDocument.Parse(text.GetString() ?? "{}");
                foreach (var name in new[] { "ruleSetCount", "RuleSetCount" })
                {
                    if (inner.RootElement.TryGetProperty(name, out var count) && count.TryGetInt32(out var n))
                        return n;
                }

                return 0;
            }

            foreach (var name in new[] { "ruleSetCount", "RuleSetCount" })
            {
                if (root.TryGetProperty(name, out var count) && count.TryGetInt32(out var n))
                    return n;
            }
        }
        catch (JsonException)
        {
            // ignore malformed tool result
        }

        return 0;
    }
}

using System.Text.Json;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public static class WorkflowSnapshotParser
{
    public static WorkflowSnapshot? Parse(AgentMessageDoc? workflowRow)
    {
        if (workflowRow is null) return null;
        return Parse(workflowRow.Content, workflowRow.WorkflowPhase);
    }

    public static WorkflowSnapshot? Parse(string? content, string? workflowPhase)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            return new WorkflowSnapshot(
                CreationComplete: ReadBool(root, "creationSnapshot", "creationComplete"),
                JourneyRuleSetCount: ReadJourneyRuleSetCount(root),
                WorkflowPhase: workflowPhase,
                PatManifestCount: ReadArrayLength(root, "pointAccountManifest", "items"),
                VerificationPatCount: ReadArrayLength(root, "verificationRecord", "pointAccountManifest", "items"),
                UpsertFailedSinceValidate: ReadBool(root, "campaignValidationSummary", "upsertFailedSinceValidate"),
                LastRemediationPreview: ReadRemediationPreview(root),
                UserRequestedNewEventModel: ReadBool(root, "userRequestedNewEventModel"),
                FetchFailed: ReadBool(root, "eventModelCandidates", "fetchFailed"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReadJourneyRuleSetCount(JsonElement root)
    {
        var fromSnapshot = ReadInt(root, "creationSnapshot", "journeyRuleSetCount");
        if (fromSnapshot.HasValue) return fromSnapshot;

        return ReadInt(root, "journeyDigestProposed", "ruleSetCount");
    }

    private static string? ReadRemediationPreview(JsonElement root)
    {
        if (!root.TryGetProperty("lastToolRemediationSummary", out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;

        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length <= 200 ? text : text[..200];
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

    private static int? ReadInt(JsonElement root, params string[] path)
    {
        if (!TryNavigate(root, out var el, path)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        return null;
    }

    private static int ReadArrayLength(JsonElement root, params string[] path)
    {
        if (!TryNavigate(root, out var el, path)) return 0;
        return el.ValueKind == JsonValueKind.Array ? el.GetArrayLength() : 0;
    }

    private static bool TryNavigate(JsonElement root, out JsonElement current, params string[] path)
    {
        current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return false;

            if (current.ValueKind == JsonValueKind.String)
            {
                var nested = current.GetString();
                if (string.IsNullOrWhiteSpace(nested)) return false;
                try
                {
                    using var nestedDoc = JsonDocument.Parse(nested);
                    current = nestedDoc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return false;
                }
            }
        }
        return true;
    }
}

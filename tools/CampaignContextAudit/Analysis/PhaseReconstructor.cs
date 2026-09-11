using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

/// <summary>
/// Approximate, tool-driven per-turn phase floor. Production does NOT persist per-turn phase on
/// chat rows (only final state on a separate workflow row), and faithfully replaying
/// CampaignWorkflowEngine here would require its internal artifact/accumulator types. So we infer a
/// MONOTONIC phase floor from the most advanced workflow tool observed up to each user turn. It is
/// labeled approximate in the report; it can lag real phase (CampaignJourney has no distinct tool)
/// but never claims a phase the observed tools don't support.
/// </summary>
public static class PhaseReconstructor
{
    private static readonly string[] RankOrder =
    {
        "DataAnalysis", "EventModels", "CampaignSetup", "PointAccountTypes", "CampaignJourney", "Verification", "Done"
    };

    private static int Rank(string phase)
    {
        var i = Array.IndexOf(RankOrder, phase);
        return i < 0 ? 0 : i;
    }

    private static string? PhaseForTool(string toolName)
    {
        var key = toolName.Replace("_", "").ToLowerInvariant();
        return key switch
        {
            "getallmodels" or "listmodels" or "getmodel" or "getmanymodels"
                or "getmodelattributesforrules" or "savemodel" => "EventModels",
            "upsertcampaign" => "CampaignSetup",
            "upsertpointaccounttype" => "PointAccountTypes",
            "validatecampaign" or "getcampaignassistantcontext" => "CampaignJourney",
            "processevent" => "Verification",
            _ => null
        };
    }

    /// <summary>
    /// Phase floor active when composing each user turn (keyed by user-row Sequence), inferred from
    /// workflow tools whose call sequence is &lt;= that user sequence. Because a turn's own tool calls
    /// occur at later sequences than its user row, this naturally excludes the current turn's
    /// not-yet-executed tools. When <paramref name="overridePhase"/> is set, every turn uses it.
    /// </summary>
    public static IReadOnlyDictionary<long, string> PhaseByUserTurn(
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline,
        string? overridePhase = null)
    {
        var userSeqs = chatRows
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Sequence)
            .OrderBy(s => s)
            .ToList();

        var result = new Dictionary<long, string>();
        foreach (var seq in userSeqs)
        {
            if (!string.IsNullOrWhiteSpace(overridePhase))
            {
                result[seq] = overridePhase!;
                continue;
            }
            var rank = 0;
            foreach (var e in timeline)
            {
                if (e.Sequence > seq) continue;
                var p = PhaseForTool(e.ToolName);
                if (p != null) rank = Math.Max(rank, Rank(p));
            }
            result[seq] = RankOrder[rank];
        }
        return result;
    }
}

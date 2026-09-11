using System.Text.Json;
using System.Text.RegularExpressions;
using CampaignContextAudit.Models;
using CampaignContextAudit.Transcript;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Utility;

namespace CampaignContextAudit.Analysis;

public static partial class OutcomeDetectors
{
    private static readonly string[] SuccessNarrativePatterns =
    {
        "successfully created",
        "journey is ready",
        "campaign is complete",
        "what's built & saved",
        "ready to go once"
    };

    private static readonly string[] ToolUnavailablePatterns =
    {
        "not available",
        "not enabled",
        "tool missing",
        "doesn't appear to be enabled",
        "does not appear to be enabled",
        "no upsert_",
        "not enabled in this session"
    };

    private static readonly HashSet<string> ValidateUpsertTools =
        new(StringComparer.OrdinalIgnoreCase) { "validate_campaign", "upsert_campaign" };

    public static IReadOnlyList<Finding> DetectAll(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline,
        long? workflowSequence = null)
    {
        var callIdToTool = BuildCallIdToToolName(chatRows);
        var findings = new List<Finding>();
        findings.AddRange(CreationIncompleteAtDone(snapshot, workflowSequence));
        findings.AddRange(JourneyEmptyAtSuccess(snapshot, chatRows, timeline, callIdToTool));
        findings.AddRange(SuccessNarrativeDrift(snapshot, chatRows));
        findings.AddRange(McpInvocationErrorCluster(chatRows, callIdToTool));
        findings.AddRange(ValidationUpsertLoop(chatRows, callIdToTool));
        findings.AddRange(ManifestDrift(snapshot, workflowSequence));
        findings.AddRange(FalseToolUnavailable(chatRows));
        findings.AddRange(DetectVerificationDeferred(chatRows, timeline));
        findings.AddRange(VerificationModelRebuildDrift(snapshot, chatRows, timeline));
        return findings;
    }

    private static readonly string[] CatalogReloadUserPhrases =
    [
        "reload model", "refresh catalog", "re-fetch model", "refetch model", "fetch model again",
        "reload the model", "refresh the model", "updated the catalog", "changed the model"
    ];

    public static IReadOnlyList<Finding> VerificationModelRebuildDrift(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline)
    {
        if (snapshot is not { CreationComplete: true })
            return [];

        var resultsByCallId = BuildResultJsonByCallId(chatRows);
        var failedSeqs = timeline
            .Where(e => IsProcessEventTool(e.ToolName)
                        && resultsByCallId.TryGetValue(e.CallId, out var json)
                        && !CampaignWorkflowToolSuccess.LooksSuccessful(json))
            .Select(e => e.Sequence)
            .ToList();

        if (failedSeqs.Count == 0)
            return [];

        var firstFailure = failedSeqs.Min();
        if (UserRequestedCatalogReloadAfterSeq(chatRows, firstFailure))
            return [];

        var findings = new List<Finding>();
        foreach (var e in timeline.Where(e => e.Sequence > firstFailure && IsModelRebuildTool(e.ToolName)))
        {
            findings.Add(new Finding(
                "VERIFICATION_MODEL_REBUILD_DRIFT",
                "degrading",
                $"Post-creation {e.ToolName} after failed process_event (seq {firstFailure}); prefer rule-path fixes unless the user requested catalog reload.",
                [firstFailure.ToString(), e.Sequence.ToString()]));
        }

        return findings;
    }

    private static bool IsProcessEventTool(string toolName) =>
        string.Equals(toolName, "process_event", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "ProcessEvent", StringComparison.OrdinalIgnoreCase);

    private static bool IsModelRebuildTool(string toolName) =>
        string.Equals(toolName, "get_model", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetModel", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "save_model", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "SaveModel", StringComparison.OrdinalIgnoreCase);

    private static bool UserRequestedCatalogReloadAfterSeq(IReadOnlyList<AgentMessageDoc> chatRows, long afterSeq)
    {
        return chatRows
            .Where(r => r.Sequence > afterSeq
                        && string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Any(r => ContainsAnyPhrase(MeaiEnvelope.ExtractPlainText(r.Content), CatalogReloadUserPhrases));
    }

    private static Dictionary<string, string> BuildResultJsonByCallId(IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in chatRows)
        {
            if (string.Equals(row.Role, "tool", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(row.ToolCallId))
                map[row.ToolCallId] = row.ToolResultJson ?? string.Empty;

            foreach (var fr in MeaiEnvelope.ExtractFunctionResults(row.Content))
            {
                if (!string.IsNullOrWhiteSpace(fr.CallId))
                    map[fr.CallId] = fr.ResultRaw ?? string.Empty;
            }
        }

        return map;
    }

    private static readonly string[] HttpPatDeferralPatterns =
    [
        "post /api",
        "pointaccounttype/upsert",
        "paste back",
        "paste the",
        "paste your",
        "4 guids",
        "four guids",
        "not in my tool surface",
        "not surfaced",
        "not on my tool surface"
    ];

    private static readonly HashSet<string> CampaignMutatorTools =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "validate_campaign",
            "upsert_campaign",
            "upsert_point_account_type"
        };

    public static IReadOnlyList<Finding> DetectEventModelsGateBlocksMutators(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline,
        bool? modelGatePassed,
        long? workflowSequence = null)
    {
        if (modelGatePassed != false)
            return [];

        var callIdToTool = BuildCallIdToToolName(chatRows);
        var hasMutatorActivity = chatRows
            .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Any(r =>
            {
                var toolName = ResolveToolName(r, callIdToTool);
                return !string.IsNullOrWhiteSpace(toolName) && CampaignMutatorTools.Contains(toolName);
            })
            || timeline.Any(t => CampaignMutatorTools.Contains(t.ToolName))
            || chatRows.Any(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                                 && MatchesAnyPattern(MeaiEnvelope.ExtractPlainText(r.Content), HttpPatDeferralPatterns));

        if (!hasMutatorActivity)
            return [];

        var hasPatUpsertSuccess = chatRows
            .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Any(r => string.Equals(ResolveToolName(r, callIdToTool), "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                      && ToolOutcomeClassifier.Classify(ResolveToolName(r, callIdToTool), r.ToolResultJson)
                          == ToolOutcomeKind.MutationDigest);

        if (hasPatUpsertSuccess)
            return [];

        var cited = timeline
            .Where(t => CampaignMutatorTools.Contains(t.ToolName))
            .Select(t => t.Sequence.ToString())
            .Concat(chatRows
                .Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                            && MatchesAnyPattern(MeaiEnvelope.ExtractPlainText(r.Content), HttpPatDeferralPatterns))
                .Select(r => r.Sequence.ToString()))
            .Distinct()
            .Take(4)
            .ToList();

        if (cited.Count == 0 && workflowSequence is > 0)
            cited.Add(workflowSequence.Value.ToString());

        return
        [
            new Finding(
                "EVENT_MODELS_GATE_BLOCKS_MUTATORS",
                "blocking",
                "Events gate closed (modelGatePassed=false) but agent attempted campaign mutators or deferred PAT/campaign creation to HTTP.",
                cited)
        ];
    }

    public static IReadOnlyList<Finding> DetectPatMutatorDeferredGateOpen(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline,
        bool? modelGatePassed,
        long? workflowSequence = null)
    {
        if (modelGatePassed != true)
            return [];
        if (snapshot?.PatManifestCount is > 0)
            return [];

        var callIdToTool = BuildCallIdToToolName(chatRows);
        var hasPatUpsertSuccess = chatRows
            .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Any(r => string.Equals(ResolveToolName(r, callIdToTool), "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                      && ToolOutcomeClassifier.Classify(ResolveToolName(r, callIdToTool), r.ToolResultJson)
                          == ToolOutcomeKind.MutationDigest);

        if (hasPatUpsertSuccess)
            return [];

        var hasPatNotFound = chatRows
            .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Any(r => string.Equals(ResolveToolName(r, callIdToTool), "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                      && (r.ToolResultJson ?? "").Contains("not found", StringComparison.OrdinalIgnoreCase));

        var hasHttpDeferral = chatRows
            .Any(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                      && MatchesAnyPattern(MeaiEnvelope.ExtractPlainText(r.Content), HttpPatDeferralPatterns));

        if (!hasPatNotFound && !hasHttpDeferral)
            return [];

        var cited = chatRows
            .Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                        && MatchesAnyPattern(MeaiEnvelope.ExtractPlainText(r.Content), HttpPatDeferralPatterns))
            .Select(r => r.Sequence.ToString())
            .Concat(chatRows
                .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(ResolveToolName(r, callIdToTool), "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                            && (r.ToolResultJson ?? "").Contains("not found", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Sequence.ToString()))
            .Distinct()
            .Take(4)
            .ToList();

        if (cited.Count == 0 && workflowSequence is > 0)
            cited.Add(workflowSequence.Value.ToString());

        return
        [
            new Finding(
                "PAT_MUTATOR_DEFERRED_GATE_OPEN",
                "blocking",
                "Events gate open (modelGatePassed=true) but agent deferred PAT creation to HTTP or upsert_point_account_type failed with not found.",
                cited)
        ];
    }

    /// <summary>
    /// Verification episode: repeated process_event payload cast failures (same schema class).
    /// </summary>
    public static IReadOnlyList<Finding> DetectEventPayloadSchemaLoop(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline)
    {
        if (snapshot is not { CreationComplete: true })
            return [];

        var resultsByCallId = BuildResultJsonByCallId(chatRows);
        var castFailures = timeline
            .Where(e => IsProcessEventTool(e.ToolName)
                        && resultsByCallId.TryGetValue(e.CallId, out var json)
                        && !CampaignWorkflowToolSuccess.LooksSuccessful(json)
                        && LooksLikeEventPayloadCastFailure(json))
            .ToList();

        if (castFailures.Count < 3)
            return [];

        var groups = castFailures
            .GroupBy(e => ClassifyEventPayloadCastFailure(resultsByCallId[e.CallId]))
            .Where(g => g.Count() >= 3)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (groups.Count == 0)
            return [];

        var top = groups[0];
        var cited = top.Select(e => e.Sequence.ToString()).Distinct().Take(4).ToList();
        return
        [
            new Finding(
                "EVENT_PAYLOAD_SCHEMA_LOOP",
                "degrading",
                $"≥3 failed process_event with same payload cast class ({top.Key}) during verification.",
                cited)
        ];
    }

    private static bool LooksLikeEventPayloadCastFailure(string json) =>
        json.Contains("InvalidCastException", StringComparison.OrdinalIgnoreCase)
        || (json.Contains("Expected type", StringComparison.OrdinalIgnoreCase)
            && json.Contains("List", StringComparison.OrdinalIgnoreCase));

    private static string ClassifyEventPayloadCastFailure(string json) =>
        ValidateLoopCoach.MapViolationCode(json) ?? "UNKNOWN_CAST";

    /// <summary>
    /// In-transcript episode: agent stalled on PAT mutators (Events gate closed narrative) and only
    /// upserted PATs after a user message — even when the thread eventually completes successfully.
    /// </summary>
    public static IReadOnlyList<Finding> DetectGateClosedPatStall(
        IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var callIdToTool = BuildCallIdToToolName(chatRows);
        var ordered = chatRows.OrderBy(r => r.Sequence).ToList();
        var firstPatSeq = FindFirstSuccessfulPatUpsertSequence(ordered, callIdToTool);

        var stallRow = ordered
            .FirstOrDefault(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                                 && LooksLikePatGateStall(MeaiEnvelope.ExtractPlainText(r.Content))
                                 && (firstPatSeq is null || r.Sequence < firstPatSeq));

        if (stallRow is null)
            return [];

        if (firstPatSeq is null)
        {
            return
            [
                new Finding(
                    "GATE_CLOSED_PAT_STALL",
                    "blocking",
                    "Agent claimed PAT mutators were unavailable and never completed upsert_point_account_type in this transcript.",
                    [stallRow.Sequence.ToString()])
            ];
        }

        var userIntervention = ordered
            .FirstOrDefault(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase)
                                 && r.Sequence > stallRow.Sequence
                                 && r.Sequence <= firstPatSeq);

        if (userIntervention is null)
            return [];

        var cited = new[] { stallRow.Sequence.ToString(), userIntervention.Sequence.ToString(), firstPatSeq.Value.ToString() }
            .Distinct()
            .Take(4)
            .ToList();

        return
        [
            new Finding(
                "GATE_CLOSED_PAT_STALL",
                "blocking",
                $"Agent claimed PAT mutators unavailable at seq {stallRow.Sequence}; "
                + $"first successful upsert_point_account_type at seq {firstPatSeq} only after user message at seq {userIntervention.Sequence}.",
                cited)
        ];
    }

    public static IReadOnlyList<Finding> DetectPatSkippedInlineManifest(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        bool? modelGatePassed,
        long? workflowSequence = null)
    {
        if (modelGatePassed != true)
            return [];
        if (snapshot is { CreationComplete: true })
            return [];
        if (snapshot?.PatManifestCount is > 0)
            return [];

        var callIdToTool = BuildCallIdToToolName(chatRows);
        var hasPatUpsertSuccess = chatRows
            .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Any(r => string.Equals(ResolveToolName(r, callIdToTool), "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                      && ToolOutcomeClassifier.Classify(ResolveToolName(r, callIdToTool), r.ToolResultJson)
                          == ToolOutcomeKind.MutationDigest);

        if (hasPatUpsertSuccess)
            return [];

        var cited = new List<string>();
        foreach (var row in chatRows.Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var call in MeaiEnvelope.ExtractFunctionCalls(row.Content))
            {
                if (!ValidateUpsertTools.Contains(call.Name))
                    continue;
                if (!LooksLikeSkippedPatInlineManifest(call.ArgumentsRaw))
                    continue;
                cited.Add(row.Sequence.ToString());
                break;
            }
        }

        if (cited.Count == 0 && workflowSequence is > 0)
            cited.Add(workflowSequence.Value.ToString());
        if (cited.Count == 0)
            return [];

        return
        [
            new Finding(
                "PAT_SKIPPED_INLINE_MANIFEST",
                "blocking",
                "Events gate open but agent called validate/upsert campaign with journey or inline fake PAT ids before any successful upsert_point_account_type.",
                cited.Distinct().Take(4).ToList())
        ];
    }

    private static bool LooksLikeSkippedPatInlineManifest(string? argsRaw)
    {
        if (string.IsNullOrWhiteSpace(argsRaw))
            return false;

        var args = argsRaw;
        if (args.Contains("SPENDABLE_PAT_ID", StringComparison.OrdinalIgnoreCase))
            return true;
        if (args.Contains("pat-spendable", StringComparison.OrdinalIgnoreCase))
            return true;
        if (args.Contains("pointAccountManifest", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!args.Contains("journey", StringComparison.OrdinalIgnoreCase))
            return false;

        return args.Contains("\"rules\"", StringComparison.Ordinal)
               || args.Contains("navigation", StringComparison.OrdinalIgnoreCase)
               || args.Contains("pointAccountTypeId", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<Finding> DetectFalseValidationPassZeroRuleSets(
        IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var callIdToTool = BuildCallIdToToolName(chatRows);
        var ordered = chatRows.OrderBy(r => r.Sequence).ToList();
        var findings = new List<Finding>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var row = ordered[i];
            if (!string.Equals(row.Role, "tool", StringComparison.OrdinalIgnoreCase))
                continue;

            var toolName = ResolveToolName(row, callIdToTool);
            if (!string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ToolOutcomeClassifier.Classify(toolName, row.ToolResultJson) != ToolOutcomeKind.ValidateAck)
                continue;

            if (TryReadRuleSetCount(row.ToolResultJson) > 0)
                continue;

            if (!TryReadHasJourney(row.ToolResultJson))
                continue;

            var matched = false;
            for (var j = i + 1; j < ordered.Count && !matched; j++)
            {
                var next = ordered[j];
                if (!string.Equals(next.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var call in MeaiEnvelope.ExtractFunctionCalls(next.Content))
                {
                    if (!string.Equals(call.Name, "upsert_campaign", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!LooksLikeJourneyPayload(call.ArgumentsRaw))
                        continue;

                    findings.Add(new Finding(
                        "FALSE_VALIDATION_PASS_ZERO_RULESETS",
                        "blocking",
                        "validate_campaign returned IsValid with RuleSetCount=0 and HasJourney, then upsert_campaign with journey payload.",
                        [row.Sequence.ToString(), next.Sequence.ToString()]));
                    matched = true;
                    break;
                }
            }
        }

        return findings;
    }

    public static IReadOnlyList<Finding> DetectJourneyShapeNodesNotChildren(
        IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var cited = new List<string>();
        foreach (var row in chatRows.Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var call in MeaiEnvelope.ExtractFunctionCalls(row.Content))
            {
                if (!ValidateUpsertTools.Contains(call.Name))
                    continue;
                if (!LooksLikeJourneyNodesArray(call.ArgumentsRaw))
                    continue;

                cited.Add(row.Sequence.ToString());
                break;
            }
        }

        if (cited.Count == 0)
            return [];

        return
        [
            new Finding(
                "JOURNEY_SHAPE_NODES_NOT_CHILDREN",
                "blocking",
                "validate/upsert args use journey.nodes[] instead of journey.children[].",
                cited.Distinct().Take(4).ToList())
        ];
    }

    private static bool LooksLikeJourneyPayload(string? argsRaw)
    {
        if (string.IsNullOrWhiteSpace(argsRaw))
            return false;

        if (!argsRaw.Contains("journey", StringComparison.OrdinalIgnoreCase))
            return false;

        return argsRaw.Contains("nodes", StringComparison.OrdinalIgnoreCase)
               || argsRaw.Contains("rules", StringComparison.OrdinalIgnoreCase)
               || argsRaw.Contains("children", StringComparison.OrdinalIgnoreCase)
               || argsRaw.Contains("navigation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeJourneyNodesArray(string? argsRaw)
    {
        if (string.IsNullOrWhiteSpace(argsRaw))
            return false;

        if (!argsRaw.Contains("journey", StringComparison.OrdinalIgnoreCase))
            return false;

        return argsRaw.Contains("nodes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadHasJourney(string? toolResultJson)
    {
        var body = toolResultJson ?? "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            return TryReadHasJourney(doc.RootElement);
        }
        catch (JsonException)
        {
            return body.Contains("\"HasJourney\":true", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("\"hasJourney\":true", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryReadHasJourney(JsonElement root)
    {
        if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var inner = JsonDocument.Parse(text.GetString() ?? "{}");
                return TryReadHasJourney(inner.RootElement);
            }
            catch (JsonException) { }
        }

        foreach (var name in new[] { "hasJourney", "HasJourney" })
        {
            if (root.TryGetProperty(name, out var flag) && flag.ValueKind == JsonValueKind.True)
                return true;
        }

        if (root.TryGetProperty("Summary", out var summary))
        {
            foreach (var name in new[] { "hasJourney", "HasJourney" })
            {
                if (summary.TryGetProperty(name, out var flag) && flag.ValueKind == JsonValueKind.True)
                    return true;
            }
        }

        return false;
    }

    private static readonly string[] JourneyVerificationIntentPhrases =
    [
        "test with", "sample payload", "test payload", "process event", "process_event",
        "process the event", "run a test", "test the campaign", "test event", "verify with"
    ];

    public static IReadOnlyList<Finding> DetectVerificationDeferred(
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline)
    {
        var testIntentTurns = chatRows
            .Where(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Count(r => ContainsAnyPhrase(MeaiEnvelope.ExtractPlainText(r.Content), JourneyVerificationIntentPhrases));

        if (testIntentTurns < 2)
            return [];

        var hasProcessEvent = timeline.Any(t =>
            string.Equals(t.ToolName, "process_event", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.ToolName, "ProcessEvent", StringComparison.OrdinalIgnoreCase));

        if (hasProcessEvent)
            return [];

        var cited = chatRows
            .Where(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase)
                        && ContainsAnyPhrase(MeaiEnvelope.ExtractPlainText(r.Content), JourneyVerificationIntentPhrases))
            .Select(r => r.Sequence.ToString())
            .Take(4)
            .ToList();

        return
        [
            new Finding(
                "VERIFICATION_DEFERRED",
                "intent-breaking",
                "User expressed test intent at least twice but no process_event ran.",
                cited)
        ];
    }

    public static IReadOnlyList<Finding> DetectVerificationBlockedNoAllowlist(
        string? workflowContent,
        IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var hasTestIntent = chatRows
            .Any(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase)
                      && ContainsAnyPhrase(MeaiEnvelope.ExtractPlainText(r.Content), JourneyVerificationIntentPhrases));

        if (!hasTestIntent)
            return [];

        if (!TryReadVerificationBlocked(workflowContent, out var blocked) || !blocked)
            return [];

        return
        [
            new Finding(
                "VERIFICATION_BLOCKED_NO_ALLOWLIST",
                "blocking",
                "Tenant campaign test allowlist is empty — draft verification cannot run.",
                [])
        ];
    }

    private static bool TryReadVerificationBlocked(string? workflowContent, out bool blocked)
    {
        blocked = false;
        if (string.IsNullOrWhiteSpace(workflowContent))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(workflowContent);
            var root = doc.RootElement;
            if (root.TryGetProperty("verificationBlockedNoAllowlist", out var flag)
                && flag.ValueKind == JsonValueKind.True)
            {
                blocked = true;
                return true;
            }

            if (root.TryGetProperty("tenantTestAccountAllowlistJson", out var jsonEl)
                && jsonEl.ValueKind == JsonValueKind.String)
            {
                var json = jsonEl.GetString();
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                {
                    blocked = true;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool ContainsAnyPhrase(string? message, IReadOnlyList<string> phrases)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var lower = message.ToLowerInvariant();
        return phrases.Any(p => lower.Contains(p, StringComparison.Ordinal));
    }

    public static IReadOnlyList<Finding> CreationIncompleteAtDone(WorkflowSnapshot? snapshot, long? workflowSequence = null)
    {
        if (snapshot is not { CreationComplete: false, WorkflowPhase: not null }) return [];
        if (!string.Equals(snapshot.WorkflowPhase, "Done", StringComparison.OrdinalIgnoreCase)) return [];

        return
        [
            new Finding(
                "CREATION_INCOMPLETE_AT_DONE",
                "blocking",
                "Workflow phase is Done but creationSnapshot.creationComplete is false.",
                [(workflowSequence ?? 0).ToString()])
        ];
    }

    public static IReadOnlyList<Finding> JourneyEmptyAtSuccess(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyList<ToolEvent> timeline,
        IReadOnlyDictionary<string, string>? callIdToTool = null)
    {
        callIdToTool ??= BuildCallIdToToolName(chatRows);
        if (snapshot?.JourneyRuleSetCount is not 0) return [];

        var hasUpsertDigest = chatRows
            .Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Any(r => string.Equals(ResolveToolName(r, callIdToTool), "upsert_campaign", StringComparison.OrdinalIgnoreCase)
                      && ToolOutcomeClassifier.Classify(ResolveToolName(r, callIdToTool), r.ToolResultJson)
                          == ToolOutcomeKind.MutationDigest);

        var hasSuccessLanguage = chatRows
            .Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .Any(r => MatchesAnyPattern(MeaiEnvelope.ExtractPlainText(r.Content), SuccessNarrativePatterns));

        if (!hasUpsertDigest && !hasSuccessLanguage) return [];

        var cited = timeline
            .Where(e => string.Equals(e.ToolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Sequence.ToString())
            .DefaultIfEmpty("0")
            .Take(3)
            .ToList();

        return
        [
            new Finding(
                "JOURNEY_EMPTY_AT_SUCCESS",
                "blocking",
                "journeyRuleSetCount is 0 after upsert_campaign digest or assistant success language.",
                cited)
        ];
    }

    public static IReadOnlyList<Finding> SuccessNarrativeDrift(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> chatRows)
    {
        if (snapshot?.CreationComplete != false) return [];

        var findings = new List<Finding>();
        foreach (var row in chatRows.Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            var text = MeaiEnvelope.ExtractPlainText(row.Content);
            if (!MatchesAnyPattern(text, SuccessNarrativePatterns)) continue;

            findings.Add(new Finding(
                "SUCCESS_NARRATIVE_DRIFT",
                "blocking",
                "Assistant completion language while creationSnapshot.creationComplete is false.",
                [row.Sequence.ToString()]));
        }

        return findings;
    }

    public static IReadOnlyList<Finding> McpInvocationErrorCluster(
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyDictionary<string, string>? callIdToTool = null)
    {
        callIdToTool ??= BuildCallIdToToolName(chatRows);
        var findings = new List<Finding>();
        foreach (var turn in UserTurnRanges(chatRows))
        {
            var errorsByTool = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in chatRows.Where(r => r.Sequence > turn.Start && r.Sequence <= turn.End))
            {
                if (!string.Equals(row.Role, "tool", StringComparison.OrdinalIgnoreCase)) continue;
                if (ToolOutcomeClassifier.Classify(ResolveToolName(row, callIdToTool), row.ToolResultJson)
                    != ToolOutcomeKind.McpInvocationError) continue;

                var toolName = ResolveToolName(row, callIdToTool);
                if (string.IsNullOrWhiteSpace(toolName)) continue;

                if (!errorsByTool.TryGetValue(toolName, out var seqs))
                {
                    seqs = [];
                    errorsByTool[toolName] = seqs;
                }
                seqs.Add(row.Sequence);
            }

            foreach (var (tool, seqs) in errorsByTool.Where(kv => kv.Value.Count >= 2))
            {
                findings.Add(new Finding(
                    "MCP_INVOCATION_ERROR_CLUSTER",
                    "blocking",
                    $"At least two MCP invocation errors on {tool} within one user turn.",
                    seqs.Select(s => s.ToString()).ToList()));
            }
        }

        return findings;
    }

    public static IReadOnlyList<Finding> ValidationUpsertLoop(
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyDictionary<string, string>? callIdToTool = null)
    {
        callIdToTool ??= BuildCallIdToToolName(chatRows);
        var maxRuleSetCount = 0;
        var failedCycles = 0;
        long? firstFailedSeq = null;
        long? lastFailedSeq = null;

        foreach (var row in chatRows.OrderBy(r => r.Sequence))
        {
            if (!string.Equals(row.Role, "tool", StringComparison.OrdinalIgnoreCase)) continue;
            var toolName = ResolveToolName(row, callIdToTool);
            if (string.IsNullOrWhiteSpace(toolName) || !ValidateUpsertTools.Contains(toolName)) continue;

            var kind = ToolOutcomeClassifier.Classify(toolName, row.ToolResultJson);
            if (kind is ToolOutcomeKind.MutationDigest)
            {
                maxRuleSetCount = Math.Max(maxRuleSetCount, TryReadRuleSetCount(row.ToolResultJson));
                continue;
            }

            if (kind is ToolOutcomeKind.ValidationErrors or ToolOutcomeKind.McpInvocationError)
            {
                failedCycles++;
                firstFailedSeq ??= row.Sequence;
                lastFailedSeq = row.Sequence;
            }
        }

        if (failedCycles < 2 || maxRuleSetCount > 0) return [];

        return
        [
            new Finding(
                "VALIDATION_UPSERT_LOOP",
                "degrading",
                "Two or more failed validate/upsert cycles without ruleSetCount increase.",
                new[] { firstFailedSeq?.ToString() ?? "n/a", lastFailedSeq?.ToString() ?? "n/a" }
                    .Where(s => s != "n/a")
                    .Distinct()
                    .ToList())
        ];
    }

    public static IReadOnlyList<Finding> DetectStallAndAbort(
        WorkflowSnapshot? snapshot,
        IReadOnlyList<AgentMessageDoc> allRows,
        long effectiveWallMs,
        int stallMs,
        int abortMs)
    {
        if (effectiveWallMs <= 0) return [];
        if (HasJourneyDelivery(snapshot, allRows)) return [];

        var userSeq = allRows.FirstOrDefault(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase))?.Sequence;
        var lastSeq = allRows.OrderBy(r => r.Sequence).LastOrDefault()?.Sequence;
        var seqs = new List<string>();
        if (userSeq is > 0) seqs.Add(userSeq.Value.ToString());
        if (lastSeq is > 0 && lastSeq != userSeq) seqs.Add(lastSeq.Value.ToString());

        if (effectiveWallMs >= abortMs)
        {
            return
            [
                new Finding(
                    "CREATION_ABORTED",
                    "blocking",
                    $"Session ran ~{effectiveWallMs:N0} ms with no journey delivery.",
                    seqs)
            ];
        }

        if (effectiveWallMs >= stallMs)
        {
            return
            [
                new Finding(
                    "SESSION_STALLED_NO_DELIVERY",
                    "blocking",
                    $"Session ran ~{effectiveWallMs:N0} ms with no journey delivery.",
                    seqs)
            ];
        }

        return [];
    }

    public static IReadOnlyList<Finding> ApplyValidationLoopSeverity(
        IReadOnlyList<Finding> findings,
        WorkflowSnapshot? snapshot)
    {
        if (snapshot?.JourneyRuleSetCount is > 0) return findings;

        return findings.Select(f =>
            f.Code == "VALIDATION_UPSERT_LOOP"
                ? new Finding(f.Code, "blocking", f.Summary, f.CitedSequences)
                : f).ToList();
    }

    private static bool HasJourneyDelivery(WorkflowSnapshot? snapshot, IReadOnlyList<AgentMessageDoc> chatRows)
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

    internal static bool HasJourneyDeliveryForGrading(WorkflowSnapshot? snapshot, IReadOnlyList<AgentMessageDoc> chatRows) =>
        HasJourneyDelivery(snapshot, chatRows);

    public static IReadOnlyList<Finding> ManifestDrift(WorkflowSnapshot? snapshot, long? workflowSequence = null)
    {
        if (snapshot is not { PatManifestCount: > 0, VerificationPatCount: 0 }) return [];

        return
        [
            new Finding(
                "MANIFEST_DRIFT",
                "degrading",
                "Workflow pointAccountManifest has items but verificationRecord manifest is empty.",
                [(workflowSequence ?? 0).ToString()])
        ];
    }

    public static IReadOnlyList<Finding> FalseToolUnavailable(IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var ordered = chatRows.OrderBy(r => r.Sequence).ToList();
        var findings = new List<Finding>();

        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var text = MeaiEnvelope.ExtractPlainText(current.Content);
            if (!MatchesAnyPattern(text, ToolUnavailablePatterns)) continue;

            var claimedTools = ExtractMentionedTools(text);
            if (claimedTools.Count == 0) continue;

            var next = ordered[i + 1];
            if (!string.Equals(next.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var call in MeaiEnvelope.ExtractFunctionCalls(next.Content))
            {
                if (!claimedTools.Contains(call.Name)) continue;

                findings.Add(new Finding(
                    "FALSE_TOOL_UNAVAILABLE",
                    "degrading",
                    $"Assistant seq {current.Sequence} claims {call.Name} unavailable; seq {next.Sequence} invokes it.",
                    [current.Sequence.ToString(), next.Sequence.ToString()]));
                break;
            }
        }

        return findings;
    }

    private static Dictionary<string, string> BuildCallIdToToolName(IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in chatRows)
        {
            if (!string.Equals(row.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var call in MeaiEnvelope.ExtractFunctionCalls(row.Content))
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

    private static long? FindFirstSuccessfulPatUpsertSequence(
        IReadOnlyList<AgentMessageDoc> ordered,
        IReadOnlyDictionary<string, string> callIdToTool)
    {
        foreach (var row in ordered)
        {
            if (!string.Equals(row.Role, "tool", StringComparison.OrdinalIgnoreCase))
                continue;

            var toolName = ResolveToolName(row, callIdToTool);
            if (!string.Equals(toolName, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase))
                continue;

            if (LooksLikePatUpsertSuccess(row.ToolResultJson))
                return row.Sequence;
        }

        return null;
    }

    private static bool LooksLikePatUpsertSuccess(string? toolResultJson)
    {
        var raw = toolResultJson ?? "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (raw.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return false;
        if (ToolOutcomeClassifier.Classify("upsert_point_account_type", raw) == ToolOutcomeKind.McpInvocationError)
            return false;

        return raw.Contains("\"name\"", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("extAccountId", StringComparison.OrdinalIgnoreCase)
               || ToolOutcomeClassifier.Classify("upsert_point_account_type", raw) == ToolOutcomeKind.MutationDigest;
    }

    private static bool LooksLikePatGateStall(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var mentionsPat = text.Contains("upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("point account type", StringComparison.OrdinalIgnoreCase)
                          || MatchesAnyPattern(text, HttpPatDeferralPatterns);

        if (!mentionsPat)
            return false;

        if (MatchesAnyPattern(text, HttpPatDeferralPatterns))
            return true;

        return text.Contains("don't see", StringComparison.OrdinalIgnoreCase)
               || text.Contains("do not see", StringComparison.OrdinalIgnoreCase)
               || text.Contains("cannot see", StringComparison.OrdinalIgnoreCase)
               || text.Contains("can't see", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not in this session", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not on this session", StringComparison.OrdinalIgnoreCase)
               || text.Contains("tool surface", StringComparison.OrdinalIgnoreCase);
    }

    private static int TryReadRuleSetCount(string? toolResultJson)
    {
        var body = toolResultJson ?? "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            return TryReadRuleSetCount(doc.RootElement);
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static int TryReadRuleSetCount(JsonElement root)
    {
        if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var inner = JsonDocument.Parse(text.GetString() ?? "{}");
                return TryReadRuleSetCount(inner.RootElement);
            }
            catch (JsonException) { }
        }

        foreach (var name in new[] { "ruleSetCount", "RuleSetCount" })
        {
            if (root.TryGetProperty(name, out var count) && count.TryGetInt32(out var n))
                return n;
        }

        if (root.TryGetProperty("Summary", out var summary))
        {
            foreach (var name in new[] { "ruleSetCount", "RuleSetCount" })
            {
                if (summary.TryGetProperty(name, out var count) && count.TryGetInt32(out var n))
                    return n;
            }
        }

        return 0;
    }

    private static HashSet<string> ExtractMentionedTools(string text)
    {
        var tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ToolNameRegex().Matches(text))
        {
            var name = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(name))
                tools.Add(name);
        }
        return tools;
    }

    private static bool MatchesAnyPattern(string text, IEnumerable<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record UserTurnRange(long Start, long End);

    private static IEnumerable<UserTurnRange> UserTurnRanges(IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var userSeqs = chatRows
            .Where(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Sequence)
            .OrderBy(s => s)
            .ToList();

        for (var i = 0; i < userSeqs.Count; i++)
        {
            var start = userSeqs[i];
            var end = i + 1 < userSeqs.Count ? userSeqs[i + 1] : long.MaxValue;
            yield return new UserTurnRange(start, end);
        }
    }

    [GeneratedRegex("`([a-z][a-z0-9_]+)`", RegexOptions.IgnoreCase)]
    private static partial Regex ToolNameRegex();
}

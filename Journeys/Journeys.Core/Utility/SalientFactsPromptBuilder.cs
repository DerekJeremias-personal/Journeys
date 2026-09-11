using System.Text;
using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Workflow;

namespace Journeys.Core.Utility;

/// <summary>
/// Compact business-intent block for SESSION — survives history eviction via WORKFLOW ARTIFACTS.
/// </summary>
public static class SalientFactsPromptBuilder
{
    private const int MaxFieldChars = 800;
    private const int MaxOpeningUserChars = 600;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? Build(CampaignWorkflowState state, string? openingUserMessage = null)
    {
        var briefJson = state.Artifacts.CampaignDesignBriefApproved
                        ?? state.Artifacts.CampaignDesignBriefProposed;

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(briefJson))
            AppendBriefLines(lines, briefJson);

        AppendEventModelsGateLines(lines, state);
        AppendBuildContextPackageLines(lines, state);
        AppendBuildGateLines(lines, state);
        AppendJourneyGateLines(lines, state);
        AppendPatDeferralLines(lines, state);
        AppendJourneyPatternLines(lines, state, briefJson, openingUserMessage);
        AppendJourneyContractPinLines(lines, state);
        AppendCreationLines(lines, state);
        AppendVerificationLines(lines, state);
        AppendVerificationDebugLines(lines, state);

        if (lines.Count == 0 && !string.IsNullOrWhiteSpace(openingUserMessage))
        {
            var trimmed = openingUserMessage.Trim();
            if (trimmed.Length > MaxOpeningUserChars)
                trimmed = trimmed[..MaxOpeningUserChars] + "…";
            lines.Add($"openingUserIntent: {trimmed}");
        }

        if (lines.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("SALIENT FACTS (never evicted — authoritative business intent)");
        foreach (var line in lines)
            sb.AppendLine(line);
        return sb.ToString().TrimEnd();
    }

    private static void AppendBriefLines(List<string> lines, string briefJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(briefJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            AppendField(lines, "objective", ReadString(root, "objective"));
            AppendField(lines, "audience", ReadString(root, "audience"));
            AppendField(lines, "mechanic", ReadString(root, "mechanic"));
            AppendField(lines, "successCriteria", ReadString(root, "successCriteria"));

            var campaignClass = ReadNestedString(root, "recommendedProgramShape", "campaignClass");
            AppendField(lines, "campaignClass", campaignClass);

            if (root.TryGetProperty("plannedEventModelIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
            {
                var idList = ids.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (idList.Count > 0)
                    AppendField(lines, "plannedEventModelIds", string.Join(", ", idList));
            }
        }
        catch (JsonException)
        {
            AppendField(lines, "designBriefDigest", Truncate(briefJson, MaxFieldChars));
        }
    }

    private static void AppendField(List<string> lines, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        lines.Add($"{key}: {Truncate(value.Trim(), MaxFieldChars)}");
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static string? ReadNestedString(JsonElement obj, string parent, string child)
    {
        if (!obj.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object)
            return null;
        return ReadString(p, child);
    }

    private static void AppendEventModelsGateLines(List<string> lines, CampaignWorkflowState state)
    {
        if (state.UserSkippedEventModels || state.CampaignKind != CampaignWorkflowKind.EventDriven)
            return;

        var briefJson = state.Artifacts.CampaignDesignBriefApproved
                        ?? state.Artifacts.CampaignDesignBriefProposed;
        if (string.IsNullOrWhiteSpace(briefJson))
            return;

        var readiness = EventModelsReadiness.Evaluate(state);
        if (readiness.IsReady)
            return;

        lines.Add("eventsGate: closed");
        lines.Add("nextStep: resolve event model before PAT/campaign upsert — mutators filtered not missing");

        var expectedId = EventModelExpectedIdResolver.Resolve(state);
        if (!string.IsNullOrWhiteSpace(expectedId))
            lines.Add($"expectedEventModelId: {expectedId}");
    }

    private static void AppendBuildContextPackageLines(List<string> lines, CampaignWorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed)
            && string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefApproved))
            return;

        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true })
        {
            lines.Add("buildSubStep: complete");
            if (!string.IsNullOrWhiteSpace(state.Artifacts.EventPayloadScaffold))
                AppendField(lines, "eventPayloadScaffold", state.Artifacts.EventPayloadScaffold);

            var campaignId = CreationSnapshotArtifact.Read(state)?.CampaignId;
            if (!string.IsNullOrWhiteSpace(campaignId))
            {
                AppendField(lines, "processEventCampaignId", campaignId);
                AppendField(lines, "previewTierMoveCampaignId", campaignId + " — entity Id from upsert digest");
            }

            var externalId = CreationSnapshotArtifact.Read(state)?.CampaignExternalId;
            if (!string.IsNullOrWhiteSpace(externalId))
                AppendField(lines, "campaignExternalRef", externalId);
            return;
        }

        if (state.CampaignKind == CampaignWorkflowKind.EventDriven
            && !state.UserSkippedEventModels
            && !EventModelsReadiness.Evaluate(state).IsReady)
            return;

        var subStep = WorkflowSkillRegistry.ResolveBuildSubStep(state);
        lines.Add($"buildSubStep: {subStep.ToString().ToLowerInvariant()}");

        var brief = state.Artifacts.CampaignDesignBriefApproved
                    ?? state.Artifacts.CampaignDesignBriefProposed;
        var episode = RuleEpisodeInferrer.Infer(brief, null, state.Artifacts.LastValidateViolationCode);
        lines.Add($"ruleEpisode: {episode.ToString().ToLowerInvariant()}");

        if (subStep == CampaignBuildSubStep.PatRequired)
        {
            lines.Add("patToolOnSurface: yes when listed in SKILL MANIFEST toolsThisTurn");
            if (EventModelsReadiness.Evaluate(state).IsReady)
            {
                lines.Add("patMutatorsAvailable: upsert_point_account_type on tool surface now — do not claim unavailable; do not ask user to proceed");
                if (state.Phase == CampaignWorkflowPhase.EventModels)
                {
                    lines.Add("buildSubStepGovernanceCrossPhase: PatRequired — upsert_point_account_type now despite EventModels phase");
                    lines.Add("continuationHopSuppressed: PatRequired — stay on campaign mutators; EventModels prep deferred");
                }
            }
        }

        if (subStep == CampaignBuildSubStep.JourneyRequired)
        {
            lines.Add("campaignDtoEventsShape: stringGuidArray — events[] holds event model id strings only; modelMetaData at campaign root keyed by id");
            lines.Add("depositPointsOutcomeTemplate: PathValueProvider event.ordertotal + PointsPerDollar — not Aggregate or SimpleCalculation");
            if (HasResolvedProcessEventModel(state))
                lines.Add("earnAmountPath: event.ordertotal — PathValueProvider + PointsPerDollar; not AggregateValueProvider on DepositPointsOutcome");
            lines.Add("processEventPreReqs: get_account(allowlisted ext id) → process_event(campaignId from snapshot) → advance timeOfOccurrence per event");

            if (state.Phase == CampaignWorkflowPhase.EventModels
                && EventModelsReadiness.Evaluate(state).IsReady)
            {
                lines.Add("buildSubStepGovernanceCrossPhase: JourneyRequired — validate_campaign then upsert_campaign despite EventModels phase");
            }

            var eventModelId = !string.IsNullOrWhiteSpace(state.Artifacts.SelectedEventModelId)
                ? state.Artifacts.SelectedEventModelId
                : EventModelExpectedIdResolver.Resolve(state)
                  ?? EventModelContractsAccumulator.ReadResolved(state)
                      .FirstOrDefault(d => d.IsProcessEventEligible)?.EventModelId;
            if (!string.IsNullOrWhiteSpace(eventModelId))
                lines.Add($"eventModelIdForEventsArray: {eventModelId}");

            if (!string.IsNullOrWhiteSpace(state.Artifacts.JourneyScaffold))
                lines.Add("journeyScaffoldPinned: yes — use WORKFLOW ARTIFACTS skeleton before first validate_campaign");

            if (!string.IsNullOrWhiteSpace(state.Artifacts.JourneyAuthoringTemplateJson))
                lines.Add("journeyAuthoringTemplatePinned: yes — copy children[] tree from WORKFLOW ARTIFACTS; never nodes[]");

            var partialSnap = CreationSnapshotArtifact.Read(state);
            if (!string.IsNullOrWhiteSpace(partialSnap?.CampaignId))
            {
                AppendField(lines, "processEventCampaignId", partialSnap.CampaignId);
                AppendField(lines, "previewTierMoveCampaignId", partialSnap.CampaignId + " — entity Id from upsert digest");
            }

            if (!string.IsNullOrWhiteSpace(partialSnap?.CampaignExternalId))
                AppendField(lines, "campaignExternalRef", partialSnap.CampaignExternalId);
        }

        if (VerificationDebugContext.IsActiveDebug(state))
            lines.Add("verificationDebug: rule-path fix preferred — upsert_campaign + sampleScaffold; do not rebuild model");

        var validateFix = state.Artifacts.LastValidateFailedBeforeUpsert
            ? ValidateLoopCoach.MapSalientFixLine("REVALIDATE_BEFORE_UPSERT")
            : ValidateLoopCoach.MapSalientFixLine(state.Artifacts.LastValidateViolationCode);
        if (!string.IsNullOrWhiteSpace(validateFix))
            lines.Add(validateFix);
    }

    private static bool HasResolvedProcessEventModel(CampaignWorkflowState state) =>
        EventModelContractsAccumulator.ReadResolved(state).Any(d => d.IsProcessEventEligible);

    private static void AppendBuildGateLines(List<string> lines, CampaignWorkflowState state)
    {
        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true })
            return;

        if (state.CampaignKind == CampaignWorkflowKind.EventDriven
            && !state.UserSkippedEventModels
            && !EventModelsReadiness.Evaluate(state).IsReady)
            return;

        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0)
            return;

        if (string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed)
            && string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefApproved))
            return;

        lines.Add("buildGate: patRequired — see SKILL MANIFEST nextStep");
    }

    private static void AppendJourneyGateLines(List<string> lines, CampaignWorkflowState state)
    {
        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true })
            return;

        if (state.CampaignKind == CampaignWorkflowKind.EventDriven
            && !state.UserSkippedEventModels
            && !EventModelsReadiness.Evaluate(state).IsReady)
            return;

        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0)
            return;

        var snap = CreationSnapshotArtifact.Read(state);
        if (snap is { JourneyRuleSetCount: > 0 })
            return;

        lines.Add("journeyGate: rulesRequired — see SKILL MANIFEST; do not upsert journey until validate RuleSetCount > 0");
    }

    private static void AppendPatDeferralLines(List<string> lines, CampaignWorkflowState state)
    {
        if (state.Artifacts.PatUpsertPending)
            lines.Add("patUpsertPending: yes — call upsert_point_account_type on next segment");

        if (!state.Artifacts.PatHttpDeferralShown
            && !state.Artifacts.PatUpsertPending
            && !MutatorSurfaceDeferralPatterns.LooksLikeDeferral(state.Artifacts.LastToolRemediationSummary))
        {
            return;
        }

        lines.Add("patCreation: pending — upsert_point_account_type available; do not HTTP-defer or ask for pasted GUIDs");
    }

    private static void AppendJourneyPatternLines(
        List<string> lines,
        CampaignWorkflowState state,
        string? briefJson,
        string? openingUserMessage)
    {
        if (state.Artifacts.JourneyPatternPrepComplete
            && !string.IsNullOrWhiteSpace(state.Artifacts.JourneyPatternId))
        {
            lines.Add($"journeyPatternId: {state.Artifacts.JourneyPatternId}");
            lines.Add("journeySkeletonPinned: yes — use WORKFLOW ARTIFACTS skeleton before first validate_campaign");
            return;
        }

        if (MentionsTierLadderIntent(briefJson) || MentionsTierLadderIntent(openingUserMessage))
            lines.Add("journeyPatternRecommended: tier-navigation-point-balance — skeleton pins when PAT manifest is ready");
    }

    private static void AppendJourneyContractPinLines(List<string> lines, CampaignWorkflowState state)
    {
        if (!state.Artifacts.JourneyContractSummaryPinActive
            || !state.Artifacts.JourneyContractSummaryFetchedThisEpisode)
            return;

        var matrixVersion = state.Artifacts.JourneyContractSummaryMatrixVersion ?? "unknown";
        lines.Add($"journeyContractPinned: yes (matrixVersion: {matrixVersion})");

        if (!string.IsNullOrWhiteSpace(state.Artifacts.JourneyContractCriticalRowIds))
            lines.Add($"contractCriticalRows: {state.Artifacts.JourneyContractCriticalRowIds}");
    }

    private static bool MentionsTierLadderIntent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.ToLowerInvariant();
        return lower.Contains("tier", StringComparison.Ordinal)
               || lower.Contains("bronze", StringComparison.Ordinal)
               || lower.Contains("silver", StringComparison.Ordinal)
               || lower.Contains("gold", StringComparison.Ordinal)
               || lower.Contains("platinum", StringComparison.Ordinal);
    }

    private static void AppendCreationLines(List<string> lines, CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        if (snap is not { CreationComplete: true })
            return;

        lines.Add("creationComplete: yes");
        AppendField(lines, "campaignId", snap.CampaignId);
        AppendField(lines, "campaignStatus", snap.CampaignStatus);
        if (!string.IsNullOrWhiteSpace(snap.CampaignExternalId))
            AppendField(lines, "campaignExternalRef", snap.CampaignExternalId);
        if (!string.IsNullOrWhiteSpace(snap.CampaignId))
            AppendField(lines, "previewTierMoveCampaignId", snap.CampaignId + " — entity Id from upsert digest");

        if (state.CampaignKind == CampaignWorkflowKind.EventDriven
            && string.Equals(snap.CampaignStatus, CampaignStatusStrings.Draft, StringComparison.OrdinalIgnoreCase))
        {
            var verified = !string.IsNullOrWhiteSpace(state.Artifacts.VerificationRecord)
                && ToolResultSuccessEvaluator.LooksSuccessful(state.Artifacts.VerificationRecord)
                && !VerificationRecordManifestBridge.IsManifestSeedOnly(state.Artifacts.VerificationRecord);
            if (!verified)
            {
                lines.Add("draftVerification: pass campaignId on process_event — Live promotion NOT required");
                lines.Add("nextStep: get_account → process_event(campaignId) — not Live promotion");
            }
            else
            {
                lines.Add("nextStep: user may request Live promotion; do not promote without explicit approval");
            }
        }

        lines.Add($"journeyRuleSetCount: {snap.JourneyRuleSetCount}");

        if (snap.PointAccountTypes.Count > 0)
        {
            const int maxPatIds = 8;
            var ids = snap.PointAccountTypes.Select(p => p.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            var display = ids.Count <= maxPatIds
                ? string.Join(", ", ids)
                : string.Join(", ", ids.Take(maxPatIds)) + $" (+{ids.Count - maxPatIds} more)";
            AppendField(lines, "pointAccountTypeIds", display);
        }

        AppendResolvedEventModelIds(lines, state);
    }

    private static void AppendResolvedEventModelIds(List<string> lines, CampaignWorkflowState state)
    {
        var resolved = EventModelContractsAccumulator.ReadResolved(state);
        if (resolved.Count == 0)
            return;

        var ids = resolved
            .Select(c => c.EventModelId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return;

        const int maxChars = 200;
        var display = string.Join(", ", ids);
        if (display.Length > maxChars)
            display = display[..maxChars] + "…";

        lines.Add($"resolvedEventModelIds: {display}");
    }

    private static void AppendVerificationLines(List<string> lines, CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        var creationComplete = snap is { CreationComplete: true };
        var hasVerificationContext = creationComplete
            || !string.IsNullOrWhiteSpace(state.Artifacts.VerificationTestAccountId)
            || !string.IsNullOrWhiteSpace(state.Artifacts.TenantTestAccountAllowlistJson)
            || state.Artifacts.VerificationBlockedNoAllowlist;

        if (!hasVerificationContext)
            return;

        if (state.Artifacts.VerificationBlockedNoAllowlist)
        {
            lines.Add("draftTestAccounts: (none — verification blocked)");
            lines.Add("verificationBlocked: yes");
        }
        else if (!string.IsNullOrWhiteSpace(state.Artifacts.TenantTestAccountAllowlistJson))
        {
            var allowlist = ReadAllowlistJson(state.Artifacts.TenantTestAccountAllowlistJson);
            if (allowlist.Count == 0)
                lines.Add("draftTestAccounts: (none — verification blocked)");
            else
                AppendField(lines, "draftTestAccounts", string.Join(", ", allowlist));
        }

        AppendField(lines, "verificationTestAccountId", state.Artifacts.VerificationTestAccountId);
        lines.Add($"verificationAccountConfirmed: {(state.Artifacts.VerificationAccountConfirmed ? "yes" : "no")}");

        var verificationComplete = !string.IsNullOrWhiteSpace(state.Artifacts.VerificationRecord)
            && ToolResultSuccessEvaluator.LooksSuccessful(state.Artifacts.VerificationRecord);
        lines.Add($"verificationComplete: {(verificationComplete ? "yes" : "no")}");
    }

    private static void AppendVerificationDebugLines(List<string> lines, CampaignWorkflowState state)
    {
        if (!VerificationDebugContext.IsActiveDebug(state))
            return;

        lines.Add("verificationDebug: rule-path fix preferred — upsert_campaign + sampleScaffold; do not rebuild model");
    }

    private static IReadOnlyList<string> ReadAllowlistJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

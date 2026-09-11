using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// History budget trimmer — shrinks oversized tool rows before dropping whole user segments.
/// Optionally pins the first user segment and the most recently completed segment.
/// </summary>
public static class CampaignAgentTokenBudget
{
    public const int DefaultMaxUserTurns = 40;
    public const int DefaultMaxChars = 80000;

    private static readonly string[] ShrinkPriorityTools =
    [
        "list_models", "ListModels", "get_all_models", "GetAllModels",
        "get_rules_engine_contract_summary", "GetRulesEngineContractSummary",
        "get_rule_pattern_recipes", "GetRulePatternRecipes",
        "list_campaigns", "ListCampaigns",
        "get_campaign_assistant_context", "GetCampaignAssistantContext",
        "get_model", "GetModel", "get_campaign", "GetCampaign",
        "validate_campaign", "ValidateCampaign",
        "upsert_campaign", "UpsertCampaign",
        "upsert_point_account_type", "UpsertPointAccountType",
        "save_model", "SaveModel"
    ];

    public static IReadOnlyList<AgentMessage> Apply(
        IReadOnlyList<AgentMessage> orderedBySequence,
        HistoryBudgetOptions? options = null)
    {
        options ??= HistoryBudgetOptions.Default;
        return ApplyCore(orderedBySequence, options);
    }

    public static IReadOnlyList<AgentMessage> Apply(
        IReadOnlyList<AgentMessage> orderedBySequence,
        bool pinFirstUserSegment,
        int maxUserTurns = DefaultMaxUserTurns,
        int maxChars = DefaultMaxChars) =>
        Apply(orderedBySequence, new HistoryBudgetOptions
        {
            PinFirstUserSegment = pinFirstUserSegment,
            MaxUserTurns = maxUserTurns,
            MaxChars = maxChars
        });

    private static IReadOnlyList<AgentMessage> ApplyCore(
        IReadOnlyList<AgentMessage> orderedBySequence,
        HistoryBudgetOptions options)
    {
        var list = orderedBySequence.OrderBy(m => m.Sequence).Select(Clone).ToList();
        TrimLeadingUntilFirstUser(list);

        while (list.Count > 0)
        {
            if (!IsOverBudget(list, options))
                break;

            if (options.ShrinkToolResults && TryShrinkOneRow(list, options))
                continue;

            if (!RemoveOldestRemovableUserSegment(list, options))
                break;

            TrimLeadingUntilFirstUser(list);
        }

        return list;
    }

    private static bool IsOverBudget(List<AgentMessage> list, HistoryBudgetOptions options)
    {
        var chars = CampaignAgentHistoryCharCounter.CountEffective(list);
        var userTurns = list.Count(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        return chars > options.MaxChars || userTurns > options.MaxUserTurns;
    }

    private static bool TryShrinkOneRow(List<AgentMessage> list, HistoryBudgetOptions options)
    {
        var threshold = options.ShrinkThresholdChars;
        var toolCallIds = CollectToolCallIdsOnRows(list);
        var pick = FindLargestShrinkable(list, threshold, requireOverThreshold: true, toolCallIds, options);
        if (pick is not null)
            return ApplyShrinkPick(list, pick.Value, threshold, toolCallIds);

        return TryAggressiveShrinkOneRow(list, options);
    }

    /// <summary>When pinned segments block drops, stub the largest remaining tool/embedded result even if under threshold.</summary>
    private static bool TryAggressiveShrinkOneRow(List<AgentMessage> list, HistoryBudgetOptions options)
    {
        var toolCallIds = CollectToolCallIdsOnRows(list);
        var pick = FindLargestShrinkable(list, threshold: int.MaxValue, requireOverThreshold: false, toolCallIds, options);
        if (pick is not null)
            return ApplyShrinkPick(list, pick.Value, int.MaxValue, toolCallIds);

        return TryTruncateLargestAssistantText(list, aggressive: true);
    }

    private static HashSet<string> CollectToolCallIdsOnRows(IReadOnlyList<AgentMessage> list)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in list)
        {
            if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(m.ToolCallId))
                set.Add(m.ToolCallId);
        }

        return set;
    }

    private static bool TryTruncateLargestAssistantText(List<AgentMessage> list, bool aggressive = false)
    {
        var toolCallIds = CollectToolCallIdsOnRows(list);
        var bestIndex = -1;
        var bestLen = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var row = list[i];
            if (!string.Equals(row.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(row.Content))
                continue;

            var len = MeaiEmbeddedResultShrinker.IsMeaiEnvelope(row.Content)
                ? MeaiEmbeddedResultShrinker.CountEffectiveAssistantContentChars(row.Content, toolCallIds)
                : row.Content.Length;

            if (len > bestLen)
            {
                bestLen = len;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        var content = list[bestIndex].Content!;
        if (MeaiEmbeddedResultShrinker.IsMeaiEnvelope(content))
        {
            var minChars = aggressive ? 256 : 2048;
            if (!MeaiEmbeddedResultShrinker.TryTruncateOneContentBlock(content, 2048, out var updated, minChars))
                return false;
            list[bestIndex] = CloneWithContent(list[bestIndex], updated);
            return true;
        }

        if (!aggressive || content.Length <= 256)
            return false;

        list[bestIndex] = CloneWithContent(list[bestIndex],
            content[..2048] + "\n\n[historyTextTrimmed: narrative shortened for history budget]");
        return true;
    }

    private static bool ApplyShrinkPick(List<AgentMessage> list, ShrinkPick pick, int threshold, IReadOnlySet<string> toolCallIds)
    {
        if (pick.IsAssistant)
        {
            if (!MeaiEmbeddedResultShrinker.TryShrinkOneResult(
                    list[pick.Index].Content!, threshold, ShrinkPriorityTools, out var updated, toolCallIds))
                return false;
            list[pick.Index] = CloneWithContent(list[pick.Index], updated);
            return true;
        }

        var stub = ToolResultHistoryStub.Build(list[pick.Index].ToolName, pick.Size);
        list[pick.Index] = CloneWithToolResult(list[pick.Index], stub);
        return true;
    }

    private readonly record struct ShrinkPick(int Index, int Size, int Priority, bool IsAssistant, string? ToolName);

    private static ShrinkPick? FindLargestShrinkable(
        List<AgentMessage> list,
        int threshold,
        bool requireOverThreshold,
        IReadOnlySet<string> toolCallIds,
        HistoryBudgetOptions options)
    {
        ShrinkPick? best = null;
        var segments = GetUserSegmentRanges(list);
        var protectedIndices = GetProtectedSegmentIndices(segments.Count, options);
        for (var segIdx = 0; segIdx < segments.Count; segIdx++)
        {
            var (start, length) = segments[segIdx];
            for (var i = start; i < start + length; i++)
            {
                var row = list[i];
                if (string.Equals(row.Role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    var size = row.ToolResultJson?.Length ?? 0;
                    if (size == 0 || row.ToolResultJson?.Contains("\"historyStub\":true", StringComparison.Ordinal) == true)
                        continue;
                    if (requireOverThreshold && size <= threshold)
                        continue;
                    if (ShouldSkipContractSummaryShrink(options, protectedIndices, segIdx, row.ToolName))
                        continue;
                    var priority = ShrinkPriority(row.ToolName);
                    if (best is null || priority < best.Value.Priority
                        || (priority == best.Value.Priority && size > best.Value.Size))
                        best = new ShrinkPick(i, size, priority, false, row.ToolName);
                    continue;
                }

                if (!string.Equals(row.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(row.Content)
                    || !MeaiEmbeddedResultShrinker.IsMeaiEnvelope(row.Content))
                    continue;

                if (!MeaiEmbeddedResultShrinker.TryGetLargestEmbeddedResultSize(
                        row.Content, threshold, requireOverThreshold, out var embSize, out var toolName, toolCallIds))
                    continue;

                if (ShouldSkipContractSummaryShrink(options, protectedIndices, segIdx, toolName))
                    continue;

                var embPriority = ShrinkPriority(toolName);
                if (best is null || embPriority < best.Value.Priority
                    || (embPriority == best.Value.Priority && embSize > best.Value.Size))
                    best = new ShrinkPick(i, embSize, embPriority, true, toolName);
            }
        }

        return best;
    }

    private static bool ShouldSkipContractSummaryShrink(
        HistoryBudgetOptions options,
        HashSet<int> protectedIndices,
        int segmentIndex,
        string? toolName)
    {
        if (!options.JourneyContractSummaryPinActive || !protectedIndices.Contains(segmentIndex))
            return false;

        return IsContractSummaryTool(toolName);
    }

    private static bool IsContractSummaryTool(string? toolName) =>
        string.Equals(toolName, "get_rules_engine_contract_summary", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetRulesEngineContractSummary", StringComparison.OrdinalIgnoreCase);

    private static int ShrinkPriority(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return ShrinkPriorityTools.Length;

        for (var i = 0; i < ShrinkPriorityTools.Length; i++)
        {
            if (string.Equals(toolName, ShrinkPriorityTools[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return ShrinkPriorityTools.Length;
    }

    private static bool RemoveOldestRemovableUserSegment(List<AgentMessage> list, HistoryBudgetOptions options)
    {
        var segments = GetUserSegmentRanges(list);
        if (segments.Count == 0)
        {
            list.Clear();
            return false;
        }

        var protectedIndices = GetProtectedSegmentIndices(segments.Count, options);

        for (var i = 0; i < segments.Count; i++)
        {
            if (protectedIndices.Contains(i))
                continue;

            var (start, length) = segments[i];
            list.RemoveRange(start, length);
            return true;
        }

        return false;
    }

    internal static HashSet<int> GetProtectedSegmentIndices(int segmentCount, HistoryBudgetOptions options)
    {
        var protectedIndices = new HashSet<int>();
        if (segmentCount == 0)
            return protectedIndices;

        if (options.PinFirstUserSegment)
            protectedIndices.Add(0);

        if (options.PinMostRecentCompletedSegment && segmentCount >= 3)
            protectedIndices.Add(segmentCount - 2);

        if (segmentCount >= 3)
            protectedIndices.Add(segmentCount - 1);
        return protectedIndices;
    }

    private static void TrimLeadingUntilFirstUser(List<AgentMessage> list)
    {
        while (list.Count > 0 && !string.Equals(list[0].Role, "user", StringComparison.OrdinalIgnoreCase))
            list.RemoveAt(0);
    }

    internal static List<(int Start, int Length)> GetUserSegmentRanges(IReadOnlyList<AgentMessage> list)
    {
        var ranges = new List<(int Start, int Length)>();
        var i = 0;
        while (i < list.Count)
        {
            if (!string.Equals(list[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < list.Count && !string.Equals(list[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                i++;
            ranges.Add((start, i - start));
        }

        return ranges;
    }

    private static AgentMessage Clone(AgentMessage m) =>
        new(
            m.TenantId,
            m.OwnerUserId,
            m.ConversationId,
            m.Sequence,
            m.Role,
            m.Content,
            m.LinkedCampaignId,
            m.ToolCallId,
            m.ToolName,
            m.ToolArgumentsJson,
            m.ToolResultJson,
            m.Id,
            m.CreateDate,
            m.LastUpdated,
            m.WorkflowPhase,
            m.WorkflowCampaignKind,
            m.WorkflowUserSkippedEventModels,
            m.WorkflowModelGatePassed,
            m.TtlSeconds,
            m.ToolDurationMs,
            m.TurnMetricsJson);

    private static AgentMessage CloneWithToolResult(AgentMessage m, string? toolResultJson)
    {
        var clone = Clone(m);
        clone.ToolResultJson = toolResultJson;
        return clone;
    }

    private static AgentMessage CloneWithContent(AgentMessage m, string? content)
    {
        var clone = Clone(m);
        clone.Content = content;
        return clone;
    }
}

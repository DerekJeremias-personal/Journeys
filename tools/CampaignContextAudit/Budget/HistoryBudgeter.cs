using CampaignContextAudit.Models;
using Journeys.CampaignAgent.Remediation;

namespace CampaignContextAudit.Budget;

public sealed record HistoryBudgetSettings
{
    public bool PinFirstUserSegment { get; init; } = true;
    public bool PinMostRecentCompletedSegment { get; init; } = true;
    public bool ShrinkToolResults { get; init; } = true;
    public int ShrinkThresholdChars { get; init; } = 2048;
    public int MaxChars { get; init; } = HistoryBudgeter.MaxChars;
    public int MaxUserTurns { get; init; } = HistoryBudgeter.MaxUserTurns;

    /// <summary>When true, contract summary rows in protected segments are exempt from shrink.</summary>
    public bool JourneyContractSummaryPinActive { get; init; }

    public static HistoryBudgetSettings ProductionDefaults { get; } = new();
}

public sealed record DroppedSegment(long FirstSequence, long LastSequence, int CharCount, IReadOnlyList<string> ToolNames);
public sealed record BudgetResult(IReadOnlyList<AgentMessageDoc> Kept, IReadOnlyList<DroppedSegment> DroppedSegments, int FinalChars, int FinalUserTurns);

public static class HistoryBudgeter
{
    public const int MaxUserTurns = 40;
    public const int MaxChars = 80000;

    private static readonly string[] ShrinkPriorityTools =
    [
        "list_models", "get_all_models", "get_rules_engine_contract_summary", "get_rule_pattern_recipes", "list_campaigns",
        "get_campaign_assistant_context", "get_model", "get_campaign",
        "validate_campaign", "upsert_campaign", "upsert_point_account_type", "save_model"
    ];

    public static BudgetResult Apply(IReadOnlyList<AgentMessageDoc> orderedBySequence) =>
        Apply(orderedBySequence, HistoryBudgetSettings.ProductionDefaults);

    public static BudgetResult Apply(IReadOnlyList<AgentMessageDoc> orderedBySequence, HistoryBudgetSettings settings)
    {
        var list = orderedBySequence.OrderBy(m => m.Sequence).Select(Clone).ToList();
        var dropped = new List<DroppedSegment>();
        TrimLeadingUntilFirstUser(list, dropped);

        while (list.Count > 0)
        {
            if (!IsOverBudget(list, settings))
                break;

            if (settings.ShrinkToolResults && TryShrinkOneRow(list, settings))
                continue;

            if (!RemoveOldestRemovableUserSegment(list, dropped, settings))
                break;

            TrimLeadingUntilFirstUser(list, dropped);
        }

        return new BudgetResult(list, dropped, CountEffective(list), list.Count(IsUser));
    }

    public static int CountEffective(IReadOnlyList<AgentMessageDoc> list)
    {
        var toolCallIds = CollectToolCallIdsOnRows(list);
        var total = 0;
        foreach (var m in list)
            total += EffectiveCharCount(m, toolCallIds);
        return total;
    }

    private static HashSet<string> CollectToolCallIdsOnRows(IReadOnlyList<AgentMessageDoc> list)
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

    private static int EffectiveCharCount(AgentMessageDoc m, IReadOnlySet<string> toolCallIdsOnRows)
    {
        var toolChars = m.ToolResultJson?.Length ?? 0;
        if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase))
            return toolChars;

        if (string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            && MeaiEmbeddedResultShrinker.IsMeaiEnvelope(m.Content))
            return MeaiEmbeddedResultShrinker.CountEffectiveAssistantContentChars(m.Content, toolCallIdsOnRows) + toolChars;

        return (m.Content?.Length ?? 0) + toolChars;
    }

    private static bool IsOverBudget(List<AgentMessageDoc> list, HistoryBudgetSettings settings)
    {
        var chars = CountEffective(list);
        var userTurns = list.Count(IsUser);
        return chars > settings.MaxChars || userTurns > settings.MaxUserTurns;
    }

    private static bool TryShrinkOneRow(List<AgentMessageDoc> list, HistoryBudgetSettings settings)
    {
        var threshold = settings.ShrinkThresholdChars;
        var toolCallIds = CollectToolCallIdsOnRows(list);
        var pick = FindLargestShrinkable(list, threshold, requireOverThreshold: true, toolCallIds, settings);
        if (pick is not null)
            return ApplyShrinkPick(list, pick.Value, threshold, toolCallIds);

        pick = FindLargestShrinkable(list, int.MaxValue, requireOverThreshold: false, toolCallIds, settings);
        if (pick is not null)
            return ApplyShrinkPick(list, pick.Value, int.MaxValue, toolCallIds);

        return TryTruncateLargestAssistantText(list, aggressive: true);
    }

    private static bool TryTruncateLargestAssistantText(List<AgentMessageDoc> list, bool aggressive = false)
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

    private readonly record struct ShrinkPick(int Index, int Size, int Priority, bool IsAssistant, string? ToolName);

    private static bool ApplyShrinkPick(List<AgentMessageDoc> list, ShrinkPick pick, int threshold, IReadOnlySet<string> toolCallIds)
    {
        if (pick.IsAssistant)
        {
            if (!MeaiEmbeddedResultShrinker.TryShrinkOneResult(
                    list[pick.Index].Content!, threshold, ShrinkPriorityTools, out var updated, toolCallIds))
                return false;
            list[pick.Index] = CloneWithContent(list[pick.Index], updated);
            return true;
        }

        list[pick.Index] = CloneWithToolResult(list[pick.Index], ToolResultHistoryStub.Build(list[pick.Index].ToolName, pick.Size));
        return true;
    }

    private static ShrinkPick? FindLargestShrinkable(
        List<AgentMessageDoc> list,
        int threshold,
        bool requireOverThreshold,
        IReadOnlySet<string> toolCallIds,
        HistoryBudgetSettings settings)
    {
        ShrinkPick? best = null;
        var segments = GetUserSegmentRanges(list);
        var protectedIndices = GetProtectedSegmentIndices(segments.Count, settings);
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
                    if (ShouldSkipContractSummaryShrink(settings, protectedIndices, segIdx, row.ToolName))
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

                if (ShouldSkipContractSummaryShrink(settings, protectedIndices, segIdx, toolName))
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
        HistoryBudgetSettings settings,
        HashSet<int> protectedIndices,
        int segmentIndex,
        string? toolName)
    {
        if (!settings.JourneyContractSummaryPinActive || !protectedIndices.Contains(segmentIndex))
            return false;

        return string.Equals(toolName, "get_rules_engine_contract_summary", StringComparison.OrdinalIgnoreCase);
    }

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

    private static bool RemoveOldestRemovableUserSegment(
        List<AgentMessageDoc> list,
        List<DroppedSegment> dropped,
        HistoryBudgetSettings settings)
    {
        var segments = GetUserSegmentRanges(list);
        if (segments.Count == 0)
        {
            if (list.Count > 0)
                dropped.Add(ToSegment(list));
            list.Clear();
            return false;
        }

        var protectedIndices = GetProtectedSegmentIndices(segments.Count, settings);
        for (var i = 0; i < segments.Count; i++)
        {
            if (protectedIndices.Contains(i))
                continue;

            var (start, length) = segments[i];
            var segment = list.GetRange(start, length);
            dropped.Add(ToSegment(segment));
            list.RemoveRange(start, length);
            return true;
        }

        return false;
    }

    internal static HashSet<int> GetProtectedSegmentIndices(int segmentCount, HistoryBudgetSettings settings)
    {
        var protectedIndices = new HashSet<int>();
        if (segmentCount == 0)
            return protectedIndices;

        if (settings.PinFirstUserSegment)
            protectedIndices.Add(0);

        if (settings.PinMostRecentCompletedSegment && segmentCount >= 3)
            protectedIndices.Add(segmentCount - 2);

        if (segmentCount >= 3)
            protectedIndices.Add(segmentCount - 1);
        return protectedIndices;
    }

    private static bool IsUser(AgentMessageDoc m) =>
        string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);

    private static void TrimLeadingUntilFirstUser(List<AgentMessageDoc> list, List<DroppedSegment> dropped)
    {
        var removed = new List<AgentMessageDoc>();
        while (list.Count > 0 && !IsUser(list[0]))
        {
            removed.Add(list[0]);
            list.RemoveAt(0);
        }

        if (removed.Count > 0)
            dropped.Add(ToSegment(removed));
    }

    private static List<(int Start, int Length)> GetUserSegmentRanges(IReadOnlyList<AgentMessageDoc> list)
    {
        var ranges = new List<(int Start, int Length)>();
        var i = 0;
        while (i < list.Count)
        {
            if (!IsUser(list[i]))
            {
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < list.Count && !IsUser(list[i]))
                i++;
            ranges.Add((start, i - start));
        }

        return ranges;
    }

    private static AgentMessageDoc Clone(AgentMessageDoc m) =>
        new()
        {
            Sequence = m.Sequence,
            Role = m.Role,
            Content = m.Content,
            ToolCallId = m.ToolCallId,
            ToolName = m.ToolName,
            ToolResultJson = m.ToolResultJson
        };

    private static AgentMessageDoc CloneWithToolResult(AgentMessageDoc m, string stub)
    {
        var clone = Clone(m);
        clone.ToolResultJson = stub;
        return clone;
    }

    private static AgentMessageDoc CloneWithContent(AgentMessageDoc m, string content)
    {
        var clone = Clone(m);
        clone.Content = content;
        return clone;
    }

    private static DroppedSegment ToSegment(List<AgentMessageDoc> rows) => new(
        rows.First().Sequence,
        rows.Last().Sequence,
        CountEffective(rows),
        rows.Where(r => !string.IsNullOrEmpty(r.ToolName)).Select(r => r.ToolName!).ToList());
}

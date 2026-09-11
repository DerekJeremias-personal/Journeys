using Backend.Dto.Structures.Model;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Ranks eventable models so the campaign agent can default to the order event for point-earning
/// campaigns. Scoring: order-name synonyms (exact &gt; substring) plus a boost when the model carries
/// a decimal/numeric "amount-like" attribute. A single recommended default is flagged only when the
/// top candidate clears the strong-match threshold.
/// </summary>
public static class EventModelRanker
{
    public const string EventableTag = "eventable";
    private const int MaxCandidates = 5;

    private static readonly string[] OrderSynonyms =
    {
        "order", "invoice", "transaction", "txn", "billing", "bill",
        "charge", "purchase", "sale", "receipt"
    };

    private static readonly string[] AmountKeywords =
    {
        "amount", "total", "price", "subtotal", "grandtotal", "value", "cost", "revenue", "spend"
    };

    private static readonly string[] NumericDataTypes =
    {
        "decimal", "number", "numeric", "double", "float", "money", "currency", "int", "integer", "long"
    };

    private static readonly string[] RichMetadataKeys = { "AccountXIdSymbol", "NaturalKeySymbols", "TimeOfOccurrence" };

    private const int ExactNameScore = 100;
    private const int SubstringNameScore = 40;
    private const int MonetaryBoost = 50;
    private const int RichMetadataPerKey = 5;

    public static EventModelCandidateSet Rank(IEnumerable<ModelDto> models)
    {
        var set = new EventModelCandidateSet();
        if (models == null)
            return set;

        var scored = new List<(EventModelCandidate Candidate, DateTimeOffset Updated)>();

        foreach (var model in models)
        {
            if (model == null)
                continue;
            if (!string.Equals(model.Tag, EventableTag, StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsWrapper(model))
                continue;
            if (string.IsNullOrWhiteSpace(model.ID))
                continue;

            var candidate = Score(model);
            scored.Add((candidate, model.LastUpdated ?? DateTimeOffset.MinValue));
        }

        var ordered = scored
            .OrderByDescending(x => x.Candidate.Score)
            .ThenByDescending(x => x.Candidate.MatchReasons.Count)
            .ThenByDescending(x => x.Updated)
            .Select(x => x.Candidate)
            .Take(MaxCandidates)
            .ToList();

        set.Candidates = ordered;

        var top = ordered.FirstOrDefault();
        if (top is { IsStrongMatch: true })
            set.RecommendedDefaultId = top.EventModelId;

        return set;
    }

    private static bool IsWrapper(ModelDto model)
    {
        if (model.IsContainer)
            return true;
        var name = model.Name ?? string.Empty;
        return name.EndsWith("AndRuleState", StringComparison.OrdinalIgnoreCase);
    }

    private static EventModelCandidate Score(ModelDto model)
    {
        var candidate = new EventModelCandidate
        {
            EventModelId = model.ID,
            Name = model.Name,
            DisplayName = model.DisplayName,
            ModelType = model.ModelType
        };

        var score = 0;
        var exactName = false;
        var anyName = false;

        foreach (var name in new[] { model.Name, model.DisplayName })
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            foreach (var syn in OrderSynonyms)
            {
                if (string.Equals(name, syn, StringComparison.OrdinalIgnoreCase))
                {
                    if (!exactName)
                    {
                        score += ExactNameScore;
                        candidate.MatchReasons.Add($"name:{syn}");
                    }
                    exactName = true;
                    anyName = true;
                }
                else if (!exactName && name.Contains(syn, StringComparison.OrdinalIgnoreCase))
                {
                    if (!anyName)
                    {
                        score += SubstringNameScore;
                        candidate.MatchReasons.Add($"name~:{syn}");
                    }
                    anyName = true;
                }
            }
        }

        var monetarySymbol = FindMonetaryAttributeSymbol(model);
        if (monetarySymbol != null)
        {
            score += MonetaryBoost;
            candidate.HasMonetaryAttribute = true;
            candidate.MatchReasons.Add($"amount:{monetarySymbol}");
        }

        var richKeys = CountRichMetadata(model);
        score += richKeys * RichMetadataPerKey;

        candidate.Score = score;
        candidate.IsStrongMatch = exactName || (anyName && candidate.HasMonetaryAttribute);
        return candidate;
    }

    private static string? FindMonetaryAttributeSymbol(ModelDto model)
    {
        if (model.Attributes == null)
            return null;

        foreach (var attr in model.Attributes)
        {
            if (attr == null)
                continue;
            var dataType = attr.DataType ?? string.Empty;
            if (!NumericDataTypes.Any(t => dataType.Contains(t, StringComparison.OrdinalIgnoreCase)))
                continue;

            foreach (var name in new[] { attr.Symbol, attr.DisplayName })
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (AmountKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return attr.Symbol ?? attr.DisplayName;
            }
        }

        return null;
    }

    private static int CountRichMetadata(ModelDto model)
    {
        if (model.ModelMetaData == null)
            return 0;
        return RichMetadataKeys.Count(k =>
            model.ModelMetaData.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v));
    }
}

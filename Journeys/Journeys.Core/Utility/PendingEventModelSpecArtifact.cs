using System.Text.Json;
using System.Text.RegularExpressions;
using Journeys.Core.Models;

namespace Journeys.Core.Utility;

/// <summary>Reads/writes the deferred event model spec the user stated before/during DataAnalysis.</summary>
public static class PendingEventModelSpecArtifact
{
    private const int RawExcerptMaxLength = 200;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex ModelNameRegex = new(
        @"(?:model\s+)?called\s+(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttributeTokenRegex = new(
        @"(?:String|Int|Integer|Decimal|DateTime|Bool|Boolean|Number|Double|Float)\s+([A-Za-z]\w*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryCaptureFromUserMessage(CampaignWorkflowState state, string? message)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!EventModelCreateNewIntent.LooksLikeRequest(message))
            return false;

        var msg = message!.Trim();
        var nameMatch = ModelNameRegex.Match(msg);
        if (!nameMatch.Success)
            return false;

        var dto = new PendingEventModelSpecDto
        {
            Name = nameMatch.Groups[1].Value,
            AttributesSummary = ExtractAttributesSummary(msg),
            RawUserExcerpt = TruncateExcerpt(msg)
        };

        Write(state, dto);
        return true;
    }

    public static void Write(CampaignWorkflowState state, PendingEventModelSpecDto dto)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dto);
        state.Artifacts.PendingEventModelSpec = JsonSerializer.Serialize(dto, JsonOpts);
    }

    public static PendingEventModelSpecDto? Read(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.Artifacts.PendingEventModelSpec))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingEventModelSpecDto>(
                state.Artifacts.PendingEventModelSpec,
                JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Clear(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Artifacts.PendingEventModelSpec = null;
    }

    public static bool IsSatisfied(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var spec = Read(state);
        if (spec == null || string.IsNullOrWhiteSpace(spec.Name))
            return false;

        return EventModelContractsAccumulator.ReadResolved(state)
            .Any(d => string.Equals(d.EventModelName, spec.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractAttributesSummary(string message)
    {
        var names = new List<string>();
        foreach (Match match in AttributeTokenRegex.Matches(message))
        {
            var name = match.Groups[1].Value;
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                names.Add(name);
        }

        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static string TruncateExcerpt(string message) =>
        message.Length <= RawExcerptMaxLength
            ? message
            : message[..RawExcerptMaxLength];
}

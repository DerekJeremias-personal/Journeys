using System.Linq;
using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Services;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public static class CampaignShellDigestBuilder
{
    private const string SetupJourneyPayloadWarning =
        "Journey payload present; full journey authoring belongs in CampaignJourney phase.";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static CampaignShellDigest BuildFromCampaign(
        Campaign campaign,
        IReadOnlyDictionary<string, bool>? processEventEligibilityByModelId = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var digest = new CampaignShellDigest
        {
            CampaignId = campaign.Id ?? string.Empty,
            Name = campaign.Name,
            Status = campaign.Status ?? string.Empty,
            ExtCampaignId = campaign.ExtCampaignId,
            EventModelIds = CollectEventModelIds(campaign.Events),
            StartDateUtc = FormatUtc(campaign.StartDate),
            EndDateUtc = campaign.EndDate.HasValue ? FormatUtc(campaign.EndDate.Value) : null,
            HasJourneyPayload = campaign.Journey != null && HasJourneyPayloadOnNode(campaign.Journey)
        };

        ApplyIneligibleEventModelIds(digest, processEventEligibilityByModelId);
        return digest;
    }

    public static bool TryBuildFromUpsertResult(
        string? json,
        out string? digestJson,
        IReadOnlyDictionary<string, bool>? processEventEligibilityByModelId = null)
    {
        digestJson = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var dto = JsonSerializer.Deserialize<CampaignDto>(json, JsonOpts);
            if (dto == null || (string.IsNullOrWhiteSpace(dto.Id) && string.IsNullOrWhiteSpace(dto.Name)))
                return false;

            var digest = BuildFromDto(dto, addSetupJourneyWarning: true, processEventEligibilityByModelId);
            digestJson = JsonSerializer.Serialize(digest, JsonOpts);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static CampaignShellDigest BuildFromDto(
        CampaignDto dto,
        bool addSetupJourneyWarning,
        IReadOnlyDictionary<string, bool>? processEventEligibilityByModelId = null)
    {
        var digest = new CampaignShellDigest
        {
            CampaignId = dto.Id ?? string.Empty,
            Name = dto.Name,
            Status = dto.Status ?? string.Empty,
            ExtCampaignId = dto.ExtCampaignId,
            EventModelIds = CollectEventModelIds(dto.Events),
            StartDateUtc = FormatUtc(dto.StartDate),
            EndDateUtc = dto.EndDate.HasValue ? FormatUtc(dto.EndDate.Value) : null,
            HasJourneyPayload = dto.Journey != null && HasJourneyPayloadOnDto(dto.Journey)
        };

        if (addSetupJourneyWarning && digest.HasJourneyPayload)
            digest.Warnings.Add(SetupJourneyPayloadWarning);

        ApplyIneligibleEventModelIds(digest, processEventEligibilityByModelId);
        return digest;
    }

    private static void ApplyIneligibleEventModelIds(
        CampaignShellDigest digest,
        IReadOnlyDictionary<string, bool>? processEventEligibilityByModelId)
    {
        if (processEventEligibilityByModelId == null || processEventEligibilityByModelId.Count == 0)
            return;

        foreach (var eventModelId in digest.EventModelIds)
        {
            if (processEventEligibilityByModelId.TryGetValue(eventModelId, out var eligible) && !eligible)
                digest.IneligibleEventModelIds.Add(eventModelId);
        }
    }

    private static List<string> CollectEventModelIds(IEnumerable<string>? events)
    {
        var ids = new List<string>();
        if (events == null)
            return ids;

        foreach (var raw in events)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            ids.Add(raw.Trim());
        }

        return ids;
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("o");

    private static bool HasJourneyPayloadOnNode(JourneyNode node)
    {
        if (node.Rules is { Count: > 0 } rules && rules.Any(CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload))
            return true;

        if (HasNavigationConstraints(node.Navigation) || node.NavigationCriteria is { Count: > 0 })
            return true;

        if (node.Children == null)
            return false;

        foreach (var child in node.Children)
        {
            if (child != null && HasJourneyPayloadOnNode(child))
                return true;
        }

        return false;
    }

    private static bool HasJourneyPayloadOnDto(JourneyDto journey)
    {
        if (journey.Rules is { Count: > 0 } rules && rules.Any(CampaignJourneyAuthoringShapeValidator.HasRuleSetDtoPayload))
            return true;

        if (HasNavigationConstraints(journey.Navigation))
            return true;

        if (journey.Children == null)
            return false;

        foreach (var child in journey.Children)
        {
            if (child != null && HasJourneyPayloadOnDto(child))
                return true;
        }

        return false;
    }

    private static bool HasNavigationConstraints(JsonElement? navigation)
    {
        if (navigation == null)
            return false;

        var nav = navigation.Value;
        if (nav.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;

        if (nav.ValueKind != JsonValueKind.Object)
            return true;

        foreach (var prop in nav.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                if (prop.Value.EnumerateObject().Any())
                    return true;
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                if (prop.Value.GetArrayLength() > 0)
                    return true;
                continue;
            }

            return true;
        }

        return false;
    }
}

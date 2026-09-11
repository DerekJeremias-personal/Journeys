using System.Text.Json;
using Journeys.Core.Extensions;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public static class CampaignJourneyArtifactDigestBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static CampaignJourneyArtifactDigest BuildFromCampaign(
        Campaign campaign,
        IReadOnlyCollection<string>? manifestPatIds)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var walk = JourneyDigestWalkUtility.Walk(campaign.Journey);
        var manifestSet = ToManifestSet(manifestPatIds);

        var referenced = new List<PointAccountReferenceDigest>();
        foreach (var (patId, kinds) in walk.ReferencedPatIds.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            referenced.Add(new PointAccountReferenceDigest
            {
                PointAccountTypeId = patId,
                UsageCount = kinds.Count,
                OutcomeKinds = kinds.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(),
                InManifest = manifestSet != null && manifestSet.Contains(patId)
            });
        }

        var unresolved = walk.ReferencedPatIds.Keys
            .Where(id => manifestSet == null || !manifestSet.Contains(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CampaignJourneyArtifactDigest
        {
            CampaignId = campaign.Id ?? string.Empty,
            JourneyNodeCount = walk.JourneyNodeCount,
            RuleSetCount = walk.RuleSetCount,
            OutcomeKindCounts = walk.OutcomeKindCounts,
            ReferencedPointAccountTypes = referenced,
            UnresolvedPatIds = unresolved
        };
    }

    public static bool TryBuildFromUpsertResult(
        string upsertJson,
        string? manifestJson,
        out string? digestJson)
    {
        digestJson = null;
        if (string.IsNullOrWhiteSpace(upsertJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(upsertJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var dto = JsonSerializer.Deserialize<CampaignDto>(upsertJson, JsonOpts);
            if (dto == null || (string.IsNullOrWhiteSpace(dto.Id) && string.IsNullOrWhiteSpace(dto.Name)))
                return false;

            var campaign = dto.FromDto();
            if (campaign == null)
                return false;

            var manifest = PointAccountManifestBuilder.Parse(manifestJson);
            var manifestPatIds = manifest.Items.Select(i => i.Id).ToList();
            var digest = BuildFromCampaign(campaign, manifestPatIds);
            digestJson = JsonSerializer.Serialize(digest, JsonOpts);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static HashSet<string>? ToManifestSet(IReadOnlyCollection<string>? manifestPatIds)
    {
        if (manifestPatIds == null || manifestPatIds.Count == 0)
            return null;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in manifestPatIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                set.Add(id.Trim());
        }

        return set.Count == 0 ? null : set;
    }
}

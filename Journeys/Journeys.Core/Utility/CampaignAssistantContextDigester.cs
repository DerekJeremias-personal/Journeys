using System.Text.Json;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Compacts <c>get_campaign_assistant_context</c> results while preserving shell/manifest/journey digests
/// and event-processing contracts.
/// </summary>
public static class CampaignAssistantContextDigester
{
    public const string Note =
        "Assistant context digested for context efficiency. Shell, manifest, journey, and processing contracts retained; " +
        "attribute restriction text and sample JSON template omitted.";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static ReadToolDigestResult Digest(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Passthrough(rawJson ?? string.Empty);

        if (!rawJson.TrimStart().StartsWith('{'))
            return Passthrough(rawJson);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return Passthrough(rawJson); }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("$type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "text"
                && root.TryGetProperty("text", out var inner) && inner.ValueKind == JsonValueKind.String)
            {
                var innerJson = inner.GetString() ?? string.Empty;
                var innerResult = Digest(innerJson);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                }, JsonOpts);
                return new ReadToolDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            CampaignAssistantContextDto? ctx;
            try { ctx = JsonSerializer.Deserialize<CampaignAssistantContextDto>(rawJson, JsonOpts); }
            catch (JsonException) { return Passthrough(rawJson); }

            if (ctx == null || string.IsNullOrWhiteSpace(ctx.CampaignId))
                return Passthrough(rawJson);

            var slim = BuildSlim(ctx);
            var json = JsonSerializer.Serialize(slim, JsonOpts);
            return new ReadToolDigestResult(json, true, rawJson.Length, json.Length);
        }
    }

    private static object BuildSlim(CampaignAssistantContextDto ctx) =>
        new
        {
            note = Note,
            schemaVersion = ctx.SchemaVersion,
            tenantId = ctx.TenantId,
            campaignId = ctx.CampaignId,
            status = ctx.Status,
            warnings = ctx.Warnings,
            campaignShell = ctx.CampaignShell,
            pointAccountManifest = ctx.PointAccountManifest,
            journey = ctx.Journey,
            eventModels = ctx.EventModels.Select(em => new
            {
                modelId = em.ModelId,
                modelType = em.ModelType,
                name = em.Name,
                loadWarning = em.LoadWarning,
                processingContract = em.ProcessingContract,
                attributeCount = em.Attributes.Count
            }).ToList(),
            sampleScaffold = ctx.SampleScaffold == null
                ? null
                : new
                {
                    requiredEventSymbols = ctx.SampleScaffold.RequiredEventSymbols,
                    accountLinkSymbolPath = ctx.SampleScaffold.AccountLinkSymbolPath,
                    naturalKeySymbols = ctx.SampleScaffold.NaturalKeySymbols,
                    fieldRoles = ctx.SampleScaffold.FieldRoles
                }
        };

    private static ReadToolDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);
}

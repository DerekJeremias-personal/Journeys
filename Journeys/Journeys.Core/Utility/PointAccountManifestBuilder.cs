using System.Text.Json;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public static class PointAccountManifestBuilder
{
    private const string AmbiguousTierWarningPrefix =
        "Point account type role ambiguous (tier-like name without NonSpendable ledger type):";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void AppendFromPatResult(CampaignWorkflowState state, string patJson)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(patJson))
            return;

        // Tolerate MCP/MEAI result wrappers (text-content envelope or double-encoded string) so a
        // successful UpsertPointAccountType still populates the manifest (which gates CampaignJourney).
        patJson = ToolResultJsonNormalizer.Unwrap(patJson) ?? patJson;

        if (!TryDeserializePat(patJson, out var pat))
            return;

        AppendPatDto(state, pat);
    }

    /// <summary>
    /// Registers PATs from a <c>ListPointAccountTypes</c> page (<c>Entities[]</c>).
    /// </summary>
    public static void AppendFromListResult(CampaignWorkflowState state, string listJson)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(listJson))
            return;

        listJson = ToolResultJsonNormalizer.Unwrap(listJson) ?? listJson;

        try
        {
            using var doc = JsonDocument.Parse(listJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            if (!TryGetPropertyCaseInsensitive(root, "entities", out var entities)
                || entities.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entity in entities.EnumerateArray())
            {
                if (entity.ValueKind != JsonValueKind.Object)
                    continue;

                if (!TryDeserializePat(entity.GetRawText(), out var pat))
                    continue;

                AppendPatDto(state, pat);
            }
        }
        catch (JsonException)
        {
            // Leave manifest unchanged on parse failure.
        }
    }

    private static void AppendPatDto(CampaignWorkflowState state, PointAccountTypeDto pat)
    {
        var manifest = Parse(state.Artifacts.PointAccountManifest);

        if (manifest.Items.Any(i => string.Equals(i.Id, pat.Id, StringComparison.OrdinalIgnoreCase)))
            return;

        manifest.Items.Add(ToManifestItem(pat));
        state.Artifacts.PointAccountManifest = Serialize(manifest);
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryDeserializePat(string patJson, out PointAccountTypeDto pat)
    {
        pat = null!;
        try
        {
            var dto = JsonSerializer.Deserialize<PointAccountTypeDto>(patJson, JsonOpts);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return false;

            pat = dto;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static PointAccountManifestDigest Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PointAccountManifestDigest();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new PointAccountManifestDigest();

            var digest = new PointAccountManifestDigest
            {
                SchemaVersion = ReadSchemaVersion(root)
            };

            if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                return digest;

            foreach (var itemEl in itemsEl.EnumerateArray())
            {
                if (itemEl.ValueKind != JsonValueKind.Object)
                    continue;

                var id = ReadString(itemEl, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var displayLabel = ReadString(itemEl, "displayLabel");
                digest.Items.Add(new PointAccountManifestItemDigest
                {
                    Id = id,
                    DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? id : displayLabel,
                    LedgerType = ReadString(itemEl, "ledgerType"),
                    IsSpendable = ReadBool(itemEl, "isSpendable"),
                    Status = ReadString(itemEl, "status"),
                    Role = ReadString(itemEl, "role") ?? "other"
                });
            }

            return digest;
        }
        catch (JsonException)
        {
            return new PointAccountManifestDigest();
        }
    }

    public static string Serialize(PointAccountManifestDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        if (digest.SchemaVersion < 1)
            digest.SchemaVersion = 1;

        return JsonSerializer.Serialize(digest, JsonOpts);
    }

    public static string InferRole(PointAccountTypeDto pat, List<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(pat);

        if (pat.IsSpendable == true)
            return "spendable";

        if (!string.IsNullOrWhiteSpace(pat.LedgerType)
            && string.Equals(pat.LedgerType, PointLedgerTypeStrings.ESCROW, StringComparison.OrdinalIgnoreCase))
            return "escrow";

        if (!string.IsNullOrWhiteSpace(pat.LedgerType)
            && string.Equals(pat.LedgerType, PointLedgerTypeStrings.NONSPENDABLE, StringComparison.OrdinalIgnoreCase)
            && pat.IsSpendable == false)
            return "tierQualification";

        var label = pat.Name ?? string.Empty;
        if (ContainsEscrow(label))
            return "escrow";

        if (LooksTierLike(label) && pat.IsSpendable == false)
        {
            if (!string.Equals(pat.LedgerType, PointLedgerTypeStrings.NONSPENDABLE, StringComparison.OrdinalIgnoreCase))
                warnings?.Add($"{AmbiguousTierWarningPrefix} {label} — use ledgerType NonSpendable with isSpendable false.");

            return "tierQualification";
        }

        return "other";
    }

    public static PointAccountManifestDigest BuildFromPatDtos(IEnumerable<PointAccountTypeDto> pats)
    {
        ArgumentNullException.ThrowIfNull(pats);

        var digest = new PointAccountManifestDigest();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pat in pats)
        {
            if (pat == null || string.IsNullOrWhiteSpace(pat.Id))
                continue;

            if (!seen.Add(pat.Id))
                continue;

            digest.Items.Add(ToManifestItem(pat));
        }

        return digest;
    }

    private static PointAccountManifestItemDigest ToManifestItem(PointAccountTypeDto pat)
    {
        var displayLabel = string.IsNullOrWhiteSpace(pat.Name) ? pat.Id! : pat.Name;
        return new PointAccountManifestItemDigest
        {
            Id = pat.Id!,
            DisplayLabel = displayLabel,
            LedgerType = pat.LedgerType,
            IsSpendable = pat.IsSpendable,
            Status = pat.Status,
            Role = InferRole(pat)
        };
    }

    private static int ReadSchemaVersion(JsonElement root)
    {
        if (root.TryGetProperty("schemaVersion", out var schemaVersion))
            return schemaVersion.GetInt32();

        if (root.TryGetProperty("version", out var version))
            return version.GetInt32();

        return 1;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool ContainsEscrow(string value) =>
        value.Contains("escrow", StringComparison.OrdinalIgnoreCase);

    private static bool LooksTierLike(string value) =>
        PatNamingHeuristics.LooksTierQualLike(value);
}

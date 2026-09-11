using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Journeys.Core.Utility;

/// <summary>
/// Canonical fingerprint of campaign JSON for validate/upsert staleness checks.
/// </summary>
public static class CampaignPayloadFingerprint
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string? TryCompute(string? campaignJson)
    {
        if (string.IsNullOrWhiteSpace(campaignJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(campaignJson);
            var canonical = Canonicalize(doc.RootElement);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Canonicalize(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Object => CanonicalizeObject(el),
            JsonValueKind.Array => CanonicalizeArray(el),
            JsonValueKind.String => JsonSerializer.Serialize(el.GetString()),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => "null"
        };

    private static string CanonicalizeObject(JsonElement obj)
    {
        var props = obj.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{JsonSerializer.Serialize(p.Name)}:{Canonicalize(p.Value)}");
        return "{" + string.Join(",", props) + "}";
    }

    private static string CanonicalizeArray(JsonElement arr) =>
        "[" + string.Join(",", arr.EnumerateArray().Select(Canonicalize)) + "]";
}

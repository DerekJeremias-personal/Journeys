using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Backend.Dto.Structures.Tenant;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Maps <see cref="TenantDto"/> to LLM slice using reflection so minor DTO renames stay tolerant.
/// </summary>
internal static class TenantDtoCampaignContextMapper
{
    private static readonly string[] MarketingAsOfNames =
    {
        "MarketingContextAsOf", "MarketingContextAsOfUtc", "MarketingAsOf", "MarketingAsOfUtc"
    };

    private static readonly string[] ConsumerAsOfNames =
    {
        "EndConsumerContextAsOf", "EndConsumerContextAsOfUtc", "EndConsumerAsOf", "EndConsumerAsOfUtc"
    };

    private static readonly string[] LastUpdatedNames =
    {
        "LastUpdated", "LastUpdatedUtc", "UpdatedUtc", "ModifiedUtc", "LastModified"
    };

    private static readonly string[] ETagNames = { "ETag", "Etag", "EtagValue" };

    public static CampaignAgentTenantLlmSlice Map(TenantDto dto, int maxMarketingChars, int maxConsumerChars, ILogger logger)
    {
        var marketing = Truncate(GetStringProperty(dto, "MarketingContext"), maxMarketingChars, "MarketingContext", logger);
        var consumer = Truncate(GetStringProperty(dto, "EndConsumerContext"), maxConsumerChars, "EndConsumerContext", logger);

        var mAsOf = GetAsOfString(dto, MarketingAsOfNames) ?? GetStringProperty(dto, "MarketingContextAsOfText");
        var cAsOf = GetAsOfString(dto, ConsumerAsOfNames) ?? GetStringProperty(dto, "EndConsumerContextAsOfText");

        var version = BuildCacheVersion(dto);
        var testAccounts = dto.CampaignTestAccountExtIds;
        return new CampaignAgentTenantLlmSlice(marketing, consumer, mAsOf, cAsOf, version, testAccounts);
    }

    public static string BuildCacheVersion(TenantDto dto)
    {
        var id = GetStringProperty(dto, "Id") ?? GetStringProperty(dto, "Name") ?? "";
        var lu = GetDateProperty(dto, LastUpdatedNames);
        var etag = GetStringProperty(dto, ETagNames);
        var allowlist = CampaignTestAccountAllowlist.Normalize(dto.CampaignTestAccountExtIds);
        var joined = string.Join("|", allowlist.Select(a => a.ToLowerInvariant()));
        var allowlistHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
        return $"{id}|{lu?.ToString("O", CultureInfo.InvariantCulture) ?? ""}|{etag ?? ""}|{allowlistHash}";
    }

    private static string? GetAsOfString(TenantDto dto, IReadOnlyList<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var p = typeof(TenantDto).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p == null)
                continue;
            var v = p.GetValue(dto);
            var formatted = FormatValue(v);
            if (!string.IsNullOrWhiteSpace(formatted))
                return formatted;
        }

        return null;
    }

    private static DateTimeOffset? GetDateProperty(TenantDto dto, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var p = typeof(TenantDto).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p == null)
                continue;
            var v = p.GetValue(dto);
            switch (v)
            {
                case DateTimeOffset dto2:
                    return dto2;
                case DateTime dt:
                    return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            }
        }

        return null;
    }

    private static string? GetStringProperty(TenantDto dto, string name)
    {
        var p = typeof(TenantDto).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p == null)
            return null;
        return FormatValue(p.GetValue(dto));
    }

    private static string? GetStringProperty(TenantDto dto, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var s = GetStringProperty(dto, name);
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        return null;
    }

    private static string? FormatValue(object? value) =>
        value switch
        {
            null => null,
            string s => string.IsNullOrWhiteSpace(s) ? null : s,
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToString("O", CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

    private static string? Truncate(string? text, int maxChars, string fieldName, ILogger logger)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;
        logger.LogWarning(
            "Campaign agent tenant context field {Field} truncated from {OriginalLength} to {MaxChars} characters",
            fieldName,
            text.Length,
            maxChars);
        return text[..maxChars] + "\n…[truncated]";
    }
}

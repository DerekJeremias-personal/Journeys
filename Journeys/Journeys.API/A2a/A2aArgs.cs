using System.Text.Json;

namespace Journeys.API.A2a;

internal static class A2aArgs
{
    internal static string ResolveTenant(string? sendMessageTenant, JsonElement args)
    {
        var t = GetString(args, "tenantId");
        if (!string.IsNullOrWhiteSpace(t))
            return t!;
        if (!string.IsNullOrWhiteSpace(sendMessageTenant))
            return sendMessageTenant!;
        throw new ArgumentException("tenantId is required in tool arguments or SendMessageRequest.tenant.");
    }

    internal static string? GetString(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Null => null,
            _ => p.GetRawText()
        };
    }

    internal static int GetInt(JsonElement args, string name, int defaultValue = 0)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p))
            return defaultValue;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetInt32(out var i) ? i : defaultValue,
            _ => defaultValue
        };
    }

    internal static bool GetBool(JsonElement args, string name, bool defaultValue = false)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p))
            return defaultValue;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    internal static string? GetJsonString(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.GetRawText();
    }
}

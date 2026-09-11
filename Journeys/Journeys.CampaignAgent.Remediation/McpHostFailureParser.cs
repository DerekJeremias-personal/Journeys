using System.Text.Json;
using System.Text.RegularExpressions;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>
/// Parses MCP host opaque failures (e.g. isError + content[].text) into <see cref="BackendToolFailure"/>.
/// </summary>
public static class McpHostFailureParser
{
    private static readonly Regex InvocationErrorRegex = new(
        @"An error occurred invoking\s+'(?<tool>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParse(string toolName, string? resultJson, out BackendToolFailure? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(resultJson))
            return false;

        var trimmed = resultJson.Trim();

        if (TryParseMcpContentEnvelope(trimmed, toolName, out failure))
            return true;

        if (TryParsePlainInvocationError(trimmed, toolName, out failure))
            return true;

        return false;
    }

    private static bool TryParseMcpContentEnvelope(string json, string toolName, out BackendToolFailure? failure)
    {
        failure = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!root.TryGetProperty("isError", out var isError) || isError.ValueKind != JsonValueKind.True)
                return false;

            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                return false;

            string? message = null;
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                if (!item.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
                    continue;
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    message = text;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(message))
                return false;

            failure = new BackendToolFailure
            {
                ToolName = toolName,
                RawJson = json,
                ParseStatus = BackendToolFailureParseStatus.Success,
                Message = message,
                ValidationErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParsePlainInvocationError(string text, string toolName, out BackendToolFailure? failure)
    {
        failure = null;
        var match = InvocationErrorRegex.Match(text);
        if (!match.Success)
            return false;

        failure = new BackendToolFailure
        {
            ToolName = toolName,
            RawJson = text,
            ParseStatus = BackendToolFailureParseStatus.Success,
            Message = text,
            ValidationErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
        return true;
    }
}

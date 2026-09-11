namespace Journeys.CampaignAgent.Remediation;

/// <summary>Compact stub for oversized tool results in history (budget shrink + persist fail-safe).</summary>
public static class ToolResultHistoryStub
{
    public const int DefaultMaxPersistedChars = 4096;

    public static string Build(string? toolName, int originalChars) =>
        $$"""{"historyStub":true,"tool":"{{EscapeJson(toolName ?? "unknown")}}","originalChars":{{originalChars}},"note":"Full tool result omitted for history budget; re-call tool or read WORKFLOW ARTIFACTS."}""";

    public static string? MaybeStub(string? toolName, string? toolResultJson, int maxChars = DefaultMaxPersistedChars)
    {
        if (maxChars <= 0 || string.IsNullOrEmpty(toolResultJson) || toolResultJson.Length <= maxChars)
            return toolResultJson;

        if (toolResultJson.Contains("\"historyStub\":true", StringComparison.Ordinal))
            return toolResultJson;

        return Build(toolName, toolResultJson.Length);
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}

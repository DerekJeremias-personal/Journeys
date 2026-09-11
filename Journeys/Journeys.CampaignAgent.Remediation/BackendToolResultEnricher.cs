using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Journeys.CampaignAgent.Remediation;

public static class BackendToolResultEnricher
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static bool TryEnrich(string? resultJson, AgentRemediationPayload payload, out string? enrichedJson)
    {
        enrichedJson = null;
        if (string.IsNullOrWhiteSpace(resultJson))
            return false;

        try
        {
            var node = JsonNode.Parse(resultJson);
            if (node is not JsonObject obj)
                return false;

            if (obj.ContainsKey("_agentRemediation"))
                return false;

            var remediationNode = JsonSerializer.SerializeToNode(payload, SerializeOptions);
            obj["_agentRemediation"] = remediationNode;
            enrichedJson = obj.ToJsonString(SerializeOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? SerializeResultToJson(object? result)
    {
        if (result is null)
            return null;

        if (result is string s)
            return s;

        if (result is JsonElement el)
            return el.GetRawText();

        try
        {
            return JsonSerializer.Serialize(result, SerializeOptions);
        }
        catch
        {
            return result.ToString();
        }
    }
}

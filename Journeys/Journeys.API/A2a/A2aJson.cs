using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.API.A2a;

internal static class A2aJsonOptions
{
    internal static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

internal sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement Params { get; set; }
}

internal sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

internal sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

internal sealed class SendMessageRequestDto
{
    public string? Tenant { get; set; }
    public MessageDto? Message { get; set; }
    public SendMessageConfigurationDto? Configuration { get; set; }
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}

internal sealed class SendMessageConfigurationDto
{
    public bool? ReturnImmediately { get; set; }
    public int? HistoryLength { get; set; }
}

internal sealed class SendMessageResponseDto
{
    public TaskDto? Task { get; set; }
    public MessageDto? Message { get; set; }
}

internal sealed class MessageDto
{
    public string MessageId { get; set; } = "";
    public string? ContextId { get; set; }
    public string? TaskId { get; set; }
    public string Role { get; set; } = "ROLE_USER";
    public List<PartDto> Parts { get; set; } = new();
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}

internal sealed class PartDto
{
    public string? Text { get; set; }
    public string? Raw { get; set; }
    public string? Url { get; set; }
    public JsonElement? Data { get; set; }
    public string? MediaType { get; set; }
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}

internal sealed class TaskDto
{
    public string Id { get; set; } = "";
    public string? ContextId { get; set; }
    public TaskStatusDto Status { get; set; } = new();
    public List<ArtifactDto>? Artifacts { get; set; }
    public List<MessageDto>? History { get; set; }
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}

internal sealed class TaskStatusDto
{
    public string State { get; set; } = "TASK_STATE_WORKING";
    public MessageDto? Message { get; set; }
    public string? Timestamp { get; set; }
}

internal sealed class ArtifactDto
{
    public string ArtifactId { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<PartDto> Parts { get; set; } = new();
}

internal sealed class GetTaskParamsDto
{
    public string? Id { get; set; }
    public int? HistoryLength { get; set; }
}

internal sealed class CancelTaskParamsDto
{
    public string? Id { get; set; }
}

internal sealed class AgentCardDto
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Url { get; set; }
    public object? Provider { get; set; }
    public string? Version { get; set; }
    public string? DocumentationUrl { get; set; }
    public List<string> DefaultInputModes { get; set; } = new();
    public List<string>? DefaultOutputModes { get; set; }
    public AgentCapabilitiesDto Capabilities { get; set; } = new();
    public Dictionary<string, object>? SecuritySchemes { get; set; }
    public List<object>? Security { get; set; }
    public List<AgentSkillDto> Skills { get; set; } = new();
    public List<SupportedInterfaceDto> SupportedInterfaces { get; set; } = new();
}

internal sealed class AgentCapabilitiesDto
{
    public bool Streaming { get; set; }
    public bool PushNotifications { get; set; }
    public bool StateTransitionHistory { get; set; }
    public bool ExtendedAgentCard { get; set; }
}

internal sealed class AgentSkillDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<string>? Examples { get; set; }
}

internal sealed class SupportedInterfaceDto
{
    public string Url { get; set; } = "";

    [JsonPropertyName("protocolBinding")]
    public string ProtocolBinding { get; set; } = "JSONRPC";

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "1.0";
}

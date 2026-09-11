using System.Text.Json.Serialization;

namespace Journeys.Infra.Backend.Models;

public class GetRequest
{
    [JsonPropertyName("ModelId")]
    public string ModelId { get; set; }

    [JsonPropertyName("ModelType")]
    public string ModelType { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("Pks")]
    public Dictionary<string, string>? Pks { get; set; }

    public GetRequest(string modelType, string modelId, string id, Dictionary<string, string>? pks = null)
    {
        ModelId = modelId;
        ModelType = modelType;
        Id = id;
        Pks = pks;
    }
}

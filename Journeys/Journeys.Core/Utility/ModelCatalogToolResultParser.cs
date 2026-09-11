using System.Text.Json;
using Backend.Dto.Structures.Model;

namespace Journeys.Core.Utility;

/// <summary>
/// Parses model lists from list_models / get_all_models tool results, including digested catalog shapes.
/// </summary>
public static class ModelCatalogToolResultParser
{
    private static readonly JsonSerializerOptions ModelJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] CatalogToolNames =
    [
        "ListModels", "list_models", "GetAllModels", "get_all_models"
    ];

    public static bool IsCatalogTool(string? toolName) =>
        !string.IsNullOrWhiteSpace(toolName)
        && CatalogToolNames.Any(n => string.Equals(n, toolName, StringComparison.OrdinalIgnoreCase));

    public static bool TryParseModels(string? toolJson, out List<ModelDto> models)
    {
        models = [];
        if (string.IsNullOrWhiteSpace(toolJson))
            return false;

        try
        {
            var normalized = ToolResultJsonNormalizer.Unwrap(toolJson);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            using var doc = JsonDocument.Parse(normalized);
            return TryParseModels(doc.RootElement, models);
        }
        catch (JsonException)
        {
            models = [];
            return false;
        }
    }

    internal static bool TryParseModels(JsonElement root, List<ModelDto> models)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("text", out var textProp)
            && textProp.ValueKind == JsonValueKind.String)
        {
            var inner = textProp.GetString();
            if (!string.IsNullOrWhiteSpace(inner))
            {
                try
                {
                    using var doc = JsonDocument.Parse(inner);
                    return TryParseModels(doc.RootElement, models);
                }
                catch (JsonException)
                {
                    return false;
                }
            }
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("catalog", out var catalog)
            && catalog.ValueKind == JsonValueKind.Array)
        {
            var byId = new Dictionary<string, ModelDto>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("retainedModels", out var retained)
                && retained.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in retained.EnumerateArray())
                    TryAddModel(element, byId);
            }

            foreach (var line in catalog.EnumerateArray())
            {
                var id = GetString(line, "id");
                if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id))
                    continue;

                byId[id] = new ModelDto
                {
                    ID = id,
                    Name = GetString(line, "name"),
                    ModelType = GetString(line, "modelType"),
                    Tag = GetString(line, "tag"),
                    IsContainer = GetBool(line, "isContainer")
                };
            }

            if (byId.Count > 0)
            {
                models.AddRange(byId.Values);
                return true;
            }
        }

        if (TryGetModelArray(root, out var array))
        {
            foreach (var element in array.EnumerateArray())
                TryAddModel(element, models);
            return models.Count > 0;
        }

        return false;
    }

    private static void TryAddModel(JsonElement element, List<ModelDto> models)
    {
        var model = DeserializeModel(element);
        if (model != null && !string.IsNullOrWhiteSpace(model.ID))
            models.Add(model);
    }

    private static void TryAddModel(JsonElement element, Dictionary<string, ModelDto> byId)
    {
        var model = DeserializeModel(element);
        if (model != null && !string.IsNullOrWhiteSpace(model.ID))
            byId[model.ID] = model;
    }

    private static ModelDto? DeserializeModel(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<ModelDto>(element.GetRawText(), ModelJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetModelArray(JsonElement root, out JsonElement array)
    {
        array = default;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "models", "Items", "data" })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    array = arr;
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Journeys.Core.Utility;

public static class ModelCatalogDigester
{
    public const string Note =
        "Catalog digested for context efficiency. Models are pre-filtered by tag on the server when configured; " +
        "attributes are slimmed to rule-relevant fields. Call get_model for nested child-model detail.";

    public static ModelCatalogDigestResult Digest(string rawJson, string? sourceTag = null)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new ModelCatalogDigestResult(rawJson ?? string.Empty, false, rawJson?.Length ?? 0, rawJson?.Length ?? 0, 0, 0);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return Passthrough(rawJson); }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("$type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "text"
                && root.TryGetProperty("text", out var inner) && inner.ValueKind == JsonValueKind.String)
            {
                var innerJson = inner.GetString() ?? string.Empty;
                var innerResult = Digest(innerJson, sourceTag);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new ModelCatalogDigestResult(
                    rewrapped, true, rawJson.Length, rewrapped.Length, innerResult.ModelCount, innerResult.RetainedCount);
            }

            return DigestCore(root, rawJson, sourceTag);
        }
    }

    private static ModelCatalogDigestResult DigestCore(JsonElement root, string rawJson, string? sourceTag)
    {
        if (!TryGetModelArray(root, out var models, out var pagingRoot))
            return Passthrough(rawJson);

        var slimModels = new List<string>();
        var count = 0;
        foreach (var model in models.EnumerateArray())
        {
            count++;
            slimModels.Add(ModelReadDigester.SlimModelJson(model));
        }

        var json = WriteSlimCatalog(slimModels, pagingRoot, count, sourceTag);
        return new ModelCatalogDigestResult(json, true, rawJson.Length, json.Length, count, count);
    }

    private static ModelCatalogDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length, 0, 0);

    private static bool TryGetModelArray(JsonElement root, out JsonElement models, out JsonElement pagingRoot)
    {
        pagingRoot = root;
        models = default;
        if (root.ValueKind == JsonValueKind.Array) { models = root; return true; }
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "models", "Items", "data" })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    models = arr;
                    return true;
                }
            }
        }
        return false;
    }

    private static string WriteSlimCatalog(List<string> slimModels, JsonElement pagingRoot, int total, string? sourceTag)
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("note", Note);
            CopyNumberOrBool(w, pagingRoot, "Count");
            CopyNumberOrBool(w, pagingRoot, "IsLastPage");
            CopyString(w, pagingRoot, "ContinuationToken");

            w.WriteStartObject("summary");
            w.WriteNumber("total", total);
            if (!string.IsNullOrWhiteSpace(sourceTag))
                w.WriteString("tag", sourceTag);
            w.WriteEndObject();

            w.WriteStartArray("models");
            foreach (var slim in slimModels)
                w.WriteRawValue(slim);
            w.WriteEndArray();

            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void CopyNumberOrBool(Utf8JsonWriter w, JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number)
            {
                w.WritePropertyName(name);
                w.WriteRawValue(v.GetRawText());
            }
            else if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) w.WriteBoolean(name, v.GetBoolean());
        }
    }

    private static void CopyString(Utf8JsonWriter w, JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            w.WriteString(name, v.GetString());
    }
}

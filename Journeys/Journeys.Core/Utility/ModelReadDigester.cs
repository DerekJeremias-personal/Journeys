using System.Text.Json;

namespace Journeys.Core.Utility;

/// <summary>
/// Compacts a single-model <c>get_model</c> result: keeps identity, metadata, and rule-relevant attribute rows;
/// drops display text and other authoring noise.
/// </summary>
public static class ModelReadDigester
{
    public const int VerificationMaxAttributes = 24;
    public const int CreationMaxAttributes = 12;
    public const int CreationMaxChars = 900;

    public const string Note =
        "Model digested for context efficiency. Symbols, data types, and modelMetaData are retained; " +
        "call get_model again only if you need nested child-model schema detail.";

    public static ReadToolDigestResult Digest(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Passthrough(rawJson ?? string.Empty);

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
                var innerResult = Digest(innerJson);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new ReadToolDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            if (!LooksLikeModel(root))
                return Passthrough(rawJson);

            var json = WriteSlim(root, includeNote: true);
            return new ReadToolDigestResult(json, true, rawJson.Length, json.Length);
        }
    }

    /// <summary>Minimal model JSON for creation phases — caps attributes and enforces 900-char budget.</summary>
    public static ReadToolDigestResult DigestForCreation(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Passthrough(rawJson ?? string.Empty);

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
                var innerResult = DigestForCreation(innerJson);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new ReadToolDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            if (!LooksLikeModel(root))
                return Passthrough(rawJson);

            var json = WriteCreationSlim(root, symbolOnly: false);
            if (json.Length > CreationMaxChars)
                json = WriteCreationSlim(root, symbolOnly: true);

            return new ReadToolDigestResult(json, true, rawJson.Length, json.Length);
        }
    }

    private static string WriteCreationSlim(JsonElement root, bool symbolOnly)
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("note", Note);
            CopyString(w, root, "id", "Id");
            CopyString(w, root, "name", "Name");
            CopyString(w, root, "modelType", "ModelType");
            CopyString(w, root, "tag", "Tag");
            CopyBool(w, root, "isContainer", "IsContainer");
            if (!symbolOnly)
                CopyObject(w, root, "modelMetaData", "ModelMetaData");

            if (TryGetProperty(root, "attributes", "Attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
            {
                w.WritePropertyName("attributes");
                w.WriteStartArray();
                var count = 0;
                var total = attrs.GetArrayLength();
                foreach (var attr in attrs.EnumerateArray())
                {
                    if (count >= CreationMaxAttributes)
                        break;
                    if (symbolOnly)
                        WriteVerificationAttributeRow(w, attr);
                    else
                        WriteCreationAttributeRow(w, attr);
                    count++;
                }
                w.WriteEndArray();
                w.WriteNumber("attributeCount", total);
                if (total > CreationMaxAttributes)
                    w.WriteBoolean("attributesTruncated", true);
            }

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCreationAttributeRow(Utf8JsonWriter w, JsonElement attr)
    {
        w.WriteStartObject();
        CopyString(w, attr, "symbol", "Symbol", "name", "Name");
        CopyString(w, attr, "dataType", "DataType");
        CopyString(w, attr, "type", "Type", "attributeType", "AttributeType");

        var typeValue = ReadStringValue(attr, "type", "Type", "attributeType", "AttributeType");
        if (string.Equals(typeValue, "ModelList", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeValue, "ModelObject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeValue, "ModelKeyValue", StringComparison.OrdinalIgnoreCase))
        {
            CopyString(w, attr, "modelId", "ModelId");
        }

        w.WriteEndObject();
    }

    private static string? ReadStringValue(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }

        return null;
    }

    /// <summary>Minimal symbol paths for post-creation verification — no nested model metadata noise.</summary>
    public static ReadToolDigestResult DigestForVerification(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Passthrough(rawJson ?? string.Empty);

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
                var innerResult = DigestForVerification(innerJson);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new ReadToolDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            if (!LooksLikeModel(root))
                return Passthrough(rawJson);

            var json = WriteVerificationSlim(root);
            return new ReadToolDigestResult(json, true, rawJson.Length, json.Length);
        }
    }

    private static string WriteVerificationSlim(JsonElement root)
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("note", Note);
            CopyString(w, root, "id", "Id");
            CopyString(w, root, "name", "Name");
            CopyString(w, root, "modelType", "ModelType");

            if (TryGetProperty(root, "attributes", "Attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
            {
                w.WritePropertyName("attributes");
                w.WriteStartArray();
                var count = 0;
                var total = attrs.GetArrayLength();
                foreach (var attr in attrs.EnumerateArray())
                {
                    if (count >= VerificationMaxAttributes)
                        break;
                    WriteVerificationAttributeRow(w, attr);
                    count++;
                }
                w.WriteEndArray();
                w.WriteNumber("attributeCount", total);
                if (total > VerificationMaxAttributes)
                    w.WriteBoolean("attributesTruncated", true);
            }

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteVerificationAttributeRow(Utf8JsonWriter w, JsonElement attr)
    {
        w.WriteStartObject();
        CopyString(w, attr, "symbol", "Symbol", "name", "Name");
        CopyString(w, attr, "dataType", "DataType");
        w.WriteEndObject();
    }

    /// <summary>Rule-relevant model JSON without the outer digest note (for embedding in catalog digests).</summary>
    public static string SlimModelJson(JsonElement root) => WriteSlim(root, includeNote: false);

    private static bool LooksLikeModel(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && (HasString(root, "id", "Id") || HasString(root, "name", "Name"));

    private static string WriteSlim(JsonElement root, bool includeNote = true)
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            if (includeNote)
                w.WriteString("note", Note);
            WriteSlimModelFields(w, root);
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    internal static void WriteSlimModelFields(Utf8JsonWriter w, JsonElement root)
    {
        CopyString(w, root, "id", "Id");
        CopyString(w, root, "name", "Name");
        CopyString(w, root, "modelType", "ModelType");
        CopyString(w, root, "tag", "Tag");
        CopyBool(w, root, "isContainer", "IsContainer");
        CopyObject(w, root, "modelMetaData", "ModelMetaData");

        if (TryGetProperty(root, "attributes", "Attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
        {
            w.WritePropertyName("attributes");
            w.WriteStartArray();
            foreach (var attr in attrs.EnumerateArray())
                WriteAttributeRow(w, attr);
            w.WriteEndArray();
            w.WriteNumber("attributeCount", attrs.GetArrayLength());
        }
    }

    private static void WriteAttributeRow(Utf8JsonWriter w, JsonElement attr)
    {
        w.WriteStartObject();
        CopyString(w, attr, "symbol", "Symbol", "name", "Name");
        CopyString(w, attr, "dataType", "DataType");
        CopyString(w, attr, "type", "Type", "attributeType", "AttributeType");
        CopyString(w, attr, "status", "Status");
        CopyBool(w, attr, "includeInSampleTemplate", "IncludeInSampleTemplate");
        CopyString(w, attr, "modelId", "ModelId");
        CopyString(w, attr, "modelType", "ModelType");
        CopyString(w, attr, "listAttributeDataType", "ListAttributeDataType");
        CopyString(w, attr, "keyValueAttributeDataType", "KeyValueAttributeDataType");
        w.WriteEndObject();
    }

    private static ReadToolDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);

    private static bool HasString(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(v.GetString()))
                return true;
        return false;
    }

    private static bool TryGetProperty(JsonElement e, string name1, string name2, out JsonElement value)
    {
        if (e.TryGetProperty(name1, out value)) return true;
        if (e.TryGetProperty(name2, out value)) return true;
        value = default;
        return false;
    }

    private static void CopyString(Utf8JsonWriter w, JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
            {
                w.WriteString(ToCamel(n), v.GetString());
                return;
            }
        }
    }

    private static void CopyBool(Utf8JsonWriter w, JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                w.WriteBoolean(ToCamel(n), v.GetBoolean());
                return;
            }
        }
    }

    private static void CopyObject(Utf8JsonWriter w, JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Object)
            {
                w.WritePropertyName(ToCamel(n));
                v.WriteTo(w);
                return;
            }
        }
    }

    private static string ToCamel(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}

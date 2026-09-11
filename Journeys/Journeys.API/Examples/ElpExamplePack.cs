using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.API.Examples;

/// <summary>
/// Loads non-customer example campaign JSON from Examples/Journeys (manifest + files). Tenant: mericantires.
/// </summary>
public sealed class JourneysExamplePack
{
    public const string ExampleTenantId = "mericantires";
    private const int MaxExampleFileBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions IndexReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IWebHostEnvironment _env;

    public JourneysExamplePack(IWebHostEnvironment env)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    private string PackRoot => Path.Combine(_env.ContentRootPath, "Examples", "Journeys");

    private string IndexPath => Path.Combine(PackRoot, "campaigns-index.json");

    public async Task<string> ListSummariesJsonAsync(CancellationToken cancellationToken = default)
    {
        var parsed = await TryParseIndexAsync(cancellationToken).ConfigureAwait(false);
        if (!parsed.ok || parsed.index == null)
            return JsonSerializer.Serialize(new { error = parsed.error ?? "Invalid index." }, JsonOptions);

        var rows = parsed.index.Items.Select(i => new { i.Id, i.PatternId, i.Title, i.Summary, tenant = parsed.index.DefaultTenant }).ToList();
        return JsonSerializer.Serialize(new { schemaVersion = parsed.index.SchemaVersion, examples = rows }, JsonOptions);
    }

    public async Task<string> GetExampleJsonAsync(string exampleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exampleId))
            return JsonSerializer.Serialize(new { error = "exampleId is required." }, JsonOptions);

        var parsed = await TryParseIndexAsync(cancellationToken).ConfigureAwait(false);
        if (!parsed.ok || parsed.index == null)
            return JsonSerializer.Serialize(new { error = parsed.error ?? "Invalid index." }, JsonOptions);

        var item = parsed.index.Items.FirstOrDefault(i => string.Equals(i.Id, exampleId, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return JsonSerializer.Serialize(new { error = $"Unknown exampleId '{exampleId}'." }, JsonOptions);

        var resolved = ResolveSafeFilePath(item.RelativeFile);
        if (resolved == null)
            return JsonSerializer.Serialize(new { error = "Invalid or unsafe example file path in index." }, JsonOptions);

        try
        {
            var info = new FileInfo(resolved);
            if (!info.Exists)
                return JsonSerializer.Serialize(new { error = "Example file missing on disk.", file = item.RelativeFile }, JsonOptions);
            if (info.Length > MaxExampleFileBytes)
                return JsonSerializer.Serialize(new { error = "Example file exceeds size limit." }, JsonOptions);

            return await File.ReadAllTextAsync(resolved, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private string? ResolveSafeFilePath(string relativeFile)
    {
        if (string.IsNullOrWhiteSpace(relativeFile) || relativeFile.Contains("..", StringComparison.Ordinal))
            return null;

        var rootFull = Path.GetFullPath(PackRoot);
        var combined = Path.GetFullPath(Path.Combine(PackRoot, relativeFile));
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            return null;
        return combined;
    }

    private async Task<(bool ok, ExampleCampaignsIndex? index, string? error)> TryParseIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(IndexPath))
            return (false, null, "Index not found.");

        try
        {
            await using var stream = File.OpenRead(IndexPath);
            var index = await JsonSerializer.DeserializeAsync<ExampleCampaignsIndex>(stream, IndexReadOptions, cancellationToken).ConfigureAwait(false);
            if (index?.Items == null || index.Items.Count == 0)
                return (false, null, "Index has no items.");
            return (true, index, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private sealed class ExampleCampaignsIndex
    {
        [JsonPropertyName("schemaVersion")]
        public string? SchemaVersion { get; set; }

        [JsonPropertyName("defaultTenant")]
        public string? DefaultTenant { get; set; }

        [JsonPropertyName("items")]
        public List<ExampleCampaignIndexItem> Items { get; set; } = new();
    }

    private sealed class ExampleCampaignIndexItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("patternId")]
        public string? PatternId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("relativeFile")]
        public string RelativeFile { get; set; } = "";
    }
}

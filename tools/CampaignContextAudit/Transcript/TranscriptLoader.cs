using System.Text.Json;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Transcript;

public sealed record LoadedTranscript(
    string SourcePath,
    IReadOnlyList<AgentMessageDoc> ChatRows,
    IReadOnlyList<AgentMessageDoc> WorkflowRows,
    string? OwnerUserId = null);

public static class TranscriptLoader
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static LoadedTranscript Load(string path)
    {
        var text = File.ReadAllText(path);
        var all = JsonSerializer.Deserialize<List<AgentMessageDoc>>(text, Opts) ?? new();
        var ordered = all.OrderBy(m => m.Sequence).ToList();
        var chat = ordered.Where(m => !m.IsWorkflowRow).ToList();
        var workflow = ordered.Where(m => m.IsWorkflowRow).ToList();
        return new LoadedTranscript(path, chat, workflow);
    }

    public static IReadOnlyList<LoadedTranscript> LoadDir(string dir) =>
        Directory.EnumerateFiles(dir, "*.json")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(Load)
            .ToList();
}

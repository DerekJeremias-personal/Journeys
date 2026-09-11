using System.IO;
using CampaignContextAudit.Transcript;
using Xunit;

namespace CampaignContextAudit.Tests;

public class TranscriptLoaderTests
{
    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"transcript-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_orders_by_sequence_and_excludes_workflow_rows()
    {
        var json = """
        [
          { "sequence": 2, "role": "assistant", "content": "hi" },
          { "sequence": 0, "role": "user", "content": "hello" },
          { "sequence": 1, "role": "workflow", "content": "{}" }
        ]
        """;
        var loaded = TranscriptLoader.Load(WriteTemp(json));

        Assert.Equal(2, loaded.ChatRows.Count);
        Assert.Equal(0, loaded.ChatRows[0].Sequence);
        Assert.Equal("user", loaded.ChatRows[0].Role);
        Assert.Single(loaded.WorkflowRows);
    }
}

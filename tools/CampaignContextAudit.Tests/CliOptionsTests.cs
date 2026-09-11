using CampaignContextAudit.Cli;
using Xunit;

namespace CampaignContextAudit.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_cosmos_mode_requires_tenant()
    {
        var opts = CliOptions.Parse(["--conversation-id", "abc", "--governance", "g", "--out", "o"]);
        Assert.Null(opts);
    }

    [Fact]
    public void Parse_rejects_transcripts_and_conversation_id_together()
    {
        var opts = CliOptions.Parse([
            "--tenant", "primo",
            "--conversation-id", "abc",
            "--transcripts", "dir",
            "--governance", "g",
            "--out", "o"]);
        Assert.Null(opts);
    }

    [Fact]
    public void Parse_cosmos_mode_collects_multiple_conversation_ids()
    {
        var opts = CliOptions.Parse([
            "--tenant", "primo",
            "--conversation-id", "aaa",
            "--conversation-id", "bbb",
            "--governance", "g",
            "--out", "o"]);
        Assert.NotNull(opts);
        Assert.Equal(CliInputMode.Cosmos, opts!.InputMode);
        Assert.Equal(["aaa", "bbb"], opts.ConversationIds);
    }

    [Fact]
    public void Parse_file_mode_unchanged()
    {
        var opts = CliOptions.Parse(["--transcripts", "dir", "--governance", "g", "--out", "o"]);
        Assert.NotNull(opts);
        Assert.Equal(CliInputMode.File, opts!.InputMode);
        Assert.Equal("dir", opts.TranscriptsDir);
    }

    [Fact]
    public void Parse_stall_and_abort_ms()
    {
        var opts = CliOptions.Parse([
            "--transcripts", "dir", "--governance", "g", "--out", "o",
            "--stall-ms", "60000", "--abort-ms", "180000"]);
        Assert.NotNull(opts);
        Assert.Equal(60_000, opts!.StallMs);
        Assert.Equal(180_000, opts.AbortMs);
    }

    [Fact]
    public void Parse_static_mode_without_transcripts()
    {
        var opts = CliOptions.Parse([
            "--static-governance",
            "--governance", "/g",
            "--out", "/o"]);
        Assert.NotNull(opts);
        Assert.Equal(CliInputMode.StaticGovernance, opts!.InputMode);
    }

    [Fact]
    public void Parse_static_mode_rejects_transcripts()
    {
        var opts = CliOptions.Parse([
            "--static-governance",
            "--transcripts", "dir",
            "--governance", "g",
            "--out", "o"]);
        Assert.Null(opts);
    }
}

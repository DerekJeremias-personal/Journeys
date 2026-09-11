using System.IO;
using CampaignContextAudit;
using Xunit;

namespace CampaignContextAudit.Tests;

public class ProgramEndToEndTests
{
    [Fact]
    public async Task Main_generates_a_report_with_nonzero_stable_and_a_finding()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cca-e2e-{Guid.NewGuid():N}");
        var transcripts = Path.Combine(root, "transcripts");
        var gov = Path.Combine(root, "gov");
        var outDir = Path.Combine(root, "out");
        Directory.CreateDirectory(transcripts);
        Directory.CreateDirectory(gov);

        // Minimal governance files so stable chars are non-zero (EventModels phase used after save_model).
        File.WriteAllText(Path.Combine(gov, "SystemPrompt.txt"), new string('p', 50));
        File.WriteAllText(Path.Combine(gov, "SharedAgentToolingGovernance.txt"), new string('s', 50));
        File.WriteAllText(Path.Combine(gov, "CampaignGovernanceCore.txt"), new string('c', 50));
        File.WriteAllText(Path.Combine(gov, "WorkflowPhaseEventModelsGovernance.txt"), new string('e', 50));
        File.WriteAllText(Path.Combine(gov, "WorkflowPhaseDataAnalysisGovernance.txt"), new string('d', 50));

        var envelope = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c1","name":"save_model","arguments":{}}]}""";
        var transcript = "[" +
            """{"sequence":0,"role":"user","content":"hi"},""" +
            $"{{\"sequence\":1,\"role\":\"assistant\",\"content\":{System.Text.Json.JsonSerializer.Serialize(envelope)}}}," +
            $"{{\"sequence\":2,\"role\":\"tool\",\"toolCallId\":\"c1\",\"toolResultJson\":\"{new string('x', 4000)}\"}}," +
            """{"sequence":3,"role":"user","content":"again"}""" +
            "]";
        File.WriteAllText(Path.Combine(transcripts, "conv.json"), transcript);

        var exit = await Program.Main(new[] { "--transcripts", transcripts, "--governance", gov, "--out", outDir });

        Assert.Equal(0, exit);
        var report = Directory.GetFiles(outDir, "*.md").Single();
        var md = File.ReadAllText(report);
        Assert.Contains("Per-turn context budget", md);
        Assert.Contains("TOOL_RESULT_BLOAT", md);
        Assert.Contains("EventModels", md);          // phase floor advanced via save_model
        Assert.DoesNotContain("| 0 | DataAnalysis | 0 |", md); // stable chars not zero / phase progressed
    }
}

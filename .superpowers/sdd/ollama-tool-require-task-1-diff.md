# Review package: uncommitted Task 1 (no commits per human rule)

## Commits
(none)

## Files changed
Journeys.API/CampaignAgent/OllamaToolRequire.cs (added)
Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs (added)

## Test evidence (controller)
dotnet test filter OllamaToolRequireTests -o TEMP: Passed 9/9 (theory expands 3 InlineData).

## Diff
diff --git a/Journeys/Journeys.API/CampaignAgent/OllamaToolRequire.cs b/Journeys/Journeys.API/CampaignAgent/OllamaToolRequire.cs
new file mode 100644
index 0000000..20b9f02
--- /dev/null
+++ b/Journeys/Journeys.API/CampaignAgent/OllamaToolRequire.cs
@@ -0,0 +1,32 @@
+using Journeys.Core.Models;
+using Microsoft.Extensions.AI;
+
+namespace Journeys.API.CampaignAgent;
+
+public static class OllamaToolRequire
+{
+    public static bool ShouldRequire(
+        CampaignAgentLlmProviderKind provider,
+        int filteredToolCount,
+        CampaignWorkflowPhase phase)
+    {
+        if (provider != CampaignAgentLlmProviderKind.OpenAICompatible)
+            return false;
+        if (filteredToolCount <= 0)
+            return false;
+        if (phase == CampaignWorkflowPhase.Done)
+            return false;
+        return true;
+    }
+
+    public static void Apply(
+        ChatOptions options,
+        CampaignAgentLlmProviderKind provider,
+        CampaignWorkflowPhase phase)
+    {
+        ArgumentNullException.ThrowIfNull(options);
+        var count = options.Tools?.Count ?? 0;
+        if (ShouldRequire(provider, count, phase))
+            options.ToolMode = ChatToolMode.RequireAny;
+    }
+}
diff --git a/Journeys/Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs b/Journeys/Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs
new file mode 100644
index 0000000..e4b2379
--- /dev/null
+++ b/Journeys/Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs
@@ -0,0 +1,70 @@
+using Journeys.API.CampaignAgent;
+using Journeys.Core.Models;
+using Microsoft.Extensions.AI;
+
+namespace Journeys.Tests.CampaignAgent;
+
+public class OllamaToolRequireTests
+{
+    [Fact]
+    public void ShouldRequire_true_for_ollama_with_tools_and_not_done()
+    {
+        Assert.True(OllamaToolRequire.ShouldRequire(
+            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.DataAnalysis));
+    }
+
+    [Theory]
+    [InlineData(CampaignWorkflowPhase.EventModels)]
+    [InlineData(CampaignWorkflowPhase.CampaignBuild)]
+    [InlineData(CampaignWorkflowPhase.Verification)]
+    public void ShouldRequire_true_for_other_non_done_phases(CampaignWorkflowPhase phase)
+    {
+        Assert.True(OllamaToolRequire.ShouldRequire(
+            CampaignAgentLlmProviderKind.OpenAICompatible, 1, phase));
+    }
+
+    [Fact]
+    public void ShouldRequire_false_for_anthropic()
+    {
+        Assert.False(OllamaToolRequire.ShouldRequire(
+            CampaignAgentLlmProviderKind.Anthropic, 3, CampaignWorkflowPhase.DataAnalysis));
+    }
+
+    [Fact]
+    public void ShouldRequire_false_when_no_tools()
+    {
+        Assert.False(OllamaToolRequire.ShouldRequire(
+            CampaignAgentLlmProviderKind.OpenAICompatible, 0, CampaignWorkflowPhase.DataAnalysis));
+    }
+
+    [Fact]
+    public void ShouldRequire_false_when_done()
+    {
+        Assert.False(OllamaToolRequire.ShouldRequire(
+            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.Done));
+    }
+
+    [Fact]
+    public void Apply_sets_RequireAny_when_should_require()
+    {
+        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
+        OllamaToolRequire.Apply(
+            options, CampaignAgentLlmProviderKind.OpenAICompatible, CampaignWorkflowPhase.DataAnalysis);
+        Assert.Equal(ChatToolMode.RequireAny, options.ToolMode);
+    }
+
+    [Fact]
+    public void Apply_leaves_ToolMode_unset_for_anthropic()
+    {
+        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
+        OllamaToolRequire.Apply(
+            options, CampaignAgentLlmProviderKind.Anthropic, CampaignWorkflowPhase.DataAnalysis);
+        Assert.Null(options.ToolMode);
+    }
+
+    private sealed class TestTool(string name) : AITool
+    {
+        public override string Name => name;
+        public override string Description => name;
+    }
+}

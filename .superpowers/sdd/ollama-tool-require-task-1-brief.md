### Task 1: `OllamaToolRequire` (TDD)

**Files:**
- Create: `Journeys.API/CampaignAgent/OllamaToolRequire.cs`
- Test: `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs`

**Interfaces:**
- Consumes: `CampaignAgentLlmProviderKind`, `CampaignWorkflowPhase`, `ChatOptions` (`Microsoft.Extensions.AI`)
- Produces:
  - `OllamaToolRequire.ShouldRequire(CampaignAgentLlmProviderKind provider, int filteredToolCount, CampaignWorkflowPhase phase)` â†’ `bool`
  - `OllamaToolRequire.Apply(ChatOptions options, CampaignAgentLlmProviderKind provider, CampaignWorkflowPhase phase)` â†’ void; sets `options.ToolMode = ChatToolMode.RequireAny` iff `ShouldRequire` is true using `options.Tools?.Count ?? 0`; otherwise leaves `ToolMode` unchanged

- [ ] **Step 1: Write failing tests** `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs`

```csharp
using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public class OllamaToolRequireTests
{
    [Fact]
    public void ShouldRequire_true_for_ollama_with_tools_and_not_done()
    {
        Assert.True(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.DataAnalysis));
    }

    [Theory]
    [InlineData(CampaignWorkflowPhase.EventModels)]
    [InlineData(CampaignWorkflowPhase.CampaignBuild)]
    [InlineData(CampaignWorkflowPhase.Verification)]
    public void ShouldRequire_true_for_other_non_done_phases(CampaignWorkflowPhase phase)
    {
        Assert.True(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 1, phase));
    }

    [Fact]
    public void ShouldRequire_false_for_anthropic()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.Anthropic, 3, CampaignWorkflowPhase.DataAnalysis));
    }

    [Fact]
    public void ShouldRequire_false_when_no_tools()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 0, CampaignWorkflowPhase.DataAnalysis));
    }

    [Fact]
    public void ShouldRequire_false_when_done()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.Done));
    }

    [Fact]
    public void Apply_sets_RequireAny_when_should_require()
    {
        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
        OllamaToolRequire.Apply(
            options, CampaignAgentLlmProviderKind.OpenAICompatible, CampaignWorkflowPhase.DataAnalysis);
        Assert.Equal(ChatToolMode.RequireAny, options.ToolMode);
    }

    [Fact]
    public void Apply_leaves_ToolMode_unset_for_anthropic()
    {
        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
        OllamaToolRequire.Apply(
            options, CampaignAgentLlmProviderKind.Anthropic, CampaignWorkflowPhase.DataAnalysis);
        Assert.Null(options.ToolMode);
    }

    private sealed class TestTool(string name) : AITool
    {
        public override string Name => name;
        public override string Description => name;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~OllamaToolRequireTests"
```

If MSB3027 file lock (running `Journeys.API`): add `-o $env:TEMP\journeys-test-out`.

Expected: FAIL compile (`OllamaToolRequire` does not exist).

- [ ] **Step 3: Write minimal implementation** `Journeys.API/CampaignAgent/OllamaToolRequire.cs`

```csharp
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

public static class OllamaToolRequire
{
    public static bool ShouldRequire(
        CampaignAgentLlmProviderKind provider,
        int filteredToolCount,
        CampaignWorkflowPhase phase)
    {
        if (provider != CampaignAgentLlmProviderKind.OpenAICompatible)
            return false;
        if (filteredToolCount <= 0)
            return false;
        if (phase == CampaignWorkflowPhase.Done)
            return false;
        return true;
    }

    public static void Apply(
        ChatOptions options,
        CampaignAgentLlmProviderKind provider,
        CampaignWorkflowPhase phase)
    {
        ArgumentNullException.ThrowIfNull(options);
        var count = options.Tools?.Count ?? 0;
        if (ShouldRequire(provider, count, phase))
            options.ToolMode = ChatToolMode.RequireAny;
    }
}
```

- [ ] **Step 4: Re-run tests**

Same `dotnet test` filter as Step 2.

Expected: PASS.

- [ ] **Step 5: Do not commit** unless the user asked in this message.

---


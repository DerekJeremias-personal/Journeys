# Review package: uncommitted Task 3 (no commits per human rule)

## Commits
(none — human owns git)

## Test evidence (controller)
- Filtered tests: Passed 35/35.
- Waiver on disk: `Journeys/docs/product/graph/waivers/2026-09-17-ollama-tool-require.md` (workspace-relative `docs/product/graph/waivers/...`). Single copy.
- After stopping Journeys.API pid 14540: sln build succeeded (0 errors).
- `agent-verify` with CampaignAgent + docs + waiver (no Journeys.Tests in -Files, so full suite not triggered): docs-impact OK, graph-impact OK, `agent-verify: OK`.
- Listing Journeys.Tests in -Files runs the entire test project; 44 pre-existing BatchJobAdapterIntegrationTests failed against live Backend — unrelated to this task.

## Diff
diff --git a/Journeys/Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs b/Journeys/Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs
index e9050e4..18445d4 100644
--- a/Journeys/Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs
+++ b/Journeys/Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs
@@ -597,20 +597,24 @@ public class CampaignAgentOrchestrator : ICampaignAgentOrchestrator
 
                 // When false, the model may only request one tool invocation per model response (stops parallel duplicate UpsertCampaign batches).
                 var allowMultiTool = _configuration.GetValue("CampaignAgent:AllowMultipleToolCallsPerModelResponse", true);
 
                 var chatTemplate = new ChatOptions
                 {
                     Tools = aiTools,
                     MaxOutputTokens = maxOut,
                     AllowMultipleToolCalls = allowMultiTool
                 };
+                OllamaToolRequire.Apply(
+                    chatTemplate,
+                    CampaignAgentLlmProvider.Resolve(_configuration),
+                    workflowState.Phase);
                 var mappedPrompt = _llmPromptChatMapper.Map(
                     promptContext.PromptPlan,
                     "CampaignAgent",
                     chatTemplate,
                     conversationMessages);
 
                 var assistantText = new StringBuilder();
                 var streamedUpdates = new List<ChatResponseUpdate>();
                 var llmSw = Stopwatch.StartNew();
                 await foreach (var update in chatClient
diff --git a/Journeys/docs/product/graph/waivers/2026-09-17-ollama-tool-require.md b/Journeys/docs/product/graph/waivers/2026-09-17-ollama-tool-require.md
new file mode 100644
index 0000000..3cae805
--- /dev/null
+++ b/Journeys/docs/product/graph/waivers/2026-09-17-ollama-tool-require.md
@@ -0,0 +1,5 @@
+# Waiver: Ollama tool require and pre-brief SaveModel
+
+**Reason:** Campaign Agent Ollama now requires a function call from the filtered tool set, and `SaveModel` is legal before a campaign design brief. No new capability, ontology term, or graph edge. Campaign persist still uses a real brief.
+
+**Nodes touched by path-map (no meaning change):** `campaign-agent`, `campaigns`.
diff --git a/Journeys/docs/developer/campaign-agent-llm.md b/Journeys/docs/developer/campaign-agent-llm.md
new file mode 100644
index 0000000..ba9c71d
--- /dev/null
+++ b/Journeys/docs/developer/campaign-agent-llm.md
@@ -0,0 +1,28 @@
+# Campaign Agent LLM providers
+
+`Journeys.API` Campaign Agent selects the LLM at **startup** from `CampaignAgent:Provider` (or `CAMPAIGN_AGENT_PROVIDER`). Restart the API to change provider. There is no hot reload.
+
+**Specs:** `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md`, `docs/specs/2026-09-17-Journeys-campaign-agent-ollama-tool-require-design.md`
+
+## Values
+
+| Config | Client |
+|--------|--------|
+| unset / `Anthropic` | `Backend.Llm.Anthropic` (`AnthropicLlmChatClientFactory`). Requires `CampaignAgent:AnthropicApiKey` / `ANTHROPIC_API_KEY`. |
+| `Ollama` or `OpenAICompatible` | `Journeys.Infra.Llm` (`OpenAICompatibleLlmChatClientFactory`). No Anthropic key. |
+
+Do not HintPath `Backend.Llm.OpenAICompatible`. `Journeys.Agent` is not switched in this spec.
+
+## Development (MSI Ollama)
+
+`appsettings.Development.json` points at `http://192.168.1.191:11434/v1`, model `llama3.1:8b`. Leave the Ollama tray running on the MSI. Connect retry: 60s, 2s apart.
+
+## Live smoke
+
+Anonymous-dev Campaign Agent SSE is already allowed in Development. UX at `/loyalty/campaigns/agent`. Conversation rows need the `AgentMessage` Backend model (`docs/developer/journeys-ux.md`).
+
+1. New thread on `TestTenant1`: one turn must produce at least one **successful** MCP tool (`list_campaigns`, `list_models` / `GetModel`, or `SaveModel`). Assistant-only text is a fail. Ollama turns (except phase `Done`) send `ChatToolMode.RequireAny` so the model cannot skip tools.
+2. Follow-up, **no** campaign brief: persist a Review loyalty model (`id`, `userid`, `comments`, `NumStars`, `reviewdate`) via `SaveModel`. Confirm it in Backend. `UpsertCampaign` stays unavailable until a real brief (`ProposeCampaignDesignBrief` / warehouse).
+3. If step 1 fails on `llama3.1:8b`, set `CampaignAgent:OpenAICompatible:Model` to a tool-capable tag already on the MSI tray, **restart** `Journeys.API`, retry 1 then 2. Provider and model are startup-only.
+
+Not required: Draft campaign upsert, Live publish, CI Ollama.

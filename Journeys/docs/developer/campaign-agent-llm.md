# Campaign Agent LLM providers

`Journeys.API` Campaign Agent selects the LLM at **startup** from `CampaignAgent:Provider` (or `CAMPAIGN_AGENT_PROVIDER`). Restart the API to change provider. There is no hot reload.

**Specs:** `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md`, `docs/specs/2026-09-17-Journeys-campaign-agent-ollama-tool-require-design.md`

## Values

| Config | Client |
|--------|--------|
| unset / `Anthropic` | `Backend.Llm.Anthropic` (`AnthropicLlmChatClientFactory`). Requires `CampaignAgent:AnthropicApiKey` / `ANTHROPIC_API_KEY`. |
| `Ollama` or `OpenAICompatible` | `Journeys.Infra.Llm` (`OpenAICompatibleLlmChatClientFactory`). No Anthropic key. |

Do not HintPath `Backend.Llm.OpenAICompatible`. `Journeys.Agent` is not switched in this spec.

## Development (MSI Ollama)

`appsettings.Development.json` points at `http://192.168.1.191:11434/v1`, model `llama3.1:8b`. Leave the Ollama tray running on the MSI. Connect retry: 60s, 2s apart.

## Live smoke

Anonymous-dev Campaign Agent SSE is already allowed in Development. UX hosts the unlabeled agent rail (New builder, `/loyalty/campaigns/agent`, Live `/loyalty/campaigns/[id]/agent`) and hydrates the Journey Builder from SSE-discovered campaign ids; LLM provider selection is unchanged (this file). Conversation rows need the `AgentMessage` Backend model (`docs/developer/journeys-ux.md`).

1. New thread on `TestTenant1`: one turn must produce at least one **successful** MCP tool (`list_campaigns`, `list_models` / `GetModel`, or `SaveModel`). Assistant-only text is a fail. Ollama turns (except phase `Done`) send `ChatToolMode.RequireAny` so the model cannot skip tools.
2. Follow-up, **no** campaign brief: persist a Review loyalty model (`id`, `userid`, `comments`, `NumStars`, `reviewdate`) via `SaveModel`. Confirm it in Backend. `UpsertCampaign` stays unavailable until a real brief (`ProposeCampaignDesignBrief` / warehouse).
3. If step 1 fails on `llama3.1:8b`, set `CampaignAgent:OpenAICompatible:Model` to a tool-capable tag already on the MSI tray, **restart** `Journeys.API`, retry 1 then 2. Provider and model are startup-only.

Not required: Draft campaign upsert, Live publish, CI Ollama.

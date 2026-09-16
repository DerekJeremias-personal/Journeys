# Design: Campaign Agent Ollama provider (`Journeys.Infra.Llm`)

**Date:** 2026-09-16  
**Status:** Approved (human 2026-09-16)  
**Scope:** Startup LLM provider switch for `Journeys.API` Campaign Agent. Anthropic remains the default. `Ollama` / `OpenAICompatible` uses a **Journeys-owned** OpenAI-compatible leaf (analog of `C:\Dev\Backend\Backend.Llm.OpenAICompatible`, not that DLL). Quality diet so `llama3.1:8b` on the MSI Ollama tray can complete one streamed turn with a successful MCP tool call.  
**Depends on:** existing Campaign Agent orchestrator, `ILlmChatClientFactory` / `ILlmPromptChatMapper` in `Backend.Core`, `Backend.Llm.Anthropic` HintPath, MSI Ollama (Development). Does not depend on EXP admin-web, `Journeys.UX`, or `Journeys.Agent`.  
**Placement authority:** `docs/product/` and `docs/platform/overlays.md`.  
**If this spec and onion rules disagree:** LLM HTTP stays in Infra. Controllers stay free of provider selection. No new capability ids.  
**If this spec and `backend-dll-external` disagree:** Anthropic DLL stays named and HintPath-referenced. This spec does **not** HintPath `Backend.Llm.OpenAICompatible`.

This is the API spec the UX loyalty-shell design deferred (`Ollama ILlmChatClientFactory`). It does **not** add Campaign Agent UI.

---

## 1. Problem and goals

Campaign Agent in `Journeys.API` is hard-wired to `AnthropicLlmChatClientFactory` and `AnthropicLlmPromptChatMapper`. Local development on the MSI uses Ollama for Backend’s model agent (`BackendAgent:Provider` = `Ollama`). Journeys still requires an Anthropic key and Claude. Claude prompt-cache shaping must not be sent to Ollama. Dumping the full Backend MCP tool surface plus large history is too much for `llama3.1:8b`.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Provider switch** | `CampaignAgent:Provider` at **startup** (restart to change). Values: unset/empty/`Anthropic` → Claude; `Ollama` or `OpenAICompatible` → OpenAI-compatible HTTP. Unknown value throws listing allowed values |
| G2 | **Journeys-owned Ollama leaf** | New `Journeys.Infra.Llm` implements `ILlmChatClientFactory`. No HintPath to `Backend.Llm.OpenAICompatible.dll`. Anthropic path still uses `Backend.Llm.Anthropic` |
| G3 | **No Anthropic key on Ollama** | When provider is Ollama, DI does not construct `AnthropicLlmChatClientFactory`; `AnthropicApiKey` is not required |
| G4 | **MSI Development target** | `appsettings.Development.json` matches Backend’s Ollama block: `http://192.168.1.191:11434/v1`, model `llama3.1:8b`, `NumCtx` 16384, `ReasoningEffort` `off`, `RequestTimeoutSeconds` 1800 |
| G5 | **8B diet** | Ollama addendum + Dev history/`MaxOutputTokens` caps + `ExposeFullBackendMcpToolSurface: false` on that path. Existing workflow composer, phase files, digests, and `CampaignWorkflowToolFilter` stay |
| G6 | **Live turn** | Human: one Campaign Agent SSE turn against running MSI Ollama produces assistant text **and** at least one **successful** MCP tool call. Not required: Draft upsert, Live publish, SaveModel, or CI live Ollama |

### Non-goals (this spec)

- `Journeys.Agent` (`AnthropicClient` in `Journeys.Agent/Program.cs`)
- Campaign Agent / Journey Builder UI in `Journeys.UX`
- HintPath or project-reference `Backend.Llm.OpenAICompatible`
- Forking every governance `.txt` for Ollama
- Per-tenant Auth0 `"hayward"` changes
- New capability ids (`campaign-agent` already exists)
- Automated tests that call live Ollama
- Hot-reload of `Provider` without process restart

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Switch shape | Same as Backend `BackendAgent:Provider`, config section `CampaignAgent` |
| Leaf | Journeys analog of `Backend.Llm.OpenAICompatible` in `Journeys.Infra.Llm` (approach 2) |
| Hosts | `Journeys.API` only |
| Dev endpoint | Copy Backend Development Ollama block (MSI LAN IP) |
| Production default | `appsettings.json` omits `Provider` → Anthropic |
| Mapper | Anthropic → `AnthropicLlmPromptChatMapper`; Ollama → `PassthroughLlmPromptChatMapper` (`Backend.Core`) |
| Quality | Addendum + Dev caps + close full Backend MCP surface; keep digests and phase filter |
| Live bar | One turn + one successful MCP tool (typical `list_campaigns` / digest) |
| Connect retry | Keep retrying connect/refused/reset/DNS until cancel or `ConnectRetrySeconds` (Dev default 60), 2s apart |
| Commit of this spec | Human only |

---

## 3. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.Infra.Llm\          # new csproj; analog of Backend.Llm.OpenAICompatible
  Journeys.API\Program.cs      # DI branch on CampaignAgent:Provider
  Journeys.API\CampaignAgent\  # Ollama addendum; composer branch
  Journeys.API\appsettings*.json
  Journeys.sln                 # include Journeys.Infra.Llm
```

`Journeys.Infra.Llm` HintPaths `Backend.Core` (for `ILlmChatClientFactory` / `IChatClient` contracts only). It must not reference Core campaign services or `Backend.Llm.OpenAICompatible`.

SDK packages (same family as Backend’s leaf): `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, OpenAI client transport as required to talk to Ollama `/v1` with function invocation.

---

## 4. Provider and DI

Parse `CampaignAgent:Provider` then env `CAMPAIGN_AGENT_PROVIDER` (env wins if config is blank). Journeys-owned parser `CampaignAgentLlmProvider` (do not throw “Backend Agent” in the message).

| Value | Kind |
|-------|------|
| null, empty, whitespace, `Anthropic` | Anthropic |
| `OpenAICompatible`, `Ollama` | OpenAI-compatible |
| anything else | `InvalidOperationException` listing Anthropic, OpenAICompatible, Ollama |

**Anthropic branch (existing):** `AnthropicLlmChatClientFactory(configuration, "CampaignAgent", ...)`, `ILlmChatClientFactory` → that instance, `AnthropicLlmPromptChatMapper`. Anthropic key still required by that factory.

**Ollama branch:** `OpenAICompatibleLlmChatClientFactory` in `Journeys.Infra.Llm` with section `"CampaignAgent"`, `ILlmChatClientFactory` → that instance, `PassthroughLlmPromptChatMapper`. Do not register the Anthropic factory.

---

## 5. OpenAI-compatible factory (Journeys analog)

Behavior to match Backend’s leaf (reimplement, do not copy the assembly):

| Key | Default if blank |
|-----|------------------|
| `CampaignAgent:OpenAICompatible:BaseUrl` | `http://localhost:11434/v1` |
| `CampaignAgent:OpenAICompatible:ApiKey` | `ollama` |
| `CampaignAgent:OpenAICompatible:Model` | **required** — throw if missing after env fallback |
| `NumCtx` | omit if unset; allowed 2048–128000 |
| `ReasoningEffort` | omit if unset; `off` \| `low` \| `medium` \| `high` |
| `RequestTimeoutSeconds` | 900 if omitted (15 min idle); Dev sets 1800. `0` = infinite |
| `ConnectRetrySeconds` | 0 if omitted (no retry). Dev sets **60** |

Env fallbacks (config key wins when non-blank):

| Variable | Maps to |
|----------|---------|
| `CAMPAIGN_AGENT_PROVIDER` | `CampaignAgent:Provider` |
| `CAMPAIGN_AGENT_OPENAI_BASE_URL` | BaseUrl |
| `CAMPAIGN_AGENT_OPENAI_API_KEY` | ApiKey |
| `CAMPAIGN_AGENT_OPENAI_MODEL` | Model |
| `CAMPAIGN_AGENT_OPENAI_NUM_CTX` | NumCtx |
| `CAMPAIGN_AGENT_OPENAI_REASONING_EFFORT` | ReasoningEffort |
| `CAMPAIGN_AGENT_OPENAI_REQUEST_TIMEOUT_SECONDS` | RequestTimeoutSeconds |
| `CAMPAIGN_AGENT_OPENAI_CONNECT_RETRY_SECONDS` | ConnectRetrySeconds |

`CreateChatClient()` returns `IChatClient` with function invocation enabled. Apply `num_ctx` / think options on the Ollama chat-completions body when those knobs are set (same role as Backend `OllamaChatCompletionsOptionsHandler`). Do not log ApiKey.

---

## 6. Connect retry

Wrap the Ollama `IChatClient` so the **first** HTTP that has not yet received tokens retries on connect / connection-refused / reset / DNS until `CancellationToken` or `ConnectRetrySeconds` elapses, waiting **2 seconds** between attempts. Then SSE `error`: Ollama unreachable at `{BaseUrl}` after retry window. No key in the message.

Do **not** retry: HTTP 4xx (wrong model name), `InvalidOperationException` from config, or a stream that already emitted tokens.

Anthropic branch: no connect-retry wrapper.

---

## 7. Prompt and tool diet (Ollama path only)

Keep `SystemPrompt.txt` and all phase/governance files unchanged for Claude.

Add `Journeys.API/CampaignAgent/SystemPrompt.Ollama.txt`. `CampaignAgentPromptComposer` appends it when the resolved provider is OpenAI-compatible. Exact body:

```
OLLAMA / SMALL MODEL:
- One tool call per turn. Prefer digest tools over raw payloads.
- Do not paste full JSON catalogs or event payloads into the reply.
- If the campaign brief is not captured yet, start with a read (list_campaigns or model list).
```

Development `CampaignAgent` when Provider is Ollama:

| Key | Value |
|-----|--------|
| `ExposeFullBackendMcpToolSurface` | `false` |
| `AllowMultipleToolCallsPerModelResponse` | `false` |
| `MaxOutputTokens` | `2048` |
| `HistoryMaxChars` | `40000` |
| `HistoryMaxUserTurns` | `12` |
| `MaxPersistedToolResultChars` | `4000` |

Existing `CampaignWorkflowToolFilter` and Journeys MCP digest wrappers stay on. Do not disable digests.

Production `appsettings.json` keeps current Anthropic-oriented budgets and does not set the Ollama block.

---

## 8. Development Ollama block (commit)

Under `CampaignAgent` in `appsettings.Development.json` (same MSI target as Backend Development):

```json
"Provider": "Ollama",
"ExposeFullBackendMcpToolSurface": false,
"AllowMultipleToolCallsPerModelResponse": false,
"MaxOutputTokens": 2048,
"HistoryMaxChars": 40000,
"HistoryMaxUserTurns": 12,
"MaxPersistedToolResultChars": 4000,
"OpenAICompatible": {
  "BaseUrl": "http://192.168.1.191:11434/v1",
  "ApiKey": "ollama",
  "Model": "llama3.1:8b",
  "NumCtx": 16384,
  "ReasoningEffort": "off",
  "RequestTimeoutSeconds": 1800,
  "ConnectRetrySeconds": 60
}
```

`ApiKey` value `ollama` is the local placeholder, not a production secret.

---

## 9. Errors and logging

- Startup: unknown provider / missing Ollama model / invalid NumCtx or ReasoningEffort → process fails to start with a clear exception.
- Runtime unreachable after retry window → one SSE error (G6 human run may still fail if the tray is down for >60s).
- Never log tokens, API keys, connection strings, or full tool payloads (`docs/developer/logging.md`).

---

## 10. Tests

`Journeys.Tests`, **no live Ollama**:

- `CampaignAgentLlmProvider` parse: default/Anthropic, `Ollama`/`OpenAICompatible` aliases, unknown throws with allowed names.
- Factory: missing model throws; blank BaseUrl/ApiKey get defaults; invalid NumCtx/ReasoningEffort throw.
- Composer: Ollama addendum present only when provider is OpenAI-compatible.
- Options/Dev: `ExposeFullBackendMcpToolSurface` is false in the committed Development CampaignAgent section used for Ollama.
- Existing Campaign Agent tests remain green (Anthropic factory still constructible in tests that need it).

Connect-retry: unit-test a fake handler (connection-fail then success within window; 4xx not retried). Do not hit `192.168.1.191` from CI.

---

## 11. Live smoke (human)

1. MSI Ollama tray running; `llama3.1:8b` pulled (`GET http://192.168.1.191:11434/api/tags` → 200).
2. `Journeys.API` Development, restarted after Provider change.
3. Campaign Agent SSE (anonymous-dev is already allowed in Development) with a prompt that requires a list read.
4. Pass: streamed assistant text **and** one successful MCP tool (e.g. `list_campaigns` / digest). Fail: no tool, only tool errors, or hang past idle timeout.

---

## 12. Docs and graph

| File | Change |
|------|--------|
| `docs/developer/campaign-agent-llm.md` | New: Provider switch, MSI block, restart, live smoke, Anthropic vs Ollama DI |
| `docs/developer/local-ops.md` | Subsection pointing at that page |
| `docs/developer/index.md` | Link |
| `docs/platform/overlays.md` | Adapters cell includes `Journeys.Infra.Llm` (HTTP to Ollama `/v1`; not a Backend LLM DLL) |
| `docs/platform/architecture.md` | Adapters / Infra: `Journeys.Infra.Llm`. Backend DLLs sentence still lists `Backend.Llm.Anthropic.dll` only for LLM |
| `docs/product/graph/path-map.yaml` | prefix `Journeys.Infra.Llm` → `[campaign-agent]`, `meaningOptional: false` |
| `scripts/path-docs-map.yaml` | prefix `Journeys.Infra.Llm` → `docs/developer/campaign-agent-llm.md`, architecture, overlays |
| `Journeys.sln` | Add `Journeys.Infra.Llm` |

No new capability node. `campaign-agent` remains `IMPLEMENTED_AS` `proj-api` (and existing `proj-agent`). Do not add `campaigns` `IMPLEMENTED_AS` a new LLM project.

---

## 13. Spec coverage (self-review)

| Spec | Where |
|------|--------|
| G1 provider parse + restart | §4 |
| G2 Journeys.Infra.Llm analog | §3, §5 |
| G3 no Anthropic key on Ollama | §4 |
| G4 MSI Dev block | §8 |
| G5 diet | §7 |
| G6 live human gate | §11 |
| Connect retry | §6 |
| Tests without live Ollama | §10 |
| Graph/docs | §12 |
| Non-goals (Agent, UX, Backend OpenAICompatible DLL) | §1 |

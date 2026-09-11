# Journeys.Agent

Console app: **Claude (Anthropic)** with tools from **two MCP servers**:

1. **Journeys.API** (`…/7001/mcp`) — campaigns, journeys, accounts, **ProcessEvent**, ingestion, etc.
2. **BackEnd.Web** (`…/7155/mcp`, optional) — **dynamic model** tools (**ListModels**, **GetModel**, **SaveModel**, …) aligned with **Backend.Agent** filtering.

Uses two `McpClient` connections, merges tool lists, and routes tool execution to the correct host. Retries, gated mutating tools, multi-turn history, structured logging — same shell patterns as **Backend.Agent**.

## Prerequisites

- .NET 8 SDK
- **Journeys.API** running (Journeys MCP)
- **BackEnd.Web** running when **BackendMcpServerUrl** is set (Backend MCP)
- **ANTHROPIC_API_KEY** in environment, user-secrets, or (local only) appsettings

Do **not** commit API keys. Prefer:

```bash
cd Journeys\Journeys.Agent
dotnet user-secrets set ANTHROPIC_API_KEY "your-key"
```

## Configuration

### MCP URLs

| Key | Env fallback | Description |
|-----|----------------|-------------|
| **JourneysMcpServerUrl** | **Journeys_MCP_SERVER_URL**, **Journeys__McpServerUrl** | **Required.** Journeys API MCP, e.g. `https://localhost:7001/mcp`. |
| **BackendMcpServerUrl** | **BACKEND_MCP_SERVER_URL**, **Backend__McpServerUrl** | **Optional.** If set (non-empty), the agent also connects to BackEnd.Web MCP, e.g. `https://localhost:7155/mcp`. If omitted or empty, **Journeys only** (previous behavior). |
| **AllowSaveModel** | — | **`false`** by default in `appsettings.json`. Set **`true`** (e.g. in `appsettings.Development.json`) to expose **`SaveModel`** / **`save_model`** from Backend MCP. Without it, only read/model-list tools are available. |

### TLS (localhost HTTPS)

| Key | Description |
|-----|-------------|
| **BypassJourneysMcpServerCertificateValidation** | `true` if Journeys HTTPS dev cert is untrusted. |
| **BypassBackendMcpServerCertificateValidation** | Same for BackEnd.Web MCP. |

### Journeys tool gates

| Key | Default | Description |
|-----|---------|---------------|
| **AllowProcessEvent** | `false` | `true` to expose **ProcessEvent** (sandbox/state mutation). |
| **AllowMoveTier** | `false` | `true` to expose **MoveTier**. |

### Backend model gates (same semantics as Backend.Agent)

| Key | Default | Description |
|-----|---------|---------------|
| **AllowSaveModel** | `true` in repo `appsettings.json` | When **`false`**, **SaveModel** / **save_model** are removed before Claude sees tools. Env vars override JSON. |
| **AllowSetEntity** | `false` | Entity tools not in default Backend subset; gate kept for parity. |
| **AllowMoveEntity** | `false` | Same. |
| **AllowSaveRootTaxonomy** | on if omitted | Taxonomy mutating tools are **not** in the default Backend tool subset; gates kept for parity. |
| **AllowSaveTaxonomy** | on if omitted | Same. |
| **AllowBulkUpsertTaxonomies** | on if omitted | Same. |

### Allowlists

| Key | Description |
|-----|-------------|
| **JourneysToolAllowlist** | If non-empty, **only these names** are kept from the **Journeys** server list (Backend tools are **not** filtered by this). |
| **BackendToolAllowlist** | If non-empty, **only these names** are kept from the **Backend** server list (after model-subset + mutating gates). |

### Other

| Key | Default | Description |
|-----|---------|-------------|
| **ClaudeModel** | `claude-sonnet-4-20250514` | Override with env **CLAUDE_MODEL**. |
| **ANTHROPIC_API_KEY** | empty | Unset → lists merged tools and exits **0**. |
| **McpConnectMaxAttempts** | `30` | Per-connection retry budget (Journeys and Backend each use the same values). |
| **McpConnectRetryDelaySeconds** | `5` | Delay between connect retries. |
| **McpConnectInitialDelaySeconds** | `0` | Wait before **first** connect only. |

Environment variables override JSON.

**Shell parity** with **Backend.Agent**: `docs/superpowers/checklists/console-agents-shell-parity.md`

### Backend tool subset

When **BackendMcpServerUrl** is set, the agent keeps the same **model-catalog** tools as **Backend.Agent**:  
**ListModels**, **GetAllModels**, **get_all_models**, **GetModel**, **GetManyModels**, **GetModelAttributesForRules**, **BuildTaxonomicRule**, **build_taxonomic_rule**, **SaveModel**, **DeleteModel**, **ListExampleModels**, **GetExampleModel**.  
Entity/taxonomy MCP tools are **not** included unless you extend `FilterBackendTools` in **Program.cs**.

## Run

1. Start **Journeys.API** and, if using dual MCP, **BackEnd.Web**.
2. Set **ANTHROPIC_API_KEY** for interactive chat (optional for tool-list-only check).
3. `dotnet run --project path/to/Journeys.Agent`

- **`reset`** — clear conversation history.
- **`exit`** or empty line — quit.

## Architecture

- **Claude** via `IChatClient` + `UseFunctionInvocation()`.
- **Two MCP sessions** when **BackendMcpServerUrl** is configured; merged **McpClientTool** list (duplicate names → fatal error).
- **Logging** via `Microsoft.Extensions.Logging`.
- System prompt: **AgentSystemPrompt.txt**; Journeys contract: `Journeys.API/Mcp/MCP-Contract.md`; Backend: `BackEnd.Web/Mcp/BACKEND-MCP-Contract.md`.

## Docs in repo

- `docs/superpowers/checklists/console-agents-shell-parity.md`
- `docs/Journeys-Agent-MCP-Final-Plan.md`, `docs/Journeys-Agent-Runbook.md` (if present)

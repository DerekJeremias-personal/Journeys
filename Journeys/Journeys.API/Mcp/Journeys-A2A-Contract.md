# Journeys A2A (Agent2Agent) contract

This API implements a subset of the [A2A protocol](https://a2a-protocol.org/latest/specification/) (JSON-RPC binding) alongside the existing MCP server at `/mcp`.

## Discovery

| Resource | URL |
|----------|-----|
| **Agent Card** | `GET /.well-known/agent-card.json` |

## JSON-RPC

| Item | Value |
|------|--------|
| Endpoint | `POST /a2a/rpc` |
| Content-Type | `application/json` |
| Version header | Optional `A2A-Version: 1.0` or `0.3` (omitted = accepted) |

### Supported methods

| Method | Description |
|--------|-------------|
| `SendMessage` | Run one MCP-equivalent tool; returns a `Task` (blocking until complete). |
| `GetTask` | Params: `{ "id": "<taskId>" }` |
| `CancelTask` | Params: `{ "id": "<taskId>" }` |
| `ListTasks` | Stub: returns empty list. |

Streaming, push notifications, and extended agent card are **not** implemented.

## Tool invocation (`SendMessage`)

Optional top-level **`tenant`** or **`tenantId`** inside **`arguments`**.

Message **part** `data`:

```json
{
  "tool": "<McpToolName>",
  "arguments": { }
}
```

Names match [Journeys-MCP-Contract.md](./Journeys-MCP-Contract.md): `GetCampaign`, `ListCampaigns`, `UpsertCampaign`, `DeleteCampaign`, `ProcessEvent`, `GetAccount`, `GetRulesEngineContractSummary`, etc.

**Tenant:** `GetRulesEngineContractSummary` ignores tenant data; you may still send `tenant` for consistency. Use `"arguments": {}`.

Example:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "SendMessage",
  "params": {
    "tenant": "your-tenant-id",
    "message": {
      "messageId": "550e8400-e29b-41d4-a716-446655440000",
      "role": "ROLE_USER",
      "parts": [
        {
          "data": {
            "tool": "ListCampaigns",
            "arguments": { "status": "Live", "pageSize": 20 }
          }
        }
      ]
    }
  }
}
```

## Execution model

Handlers delegate to `JourneysMcpTools` (same behavior as MCP). Task state is stored **in memory** per API process.

## Errors

JSON-RPC and A2A-specific codes as in the Backend A2A contract (`-32001`, `-32004`, `-32009`, etc.).

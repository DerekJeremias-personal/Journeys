# Design: Campaign Agent Ollama tool require + pre-brief SaveModel

**Date:** 2026-09-17  
**Status:** Approved (human 2026-09-17)  
**Scope:** Stop silent inaction on the `Journeys.API` Campaign Agent **Ollama** path: the model must call a tool from the already-filtered set, and `SaveModel` is legal before a campaign design brief. Anthropic is unchanged.  
**Depends on:** `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md` (provider switch, 8B diet, `Journeys.Infra.Llm`). Existing `CampaignWorkflowToolFilter`, coaches, orchestrator streaming.  
**Placement authority:** `docs/product/` and `docs/platform/overlays.md`.  
**If this spec and onion rules disagree:** Filter and require live in `Journeys.API/CampaignAgent`. Controllers stay free of tool policy. No new capability ids.  
**If this spec and the 2026-09-16 Ollama spec disagree:** This spec **extends** G6 (the live MCP call still never passed) and adds a SaveModel persist bar. It does not replace the provider leaf.

Evidence: conversation `39cadda345a640d3971f70e6b800f8cc` (`TestTenant1`). Four user turns, zero MCP calls. `llama3.1:8b` described OpenAPI (`get_model`, invented `save()`) instead of emitting `tool_calls`. Context budget was healthy. `SaveModel` was **filtered out** pre-brief; coaches that say “call save_model” only run after `CampaignDesignBriefProposed` is set.

---

## 1. Problem and goals

The 2026-09-16 diet (addendum + closed full Backend MCP surface + `AllowMultipleToolCallsPerModelResponse: false`) did not make 8B invoke tools. `ChatOptions` passes `Tools` with default auto tool choice. The model treats schemas as documentation.

A second problem: pre-brief `CampaignWorkflowToolFilter` strips mutating Backend tools, including `SaveModel`. A model-only persist cannot succeed until a campaign brief exists. That brief is useful **campaign-authoring context** (business value for `upsert_campaign`). It is not a prerequisite for saving a catalog model. This spec does **not** invent a synthetic brief to fake `IsBriefCaptured`.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **SaveModel pre-brief** | Pre-brief filter allows `SaveModel` / `save_model`. Still blocks `UpsertCampaign`, `UpsertPointAccountType`, `DeleteModel` |
| G2 | **Ollama require** | When provider is Ollama/OpenAICompatible, filtered tools are non-empty, and phase is not `Done`, `ChatOptions` requires at least one tool call (`ToolMode = RequireAny` or the MEAI/OpenAI equivalent that sets `tool_choice` to require a function). Anthropic does not set this |
| G3 | **Live G6** | Human: new thread, one SSE turn on MSI Ollama produces at least one **successful** MCP call (`list_campaigns`, `list_models`/`GetModel`, or `SaveModel`) |
| G4 | **Live SaveModel** | Human: follow-up persist of a Review (or equivalent) loyalty model via `SaveModel` with **no** campaign brief. Backend has the model. Campaign upserts remain unavailable until a real brief exists |

### Non-goals

- Synthetic or auto-filled `CampaignDesignBriefProposed`
- Extra streaming hop / retry segment when the model still returns only text
- User-utterance classifier (“persist”, “hello”)
- Changing `IsBriefCaptured` or Events-gate / PAT / journey flag logic except the pre-brief `SaveModel` allow
- `Journeys.Agent`, `Journeys.UX`, HintPath `Backend.Llm.OpenAICompatible`
- New capability ids
- Automated tests that call live Ollama
- Runtime model auto-switch (provider and model stay startup-only)

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Approach | Flag-driven **require** on Ollama only. Filter still picks the legal set |
| Brief | Real brief stays the campaign-quality input. Not required for `SaveModel`. No dummy stamp |
| Require predicate | `CampaignAgentLlmProvider` is OpenAI-compatible **and** `aiTools.Count > 0` **and** `workflowState.Phase != Done` |
| Fail-closed on 8B | If G3 still fails, change `CampaignAgent:OpenAICompatible:Model` and restart. Do not add more prompt text as the fix |
| Dev diet | Unchanged except G1 filter. Keep `AllowMultipleToolCallsPerModelResponse: false` |
| Hosts | `Journeys.API` Campaign Agent only |

---

## 3. Placement

```
Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs   # G1
Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs            # G2 ChatOptions
Journeys.API/CampaignAgent/OllamaToolRequire.cs                    # one predicate; orchestrator calls it; tests cover it directly
Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs    # invert SaveModel pre-brief assert
Journeys.Tests/CampaignAgent/…                                     # require + ChatOptions tests
docs/developer/campaign-agent-llm.md                               # G3/G4 live bar + model fallback
```

No new csproj. No overlay change (`Journeys.Infra.Llm` already listed).

---

## 4. Filter (G1)

`IsPreBriefTool`: treat `SaveModel` / `save_model` as allowed (mutating Backend exception). Do not allow `DeleteModel` / `delete_model`.

Post-brief behavior unchanged: Events gate still blocks campaign mutators until `EventModelsReadiness` is ready; PAT and journey filters unchanged.

`CampaignWorkflowChecklist.IsBriefCaptured` stays “Proposed is non-empty.” Warehouse / `ProposeCampaignDesignBrief` remain how a **real** brief is captured. Campaign persist still wants that brief.

---

## 5. Require (G2)

After `CampaignWorkflowToolFilter.Apply`, when building `ChatOptions`:

- Ollama path + non-empty tools + phase ≠ `Done` → require one function call from that list (`OllamaToolRequire.ShouldRequire`).
- Otherwise leave tool choice default (Anthropic auto; Ollama auto when `Done` or no tools).

Set Microsoft.Extensions.AI `ChatOptions.ToolMode` to `ChatToolMode.RequireAny` when the helper is true. If the pinned MEAI 9.9 package does not expose `ToolMode`, the first ChatOptions unit test fails closed and implementation uses the OpenAI-compatible `tool_choice` equivalent (required function) without changing `OllamaChatCompletionsOptionsHandler` (`think` / `num_ctx` only).

`AllowMultipleToolCalls` stays the existing config (false in Development).

Do not parse the user message. Coaches and turn-start directives stay as prompt; require is what 8B cannot ignore.

---

## 6. Errors

| Case | Behavior |
|------|----------|
| `SaveModel` tool error | Existing remediation / persist failure. No brief stamp |
| Require set, model still text-only | Persist the assistant message as today. Turn ends. G3 fails → human changes model tag |
| Ollama down | Existing connect-retry then SSE error |
| Logging | Never log API keys or full tool payloads (`docs/developer/logging.md`) |

---

## 7. Tests (no live Ollama)

- Pre-brief: `SaveModel` present; `UpsertCampaign`, `UpsertPointAccountType`, `DeleteModel` absent. Warehouse-disabled pre-brief still allows `ProposeCampaignDesignBrief` and `GetModel`.
- Post-brief, Events incomplete: campaign mutators still blocked (`SaveModel` still allowed). Existing tests stay green aside from the inverted pre-brief `SaveModel` assert.
- Require helper (or orchestrator-visible options): true for Ollama + tools + not `Done`; false for Anthropic; false when tools empty; false when `Done`.
- No test hits MSI Ollama.

---

## 8. Live bar (human)

1. New conversation on `TestTenant1` against running `Journeys.API` (Provider Ollama) and MSI tray `llama3.1:8b`. One turn → at least one successful MCP tool in API logs / `AgentMessage` tool rows.
2. Same or follow-up thread: persist Review (`id`, `userid`, `comments`, `NumStars`, `reviewdate`) via `SaveModel` without proposing a campaign brief. Confirm the model in Backend. Confirm `UpsertCampaign` was not in the filtered set (or was not called).
3. If (1) fails: set `CampaignAgent:OpenAICompatible:Model` to a tool-capable tag already pullable on that box, restart API, retry (1) then (2).

Not required: Draft campaign upsert, Live publish, CI Ollama.

---

## 9. Docs

Update `docs/developer/campaign-agent-llm.md` live-smoke: successful MCP is required; SaveModel persist is the second human check; model-tag fallback if 8B cannot emit `tool_calls`. Graph: same `campaign-agent` capability; no new nodes. Run `docs-impact` / `graph-impact` on the touched CampaignAgent paths (or a waiver if meaning is unchanged beyond the live-bar sentence).

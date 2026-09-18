# Task 1 Report: `OllamaToolRequire` (TDD)

**Date:** 2026-09-17  
**Plan:** `docs/plans/2026-09-17-Journeys-campaign-agent-ollama-tool-require.md` — Task 1 only  
**Status:** Implementation complete; shell blocked in subagent — parent must verify tests

## What I Implemented

Created `OllamaToolRequire`, a static helper in `Journeys.API/CampaignAgent` that decides when Ollama/OpenAI-compatible providers should force tool use and applies that to `ChatOptions`.

### `ShouldRequire(provider, filteredToolCount, phase)` → `bool`

Returns `true` only when all of the following hold:

- Provider is `CampaignAgentLlmProviderKind.OpenAICompatible` (Ollama alias path)
- `filteredToolCount > 0`
- Phase is not `CampaignWorkflowPhase.Done`

### `Apply(options, provider, phase)` → `void`

- Throws `ArgumentNullException` if `options` is null
- Uses `options.Tools?.Count ?? 0` as the tool count
- Sets `options.ToolMode = ChatToolMode.RequireAny` when `ShouldRequire` is true
- Leaves `ToolMode` unchanged otherwise

## What I Tested and Results

Added `OllamaToolRequireTests` with 8 test cases (5 facts + 3 theory inline cases):

| Test | Asserts |
|------|---------|
| `ShouldRequire_true_for_ollama_with_tools_and_not_done` | OpenAICompatible + 2 tools + DataAnalysis → true |
| `ShouldRequire_true_for_other_non_done_phases` | EventModels, CampaignBuild, Verification → true |
| `ShouldRequire_false_for_anthropic` | Anthropic + tools → false |
| `ShouldRequire_false_when_no_tools` | OpenAICompatible + 0 tools → false |
| `ShouldRequire_false_when_done` | OpenAICompatible + tools + Done → false |
| `Apply_sets_RequireAny_when_should_require` | ToolMode = RequireAny |
| `Apply_leaves_ToolMode_unset_for_anthropic` | ToolMode stays null |

**Execution:** Shell/dotnet was blocked by preToolUse hook in this subagent session. Tests were not executed here. Parent agent should run verification (see below).

## TDD Evidence

### RED (Step 2 — before implementation)

**Command:**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~OllamaToolRequireTests" -o $env:TEMP\journeys-test-out
```

**Expected failing output (compile error):**

```
error CS0103: The name 'OllamaToolRequire' does not exist in the current context
```

(or equivalent CS0246/CS0234 for missing type in `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs`)

Tests file was created first; implementation file did not exist at RED time.

### GREEN (Step 4 — after implementation)

**Command:** Same as RED.

**Expected passing output:**

```
Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8
```

**Actual in this session:** Not run (shell blocked). Code matches plan verbatim; no linter issues on touched files.

## Files Changed

| File | Action |
|------|--------|
| `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs` | Created — verbatim from task brief |
| `Journeys.API/CampaignAgent/OllamaToolRequire.cs` | Created — verbatim from task brief |

No orchestrator, tool filter, docs, or graph changes (out of scope for Task 1).

## Self-Review Findings

1. **TDD order:** Tests written before implementation; implementation is minimal and matches brief exactly.
2. **Logic:** Three guard clauses in `ShouldRequire` cover Anthropic, zero tools, and Done phase; `Apply` delegates to `ShouldRequire` with live tool count from `ChatOptions`.
3. **Conventions:** File-scoped namespaces, nullable enabled, static helper in API layer consistent with `CampaignAgentLlmProvider`.
4. **Dependencies:** Uses existing `CampaignAgentLlmProviderKind`, `CampaignWorkflowPhase`, and `Microsoft.Extensions.AI` (already on Journeys.API; tests get it via project reference).
5. **Scope:** No wiring into orchestrator yet (Task 3); helper is ready for consumption.
6. **Test helper:** `TestTool` extends `AITool` with Name/Description overrides — same pattern as brief; consistent with MEAI test usage elsewhere in CampaignAgent tests.

## Issues or Concerns

- **Shell blocked:** Could not capture actual RED/GREEN terminal output in this subagent. Parent should run the filter command above and confirm 8 passed.
- **MSB3027:** If Journeys.API is running, use `-o $env:TEMP\journeys-test-out` as documented in the brief.

## Verification Command (for parent)

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~OllamaToolRequireTests" -o $env:TEMP\journeys-test-out
```

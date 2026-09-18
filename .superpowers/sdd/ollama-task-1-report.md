# Task 1 Report: `CampaignAgentLlmProvider` (TDD)

**Date:** 2026-09-16  
**Plan:** `docs/plans/2026-09-16-Journeys-campaign-agent-ollama.md` — Task 1 only  
**Status:** Complete (implementation + tests written; shell blocked in subagent — controller must verify)

## Summary

Implemented the Campaign Agent LLM provider switch parser per plan Task 1. TDD order followed: tests first, then implementation.

## Files Created

| File | Action |
|------|--------|
| `Journeys.Tests/CampaignAgent/CampaignAgentLlmProviderTests.cs` | Created — verbatim from plan |
| `Journeys.API/CampaignAgent/CampaignAgentLlmProvider.cs` | Created — verbatim from plan |

## Implementation Details

### Types

- `CampaignAgentLlmProviderKind` enum: `Anthropic`, `OpenAICompatible`
- `CampaignAgentLlmProvider` static class with:
  - `Resolve(IConfiguration configuration)` — reads `CampaignAgent:Provider`, falls back to `CAMPAIGN_AGENT_PROVIDER`
  - `Parse(string? value)` — case-insensitive parsing with Ollama alias

### Behavior

| Input | Result |
|-------|--------|
| null / empty / whitespace | `Anthropic` (default) |
| `Anthropic` / `anthropic` | `Anthropic` |
| `OpenAICompatible` / `openaiCompatible` | `OpenAICompatible` |
| `Ollama` / `ollama` | `OpenAICompatible` |
| Unknown (e.g. `AzureOpenAI`) | `InvalidOperationException` with "Campaign Agent" message listing allowed values |

### Configuration Precedence

1. `CampaignAgent:Provider` (section key) wins when non-blank
2. `CAMPAIGN_AGENT_PROVIDER` (env key) used when section blank

## Test Coverage (13 test cases)

| Test | Cases |
|------|-------|
| `Parse_defaults_or_anthropic` | null, "", "  ", "Anthropic", "anthropic" → Anthropic |
| `Parse_openai_compatible_aliases` | OpenAICompatible, openaiCompatible, Ollama, ollama → OpenAICompatible |
| `Parse_unknown_throws_campaign_agent_message` | AzureOpenAI throws; message contains Campaign Agent, Anthropic, OpenAICompatible, Ollama; does NOT contain Backend Agent |
| `Resolve_uses_section_then_env_key_when_section_blank` | env-only Ollama → OpenAICompatible |
| `Resolve_section_wins_when_set` | section Anthropic + env Ollama → Anthropic |

## Verification Command

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~CampaignAgentLlmProviderTests
```

**Expected:** 13 passed, 0 failed.

**Subagent note:** Shell was blocked by preToolUse hook; tests were not executed in this session. Controller/parent agent should run the command above.

## Constraints Honored

- [x] TDD: tests written before implementation
- [x] Verbatim code from plan
- [x] net8, file-scoped namespaces, nullable enable
- [x] No HintPath to Backend.Llm.OpenAICompatible
- [x] No changes to Journeys.Agent or Journeys.UX
- [x] No git commit

## Out of Scope (Task 1)

- `Journeys.Infra.Llm` project (Task 2)
- DI extension / Program.cs branch (Task 4)
- Ollama addendum / Development.json (Task 5)
- Docs / graph (Task 6)

## Concerns

None for Task 1 scope. Implementation is a pure configuration parser with no external dependencies beyond `Microsoft.Extensions.Configuration` (already referenced via Journeys.API).

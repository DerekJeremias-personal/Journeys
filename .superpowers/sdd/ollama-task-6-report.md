# Task 6 report — Docs, graph, overlays

## Done

- Created `docs/developer/campaign-agent-llm.md` (verbatim from plan).
- `docs/developer/index.md` — Campaign Agent LLM bullet added.
- `docs/developer/local-ops.md` — `## Campaign Agent LLM` section added.
- `docs/platform/overlays.md` — Adapters cell includes `+ Journeys.Infra.Llm`.
- `docs/platform/architecture.md` — Infra.Llm sentence after C# arrow; Backend DLLs unchanged (Anthropic only); Host OpenAICompatible/FunctionInvoking sentence preserved.
- `docs/product/graph/path-map.yaml` — `Journeys.Infra.Llm` → `campaign-agent` at top of entries.
- `scripts/path-docs-map.yaml` — matching docs mapping at top of entries.

## Capability ids

Seven capabilities unchanged in `nodes.yaml`: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`. No new nodes. No `Backend.Llm.OpenAICompatible`.

## Not touched

`Journeys.Agent`, `Journeys.UX`. No git commit.

## Verify (Task 7)

Run `docs-impact.ps1` and `graph-impact.ps1` on the changed files; expect exit 0.

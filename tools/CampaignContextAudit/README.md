# CampaignContextAudit

Re-runnable analysis tool for the Campaign Agent LLM context-management audit.
See spec: `ELP/docs/superpowers/specs/2026-05-29-campaign-agent-context-management-assessment-design.md`.
Extended v2: `ELP/docs/superpowers/specs/2026-06-16-campaign-context-audit-retention-performance-design.md`.  
Cosmos loader: `ELP/docs/superpowers/specs/2026-06-16-campaign-context-audit-cosmos-loader-design.md`.  
Auto rubric: `ELP/docs/superpowers/specs/2026-06-17-campaign-context-audit-auto-rubric-design.md`.  
Delivery competence: `ELP/docs/superpowers/specs/2026-06-17-campaign-context-audit-delivery-competence-design.md`.  
Static governance audit: `ELP/docs/superpowers/specs/2026-06-19-campaign-agent-governance-static-audit-design.md`.

## What it does
Reconstructs, per conversation turn, what the production Campaign Agent feeds the LLM:
stable governance (sized from the live `.txt` files), budgeted history (exact port of
`CampaignAgentOrchestrator.ApplyTokenBudget`: 80,000 chars / 40 user turns, oldest user
segment dropped first), **SESSION estimate** (production prompt builders from workflow row +
budgeted thread context), outcome/drift findings from workflow snapshot + tool semantics,
and performance summaries from persisted metrics or `_ts` fallback. Emits a dated markdown
report with **creation delivery grade** (tiered milestones + abort/stall detection), per-turn budget table, detected findings, context rubric scorecard, and capped overall competency/efficiency verdicts.

## Re-run

### Static governance audit (no transcript)

Inventory loaded vs orphan governance `.txt` files, detect cross-layer duplication/contradictions, profile per-skill competency bands, and optionally boost findings from trace `*-findings.json`.

```bash
dotnet run --project ELP/tools/CampaignContextAudit -- \
  --static-governance \
  --governance ELP/Elevate.ELP/Elevate.ELP.API/CampaignAgent \
  --source-root ELP/Elevate.ELP \
  --out ELP/docs/assessments/campaign-agent-context \
  --json-out ELP/docs/assessments/campaign-agent-context/findings \
  --correlate-findings ELP/docs/assessments/campaign-agent-context/findings \
  --no-warehouse
```

| Flag | Description |
|------|-------------|
| `--static-governance` | Static corpus audit (mutually exclusive with `--transcripts` / `--conversation-id`) |
| `--source-root` | `Elevate.ELP` root for coach/registry/remediation extraction (defaults from `--governance`) |
| `--correlate-findings` | Directory of trace audit `*-findings.json` (schema v3) for hybrid severity boost |

Output: `{date}-governance-static-audit.md` and optional `{date}-governance-static-findings.json` (schema v1).

### Live Cosmos (no JSON export)

Requires `KeyValueStorage` in `Elevate.ELP.API/appsettings.Development.json` and/or user secrets (same as running the API locally). The audit tool loads that project's config with `appsettings.Development.json` applied **after** environment variables, so `BaseUrl: https://localhost:7155` in Development wins over a shell `KeyValueStorage__BaseUrl` export (matching Visual Studio F5). User secrets still override Development when set.

```bash
dotnet run --project ELP/tools/CampaignContextAudit -- \
  --tenant primo \
  --conversation-id 4073a72a920543959bddef2b10900b5b \
  --governance ELP/Elevate.ELP/Elevate.ELP.API/CampaignAgent \
  --out ELP/docs/assessments/campaign-agent-context
```

Repeat `--conversation-id` to audit multiple threads in one run. Mutually exclusive with `--transcripts`.

### Offline JSON export

1. Drop conversation transcripts (each a JSON array of `AgentMessage` Cosmos docs) into
   `ELP/AgentMessageSamples/` as `*.json`.
2. Run:
   ```bash
   dotnet run --project ELP/tools/CampaignContextAudit -- \
     --transcripts ELP/AgentMessageSamples \
     --governance ELP/Elevate.ELP/Elevate.ELP.API/CampaignAgent \
     --out ELP/docs/assessments/campaign-agent-context
   ```
3. A new dated report is written per transcript with **automated rubric grades and overall verdict** (no manual grading step).

## CLI flags

| Flag | Description |
|------|-------------|
| `--transcripts <dir>` | Folder of `*.json` transcript exports (**required for file mode**; mutually exclusive with `--conversation-id`) |
| `--tenant <id>` | Tenant id (**required for Cosmos mode**) |
| `--conversation-id <id>` | Conversation thread id; repeatable (**Cosmos mode**) |
| `--governance <dir>` | Live Campaign Agent governance `.txt` files (required) |
| `--out <dir>` | Output folder for markdown reports (required) |
| `--json-out <dir>` | Also write `{date}-{name}-findings.json` per transcript |
| `--baseline <findings.json>` | Diff current run against a prior `findings.json`; writes `{name}-diff-report.md` to `--out` |
| `--tenant-slice-file <path>` | Optional tenant curated text included in SESSION estimates |
| `--slow-tool-ms <ms>` | Threshold for `SLOW_TOOL` finding (default `10000`) |
| `--stall-ms <ms>` | Threshold for stall detection and `LONG_SINGLE_TURN` (default `120000`) |
| `--abort-ms <ms>` | Threshold for `CREATION_ABORTED` and delivery F (default `300000`) |
| `--phase <Phase>` | Force every turn to one workflow phase (otherwise inferred) |
| `--no-warehouse` | Size DataAnalysis governance without warehouse tools |

## Exit codes

| Code | Meaning |
|------|---------|
| 2 | Usage / invalid flag combination |
| 3 | Governance or transcripts dir not found |
| 4 | No `*.json` transcripts in folder |
| 5 | Baseline JSON not found |
| 6 | Backend config missing (`KeyValueStorage`) |
| 7 | Conversation not found in Cosmos |

## Report sections

- **Outcome summary** — workflow snapshot fields (`creationComplete`, `journeyRuleSetCount`, `workflowPhase`, PAT counts, `linkedCampaignId`) and blocking finding codes.
- **Creation delivery** — headline delivery grade (A–F), milestone checklist, expected minimum vs highest reached, effective wall time. See spec `docs/superpowers/specs/2026-06-17-campaign-context-audit-delivery-competence-design.md`.
- **Per-turn context budget** — columns include `SESSION (est.)` and `Total (est.)` (`Stable + History + SESSION`). Token counts remain ~chars/4 estimates.
- **Performance summary** — wall time by category (`llm`, `tools`, `persist`, `other`) and `dataSource` (`persisted`, `partial_persisted`, or `_ts_inferred`).
- **Detected findings** — competency/efficiency heuristics plus outcome codes (e.g. `CREATION_INCOMPLETE_AT_DONE`).
- **Rubric scorecard** — 8 dimensions graded A–F (+/-) with cited rationales; overall competency (capped when delivery is F/D) and efficiency verdicts. See spec `docs/superpowers/specs/2026-06-17-campaign-context-audit-auto-rubric-design.md`.

## findings.json

When `--json-out` is set, each transcript emits schema **v3** JSON with:
`delivery`, `outcomeSummary`, `findings`, `budgetByTurn`, `rubric`, `overallCompetency` (with optional uncapped grade), `overallEfficiency`, and optional `performanceSummary`.
Use with `--baseline` for CI regression on finding codes and outcome fields.

## Fidelity notes
- History budgeting and the phase→governance-file map are ports of production code; re-verify
  against `CampaignAgentOrchestrator.cs` and `CampaignWorkflowPhaseGovernanceFiles.cs` after any change there.
- SESSION is estimated via `CampaignWorkflowArtifactPromptBuilder` + `CampaignAgentThreadContextBuilder`
  from the workflow row and budgeted history; tenant curated slice is omitted unless `--tenant-slice-file` is supplied.
- Performance uses `turnMetricsJson` / `toolDurationMs` when present on exports; otherwise infers from Cosmos `_ts` (lower confidence).
- Pass `--no-warehouse` if the assessed deployment ran with data-warehouse tools disabled.

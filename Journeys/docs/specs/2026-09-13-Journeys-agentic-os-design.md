# Design: Journeys agentic construction OS

**Date:** 2026-09-13  
**Status:** Approved (workflow v1: brainstorm → `/aidlc classic` → stock reviewers → `agent-verify` → human merge)  
**Scope:** Fill required product/platform context (Phase 0), then install AWS AI-DLC as the Cursor execution kernel for this repo (Phase 1).  
**Depends on:** [2026-09-08 agentic SDLC information architecture](2026-09-08-Journeys-agentic-sdlc-design.md) (semantic plane already in tree).  
**Placement authority:** same LinkedIn articles as the 2026-09-08 spec. AI-DLC is the gated execution engine; it is not a second product graph.  
**If this spec and AI-DLC disagree on product meaning:** this repo’s `docs/product/` wins.  
**If this spec and AI-DLC disagree on stage sequence / gates:** AI-DLC wins, unless `AGENTS.md` human-only rules apply (merge, auth, ledger, tenant isolation).

---

## 1. Problem and goals

The 2026-09-08 work gave agents a door (`AGENTS.md`), product labels, and `agent-verify`. That is a briefing, not an operating system. A large construction step still lets agents skip sequence, invent outcomes, and open PRs that compile.

AWS [aidlc-workflows](https://github.com/awslabs/aidlc-workflows) (clone: `C:\Dev\AI\AIDLC\aidlc-workflows`) is a harness-neutral engine: phases, stages, reviewers, fail-closed hooks, audit. It is not “a skill.” Reducing it to `.agents/skills` throws away the gates.

Journeys context is incomplete for construction: architecture is enough to place files; customers, outcomes, security, logging, coding standards, and the runtime map are not. Agents will invent those.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Product truth for construction** | Phase 0 files exist and an agent can state what Journeys is, who it serves, what an outcome is, and what it must not log or change — without inventing labels |
| G2 | **Extract, don’t invent** | Outcomes, PAT/ledger, auth, logging sinks, and store map describe today’s code. Known debt is labeled debt |
| G3 | **Customer jobs authored** | Market/positioning matches the signal-response / pay-per-use / loyalty-and-beyond statement in §3 |
| G4 | **Execution kernel** | Cursor AI-DLC is installed in this folder; `/aidlc` runs Classic against brownfield Journeys |
| G5 | **One canon** | AI-DLC knowledge/rules point at `docs/product/` and `AGENTS.md`. No second graph under `aidlc/` |
| G6 | **Verify stays fail-closed** | Construction writes run `scripts/agent-verify.ps1` (docs-impact + graph-impact + build; tests when required) |

### Non-goals (v1)

- Linear as a **ticket poller** (agents pulling work from Linear). Phase 2 projection (board only) is [2026-09-14 Linear AI-DLC projection](2026-09-14-Journeys-linear-aidlc-projection-design.md)
- Playwright / UI e2e (no UI in this sln)
- Agent merge to `main` or release
- Dual PR-time Codex + security loop (AI-DLC in-workflow reviewers are enough for v1)
- Rebuilding a Journeys-native 33-stage engine
- Copying `aidlc-workflows/core` into this sln
- Fixing hardcoded Auth0 tenant `"hayward"` or designing per-tenant Auth0
- Changing onion architecture, Backend DLL names, or product behavior
- Standing up Neo4j
- Pay-per-use billing implementation (positioning only)

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| V1 OS | AI-DLC as execution kernel; Journeys as tenant (product canon stays here) |
| Context before install | **B** — outcomes/ledger, security/tenancy, logging, customer jobs, **plus** coding standards and runtime map |
| First workflow | **Classic** (brownfield; skip Ideation; Inception → Construction) |
| Planning vs execution | **Plan** with `/brainstorming` + spec/plan. **Execute** with `/aidlc`. Do not skip planning for non-trivial work. Do not use Express to dodge reviewers on production features |
| Reviewers (v1) | Shipped ensemble: `architecture-reviewer`, `product-lead`, plus `quality` / `devsecops` on stages that dispatch them. Required receipts must pass to exit the stage. Three-fold code/security/performance reviewers are later |
| Stories / tickets | AI-DLC units and `stories.md` in the intent record. Prefer small units. Linear is Phase 2: [projection spec](2026-09-14-Journeys-linear-aidlc-projection-design.md) |
| Spec location | `docs/specs/` (not `docs/superpowers/`) |
| AI-DLC as a skill only | Rejected |
| Home-grown kernel | Rejected |
| Auth / ledger / tenant isolation | Human-gated. Document current behavior. Agents do not “fix” `hayward` |
| PII / log redaction (interim) | Never log secrets, API keys, JWT/raw tokens, connection strings, or full event payloads. Account xref and email: log only as already done in existing code; do not add new PII fields to logs until a human expands this list |
| Commit of this spec | Human only (do not auto-commit) |

---

## 3. Product meaning to write (authored)

Journeys is a **signal response engine**. The customer has one or more data payloads of **homogeneous shape**. Rules run against those properties. **Positive evaluations** deliver outcomes.

**Commercial model:** pay-per-use. Revenue is a markup on Cosmos DB and other hosting costs. Do not implement billing in this spec.

**Buyer:** anyone with that signal shape who wants rules and journeys to change **account** state. The campaign/journey agent does the heavy lifting for complex rulesets. Loyalty is the deepest proof; the shape is general.

**Worked client profile (loyalty):** process large volumes of orders / invoices / receipts; deposit points per dollar spent / invoiced / received using the account’s **current journey node(s)** (tier) and the outcome definition; tier from the current balance of a **Tier_Qualification** `PointAccountType` (`Journeys.Core/Models/PointAccountType.cs`); spendable / redeemable until expire.

**Verticals (examples, not a closed list):**

- B2C: retail, QSR, FSR, travel (airline, car rental)
- B2B: manufacturer incentives across dealers; employee incentives
- Healthcare / wellness: member = insured; program run by payer to reduce incident rate/severity; outcomes may be real insurance cost benefits
- Any org that needs parse → organize → rule/journey progression → specialized webhooks

**Not:** a points spreadsheet with webhooks; a chatbot bolted onto CRUD; a UI in this solution.

**Personas (existing ids — extend jobs, do not rename):**

| Id | Job in this product |
|----|---------------------|
| `technical-buyer` | Chooses a signal engine over rebuild or rigid SaaS; cares about time-to-value and hosting markup |
| `program-operator` | Correct outcomes on live volume (points, tier, notify, tag) |
| `campaign-author` | Human or agent; configures models, journeys, campaigns through the same APIs the engine runs |
| `tenant-admin` | Tenant-scoped Auth0/API key access and configuration |

---

## 4. Phase 0 — context pack (this repo)

Documentation and graph only. No product code. No `aidlc config`.

### 4.1 Files

| File | Kind | Rule |
|------|------|------|
| `docs/product/market-and-positioning.md` | Replace/expand | §3 text. Keep the existing “not spreadsheet / not chatbot / not UI” lines |
| `docs/product/ontology/outcome.md` | New | Product-facing four kinds + “also implemented” code types. Capability id stays `outcomes` |
| `docs/product/ontology/loyalty-account.md` | New | Account, PAT, ledger types, tier-qual coupling. Point at `PointAccountType.cs` |
| `docs/product/ontology/journey.md` | Expand | Criteria-based per-account progression; progression may change any account state; deposits may read **all current journey nodes** |
| `docs/product/use-cases/process-event.md` | Expand | Homogeneous payload → rules → outcomes; loyalty worked profile as the example |
| `docs/product/index.md` | Update | Link new ontology files |
| `docs/product/taxonomies/personas.md` | Expand | Jobs from §3 table |
| `docs/platform/security.md` | New | Auth and tenancy as code behaves today + human-gated debt |
| `docs/platform/runtime.md` | New | What each store/adapter is for |
| `docs/developer/logging.md` | New | Sinks, required fields, redaction |
| `docs/developer/coding-standards.md` | New | Extract from path rules + Core/API; do not invent a new style guide |
| `docs/platform/index.md`, `docs/developer/index.md` | Update | Link the new pages |
| `docs/product/graph/nodes.yaml`, `edges.yaml`, `path-map.yaml` | Update | Extend existing nodes; see §7 |
| `scripts/path-docs-map.yaml` | Update | New platform/developer docs on the matching prefixes |

### 4.2 Outcomes (extract + name)

Product-facing kinds (do not add capability ids):

| Kind | Code | Meaning |
|------|------|---------|
| Points | `DepositPointsOutcome`, `SpendPointsOutcome`, `ExpirePointsOutcome` | Deposit, withdrawal/spend, expire against named PAT(s) |
| Notification | `NotificationOutcome` | Email provider, webhook, other notification adapters |
| Journey progression | journey navigation / related rules in Core | Per-account; can change account state |
| Tags | `TagOutcome` | Applied on positive evaluation |

Also implemented (document, do not promote): `WorkflowOutcome`, `ThirdPartyOutcome`, `RuleStateOutcome`.

Coupling that must be explicit: a point deposit may depend on **which journey node (tier) the account is in**, the outcome definition, **all current journey nodes**, and other rule factors. Tier is commonly the balance of a NonSpendable / tier-qualification PAT, maintained by those deposits.

### 4.3 Loyalty account / PAT (extract)

Write from `PointAccountType`, `PointLedgerTypeStrings`, `PointAccountTypeValidator`, and `PointAccountManifestBuilder`:

- Ledger types: `Escrow`, `Spendable`, `Expired`, `NonSpendable`, `Archive`
- `IsSpendable`, lifespan days/end, `ExpiresToPointAccountTypeId`, rounding
- Tier-qualification PAT: NonSpendable + not spendable; spendable PAT is the redeemable bucket
- Manifest gate already in code: upsert real PAT ids before campaign journey content (do not invent `pat-spendable` placeholders)

Idempotency: say what the engine already does (event wrapper state, `appliedcampaigns` / `outcomestates`). Do not design a new idempotency protocol.

### 4.4 Security (extract + debt)

Write from `Journeys.Infra.Auth/AuthZeroExtensions.cs` and `Program.cs`:

- Global MVC filter `RequireLoyaltyAccount`
- JWT Bearer (Auth0); API key header `Journeys-API-KEY`; Swagger bypass
- Campaign-agent path `/api/v1/{tenantId}/campaign-agent/...`: JWT, API key, or Auth0 `isDevelopment`
- **Debt (human-gated):** Auth0 tenant is hardcoded `"hayward"`; comments say per-tenant config is TODO. Agents must not change this
- Every business operation remains `TenantId`-scoped (`docs/product/ontology/tenant.md`)
- Secrets: committed `appsettings*.json` empty keys only (already in architecture / local-ops)

Human-only surfaces (repeat in `AGENTS.md` only if the wording changes; otherwise pointer from `security.md`): auth, ledger/money-like outcomes, tenant isolation, merge/release.

### 4.5 Logging (extract + interim redaction)

- Serilog; Azure Analytics sink only when `workspaceId` and `authenticationId` are both set; else console (`Program.cs`, local-ops)
- Config names: `JourneysLog`, `JourneysErrorLog` — document as table/id settings; do not invent ops runbooks
- Prefer structured logs with `TenantId` when the call is tenant-scoped (already common)
- Interim redaction (§2): no secrets, keys, tokens, connection strings, or full event payloads in new log sites
- Do not log a prefix of workspace/auth ids in new code (existing `Program.cs` startup lines stay until a human changes them)

### 4.6 Runtime map (extract)

| Concern | Where (today) | Notes |
|---------|---------------|--------|
| Campaign / journey / account / event persistence | `Journeys.DAL` + Backend data plane (`Backend.*` DLLs) | Instances of customer event models live in Backend; Journeys does not invent a parallel store |
| MassTransit sagas | Cosmos (`MassTransit:SagaRepository`) | Host/database/collection in config |
| Inbound files, chunks, archive | Data Lake + blob (`Journeys.Infra.DataLake`, `Journeys.Infra.BlobStorage`) | Hosted jobs register only when `DataLake:ConnectionString` is set |
| Queue | `Journeys.Infra.ServiceBus` | Connection from DI, not source |
| Notifications | `Journeys.Notification` | Adapters only; no campaign rules |
| Auth | `Journeys.Infra.Auth` | Auth0 + API keys |
| Key-value (config) | `KeyValueStorage` | URL/key from config |

If code and this table disagree at implementation time, **code wins** and the doc is fixed in the same change.

### 4.7 Coding standards (extract)

Promote existing rules; do not add a second aesthetic standard:

- Onion and DTO-at-the-edge (architecture + path rules)
- Services in Core; adapters in DAL/Infra; controllers thin
- `…Async`, constructor injection, readonly `_camelCase` fields
- `APIErrorsException` + `GlobalExceptionMiddleware`
- Enums as strings; `CancellationToken` when the stack supports it
- Tests in `Journeys.Tests`, AAA, existing folders
- Do not delete comments without cause
- Public API/service types are DTOs; `Core.Models` stays internal to Core

Nullable, analyzers, and EditorConfig are out of Phase 0 unless already present.

---

## 5. Phase 1 — AI-DLC tenant adapter

### 5.1 Install

From `C:\Dev\Journeys\Journeys`, after a native `aidlc` command is on PATH (or the versioned Cursor runtime copy channel if the public v2 installer lags `main`):

```powershell
aidlc config --harness cursor
aidlc doctor
```

Installer must **merge** marked sections into `AGENTS.md` and structurally merge `.cursor/hooks.json` / `.cursor/cli.json`. It must not replace onion path rules or the session reading order.

Collision contract:

| Surface | Policy |
|---------|--------|
| `AGENTS.md` | Keep Journeys contract first. AI-DLC marked sections may append. Hierarchy in `AGENTS.md` stays: current-turn user → `AGENTS.md` + `.cursor/rules` → skills |
| `.cursor/rules/*.mdc` | Keep existing always-on and glob rules. AI-DLC adds `aidlc.mdc` + phase pointers only |
| `.cursor/hooks.json` | Merge; fail-closed AI-DLC guards stay |
| `docs/product/` | Untouched by the installer |
| `aidlc/` | New workspace shell; method memory only |

If the installer would overwrite a Journeys-owned rule, stop and fix the install options. Do not hand-edit generated `dist/` in the AI-DLC clone.

### 5.2 Adapter content (after install)

- `aidlc/spaces/default/memory/project.md` — reading order, onion, human merge, `hayward`/ledger/tenant human-gated, no invented capability ids, point at Phase 0 files
- `aidlc/knowledge/aidlc-shared/` — **pointers** (paths) to `docs/product/` and `docs/platform/security.md` / `runtime.md`, `docs/developer/logging.md` / `coding-standards.md`. Do not duplicate the graph
- Sensor wrapping `.\scripts\agent-verify.ps1` on construction writes; fail closed
- AWS/CDK/platform stages: skip or mark not applicable for this onion .NET tree
- First run: `/aidlc classic` with an intent that names this repo as brownfield (stage 2.1 reverse-engineering is expected)

### 5.3 Data flow

```
Human: /brainstorming → spec (`docs/specs/`) + plan (`docs/plans/`) — Superpowers writing-plans; Journeys plan header (not SDD)
Human: execute + intent name (same chat; journeys-plan-to-aidlc starts classic)
  → AI-DLC engine (init + inception; ingest spec/plan; units-generation)
  → Linear upsert / ApproveCreate / pull-back (before any Journeys.* code)
  → AI-DLC construction (stage route, gates, audit under aidlc/spaces/…/intents/)
  → Conductor + personas read project.md + docs/product/
  → Code edits in Journeys.* 
  → Sensor: agent-verify (docs-impact, graph-impact, build, tests if required)
  → Human approval gates (AI-DLC)
  → Human merge (never the agent)
```

Headless Cursor cannot pass presence-gated stages. Unattended “pull next ticket” is out of v1.

---

## 6. Error handling

| Failure | Response |
|---------|----------|
| Phase 0 doc disagrees with code | Fix the doc in the same change; code is canonical for extract slices |
| Agent invents a capability id | Reject; extend `outcomes` or add a use-case node only |
| Agent edits `hayward` Auth0 or ledger semantics | Stop; human-gated |
| `aidlc config` overwrites onion rules | Abort install; restore from git; merge manually |
| `agent-verify` fails | Construction does not advance |
| AI-DLC AWS persona proposes CDK/terraform in this tree | Refuse (`docs/roadmap/non-goals.md`) |
| Linear poller / Playwright requested in v1 | Poller out of scope. Linear projection is Phase 2 ([spec](2026-09-14-Journeys-linear-aidlc-projection-design.md)) |

---

## 7. Graph (extend, do not invent capabilities)

Existing capability ids stay: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.

Phase 0 does **not** add new UseCase or Capability ids. Expand `process-event` and ontology files only. Draft→live stays in `draft-live.md` (no `promote-draft-live` node).

Required edges to add if missing after the ontology files exist:

- `process-event` `REALIZED_BY` `outcomes`
- `outcomes` `IMPLEMENTED_AS` `proj-core` (and `proj-notification` for notification adapters)
- `campaigns` `DEPENDS_ON` `outcomes` already implied via `SERVES`; keep `SERVES`

`path-map.yaml`: longest-prefix `Journeys.Core/RulesEngine/Outcomes` → `outcomes`; `Journeys.Core/Models` → `outcomes` and `campaigns`.

`path-docs-map.yaml`: Core/rules and notification prefixes must list `docs/product/ontology/outcome.md` and `loyalty-account.md` where meaning applies; Infra lists `docs/platform/runtime.md` and `security.md` as appropriate.

---

## 8. Components (isolation)

| Unit | Does | Used how | Depends on |
|------|------|----------|------------|
| Phase 0 docs | Product/platform/developer truth | Agents read via `AGENTS.md` order | Existing taxonomies, code |
| Graph + maps | Meaning bridge | `graph-impact` / `docs-impact` | Phase 0 files |
| AI-DLC engine | Stage route, gates, audit | `/aidlc` | Installed Cursor harness |
| `project.md` + knowledge pointers | Bind engine to Journeys canon | Loaded each workflow | Phase 0 |
| `agent-verify` sensor | Fail-closed local gate | Construction writes | Existing scripts |
| Human | Intent, design approval, merge | Always | — |

Each unit is understandable without the others’ internals. Changing AI-DLC version must not require rewriting `docs/product/`.

---

## 9. Testing / verification

### Phase 0

1. New/updated docs exist and are linked from `docs/product/index.md` or platform/developer indexes.
2. `outcome.md` names the four product-facing kinds and the three also-implemented types.
3. `loyalty-account.md` names the five `PointLedgerTypeStrings` and points at `PointAccountType.cs`.
4. `security.md` states `RequireLoyaltyAccount`, the three allow paths, and `"hayward"` as human-gated debt.
5. `runtime.md` table matches §4.6 or is updated to match code.
6. `.\scripts\docs-impact.ps1` and `.\scripts\graph-impact.ps1` exit 0 on the Phase 0 file set.
7. No new capability ids in `nodes.yaml`.

### Phase 1

1. `aidlc doctor` passes from this folder.
2. Journeys `.cursor/rules` for onion/session/docs-and-graph still exist and apply.
3. `AGENTS.md` still has the Journeys reading order and human-only list.
4. `/aidlc --status` (or `/aidlc-status`) shows a workspace; Classic is selectable.
5. A dry construction write that skips docs/graph fails `agent-verify`; the same write with updates or a waiver passes.

No product unit-test changes are required for either phase unless a script or sensor wrapper is added.

---

## 10. Implementation order

1. Phase 0 docs + graph + path maps (one change set).
2. Human review of Phase 0 (especially §3 wording).
3. Phase 1 install + adapter files + sensor + doctor.
4. First Classic intent (human-kicked). Reverse-engineering artifacts stay under `aidlc/`; they must not replace `docs/product/`.

A separate `docs/plans/2026-09-13-Journeys-agentic-os.md` is written only after this spec is approved.

---

## 11. Out of scope later (not forgotten)

- Linear as work-queue poller (Phase 2 is a **projection** of units — [spec](2026-09-14-Journeys-linear-aidlc-projection-design.md))
- PR admission CI (lint, static analysis) and dual PR review agents
- Integration / Playwright after review
- Agent check-in to `dev` when all gates pass (still human for `main`)
- Per-tenant Auth0
- Pay-per-use metering

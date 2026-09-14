# Journeys Agentic Construction OS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This repo’s human rule wins: do not start tasks until the user approves this plan. Prefer a fresh subagent per task.

**Goal:** Write the Phase 0 context pack (product, security, logging, standards, runtime, graph), then install AI-DLC as the Cursor execution kernel bound to that pack.

**Architecture:** Product canon stays in `docs/product/` and `AGENTS.md`. AI-DLC is the gate engine only (`aidlc/` + `.cursor/` merge). `scripts/agent-verify.ps1` remains the fail-closed local gate. No second graph under `aidlc/`.

**Tech Stack:** Markdown, YAML, PowerShell 5+, existing `scripts/*-impact.ps1`, native `aidlc` Cursor harness, net8.0 `Journeys.sln` (build only if a sensor wrapper is invoked).

**Spec:** `docs/specs/2026-09-13-Journeys-agentic-os-design.md`

## Global Constraints

- Do not invent capability ids. Existing ids only: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- Extract slices must match today’s code. Auth0 tenant `"hayward"` is human-gated debt — do not “fix” it.
- Do not copy `C:\Dev\AI\AIDLC\aidlc-workflows\core` into this sln.
- Do not copy EXP. Do not modify `c:\Dev\loyalty\ELP`.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not run `aidlc config` until Task 5. Phase 0 is docs/graph only.
- Do not implement Linear, Playwright, pay-per-use billing, per-tenant Auth0, or agent merge.
- Do not delete existing comments without cause.
- If code and an extract doc disagree, fix the doc; do not change product code.

## File map

| Path | Responsibility |
|------|----------------|
| `docs/product/market-and-positioning.md` | Signal-response / pay-per-use / verticals |
| `docs/product/taxonomies/personas.md` | Jobs from spec §3 |
| `docs/product/ontology/outcome.md` | Four product-facing kinds + also-implemented |
| `docs/product/ontology/loyalty-account.md` | PAT, ledger types, tier-qual |
| `docs/product/ontology/journey.md` | Criteria-based per-account progression |
| `docs/product/use-cases/process-event.md` | Homogeneous payload → outcomes |
| `docs/product/index.md` | Link new ontology |
| `docs/platform/security.md` | Auth0 / API key / hayward debt |
| `docs/platform/runtime.md` | Store/adapter map |
| `docs/platform/index.md` | Link security + runtime |
| `docs/developer/logging.md` | Serilog + redaction |
| `docs/developer/coding-standards.md` | Extracted style |
| `docs/developer/index.md` | Link logging + standards |
| `docs/product/graph/edges.yaml` | `process-event`→`outcomes`, `outcomes`→projects |
| `docs/product/graph/path-map.yaml` | Outcomes + Models prefixes |
| `scripts/path-docs-map.yaml` | Required docs for those prefixes |
| `AGENTS.md` | First sentence aligns with signal-response (reading order unchanged) |
| `scripts/aidlc-agent-verify-sensor.ps1` | Wrapper around `agent-verify.ps1` |
| `.agents/skills/aidlc-journeys/SKILL.md` | When/how to run the wrapper |
| `aidlc/spaces/default/memory/project.md` | After install — bind engine to canon |
| `aidlc/knowledge/aidlc-shared/journeys-canon.md` | After install — pointers only |

---

### Task 1: Authored product meaning

**Files:**
- Modify: `docs/product/market-and-positioning.md`
- Modify: `docs/product/taxonomies/personas.md`
- Modify: `docs/product/ontology/journey.md`
- Modify: `docs/product/use-cases/process-event.md`
- Modify: `AGENTS.md` (first paragraph only)

**Interfaces:**
- Consumes: spec §3 wording
- Produces: customer-jobs canon other tasks must not contradict

- [ ] **Step 1: Replace `docs/product/market-and-positioning.md` with this exact body**

```markdown
# Market and positioning

**For** anyone with one or more data payloads of homogeneous shape who wants rules run against those properties so positive evaluations deliver outcomes.

**Need:** time to value — their events to correct outcomes without a platform rebuild.

**This is:** a multi-tenant **signal response engine** (program runtime). Pay-per-use: revenue is a markup on Cosmos DB and other hosting costs. Loyalty is the deepest proof; the shape is general.

**Worked profile (loyalty):** process large volumes of orders, invoices, or receipts; deposit points per dollar spent, invoiced, or received from the account’s current journey node(s) (tier) and the outcome definition; tier from the current balance of a Tier_Qualification point account type; spendable and redeemable until expire.

**Verticals (examples, not a closed list):** B2C retail, QSR, FSR, travel (airline, car rental); B2B manufacturer incentives across dealers and employee incentives; healthcare and wellness (member is the insured; program run by the payer; outcomes may be real insurance cost benefits); any organization that needs parse → organize → rule and journey progression → specialized webhooks.

**The campaign/journey agent** does the heavy lifting for complex rulesets.

**Not:** a points spreadsheet with webhooks; a chatbot bolted onto CRUD; a UI in this solution.

**Why now:** launch windows close; rigid SaaS cannot model their signals; homegrown logic is unmaintainable.
```

- [ ] **Step 2: Replace the jobs column in `docs/product/taxonomies/personas.md`**

```markdown
# Taxonomy: personas

| Id | Name | Job |
|----|------|-----|
| `technical-buyer` | Technical founder / buyer | Chooses a signal engine over a rebuild or rigid SaaS; cares about time-to-value and hosting markup |
| `program-operator` | Program operator | Correct outcomes on live volume (points, tier, notify, tag) |
| `campaign-author` | Campaign author | Human or agent configuring models, journeys, campaigns through the same APIs the engine runs |
| `tenant-admin` | Tenant admin | Tenant-scoped Auth0 / API key access and configuration |
```

Do not rename ids.

- [ ] **Step 3: Replace `docs/product/ontology/journey.md` with this exact body**

```markdown
# Ontology: journey

A **journey** is a stateful graph of program logic (nodes, edges, gates). It is how multi-step behavior is encoded — not flat if/else in a controller.

Journeys are **criteria-based progression by account**. Progression can change any account state (tier, balances, tags, and other account fields the engine already mutates). A point deposit may depend on which journey node (tier) the account is in, the outcome definition, **all current journey nodes**, and other rule factors.

Implemented primarily in `Journeys.Core` (rules engine / journey types). Authoring UX is out of this solution; APIs and the campaign agent are in. Capability id: `journeys`.
```

- [ ] **Step 4: Replace `docs/product/use-cases/process-event.md` with this exact body**

```markdown
# Use case: process an event to outcomes

**Personas:** `program-operator`, `technical-buyer`  
**Realized by:** `event-models`, `journeys`, `rules-engine`, `outcomes`  
**Implemented as:** `Journeys.Core` (engine), `Journeys.DAL` / Infra (persist and emit)

An inbound payload in the customer’s homogeneous schema is evaluated. Rules run against those properties. On positive evaluation, outcomes fire: points (deposit, spend/withdrawal, expire), notifications (email, webhook, other adapters), journey progression, and tags. Processing is idempotent via event-wrapper state (`appliedcampaigns`, `outcomestates`, and the rest of the `*AndRuleState` contract in `docs/product/ontology/event-model.md`).

Loyalty example: orders / invoices / receipts at volume → points per dollar from current journey node(s) and the outcome definition → tier from a Tier_Qualification point account type balance.
```

- [ ] **Step 5: In `AGENTS.md`, change only the first sentence of the opening paragraph to**

`Journeys is a multi-tenant **signal response engine** (program runtime): customer-owned event models, journey graphs and declarative rules, pluggable outcomes.`

Leave the rest of `AGENTS.md` unchanged (reading order, human-only list, definition of done).

- [ ] **Step 6: Verify**

From `C:\Dev\Journeys\Journeys`:

```powershell
Select-String -Path docs\product\market-and-positioning.md -Pattern "signal response engine","pay-per-use","Tier_Qualification" | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path AGENTS.md -Pattern "signal response engine" | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: first count ≥ 3, second count = 1. Do not commit.

---

### Task 2: Outcomes and loyalty-account ontology

**Files:**
- Create: `docs/product/ontology/outcome.md`
- Create: `docs/product/ontology/loyalty-account.md`
- Modify: `docs/product/index.md`

**Interfaces:**
- Consumes: Task 1 wording; `Journeys.Core/RulesEngine/Outcomes/*`; `PointAccountType.cs`; `PointLedgerTypeStrings`
- Produces: ontology files later tasks link from graph/maps

- [ ] **Step 1: Create `docs/product/ontology/outcome.md` with this exact body**

```markdown
# Ontology: outcome

An **outcome** is what the engine delivers on a **positive rule evaluation**. Capability id: `outcomes`. Do not invent a second capability for points, notifications, tags, or progression.

## Product-facing kinds

| Kind | Code types | Meaning |
|------|------------|---------|
| Points | `DepositPointsOutcome`, `SpendPointsOutcome`, `ExpirePointsOutcome` | Deposit, withdrawal/spend, expire against named point account type(s) |
| Notification | `NotificationOutcome` | Email provider, webhook, other `Journeys.Notification` adapters |
| Journey progression | journey navigation and related rules in `Journeys.Core` | Per-account; can change account state |
| Tags | `TagOutcome` | Applied on positive evaluation |

A point deposit may read the outcome definition, **all current journey nodes**, and other rule factors (see `docs/product/ontology/journey.md` and `docs/product/ontology/loyalty-account.md`).

## Also implemented (do not promote to new capability ids)

`WorkflowOutcome`, `ThirdPartyOutcome`, `RuleStateOutcome` exist in `Journeys.Core/RulesEngine/Outcomes`. Document them here only so agents do not invent parallel types.

## Persist

Outcome state on the event wrapper uses the lowercase symbols in `docs/product/ontology/event-model.md` (`outcomestates`, `issuingoutcomeid`, and related fields).
```

- [ ] **Step 2: Create `docs/product/ontology/loyalty-account.md` with this exact body**

```markdown
# Ontology: loyalty account and point account types

A **loyalty account** is the per-member (or per-dealer / per-employee) account the engine progresses. All operations are `TenantId`-scoped.

A **point account type** (PAT) is a named ledger bucket on that account. Source type: `Journeys.Core/Models/PointAccountType.cs`.

## Ledger types (`PointLedgerTypeStrings`)

| Value | Role |
|-------|------|
| `Escrow` | Held; not the default redeemable bucket |
| `Spendable` | Redeemable / fulfillment bucket when `IsSpendable` is true |
| `Expired` | Expire-to sink |
| `NonSpendable` | Counters that are not redeemable (tier qualification uses this) |
| `Archive` | Archive sink |

Other PAT fields the engine already uses: `IsSpendable`, `PointsLifespanDays`, `PointsLifespanEndDate`, `ExpiresToPointAccountTypeId`, rounding (`RoundingOptionString`, `RoundingDecimalPlaces`).

## Tier qualification

A **Tier_Qualification** PAT is NonSpendable and not spendable. Current balance of that PAT is the usual input to journey / tier node. Spendable PAT balance is what remains available for redemption until expire. Deposits that maintain the tier-qual balance follow the outcome definition and current journey nodes (`docs/product/ontology/outcome.md`).

## Manifest gate (do not invent ids)

Campaign journey content requires real PAT ids from `upsert_point_account_type` (expired sink first, then spendable). Do not inline placeholders such as `pat-spendable` or `SPENDABLE_PAT_ID`. See `PointAccountManifestBuilder` and `CampaignBuildGate`.

## Human-gated

Ledger and money-like outcomes are a human-only risk surface (`AGENTS.md`). Agents do not redesign ledger types or invent a second points store.
```

- [ ] **Step 3: In `docs/product/index.md`, after the folder table, add this paragraph**

```markdown
Construction-relevant ontology (load with the slice you are changing): `ontology/campaign.md`, `ontology/journey.md`, `ontology/rule.md`, `ontology/event-model.md`, `ontology/outcome.md`, `ontology/loyalty-account.md`, `ontology/tenant.md`, `ontology/draft-live.md`.
```

- [ ] **Step 4: Verify**

```powershell
Select-String -Path docs\product\ontology\outcome.md -Pattern "DepositPointsOutcome","WorkflowOutcome","TagOutcome" | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path docs\product\ontology\loyalty-account.md -Pattern "Escrow","Spendable","Expired","NonSpendable","Archive" | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path docs\product\index.md -Pattern "ontology/outcome.md" | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: first ≥ 3, second ≥ 5, third ≥ 1. Do not commit.

---

### Task 3: Security, runtime, logging, coding standards

**Files:**
- Create: `docs/platform/security.md`
- Create: `docs/platform/runtime.md`
- Create: `docs/developer/logging.md`
- Create: `docs/developer/coding-standards.md`
- Modify: `docs/platform/index.md`
- Modify: `docs/developer/index.md`

**Interfaces:**
- Consumes: `Journeys.Infra.Auth/AuthZeroExtensions.cs`, `Journeys.API/Program.cs`, `ConfigureInfra.cs`, path rules
- Produces: platform/developer pages linked from indexes

- [ ] **Step 1: Create `docs/platform/security.md` with this exact body**

```markdown
# Security and tenancy

## Current behavior (extract)

`Journeys.API` registers a global MVC filter `RequireLoyaltyAccount` (`Program.cs`). Policy implementation: `Journeys.Infra.Auth/AuthZeroExtensions.cs`.

Allow paths today:

1. `/swagger` — bypass
2. Campaign-agent routes `/api/v1/{tenantId}/campaign-agent/...` — JWT, header `Journeys-API-KEY` matching `ApiKeys`, or `Auth0:{tenant}:isDevelopment`
3. All other MVC actions — development flag, or valid `Journeys-API-KEY`, or JWT with a role in `Auth0:{tenant}:AllowedRoles`

JWT is Auth0 (`JwtBearer`). Every business operation stays `TenantId`-scoped (`docs/product/ontology/tenant.md`).

Committed `appsettings*.json` hold empty keys only. Live credentials: user secrets, environment, or gitignored `appsettings.Local.json` (`docs/developer/local-ops.md`).

## Human-gated debt

Auth0 tenant is hardcoded `"hayward"` in `AuthZeroExtensions.cs` (JWT setup and the authorization assertion). Comments say per-tenant config is TODO. **Agents must not change this.** Per-tenant Auth0 is a human-owned fix.

Also human-only (`AGENTS.md`): auth, ledger/money-like outcomes, tenant isolation, merge and release.

## Isolation

Do not add APIs or services that operate globally on member/program data without an explicit, documented exception. Do not log secrets, tokens, API keys, connection strings, or full event payloads (`docs/developer/logging.md`).
```

- [ ] **Step 2: Create `docs/platform/runtime.md` with this exact body**

```markdown
# Runtime map

What each store and adapter is for **today**. If this table and code disagree, fix this file in the same change.

| Concern | Where | Notes |
|---------|-------|--------|
| Campaign / journey / account / event persistence | `Journeys.DAL` + Backend data plane (`Backend.*` DLLs) | Customer event-model instances live in Backend. Do not invent a parallel store |
| MassTransit sagas | Cosmos (`MassTransit:SagaRepository`) | Host, key, database `masstransit`, collection `sagas` in config |
| Inbound files, chunks, archive | Data Lake + blob (`Journeys.Infra.DataLake`, `Journeys.Infra.BlobStorage`) | Hosted jobs register only when `DataLake:ConnectionString` is set |
| Queue | `Journeys.Infra.ServiceBus` | Connection from DI, not source |
| Notifications | `Journeys.Notification` | Adapters only; no campaign/journey rules |
| Auth | `Journeys.Infra.Auth` | Auth0 + API keys (`docs/platform/security.md`) |
| Key-value | `KeyValueStorage` | URL and key from config |

Terraform and Databricks folders are not in this tree (`docs/roadmap/non-goals.md`).
```

- [ ] **Step 3: Create `docs/developer/logging.md` with this exact body**

```markdown
# Logging

## Sinks

`Journeys.API` uses Serilog (`Program.cs`). Azure Analytics is registered only when `Serilog:WriteTo:0:Args:workspaceId` and `authenticationId` are both set. Otherwise the host logs to the console. `JourneysLog` and `JourneysErrorLog` are config names for those table/id settings — not a second logging API.

## What to include

When the call is tenant-scoped, use structured properties and include `TenantId` (existing controllers already do this on errors).

## What not to log (interim)

Do not log secrets, API keys, JWT or raw tokens, connection strings, or full event payloads. Do not add new email or account-xref fields to logs. Do not add new startup lines that print workspace or authentication id prefixes. Existing `Program.cs` startup diagnostics stay until a human removes them.
```

- [ ] **Step 4: Create `docs/developer/coding-standards.md` with this exact body**

```markdown
# Coding standards

Extracted from `.cursor/rules` and existing Core/API habits. Do not invent a second aesthetic.

- Onion: controllers validate and delegate; services in `Journeys.Core`; persistence in `Journeys.DAL`; Azure/Auth/Backend/bus/blob in `Journeys.Infra*`. See `docs/platform/architecture.md`.
- Controller and service in/out types are `Journeys.DTO`. `Journeys.Core.Models` stays internal to Core.
- Public async methods are named `...Async` and return `Task` / `Task<T>`.
- Constructor injection; readonly `_camelCase` fields.
- Validation: `APIErrorsException` with `Dictionary<string, string>`; unexpected errors go through `GlobalExceptionMiddleware`.
- Enums serialize as strings (`JsonStringEnumConverter`).
- Prefer `CancellationToken` on async actions when the service stack supports it.
- Tests live in `Journeys.Tests`, existing folders, Arrange–Act–Assert (`docs/developer/testing.md`).
- Do not delete existing comments without cause.
- Do not take Azure SDK or Backend client dependencies in Core.
```

- [ ] **Step 5: Replace `docs/platform/index.md` with**

```markdown
# Platform

How Journeys is built. Product *meaning* is `docs/product/`.

1. [Architecture](architecture.md) — onion, DDD, projects, Backend DLLs
2. [Overlays](overlays.md) — generic layer names mapped to Journeys projects
3. [Runtime](runtime.md) — Cosmos, bus, lake, Backend, notifications
4. [Security](security.md) — Auth0, API keys, tenancy, human-gated debt
5. [Decisions](decisions/0001-onion-Journeys-rename.md) — ADRs
6. Developer ops: `docs/developer/`
```

- [ ] **Step 6: Replace `docs/developer/index.md` with**

```markdown
# Developer

- [Local ops](local-ops.md) — restore, build, and local secrets
- [Testing](testing.md)
- [Logging](logging.md) — Serilog sinks and redaction
- [Coding standards](coding-standards.md)
- [Tools](tools.md) — CampaignContextAudit outside this folder
```

- [ ] **Step 7: Verify**

```powershell
Select-String -Path docs\platform\security.md -Pattern 'hayward','RequireLoyaltyAccount','Journeys-API-KEY' | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path docs\platform\runtime.md -Pattern "MassTransit","DataLake","Backend" | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path docs\platform\index.md -Pattern "security.md","runtime.md" | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: each count ≥ 3, ≥ 3, ≥ 2. Do not commit.

---

### Task 4: Graph, path maps, impact scripts

**Files:**
- Modify: `docs/product/graph/edges.yaml`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`

**Interfaces:**
- Consumes: Task 2 ontology files; existing `nodes.yaml` (do not add capability ids)
- Produces: impact scripts exit 0/1 as specified

- [ ] **Step 1: Append these edges to `docs/product/graph/edges.yaml` (keep all existing edges)**

```yaml
  - from: process-event
    type: REALIZED_BY
    to: outcomes
  - from: outcomes
    type: IMPLEMENTED_AS
    to: proj-core
  - from: outcomes
    type: IMPLEMENTED_AS
    to: proj-notification
```

Do not add a new Capability or UseCase node. Do not remove `campaigns SERVES outcomes`.

- [ ] **Step 2: Insert these two entries at the top of `entries:` in `docs/product/graph/path-map.yaml` (longest prefix must be listed; matcher already uses longest prefix)**

```yaml
  - prefix: Journeys.Core/RulesEngine/Outcomes
    nodes: [outcomes]
    meaningOptional: false
  - prefix: Journeys.Core/Models
    nodes: [outcomes, campaigns]
    meaningOptional: false
```

Leave existing `Journeys.Core/RulesEngine` and `Journeys.Core` entries in place.

- [ ] **Step 3: Insert these entries at the top of `scripts/path-docs-map.yaml` and extend Infra/Notification**

Replace the file with:

```yaml
entries:
  - prefix: Journeys.Core/RulesEngine/Outcomes
    docs: [docs/platform/architecture.md, docs/product/ontology/outcome.md]
  - prefix: Journeys.Core/Models
    docs: [docs/platform/architecture.md, docs/product/ontology/loyalty-account.md, docs/product/ontology/outcome.md]
  - prefix: Journeys.API/CampaignAgent
    docs: [docs/platform/architecture.md, docs/developer/tools.md]
  - prefix: Journeys.API/Mcp
    docs: [docs/platform/architecture.md, docs/developer/index.md]
  - prefix: Journeys.API
    docs: [docs/platform/architecture.md, docs/platform/security.md]
  - prefix: Journeys.Core/RulesEngine
    docs: [docs/platform/architecture.md, docs/product/ontology/rule.md]
  - prefix: Journeys.Core
    docs: [docs/platform/architecture.md, docs/product/ontology/campaign.md]
  - prefix: Journeys.DTO
    docs: [docs/platform/architecture.md]
  - prefix: Journeys.DAL
    docs: [docs/platform/architecture.md]
  - prefix: Journeys.Agent
    docs: [docs/platform/architecture.md, docs/developer/tools.md]
  - prefix: Journeys.Tests
    docs: [docs/developer/testing.md]
  - prefix: Journeys.Infra
    docs: [docs/platform/architecture.md, docs/platform/runtime.md, docs/platform/security.md]
  - prefix: Journeys.Notification
    docs: [docs/platform/architecture.md, docs/product/ontology/outcome.md]
```

- [ ] **Step 4: Prove docs-impact fails without the new ontology**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md"
)
```

Expected: exit 1 and a line containing `docs/product/ontology/outcome.md`.

- [ ] **Step 5: Prove docs-impact passes with the mapped docs**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md",
  "docs\product\ontology\outcome.md"
)
```

Expected: exit 0, `docs-impact: OK`.

- [ ] **Step 6: Prove graph-impact fails without a product update**

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs"
)
```

Expected: exit 1, nodes include `outcomes`.

- [ ] **Step 7: Prove graph-impact passes with a product file in the set**

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\product\ontology\outcome.md"
)
```

Expected: exit 0.

- [ ] **Step 8: Confirm no new capability ids**

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern "label: Capability"
```

Expected: the same seven capability ids as before (`event-models` through `mcp-api`). Do not commit.

---

### Task 5: Install AI-DLC Cursor harness

**Files:** installer-managed: `.cursor/**` (merge), `aidlc/**` (new), marked sections in `AGENTS.md`. Do not hand-edit `C:\Dev\AI\AIDLC\aidlc-workflows\dist*`.

**Interfaces:**
- Consumes: Phase 0 complete (Tasks 1–4)
- Produces: `aidlc` workspace shell + Cursor harness files for Task 6

- [ ] **Step 1: Confirm Phase 0 files exist**

```powershell
@(
  "docs\product\ontology\outcome.md",
  "docs\product\ontology\loyalty-account.md",
  "docs\platform\security.md",
  "docs\platform\runtime.md",
  "docs\developer\logging.md",
  "docs\developer\coding-standards.md"
) | ForEach-Object { if (-not (Test-Path $_)) { throw "missing $_" } }
Write-Host "phase0: OK"
```

Expected: `phase0: OK`.

- [ ] **Step 2: Snapshot Journeys-owned rules before install**

```powershell
Get-ChildItem .cursor\rules\*.mdc | Select-Object -ExpandProperty Name | Sort-Object
```

Expected names include: `session-context.mdc`, `docs-and-graph-sync.mdc`, `onion-architecture.mdc`, `api-controllers.mdc`, `core-services.mdc`, `dal-adapters.mdc`, `infra-adapters.mdc`, `tests.mdc`, `campaign-agent.mdc`. Save this list; Task 5 Step 5 must still see all of them.

- [ ] **Step 3: Ensure `aidlc` is on PATH**

```powershell
Get-Command aidlc -ErrorAction SilentlyContinue
```

If missing, install the native command (Windows):

```powershell
irm https://github.com/awslabs/aidlc-workflows/releases/latest/download/install.ps1 | iex
```

If that release is still v1-only or `aidlc` still missing, stop and report — do not copy `core/` into this sln. Do not run `bun scripts/package.ts` inside Journeys.

- [ ] **Step 4: Configure this project**

From `C:\Dev\Journeys\Journeys`:

```powershell
aidlc config --harness cursor
aidlc doctor
```

Expected: doctor exits 0 (or prints a fixable PATH/hook warning you resolve without deleting Journeys rules).

- [ ] **Step 5: Collision check**

```powershell
$required = @(
  "session-context.mdc","docs-and-graph-sync.mdc","onion-architecture.mdc",
  "api-controllers.mdc","core-services.mdc","dal-adapters.mdc",
  "infra-adapters.mdc","tests.mdc","campaign-agent.mdc"
)
$have = Get-ChildItem .cursor\rules\*.mdc | Select-Object -ExpandProperty Name
$missing = $required | Where-Object { $_ -notin $have }
if ($missing) { throw "installer removed Journeys rules: $($missing -join ', ')" }
Select-String -Path AGENTS.md -Pattern "Essential reading","Human-only authorities","signal response engine" | Measure-Object | Select-Object -ExpandProperty Count
Test-Path aidlc
```

Expected: no throw; Select-String count ≥ 3; `aidlc` path True. If the installer overwrote the reading order, restore those `AGENTS.md` sections from git and keep any AI-DLC marked block **below** the Journeys contract. Do not commit.

---

### Task 6: Journeys adapter (project memory, pointers, verify wrapper)

**Files:**
- Create: `aidlc/spaces/default/memory/project.md` (path may be `aidlc/spaces/default/memory/project.md` — if `aidlc config` used a different active space, write the same file under the active space and also copy to `default`)
- Create: `aidlc/knowledge/aidlc-shared/journeys-canon.md`
- Create: `scripts/aidlc-agent-verify-sensor.ps1`
- Create: `.agents/skills/aidlc-journeys/SKILL.md`

**Interfaces:**
- Consumes: `scripts/agent-verify.ps1`; Phase 0 paths
- Produces: `aidlc-agent-verify-sensor.ps1` exit 0/1; conductor instructions

- [ ] **Step 1: Create `scripts/aidlc-agent-verify-sensor.ps1`**

```powershell
param(
    [string[]]$Files,
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$verify = Join-Path $here "agent-verify.ps1"
if (-not (Test-Path $verify)) { throw "missing $verify" }

if ($Files -and $Files.Count -gt 0) {
    if ($RunTests) {
        & $verify -Files $Files -RunTests
    } else {
        & $verify -Files $Files
    }
} else {
    if ($RunTests) {
        & $verify -RunTests
    } else {
        & $verify
    }
}
exit $LASTEXITCODE
```

This is a wrapper only. Do not reimplement impact logic.

- [ ] **Step 2: Create `.agents/skills/aidlc-journeys/SKILL.md`**

```markdown
---
name: aidlc-journeys
description: Bind AI-DLC construction in this repo to Journeys canon and agent-verify.
---

# AI-DLC on Journeys

When `/aidlc` (or an AI-DLC construction stage) is running in `C:\Dev\Journeys\Journeys`:

1. Read `AGENTS.md` then `docs/product/` as usual. Do not treat `aidlc/` artifacts as product canon.
2. Do not invent capability ids. Do not edit Auth0 `"hayward"` or ledger types.
3. Before finishing a Construction stage (especially 3.5 / 3.6), run:

```powershell
.\scripts\aidlc-agent-verify-sensor.ps1
```

Add `-RunTests` when tests or `Journeys.Tests` files changed. Exit 0 required. Non-zero means halt — do not open a PR.

4. AWS/CDK/terraform suggestions are out of this tree (`docs/roadmap/non-goals.md`).
```

- [ ] **Step 3: Create `aidlc/knowledge/aidlc-shared/journeys-canon.md`**

```markdown
# Journeys canon (pointers)

Do not copy the product graph here. Read these files:

- `AGENTS.md`
- `docs/product/index.md`
- `docs/product/market-and-positioning.md`
- `docs/product/ontology/outcome.md`
- `docs/product/ontology/loyalty-account.md`
- `docs/product/ontology/journey.md`
- `docs/platform/architecture.md`
- `docs/platform/security.md`
- `docs/platform/runtime.md`
- `docs/developer/logging.md`
- `docs/developer/coding-standards.md`
```

- [ ] **Step 4: Create `aidlc/spaces/default/memory/project.md`** (use the active space directory if `aidlc/active-space` names something other than `default`)

```markdown
# Journeys project rules

## Way of Working

- Session reading order is `AGENTS.md`, then `docs/product/`, then platform, then path rules, then specs/plans.
- Product meaning lives in `docs/product/`. `aidlc/` is execution state only. Do not create a second graph.
- Do not invent capability ids.
- Humans approve non-trivial design, merge, and release. Agents never merge to `main`.

## Human-gated

- Auth0 tenant `"hayward"` and per-tenant Auth0 — do not change.
- Ledger / money-like outcomes and tenant isolation — do not redesign.
- See `docs/platform/security.md` and `docs/product/ontology/loyalty-account.md`.

## Testing Posture

- Before completing Construction, run `.\scripts\aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed). Fail closed.
- Prefer `docs/developer/coding-standards.md` and existing `Journeys.Tests` layout.

## Deployment

- No terraform or Databricks folders in this tree.
- Skip AWS/CDK platform stages unless the human names infrastructure work that belongs outside this sln.
```

- [ ] **Step 5: Prove the wrapper fails closed without required docs**

```powershell
.\scripts\aidlc-agent-verify-sensor.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md"
)
```

Expected: non-zero exit (docs-impact missing `outcome.md`).

- [ ] **Step 6: Prove the wrapper can pass impact when docs are listed** (build may still run — if the sln fails for unrelated local reasons, report it; do not “fix” product code)

```powershell
.\scripts\aidlc-agent-verify-sensor.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md",
  "docs\product\ontology\outcome.md"
)
```

Expected: docs-impact and graph-impact OK; then `dotnet build`. If build fails, do not change Core/API to green the wrapper. Do not commit.

---

### Task 7: Phase 1 doctor and contract check

**Files:** none new. Read-only verification.

- [ ] **Step 1: Doctor**

```powershell
aidlc doctor
```

Expected: pass, or only warnings you already documented.

- [ ] **Step 2: Journeys contract still first**

```powershell
Select-String -Path AGENTS.md -Pattern "Essential reading","Human-only authorities","agents never merge" 
Get-ChildItem .cursor\rules\onion-architecture.mdc, .cursor\rules\session-context.mdc | Select-Object Name
```

Expected: matches plus both rule files exist.

- [ ] **Step 3: Adapter files exist**

```powershell
Test-Path .\scripts\aidlc-agent-verify-sensor.ps1
Test-Path .\.agents\skills\aidlc-journeys\SKILL.md
Test-Path .\aidlc\knowledge\aidlc-shared\journeys-canon.md
Test-Path .\aidlc\spaces\default\memory\project.md
```

Expected: all True (if active space is not `default`, `project.md` must exist in that space **and** you must have copied it to `default` as Task 6 said).

- [ ] **Step 4: Status (interactive harness)**

In Cursor, run `/aidlc-status` or `/aidlc --status`. Expected: workspace ready; Classic selectable. Headless `agent -p` is not required (presence gates). Do not start a real Classic construction intent in this task.

- [ ] **Step 5: Stop**

Report: Phase 0 docs, graph maps, installer collision result, doctor, wrapper fail/pass. Do not commit. Do not run `/aidlc classic` until the human names the first construction intent.

---

## Spec coverage (self-review)

| Spec section | Task |
|--------------|------|
| §3 customer jobs / market / personas | 1 |
| §4.2–4.3 outcomes + PAT | 2 |
| §4.4–4.7 security, logging, runtime, standards | 3 |
| §7 graph + maps | 4 |
| §9 Phase 0 verification | 4 steps 4–8 |
| §5.1 install + collision | 5 |
| §5.2 adapter + sensor wrapper | 6 |
| §5.3 / §9 Phase 1 | 7 |
| Non-goals (Linear, Playwright, hayward fix, billing) | Global constraints |

Native AI-DLC `fire_on: gate` TypeScript sensor is **not** in this plan (requires framework engine tools that upgrades overwrite). Fail-closed gate is `project.md` + skill + `aidlc-agent-verify-sensor.ps1`, which is the Journeys-owned equivalent named in spec G6.

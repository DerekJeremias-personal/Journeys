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

Expected: first â‰¥ 3, second â‰¥ 5, third â‰¥ 1. Do not commit.

---


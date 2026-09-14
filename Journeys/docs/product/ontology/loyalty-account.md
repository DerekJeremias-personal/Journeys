# Ontology: loyalty account and point account types

A **loyalty account** is the per-member (or per-dealer / per-employee) account the engine progresses. All operations are `TenantId`-scoped. Lookup is `ExtAccountId` / `KnownExternalIds` (external reference type from `LoyaltyAccountService`). Source: `Journeys.Core/Models/LoyaltyAccount.cs`.

A **point account type** (PAT) is a named ledger **bucket** on that account, not the account itself. Source: `PointAccountType.cs`. Ledgers (`PointLedger`) hang off the account at runtime (`JsonIgnore` on the aggregate); they are not a second Cosmos “points document” invented by agents.

## Account fields the engine already uses

| Field | Meaning |
|-------|---------|
| `Journeys` | Membership: `RootJourneyNodeId` + `JourneyNodeIds` (`journey.md`). |
| `RuleState` | Historical-rule aggregates. Key = `CampaignId\|RuleId` (`rule.md`). Count/Value/FirstOccurrence; TTL decay hydrates before eval. |
| `LockLeaseKey` / `LockLeaseExpiration` | Taken for the process turn; cleared on save in `RulesService` finally. |
| `Tags` | Applied via `TagOutcome`. |
| `ResettleASAP` | Operator/resettle path, not authoring. |

Spendable PAT **balance** and tier-qual PAT **balance** are different buckets. Navigation often reads tier-qual via `PointBalanceProvider`; earn RuleSets often deposit into **both** spendable and TQP when that is the program design.

## Ledger types (`PointLedgerTypeStrings`)

| Value | Role |
|-------|------|
| `Escrow` | Held; not the default redeemable bucket |
| `Spendable` | Redeemable / fulfillment when `IsSpendable` is true |
| `Expired` | Expire-to sink (`ExpiresToPointAccountTypeId` on the source PAT) |
| `NonSpendable` | Counters that are not redeemable (tier qualification uses this) |
| `Archive` | Archive sink |

Also used: `PointsLifespanDays`, `PointsLifespanEndDate`, rounding (`RoundingOptionString`, `RoundingDecimalPlaces`).

## Tier qualification

A **Tier_Qualification** PAT is NonSpendable and not spendable. Its current balance is the usual input to journey / tier nodes. Spendable balance is what remains for redemption until expire. Deposits that maintain the tier-qual balance follow the outcome definition **and** current journey nodes (`outcome.md`).

## Manifest gate (do not invent ids)

Campaign journey content requires real PAT ids from `upsert_point_account_type` (**expired sink first**, then spendable). Do not inline `pat-spendable` or `SPENDABLE_PAT_ID`. `PointAccountManifestBuilder` + `CampaignBuildGate` block validate/upsert until the manifest has real ids.

## Human-gated

Ledger and money-like outcomes are a human-only risk surface (`AGENTS.md`). Agents do not redesign ledger types or invent a second points store. If this file and `LoyaltyAccount` / `PointAccountType` disagree, **code is canonical**.

## Related

`outcome.md`, `journey.md`, `rule.md`, `tenant.md`, `event-model.md`.

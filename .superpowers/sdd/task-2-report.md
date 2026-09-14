# Task 2 Report: Outcomes and loyalty-account ontology

**Status:** DONE  
**Date:** 2026-09-13  
**Commits:** none (per brief)

## Summary

Added two ontology documents for outcomes and loyalty accounts, and linked them from the product index per the task brief.

## Files created

### `docs/product/ontology/outcome.md`

Defines the `outcomes` capability: product-facing kinds (Points, Notification, Journey progression, Tags), internal-only types (`WorkflowOutcome`, `ThirdPartyOutcome`, `RuleStateOutcome`), and persistence via event-model lowercase symbols.

### `docs/product/ontology/loyalty-account.md`

Defines loyalty account and point account type (PAT) semantics: `PointLedgerTypeStrings` table (Escrow, Spendable, Expired, NonSpendable, Archive), tier qualification, manifest gate (`PointAccountManifestBuilder`, `CampaignBuildGate`), and human-gated ledger risk surface.

## Files modified

### `docs/product/index.md`

Added construction-relevant ontology paragraph after the folder table, listing `ontology/outcome.md`, `ontology/loyalty-account.md`, and sibling ontology files.

## Verification

```powershell
Select-String -Path docs\product\ontology\outcome.md -Pattern "DepositPointsOutcome","WorkflowOutcome","TagOutcome" | Measure-Object | Select-Object -ExpandProperty Count
# Result: 3 (expected ≥ 3)

Select-String -Path docs\product\ontology\loyalty-account.md -Pattern "Escrow","Spendable","Expired","NonSpendable","Archive" | Measure-Object | Select-Object -ExpandProperty Count
# Result: 8 (expected ≥ 5)

Select-String -Path docs\product\index.md -Pattern "ontology/outcome.md" | Measure-Object | Select-Object -ExpandProperty Count
# Result: 1 (expected ≥ 1)
```

All checks passed.

## Scope compliance

- No product C# code changed
- No capability ids invented
- No files modified outside brief scope (except this report)
- No git commit, push, merge, or PR

## Concerns

None.

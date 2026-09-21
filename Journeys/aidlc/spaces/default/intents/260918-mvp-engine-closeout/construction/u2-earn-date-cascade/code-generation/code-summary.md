# Code summary — `u2-earn-date-cascade`

## Files created or modified

- `Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs` — eight dest-clock tests (EarnDate+365, dest end-date, neither unset, missing dest stays, dest-PAT throw, missing EarnDate last-resort, already-cascaded UtcNow stays, end-date wins over days).
- `Journeys.Tests/Stubs/StubPointAccountTypeCache.cs` — `GetPointAccountTypeAsync` is virtual so dest-PAT throw can be arranged.
- `Journeys.Core/Services/LoyaltyAccountService.cs` — hop captures EarnDate / existing expiration before rolling mutation; dest clock is `ComputeDestExpirationOnHop`; missing dest PAT logs and returns without moving the row; dest-PAT load throw is not swallowed. ExpirePoints reuses the caller lock key so the hop can finish under the outer ExpireBy* lease.
- Meaning docs: `docs/product/ontology/loyalty-account.md`, `outcome.md`, `campaign.md`, `draft-live.md`; `docs/platform/architecture.md`; `docs/developer/testing.md`.

## Implementation decisions

- Dest clock lives only in `SaveLedgerExpirations`. Deposit-time EarnDate math and `PointLedgerTypeStrings` are unchanged.
- Formula: dest end-date if set; else EarnDate + dest days; else last-resort existing expiration when dest days exist and EarnDate is missing; else unset. Never `UtcNow` and never `?? 100` as the hop lifespan base.
- Missing dest PAT: log, leave row in source, continue. Unexpected dest-PAT throw: propagate.
- Already-cascaded UtcNow dest dates are left as written unless that row hops again.

## Tests

- New: `FullyQualifiedName~LoyaltyAccountExpirationCascade` (8 cases) — Passed.
- Historical `FullyQualifiedName~ExpirePointsOutcomeTests` is not the cascade oracle. CreatePointLedger now upserts the profile account id so Award can find `test@Journeys.com`; dest-date assertions were not retargeted.
- Combined filter `FullyQualifiedName~LoyaltyAccountExpirationCascade|FullyQualifiedName~ExpirePointsOutcomeTests`: Passed 16/16.
- `.\scripts\aidlc-agent-verify-sensor.ps1` with source-manifest product/docs files (Journeys.Tests omitted from `-Files`): docs-impact OK, graph-impact OK (`campaigns`), `agent-verify: OK`.

## Deviations

- ExpirePoints inner `TryLockAccount` now reuses `loyaltyAccount.LockLeaseKey` when the outer ExpireBy* / ProcessEvent call already holds the lease. A new key lost the outer lock and returned null before the hop ran. The ExpirePoints `finally` releases only a lease this method acquired; a reused caller key stays held. Dest clock formula is unchanged. The pre-hop rolling `UtcNow` assignment is left in place and overwritten on a full hop.
- Dest ledger lookup uses `PointAccountTypeId` when `PointAccountType` is null, and assigns `expLedger.PointAccountType ??= expAcctType` after the dest PAT load.

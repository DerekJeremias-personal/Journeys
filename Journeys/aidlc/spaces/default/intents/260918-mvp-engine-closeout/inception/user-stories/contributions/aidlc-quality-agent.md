**Collaborator:** aidlc-quality-agent

## Contribution

Quality review of Given/When/Then AC testability for intent `mvp-engine-closeout`. Affirmed posture is Methodology `custom`: failing `Journeys.Tests` first on new closeout seams (hydrate/collect-rules, cascade clock, expire-before-rules under lock, NotificationOutcome Calculate/Award); historical tests stay test-after. One project, Arrange–Act–Assert, no BDD feature files. `Journeys.Tests` must not reference `Journeys.Notification`; fake `INotificationService` on `RulesEngineState` the same way `LoyaltyAccountService` is already injected. Do not extend the `RulesService` constructor for that fake. No product code.

Seam-per-Must-story plus error/MCP as AC is independently testable. US1.1 does not need ledgers; US4.1 does not need PAT math; US2.1 clock and US3.1 order can be proven as separate unit cases even though they share one ProcessEvent path. US5.1 is script-gated (`docs-impact` / `graph-impact` / `aidlc-agent-verify-sensor.ps1`), not an xUnit story — that is the right verify surface.

**Automatable as written (keep, TDD first on the new seam).**

- **AC1.1.2** — NavConstraint HistoricalRule collected/decayed; empty `Rules` does not throw. Unit-testable on Hydrate/collect. Treat nested/composite nav as a second Arrange, not a second story.
- **AC2.1.2** — Already-cascaded UtcNow dest dates stay until the next hop. Fixture of existing dest rows; assert no rewrite.
- **AC4.1.2 / AC4.1.3 / AC4.1.5** — Calculate null on missing/inactive/EventModelId mismatch; `false` send → `IsAwarded` false and sibling point/tag awards remain; CalculateOnly never sends. All Arrange on a fake `INotificationService` plus a multi-outcome RuleSet for the sibling case.
- **AC5.1.1–AC5.1.3** — Meaning docs and path-map are verified by the existing impact scripts, not by locking a Journeys.Tests case.

**AC that lock the wrong existing tests (must rewrite the Then / name the new oracle).**

`HydrateState` today collects only `campaign.Journey.Rules` (root RuleSets). `RuleServiceTests` already green on root HistoricalRule TTL/decay and `campaign.Journey.Rules` counts. That is yesterday’s collect contract, not US1.1.

`SaveLedgerExpirations` still sets dest expiration with `UtcNow + (days ?? 100)`. `ExpirePointsOutcomeTests` lock balances and award-time expiration providers, not dest `ExpirationDate` after an Escrow→Spendable hop. `TestDataFactory.GetSpendablePointAccount()` is 30-day Spendable; the program example is Spendable 365.

`UserPointsTests` lock `BringLoyaltyAccountPointsCurrentAsync` and concurrent `ExpireLoyaltyAccountPointsByEarnDate` (GET/reconcile-style expire). FR3.6 / A3 say those callers stay as they are. They do not prove ProcessEvent lock → bring-current → navigation/rules.

`RulesEngineMcpContractSummaryTests` pinning `MatrixVersion` `"2026-06-20"` **is** the correct MCP lock and must move with FR5. `JourneyContractSummaryPinArtifactsTests` and `SalientFactsPromptBuilderTests` embed `"2026-06-20"` as **fixture JSON**, not as the live matrix — do not retarget those to close AC4.1.6.

There is no `NotificationOutcome` test today (`Calculate`/`Award` still return null). `ConfigureNotifications.AddNotificationsServices` still gates `INotificationService` on `DisableDataLake`. `ConfigureInfraTests` is a DataLake adapter start test, not that registration.

**AC QA cannot automate as written.**

- **AC1.1.1** Then “the same way a root RuleSet does today” is a living comparison, not an observable. Spell TTL decay + `LoyaltyAccount.RuleState` on the **child-node** RuleSet after a second event.
- **AC1.1.3** Given/When is implementation-state (“root-only would have been scanned” / “tree walk is in place”). “Existing TTL fetch, decay, load, and finally upsert stay” is a non-regression note. If left as Then, Construction will treat current root-only collect as the stay contract. Then must be: child-node and nav historical/taxonomic rules are in the collected set; existing root TTL/upsert tests remain green as historical test-after.
- **AC2.1.1** bundles dest-end-date-wins, no-days → unset, and “`UtcNow + (days ?? 100)` is gone”. The last is a source-scan. Then is `EarnDate` unchanged and Spendable `ExpirationDate == 2027-01-01` when dest days is 365. Split the other two clock rules. Do not Arrange with the 30-day factory PAT.
- **AC2.1.3** “the miss is logged” requires ILogger string coupling we do not use on these seams. The FR3.5 cite is the **bring-current throw** seam, not dest-PAT load. Split: missing/not-found dest → entry stays, event continues; unexpected dest-PAT infra throw → fail closed (own Then).
- **AC3.1.3** “No hosted sweep or `/points/expire`” is Won't Have, not a Then. Keep: bring-current throw after lock fails the event (no nav/award); invalid/missing account skips bring-current.
- **AC4.1.1** “Award POSTs” invites `RestApiAdapter` / a `Journeys.Notification` reference. Then: fake `INotificationService.SendNotificationAsync` received `NotificationOutcomePayload` with exactly the closed field list (no event body, ledgers, or secrets).
- **AC4.1.4** two host behaviors in one AC; switch key is Construction (A1). QA cannot Arrange “default host setting” until the flag is injectable on the Core/outcome path. Split default-throw vs switch-on (`IsAwarded` false, siblings kept). Do not read the switch through `Journeys.Notification`.
- **AC4.1.6** bundles MCP matrix + “`INotificationService` is always registered”. Split. MCP: `MatrixVersion` `2026-09-18` + NotificationConfigId critical row; deposit/spend rows stay — update `RulesEngineMcpContractSummaryTests` only. Registration: `AddNotificationsServices` resolves `INotificationService` from Core even when `DisableDataLake` is true; never reference `Journeys.Notification`.

**TDD mapping (new seams only).** Red tests first: child/nav collect (`JourneyNode` / Hydrate), earn-date dest clock (`SaveLedgerExpirations` hop), expire-before-rules under existing `TryLockAccount` (`ProcessCampaignsAsync`, not HTTP e2e), NotificationOutcome Calculate/Award with state-injected fake. Then implement until those cases and the existing suite pass. `scripts/aidlc-agent-verify-sensor.ps1 -RunTests` when tests change. No coverage floor.

## Positions

- AGREE: Four Must stories by engine seam, error/MCP as AC, docs as Should Have US5.1 — independently testable; matches NFR7 seam list.
- AGREE: AC1.1.2, AC2.1.2, AC4.1.2, AC4.1.3, AC4.1.5 are automatable in `Journeys.Tests` with Arrange–Act–Assert and a state-injected fake `INotificationService`.
- AGREE: US5.1 is closed by `docs-impact` / `graph-impact` / `aidlc-agent-verify-sensor.ps1`, not by a new xUnit suite or BDD files.
- AGREE: Do not reference `Journeys.Notification` from tests; do not extend the `RulesService` constructor for the notification fake.
- AGREE: Historical root hydrate, GET/reconcile expire, and ExpirePoints balance tests stay test-after and must remain green — they are not the new-seam oracles.
- OBJECT: AC1.1.1 / AC1.1.3 — comparative “same as today” and “existing hydrate stay” would lock `RuleServiceTests` root `campaign.Journey.Rules` as the collect contract; Then must name child/nav collection + RuleState, with root TTL/upsert as non-regression only.
- OBJECT: AC2.1.1 — “formula is gone” is not observable; dest-end-date and no-days are extra cases; do not use `GetSpendablePointAccount()` 30-day Spendable or `ExpirePointsOutcomeTests` balances as the EarnDate+365 lock.
- OBJECT: AC2.1.3 — “miss is logged” is not automatable here; FR3.5 is bring-current throw, not dest-PAT load — split miss-and-continue from unexpected dest-PAT fail-closed.
- OBJECT: AC3.1.1 / AC3.1.2 — must not close on `UserPointsTests` bring-current or concurrent `ExpireLoyaltyAccountPointsByEarnDate`; new TDD cases must prove ProcessEvent/ProcessCampaigns lock → expire → rules and TryLockAccount serialization.
- OBJECT: AC3.1.3 Then “no hosted sweep / `/points/expire`” is unautomatable scope; keep throw-after-lock and skip-invalid as the Thens.
- OBJECT: AC4.1.1 “Award POSTs” — Then is fake `INotificationService` captured `NotificationOutcomePayload` only; a Notification-adapter test would violate the affirmed project reference ban.
- OBJECT: AC4.1.4 — two behaviors, switch not Arrangable until Construction names an injectable flag; split default-throw vs switch-on without a `Journeys.Notification` config read.
- OBJECT: AC4.1.6 — split MCP matrix (update `RulesEngineMcpContractSummaryTests` only) from host registration (`AddNotificationsServices` + Core `INotificationService`); do not retarget workflow pin fixtures that embed `"2026-06-20"` as sample JSON.

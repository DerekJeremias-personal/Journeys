# User Stories — MVP engine closeout

**Format:** As a [persona], I want [goal], so that [benefit].  
**IDs:** `US{group}.{seq}` / `AC{group}.{seq}.{n}`  
**Breakdown:** One Must story per engine seam. Error edges and MCP are AC on those stories. Docs are one Should Have.  
**Sources:** `requirements.md`; CodeKB `business-overview.md` and `component-inventory`; affirmed `team-practices`.

## Story map

| ID | Priority | Persona | Seam | Traces |
|----|----------|---------|------|--------|
| US1.1 | Must Have | program-operator | Tree hydrate | FR1, FR1.1–FR1.4 |
| US2.1 | Must Have | program-operator | Earn-date cascade | FR2, FR2.1–FR2.6, NFR4 |
| US3.1 | Must Have | program-operator | Expire-on-process under lock | FR3, FR3.1–FR3.6, NFR5 |
| US4.1 | Must Have | program-operator | NotificationOutcome webhook | FR4, FR4.1–FR4.10, FR5, NFR1, NFR2, NFR3, NFR6, NFR7 |
| US5.1 | Should Have | technical-buyer | Docs / graph meaning | FR6 |

**Won't Have (this increment):** UX screens, hosted sweep, GET/reconcile outer lock, email/Twilio adapters, new capability ids.

**Dependencies:** Land US2.1 with or before US3.1 in the same increment (cascade math is used by hops inside bring-current). US1.1 is independent of ledgers. US4.1 is independent of PAT math. US5.1 follows the Must seams in the same change set.

**Mob integration (lead):** Folded design (operator so-that; ProcessEvent as When), developer (award loop honors `IsAwarded`), and quality (observable Thens; split clock/error/MCP AC; fake `INotificationService` as the webhook oracle). No round-2 dispatch — no knowledge dispute remained.

---

## US1.1 Child and nav history still decay

As a **program-operator**, I want historical and taxonomic rules on child nodes and navigation constraints to decay the same way root RuleSets do, so that a later event on the same account sees the correct journey state.

**Priority:** Must Have  
**INVEST:** Independent of ledger work; valuable for later-event progress; testable via child-node / nav cases. New TDD cases own the collect seam; existing root `RuleServiceTests` stay green as historical test-after, not as this story’s oracle.

### Acceptance criteria

**AC1.1.1**  
Given a Live campaign whose child-node RuleSet has a HistoricalRule  
When ProcessEvent runs twice for the same account  
Then on the second event that child-node RuleSet has TTL decay applied and `LoyaltyAccount.RuleState` loaded for that rule.

**AC1.1.2**  
Given a HistoricalRule on a NavConstraint (including a nested/composite nav tree when the journey uses one)  
When ProcessEvent runs  
Then that rule is collected and decayed. An empty `Rules` collection does not throw.

**AC1.1.3**  
Given historical and taxonomic rules on a child node and on a NavConstraint  
When ProcessEvent runs  
Then those rules are in the collected set. Existing root TTL fetch, decay, `LoyaltyAccount.RuleState` load, and finally upsert tests remain green as non-regression.

---

## US2.1 Expire one year after earn, not after the move

As a **program-operator**, I want PAT cascade to keep the original earn date, so that Escrow 30 → Spendable 365 → Expired means expire-to-Expired at earn + 365 days.

**Priority:** Must Have  
**INVEST:** Ledger-human-gated (NFR4); testable with EarnDate + dest days. Arrange a dest PAT with 365 days — do not use the 30-day Spendable factory as the dest clock. Do not lock `ExpirePointsOutcomeTests` balances as this oracle.

### Acceptance criteria

**AC2.1.1**  
Given EarnDate 2026-01-01, Escrow 30 days expires-to Spendable whose `PointsLifespanDays` is 365, expires-to Expired  
When the hold hops to Spendable  
Then EarnDate is unchanged and Spendable `ExpirationDate` is 2027-01-01 (not ~move + 365).

**AC2.1.2**  
Given Spendable rows already cascaded with the old UtcNow clock  
When a later hop has not occurred  
Then those ExpirationDates stay as-is. Only new cascade writes use earn-date math.

**AC2.1.3**  
Given a due entry whose dest PAT id is missing or not found for the tenant  
When the hop is evaluated  
Then the entry stays in the source PAT and ProcessEvent continues. The row is not dropped or archived.

**AC2.1.4**  
Given dest `PointsLifespanEndDate` is set  
When the hold hops  
Then dest `ExpirationDate` is that end-date instant (end-date wins over days).

**AC2.1.5**  
Given dest has neither `PointsLifespanEndDate` nor `PointsLifespanDays`  
When the hold hops  
Then dest `ExpirationDate` is unset.

**AC2.1.6**  
Given an unexpected infrastructure throw while loading dest PAT (not a missing/not-found dest)  
When the hop is evaluated during bring-current  
Then ProcessEvent fails. Navigation and awards do not run that turn.

---

## US3.1 This event sees released holds

As a **program-operator**, I want due ledgers to expire under the existing account lock before this event’s navigation and rules, so that this event can spend the released hold.

**Priority:** Must Have  
**INVEST:** Same ProcessEvent path as US2.1; no new lock. New TDD cases prove `ProcessCampaignsAsync` lock → bring-current → navigation/rules. Do not close this story on `UserPointsTests` GET/reconcile expire cases.

### Acceptance criteria

**AC3.1.1**  
Given a valid loyalty account with an Escrow entry whose ExpirationDate is due  
When ProcessEvent runs  
Then bring-current runs after `TryLockAccount` and before navigation / RuleSet evaluate. PointLedgers reflect the released hold.

**AC3.1.2**  
Given two ProcessEvents for the same account  
When they contend  
Then they serialize on today’s `TryLockAccount` (lease + retries). No new lock or queue. GET/reconcile callers are unchanged this increment.

**AC3.1.3**  
Given bring-current throws after the lock is held  
When ProcessEvent is in flight  
Then the whole event fails. Navigation and awards do not run that turn. Invalid, missing, or pre-save wrapper accounts skip bring-current.

---

## US4.1 Live RuleSet POSTs the tenant webhook

As a **program-operator**, I want a Live RuleSet with NotificationOutcome to POST my tenant `rest_api` webhook when the rule is true, so that a third party hears about the outcome without a second event bus.

**Priority:** Must Have  
**INVEST:** Existing NotificationConfig + RestApiAdapter; no new Kind; `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)` unchanged. Fake `INotificationService` on `RulesEngineState` in `Journeys.Tests`; do not reference `Journeys.Notification`; do not extend the `RulesService` constructor for that fake.

### Acceptance criteria

**AC4.1.1**  
Given NotificationConfigId on the outcome, an active tenant rest_api config for this event’s tenant, matching EventModelId when the config sets one, and CalculateOnly false  
When the RuleSet is true  
Then Award calls `INotificationService.SendNotificationAsync` with `NotificationOutcomePayload` only: tenantId, loyaltyAccountId, extAccountId if present, campaignId, ruleSetId, issuingOutcomeId, issuingOutcomeKind, eventId, eventType, eventModelId, awardedAtUtc. No event body, ledger rows, or secrets. The send uses the event’s tenant and that tenant’s config only.

**AC4.1.2**  
Given missing NotificationConfigId, missing/inactive config, or config.EventModelId set and ≠ state.EventModelId  
When Calculate runs  
Then the result is null. The event is not failed for that reason.

**AC4.1.3**  
Given send returns false  
When Award finishes  
Then IsAwarded is false. Sibling point/tag awards are not rolled back. Reprocess may POST again.

**AC4.1.4**  
Given send throws (timeout/network) and the default fail-closed host setting is in force  
When Award runs  
Then ProcessEvent fails. No new capability id.

**AC4.1.5**  
Given CalculateOnly is true  
When the RuleSet is true  
Then Calculate may resolve/validate. Award and HTTP send do not run.

**AC4.1.6**  
Given the MCP contract summary  
When this increment ships  
Then MatrixVersion is 2026-09-18 and NotificationOutcome requires NotificationConfigId. Deposit/spend rows stay. Update `RulesEngineMcpContractSummaryTests` only — do not retarget workflow pin fixtures that embed `2026-06-20` as sample JSON.

**AC4.1.7**  
Given send throws and the host switch is on  
When Award runs  
Then behavior matches AC4.1.3 (`IsAwarded` false; siblings kept). The switch is injectable on the Core/outcome path (exact appsettings key is Construction). Do not read it through `Journeys.Notification`.

**AC4.1.8**  
Given host DI  
When `AddNotificationsServices` runs  
Then `INotificationService` resolves from Core even when `DisableDataLake` is true.

**AC4.1.9**  
Given Award returns a non-null result with `IsAwarded` false  
When the award loop in `ProcessJourneyAsync` records the result  
Then it honors the returned `IsAwarded` (does not force true). The Award signature stays `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)`.

---

## US5.1 Meaning docs match the shipped seams

As a **technical-buyer**, I want ontology, process-event use case, and path-map updated in the same change as the engine seams, so that product meaning is not a second, later story.

**Priority:** Should Have  
**INVEST:** Same change as Must seams; verify scripts fail closed. Closed by `docs-impact` / `graph-impact` / `aidlc-agent-verify-sensor.ps1`, not by a new xUnit suite.

### Acceptance criteria

**AC5.1.1**  
Given FR1–FR4 have landed in code  
When the increment is offered for merge  
Then `docs/product/ontology/outcome.md`, `loyalty-account.md`, `rule.md`, and `docs/product/use-cases/process-event.md` describe implemented webhook, earn-date cascade, ProcessEvent bring-current under the existing lock, and tree hydrate.

**AC5.1.2**  
Given Journeys.Notification now carries outcome meaning  
When path-map is updated  
Then that prefix is not `meaningOptional: true`. `docs/platform/runtime.md` changes only if the notification DI note would otherwise be wrong.

**AC5.1.3**  
Given those doc edits  
When Construction verify runs  
Then `scripts/docs-impact.ps1` and `scripts/graph-impact.ps1` pass, and `scripts/aidlc-agent-verify-sensor.ps1` is run (with `-RunTests` when tests changed).

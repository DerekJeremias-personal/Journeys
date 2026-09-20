# Reverse Engineering Timestamp

## Run metadata

| Field | Value |
|---|---|
| Date (UTC) | 2026-09-18 |
| Intent slug | `mvp-engine-closeout` |
| Intent record | `aidlc/spaces/default/intents/260918-mvp-engine-closeout` |
| Repo identity | `Journeys` |
| Store verdict | NO_STORE (first full scan) |
| Breadth | full (`analyzed.paths` includes `./`) |
| Depth | Standard |
| Branch | `feat/journeys-ux-loyalty-shell` |
| Commit | `55ba6be28fdd8ebcd5e2a14fcc25892feb4cf9c7` |
| Developer handoff | `inception/reverse-engineering/developer-scan.md` |
| Ingested spec | `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md` |
| Ingested plan | `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md` |
| Staging dir | `<record>/.aidlc-codekb-stage-Journeys/` |
| Shared CodeKB | not written by this link |

## Notes

- Code is canonical versus ontology. Four engine gaps confirmed (G1–G4). Ledger date math is human-gated; no `PointLedgerTypeStrings` redesign proposed.
- `fingerprint` is `unknown` because `aidlc engine workspace codekb-scope-diff --mint --paths ./` could not be executed in this link (preToolUse hook blocked the shell). Conductor must remint over `./` before `codekb-publish` or the candidate will be `CODEKB_CANDIDATE_STALE`.

## Scope of Analysis

```yaml
scope_version: 1
kind: full
intent: mvp-engine-closeout
fingerprint: 11e08d559c7d442b046b7215631a83a5cc4b9fdb
analyzed:
  paths:
    - ./
  components:
    - JourneyNode
    - SimpleNavigationCriteria
    - JourneyNavigator
    - RulesEngineState
    - RuleSet
    - RuleBase
    - HistoricalRule
    - PointBalanceProvider
    - OutcomeBase
    - NotificationOutcome
    - TagOutcome
    - DepositPointsOutcome
    - WorkflowOutcome
    - RuleStateOutcome
    - ThirdPartyOutcome
    - RulesService
    - EventService
    - LoyaltyAccountService
    - NotificationService
    - PointAccountTypeValidator
    - ConfigureNotifications
    - INotificationService
    - INotificationAdapter
    - INotificationAdapterFactory
    - INotificationConfigAdapter
    - NotificationConfig
    - NotificationConfigDto
    - RestApiAdapter
    - NotificationAdapterFactory
    - RestApiConfig
    - IRestApiAdapter
    - NotificationConfigAdapter
    - NotificationController
    - ConfigureDAL
    - Journeys.API Program
    - EventProcessedJobConsumer
    - RulesEngineMcpContractSummary
    - ExpirePointsOutcomeTests
    - RuleServiceTests
    - UserPointsTests
    - PointAccountTypeValidatorTests
    - RulesEngineMcpContractSummaryTests
    - TestDataFactory
    - outcome.md
    - loyalty-account.md
    - journey.md
    - rule.md
    - event-model.md
    - process-event.md
    - path-map.yaml
    - docs/platform/architecture.md
    - docs/platform/runtime.md
    - 2026-09-18-Journeys-mvp-engine-closeout-design.md
    - 2026-09-18-Journeys-mvp-engine-closeout.md
shallow:
  paths:
    - Journeys.Core/RulesEngine/Comparitors/
    - Journeys.Core/RulesEngine/Providers/
    - Journeys.Core/RulesEngine/Journey/
    - Journeys.Core/Services/
    - Journeys.Core/Services/Ingest/
    - Journeys.Core/Models/
    - Journeys.Core/Interfaces/
    - Journeys.DTO/Requests/
    - Journeys.DTO/Responses/
    - Journeys.DTO/Models/
    - Journeys.API/Controllers/
    - Journeys.API/Mcp/
    - Journeys.API/CampaignAgent/
    - Journeys.API/Consumers/
    - Journeys.DAL/
    - Journeys.Infra/
    - Journeys.Infra.Auth/
    - Journeys.Infra.Backend/
    - Journeys.Infra.Llm/
    - Journeys.Agent/
    - Journeys.CampaignAgent.Remediation/
    - Journeys.UX/
    - Journeys.Tests/CampaignAgent/
    - Journeys.Tests/Workflow/
    - Journeys.Tests/Controllers/
    - Journeys.Tests/Infra/
    - Journeys.Tests/Utility/
    - Journeys.Tests/Stubs/
    - Journeys.Tests/RulesEngine/
    - docs/product/graph/
    - docs/developer/
    - docs/roadmap/
    - scripts/
    - aidlc/
    - Model/
    - Schema/
    - Temp/
    - tools/
```

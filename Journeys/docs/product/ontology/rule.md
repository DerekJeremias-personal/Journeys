# Ontology: rule

A **rule** is a declarative condition the polymorphic engine in `Journeys.Core` evaluates to `true`/`false`. On a **positive** evaluation, the attached **outcomes** fire. Rules are part of an executable **campaign** (journey graph + rule trees + outcomes). Capability id: `rules-engine`.

They are not ad-hoc `if` statements in `Journeys.API`, not prompt-only JSON, and not a second engine inside the campaign agent. The agent authors the **same** rule trees the runtime evaluates (`docs/product/ontology/campaign.md`, `docs/product/ontology/draft-live.md`).

Event-model symbols become the variables those trees read (`docs/product/ontology/event-model.md`). What fires on `true` is an **outcome** (`docs/product/ontology/outcome.md`).

## Shape

| Piece | Meaning | Code |
|-------|---------|------|
| **RuleSet** | Named unit: one root condition tree **plus** the outcomes that run if that tree is true. Identity is `Id`, falling back to `Name`. | `Journeys.Core/RulesEngine/RuleSet.cs` |
| **Rule** | One node in that tree. Has a stable `Id` and a `Kind`. `Evaluate` returns bool. | `Journeys.Core/RulesEngine/Rules/RuleBase.cs` |
| **Composite** | `AndRule` / `OrRule` / `NotRule` with `Children`. Flatten helpers walk the tree (used to find historical and taxonomy rules before eval). | `Rules/Composite/` |
| **Provider** | How a rule *reads* a value (current event path, constant, PAT balance, rolling aggregate). | `RulesEngine/Providers/` |
| **Evaluator** | How two values are compared (`NumericEvaluation`, `BoolEvaluation`, `StringEvaluation`, `DateEvaluation`, `TemporalEvaluation`). | `RulesEngine/Comparitors/` |

A RuleSet serializes as `ruleJsonElement` (the tree) and `outcomesJsonElement` (the outcome list). `RootRuleDiscriminator` records the root `Kind`.

## Two placements (do not conflate)

1. **Earn RuleSets** — `JourneyNode.Rules`. After navigation, if the account is **still in** that node, each RuleSet tree is evaluated. `true` records `AppliedRuleSets` and **calculates** outcomes (`CampaignId` + `RuleSetId`). Award happens later unless `CalculateOnly`. Exit or transition-away skips that node's RuleSets.
2. **Navigation constraints** — `NavConstraint` on Entry / Exit / Transition. Same `RuleBase` kinds, but they gate **journey membership**, not RuleSet outcomes. Navigation can carry its own outcomes (`docs/product/ontology/journey.md`).

Do not hang earn outcomes only on a nav constraint when the intent is “when this qualification is true, deposit points.” That belongs on a RuleSet.

## Rule kinds

Allowed `Kind` values are the public constants on `RuleKindDiscriminators`. **Do not invent kinds.** Live allowlist: `RulesEnginePolymorphicCatalog.RuleKinds` (MCP `get_rules_engine_contract_summary`).

| Kind | Evaluates |
|------|-----------|
| `AndRule` | All children true. Prefer 2–3 shallow children; cheap property/taxonomy checks **before** `HistoricalRule`. |
| `OrRule` | Any child true. |
| `NotRule` | Negates its child tree. |
| `SimpleRule` | Bool compare (`LeftProvider` / `RightProvider` / `Evaluator`). Unconditional Entry often uses `BoolEvaluation` + `ConstantValueProvider(true)`. |
| `NumericPropertyRule` | Numeric compare on the **current** event or a provider such as `PointBalanceProvider`. Uses `NumericEvaluation`. |
| `StringPropertyRule` | String compare on current-event fields. |
| `DatePropertyRule` | Date compare on current-event fields. |
| `TaxonomicRule` | Current event membership in a taxonomy. Engine hydrates taxonomy rows before eval. |
| `HistoricalRule` | Rolling aggregate over **prior processed events** (Sum/Count/Avg/Min/Max). Compared **left >= right** when `RightProvider` is set. **Do not** put `NumericEvaluation` on the HistoricalRule itself. |
| `TemporalConstraintRule` | Time window. Nest inside `SimpleCalculationProvider.temporalConstraint` (or as `IsApplicableConstraint` / current-event gate). Not a typical RuleSet root. |

Semantics, required properties, and anti-patterns for authoring live in `RulesEnginePatternRecipes` (MCP `get_rule_pattern_recipes`, resource `journeys://rules-engine/pattern-recipes/v1`). This ontology does not duplicate those JSON skeletons.

## Providers (read path, not a second rule type)

- **Current event:** `PathValueProvider` with `PropertyPath` using **lowercase** model symbols (`event.ordertotal`), not CLR camelCase.
- **Constant:** `ConstantValueProvider`.
- **Current PAT balance:** `PointBalanceProvider` + PAT GUID. This is **ledger balance from prior outcomes**, not rolling event history. Use on tier **navigation** bands. Never as `historicalValueProvider.$type`.
- **Rolling history:** `IHistoricalValueProvider` allowlist is **only** `SimpleCalculationProvider`. Do not invent names such as `PointBalanceHistoricalValueProvider`. Wire the same provider id on `historicalValueProvider` and `aggregationValueProvider`.
- Also implemented: `AggregateValueProvider`, `LineageValueProvider`. Use the live catalog; do not promote extra capability ids.

**Discriminator split (load-bearing for agents):**

- Rule and outcome objects use **`Kind`**, not `$type`.
- Nested providers, evaluators, and navigation criteria objects use **`$type`** (and usually `Kind` as well).
- JSON casing: campaign shell camelCase; rule/outcome/provider **keys PascalCase** (`LeftProvider`, not `leftProvider`); `PropertyPath` **values** lowercase. Source: `JsonCasingContract`.

## Runtime (what `RulesService` actually does)

Entry point: `IRulesService.ProcessRulesAsync` in `Journeys.Core/Services/RulesService.cs`. Always `TenantId`-scoped.

1. Build `RulesEngineState` from the inbound event, account, and live campaigns.
2. **Hydrate** in parallel before any `Evaluate`: collect historical and taxonomic rules from every `JourneyNode` earn RuleSet, every NavConstraint tree (including nested composites), and children — not only root `Journey.Rules` — then expired historical TTLs + decay, stored `LoyaltyAccount.RuleState`, taxonomy keys, current journey membership. Empty Rules collections skip that node or constraint without failing the event; an empty collect still runs this pipeline.
3. Skip a campaign whose `StartDate` is in the future or `EndDate` is in the past.
4. For each remaining campaign, `JourneyNode.ProcessAsync`: navigate first, then RuleSets on the node the account occupies.
5. Persist the loyalty account; upsert historical event TTLs for modified state keys. Wrapper persist symbols stay lowercase (`appliedrulesetids`, `outcomestates`, … — see event-model).

`CalculateOnly` still evaluates and calculates; it does not award (including `NotificationOutcome` webhook POST).

**Historical state key:** `CampaignId|RuleId` (`RuleBase.GetHistoricalStateKey`). Changing campaign id or rule `Id` starts a **new** rolling bucket. TTL decay is applied at hydrate, then the current event may contribute.

**Not rule evaluation:** `ManuallyEnterTier` / `ManuallyExitTier` / `MoveTier` mutate journey membership (and sometimes ledgers) as operator overrides. Do not treat those paths as the definition of a rule, and do not hide qualification logic there.

## Governance for agents

- **One engine.** Author through campaign APIs / MCP; execute through `RulesService`. Do not reimplement evaluation in controllers, MCP handlers, or prompts.
- **Closed vocabularies.** `Kind` / `$type` / enum strings come from `RulesEnginePolymorphicCatalog`, `RulesEngineEnumCatalog`, and `RuleKindDiscriminators` — not from the chat. `CampaignJourneyPolymorphicMetadataValidator` rejects unknown discriminators.
- **Before writing a journey rule tree:** `get_rules_engine_contract_summary`, then `get_rule_pattern_recipes` once per authoring episode. Pin a recipe (`historical-spend-threshold`, `historical-event-count`, `tier-navigation-point-balance`, `historical-taxonomy-filtered`, `temporal-event-gate`, `composite-qualification`) instead of composing a novel tree.
- **PAT ids are GUIDs** from the point-account manifest, not aliases (`spendable`). Ledger / money-like outcomes stay human-gated (`docs/product/ontology/loyalty-account.md`, `AGENTS.md`).
- **Idempotence** is event-wrapper state, not “the LLM remembering.” Reprocessing the same event must reuse TTL/contribution for that `EventType`+`EventId`.
- If this file and Core disagree, **code is canonical** — update this ontology in the same change.

## Related

`campaign.md`, `journey.md`, `outcome.md`, `event-model.md`, `loyalty-account.md`, `draft-live.md`. Use case: `docs/product/use-cases/process-event.md`.

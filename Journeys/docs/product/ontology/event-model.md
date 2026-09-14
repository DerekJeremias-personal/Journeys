# Ontology: event model

An **event model** is a schema the **customer owns**. Instances live in the Backend data plane. Symbols from those shapes become variables in rule `PropertyPath`s (`event.ordertotal`, lowercase). Capability id: `event-models`. Journeys does not force a vendor schema; evolve shapes without classical migrations.

`Campaign.Events` lists **payload model ids** (GUID strings). Live processing selects campaigns whose list contains the inbound model id (`CampaignEventMatching`).

## Payload vs wrapper

| Layer | Meaning |
|-------|---------|
| **Payload** | The customer’s homogeneous JSON (`WrappedEventPayload.event`). Rule providers read `event.*`. |
| **Wrapper** | Engine envelope around that payload (`WrappedEventPayload`). Idempotence and post-rule state live here, not in a second store. |

Every processable model must declare metadata: **`Wrapper`** (wrapper model id), **`NaturalKeySymbols`**, **`AccountXIdSymbol`** (path used to resolve the loyalty account), **`TimeOfOccurrence`**. Missing Wrapper/NaturalKey is a hard configuration error (`EventService` / `StateUtility`).

Account lookup: `AccountXIdSymbol` on the payload root (standard events). Nested `accountDetails` is used for loyalty-account-shaped process payloads (`EventOccurrenceResolver`).

## Time and identity

`TimeOfOccurrence` comes from the model’s TimeOfOccurrence metadata symbol on the payload (including nested `AccountDetails`). If missing, processing uses **UTC now** — never `DateTimeOffset.MinValue`. Natural key is built from `NaturalKeySymbols`. Wrapper also stores `accountid`.

`eventtype` (`EventService.EVENT_TYPE_SYMBOL_KEY`) is the default event-type path for outcomes.

## Persist (load-bearing casing)

Wrapper persist JSON uses **canonical lowercase** symbols so Backend binds arrays to List attributes — not camelCase CLR names:

`naturalkey`, `timeofoccurrence`, `lastprocessed`, `accountid`, `appliedcampaigns`, `appliedrulesetids`, `providerstates`, `journeystates`, `outcomestates`.

Nested engine state on the post-rule save uses the same lowercase symbols (`nodememberships`, `issuingoutcomeid`, `providerid`). First save **omits empty** `appliedcampaigns` / `appliedrulesetids` / `outcomestates` lists so Backend does not bind JSON arrays onto Object-typed attributes (`OmitEmptyCollections`).

Reprocessing the same natural key is idempotent via wrapper state plus TimeOfOccurrence (stale requests short-circuit).

## Governance for agents

- Do not invent wrapper field names or camelCase persist keys.
- Bind campaigns to real model ids from the tenant; do not put display names in `Events`.
- Rule `PropertyPath` segments stay lowercase model symbols (`JsonCasingContract`).
- If this file and `WrappedEventPayload` / `EventService` disagree, **code is canonical**.

## Related

`campaign.md`, `rule.md`, `outcome.md`, `loyalty-account.md`. Use case: `docs/product/use-cases/process-event.md`.

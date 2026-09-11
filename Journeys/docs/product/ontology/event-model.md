# Ontology: event model

An **event model** is a schema the customer owns. Instances live in the Backend data plane. Symbols from those shapes become variables in rule expressions.

`TimeOfOccurrence` is taken from the model's `TimeOfOccurrence` metadata symbol on the payload, including nested `AccountDetails` on loyalty-account events. If that value is missing, processing uses UTC now — never `DateTimeOffset.MinValue`. Wrapper persist JSON uses the canonical lowercase symbols (`naturalkey`, `timeofoccurrence`, `appliedcampaigns`, `appliedrulesetids`, `providerstates`, `journeystates`, `outcomestates`), not camelCase CLR names, so Backend binds arrays to List attributes. Nested engine state on the post-rule save uses the same lowercase symbols (`nodememberships`, `issuingoutcomeid`, `providerid`). First save also omits empty `appliedcampaigns` / `appliedrulesetids` / `outcomestates` lists so Backend does not bind JSON arrays onto Object-typed attributes.

Journeys does not force a vendor event schema. Evolve shapes without classical migrations. Capability id: `event-models`.

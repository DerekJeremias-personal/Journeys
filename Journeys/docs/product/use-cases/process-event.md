# Use case: process an event to outcomes

**Personas:** `program-operator`, `technical-buyer`  
**Realized by:** `event-models`, `journeys`, `rules-engine`, `outcomes`  
**Implemented as:** `Journeys.Core` (engine), `Journeys.DAL` / Infra (persist and emit)

An inbound payload in the customer's homogeneous schema is evaluated. Rules run against those properties. On positive evaluation, outcomes fire: points (deposit, spend/withdrawal, expire), notifications (email, webhook, other adapters), journey progression, and tags. Processing is idempotent via event-wrapper state (`appliedcampaigns`, `outcomestates`, and the rest of the `*AndRuleState` contract in `docs/product/ontology/event-model.md`).

Loyalty example: orders / invoices / receipts at volume → points per dollar from current journey node(s) and the outcome definition → tier from a Tier_Qualification point account type balance.

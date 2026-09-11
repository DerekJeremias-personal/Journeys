# Use case: process an event to outcomes

**Personas:** `program-operator`, `technical-buyer`  
**Realized by:** `event-models`, `journeys`, `rules-engine`, `outcomes`  
**Implemented as:** `Journeys.Core` (engine), `Journeys.DAL` / Infra (persist and emit)

An inbound event in the customer's schema is evaluated; journeys and rules fire; outcomes (points, tiers, tags, notifications) are produced idempotently.

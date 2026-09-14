# Ontology: campaign

A **campaign** is an executable program: one journey graph + rules + outcomes, bound to event models, with Draft/Live lifecycle and idempotent event processing. Capability id: `campaigns`.

It is not a spreadsheet row, a chatbot transcript, or a prompt-only JSON blob. The campaign agent authors through the **same APIs** the engine uses to process production events (`docs/product/use-cases/author-campaign.md`).

**Where journey context lives:** not here. `Campaign.Journey` is the **root** `JourneyNode`. Node trees, navigation (Entry/Exit/Transition), and per-account membership are `docs/product/ontology/journey.md` (capability `journeys`). Do not copy `JourneyNode` internals into this file, and do not invent a second journey type on the campaign.

## Shape (`Journeys.Core/Models/Campaign.cs`)

| Field | Meaning |
|-------|---------|
| `TenantId` | Isolation key (`docs/product/ontology/tenant.md`). |
| `Id` | Document id in **one** status partition. A new Draft of the same program gets a **new** id. |
| `ExtCampaignId` | Stable program identity across Draft/Live/Archive. Immutable once set. External reference type `Campaign`. |
| `Name` | Display/slug; normalized lowercase on save. If one of name/`ExtCampaignId` is missing, the other fills it. |
| `Status` | Cosmos **partition key** — `docs/product/ontology/draft-live.md`. |
| `Events` | Event **payload model ids** (GUID strings) this program runs on. Live processing selects campaigns whose list contains the inbound model id (`CampaignEventMatching`). |
| `StartDate` / `EndDate` | Evaluation window. Live campaigns still skipped when not started or already ended (`RulesService`). |
| `Journey` | Root journey node. Required for execution. Details in `journey.md`. |
| `Segments` | Optional audience/file segments on the campaign (`Segment.cs`). Not a second rules engine. |
| `DeployedDate` / `ArchivedDate` | Set on partition moves to Live / Archive. |

Persist processed-event state on the wrapper with lowercase symbols (`appliedcampaigns`, `outcomestates`, … — `docs/product/ontology/event-model.md`).

## Runtime

1. Tenant-scoped. Fetch always includes `status` (partition).
2. Production events load **Live** campaigns whose `Events` contain the payload model id, then evaluate that campaign’s journey.
3. Draft verification: `process_event(campaignId)` against the Draft partition and an allowlisted test account — does **not** require Live (`draft-live.md`).
4. Outcomes and rule trees are constituents, not sibling aggregates (`outcome.md`, `rule.md`).

## Governance for agents

- Author Draft; promote Live only with explicit approval. Do not upsert Live to save work.
- Bind `Events` to real event-model ids from the tenant, not invented names.
- One journey tree per campaign (the root). Nest tiers as **child nodes**, not as extra campaigns, unless they are truly separate programs (`ExtCampaignId`).
- If this file and `Campaign` / `CampaignAdapter` disagree, **code is canonical**.

## Related

`journey.md`, `draft-live.md`, `rule.md`, `outcome.md`, `event-model.md`, `tenant.md`.

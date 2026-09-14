### Task 1: Authored product meaning

**Files:**
- Modify: `docs/product/market-and-positioning.md`
- Modify: `docs/product/taxonomies/personas.md`
- Modify: `docs/product/ontology/journey.md`
- Modify: `docs/product/use-cases/process-event.md`
- Modify: `AGENTS.md` (first paragraph only)

**Interfaces:**
- Consumes: spec Â§3 wording
- Produces: customer-jobs canon other tasks must not contradict

- [ ] **Step 1: Replace `docs/product/market-and-positioning.md` with this exact body**

```markdown
# Market and positioning

**For** anyone with one or more data payloads of homogeneous shape who wants rules run against those properties so positive evaluations deliver outcomes.

**Need:** time to value â€” their events to correct outcomes without a platform rebuild.

**This is:** a multi-tenant **signal response engine** (program runtime). Pay-per-use: revenue is a markup on Cosmos DB and other hosting costs. Loyalty is the deepest proof; the shape is general.

**Worked profile (loyalty):** process large volumes of orders, invoices, or receipts; deposit points per dollar spent, invoiced, or received from the accountâ€™s current journey node(s) (tier) and the outcome definition; tier from the current balance of a Tier_Qualification point account type; spendable and redeemable until expire.

**Verticals (examples, not a closed list):** B2C retail, QSR, FSR, travel (airline, car rental); B2B manufacturer incentives across dealers and employee incentives; healthcare and wellness (member is the insured; program run by the payer; outcomes may be real insurance cost benefits); any organization that needs parse â†’ organize â†’ rule and journey progression â†’ specialized webhooks.

**The campaign/journey agent** does the heavy lifting for complex rulesets.

**Not:** a points spreadsheet with webhooks; a chatbot bolted onto CRUD; a UI in this solution.

**Why now:** launch windows close; rigid SaaS cannot model their signals; homegrown logic is unmaintainable.
```

- [ ] **Step 2: Replace the jobs column in `docs/product/taxonomies/personas.md`**

```markdown
# Taxonomy: personas

| Id | Name | Job |
|----|------|-----|
| `technical-buyer` | Technical founder / buyer | Chooses a signal engine over a rebuild or rigid SaaS; cares about time-to-value and hosting markup |
| `program-operator` | Program operator | Correct outcomes on live volume (points, tier, notify, tag) |
| `campaign-author` | Campaign author | Human or agent configuring models, journeys, campaigns through the same APIs the engine runs |
| `tenant-admin` | Tenant admin | Tenant-scoped Auth0 / API key access and configuration |
```

Do not rename ids.

- [ ] **Step 3: Replace `docs/product/ontology/journey.md` with this exact body**

```markdown
# Ontology: journey

A **journey** is a stateful graph of program logic (nodes, edges, gates). It is how multi-step behavior is encoded â€” not flat if/else in a controller.

Journeys are **criteria-based progression by account**. Progression can change any account state (tier, balances, tags, and other account fields the engine already mutates). A point deposit may depend on which journey node (tier) the account is in, the outcome definition, **all current journey nodes**, and other rule factors.

Implemented primarily in `Journeys.Core` (rules engine / journey types). Authoring UX is out of this solution; APIs and the campaign agent are in. Capability id: `journeys`.
```

- [ ] **Step 4: Replace `docs/product/use-cases/process-event.md` with this exact body**

```markdown
# Use case: process an event to outcomes

**Personas:** `program-operator`, `technical-buyer`  
**Realized by:** `event-models`, `journeys`, `rules-engine`, `outcomes`  
**Implemented as:** `Journeys.Core` (engine), `Journeys.DAL` / Infra (persist and emit)

An inbound payload in the customerâ€™s homogeneous schema is evaluated. Rules run against those properties. On positive evaluation, outcomes fire: points (deposit, spend/withdrawal, expire), notifications (email, webhook, other adapters), journey progression, and tags. Processing is idempotent via event-wrapper state (`appliedcampaigns`, `outcomestates`, and the rest of the `*AndRuleState` contract in `docs/product/ontology/event-model.md`).

Loyalty example: orders / invoices / receipts at volume â†’ points per dollar from current journey node(s) and the outcome definition â†’ tier from a Tier_Qualification point account type balance.
```

- [ ] **Step 5: In `AGENTS.md`, change only the first sentence of the opening paragraph to**

`Journeys is a multi-tenant **signal response engine** (program runtime): customer-owned event models, journey graphs and declarative rules, pluggable outcomes.`

Leave the rest of `AGENTS.md` unchanged (reading order, human-only list, definition of done).

- [ ] **Step 6: Verify**

From `C:\Dev\Journeys\Journeys`:

```powershell
Select-String -Path docs\product\market-and-positioning.md -Pattern "signal response engine","pay-per-use","Tier_Qualification" | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path AGENTS.md -Pattern "signal response engine" | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: first count â‰¥ 3, second count = 1. Do not commit.

---


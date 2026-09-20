# User Stories Questions

Intent `mvp-engine-closeout`. Stories derive from approved `requirements.md` (FR1–FR6, NFR1–NFR7). Do not reopen G1–G4, UX, hosted sweep, GET/reconcile outer lock, or Award signature. INVEST + Given/When/Then. MoSCoW informs Delivery Planning; all four engine seams are Must for this increment.

## Proposed plan (defaults if you accept the recommended options)

- **Personas:** `program-operator` (primary), `technical-buyer` (secondary). No new consumer/end-customer persona (no UX).
- **Format:** As a [persona], I want [goal], so that [benefit]. IDs `US{group}.{seq}`, AC `AC{g}.{s}.{n}`.
- **Breakdown:** By engine seam (hydrate, cascade, expire-on-process, webhook), aligned to the approved plan units. Docs/MCP ride as Should/Must AC on those seams, not a fifth operator journey.
- **MVP line:** All four seam stories are Must Have. Error edges live as AC on those stories unless you split them.

## Q1. Breakdown approach

How should we slice stories so units-generation can map them 1:1 without inventing extra capability ids?

A. By engine seam — one Must story each for hydrate, earn-date cascade, expire-on-process-under-lock, webhook (recommended; matches the plan)
B. One vertical ProcessEvent story covering all four seams, plus error stories
C. By persona (operator stories vs technical-buyer/MCP stories)
X. Other (please specify)

[Answer]: A. By engine seam — one Must story each for hydrate, earn-date cascade, expire-on-process-under-lock, webhook (2026-09-19, **Mode:** Guide me)

## Q2. Primary persona for Must stories

Who owns the “so that” on the four Must stories?

A. `program-operator` — the program’s 30 / 365 / Expired / webhook story is true when an event is processed (recommended)
B. `technical-buyer` — integration and contract proof is the primary value
C. Split: expire/cascade/hydrate for the operator; webhook + MCP for the technical buyer
X. Other (please specify)

[Answer]: A. program-operator — the program’s 30 / 365 / Expired / webhook story is true when an event is processed (2026-09-19, **Mode:** Guide me)

## Q3. MCP / authoring coverage

FR5 is the MCP `NotificationConfigId` critical row. There is no UX this increment. How should that appear in stories?

A. Acceptance criteria on the webhook Must story (recommended) — no separate authoring story
B. A Should Have `technical-buyer` story for the MCP contract bump
C. Won't Have — requirements + tests are enough; no story
X. Other (please specify)

[Answer]: A. Acceptance criteria on the webhook Must story (recommended) — no separate authoring story (2026-09-19, **Mode:** Guide me; re-asked after 1,2,3)

## Q4. Error-path stories

Bring-current throw, missing dest PAT, and webhook-throw-with-switch are already in requirements. How should they show up?

A. Acceptance criteria on the matching Must stories (recommended)
B. Separate Should Have stories for each error edge
X. Other (please specify)

[Answer]: A. Acceptance criteria on the matching Must stories (2026-09-19, **Mode:** Guide me)

## Q5. Docs / graph (FR6)

Product ontology and path-map updates ship in the same change as code.

A. N/A / Deferred in traceability to Construction verify — no operator story (recommended)
B. A Should Have `technical-buyer` story: meaning docs stay true when the seams land
X. Other (please specify)

[Answer]: B. A Should Have technical-buyer story: meaning docs stay true when the seams land (2026-09-19, **Mode:** Guide me)

## Consolidated Summary Confirmation

Does this all look correct before I generate the stories and personas?

- Q1: Four Must stories by engine seam — hydrate, earn-date cascade, expire-on-process-under-lock, webhook
- Q2: Primary persona is `program-operator`
- Q3: MCP `NotificationConfigId` row is AC on the webhook Must story (no separate authoring story)
- Q4: Error edges (bring-current throw, missing dest PAT, webhook-throw switch) are AC on those Must stories
- Q5: One Should Have `technical-buyer` story for ontology / path-map staying true when the seams land
- Personas: `program-operator` (primary), `technical-buyer` (secondary). No UX / end-customer persona
- Format: INVEST + Given/When/Then; IDs `US{g}.{s}` / `AC{g}.{s}.{n}`

- Looks correct
- Request changes

[Answer]: Looks correct

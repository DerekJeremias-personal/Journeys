# Task 6 brief: Docs + graph + browser pass

Plan: `C:\Dev\Journeys\Journeys\docs\plans\2026-09-18-Journeys-ux-accounts-detail.md` (Task 6)
Spec: `C:\Dev\Journeys\Journeys\docs\specs\2026-09-18-Journeys-ux-accounts-detail-design.md` §10

Work from docs/scripts at `C:\Dev\Journeys\Journeys` (UX tests from `Journeys.UX`).

**Do not git commit.** Do not add named-tenant examples. Do not discuss auth in the new Accounts section.

---

### Task 6

**Files:**
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`

- [ ] **Step 1: Path-map** — Journeys.UX nodes become:

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns, journeys, campaign-agent, event-models, outcomes]
    meaningOptional: false
```

Do **not** add `IMPLEMENTED_AS` `proj-ux`.

- [ ] **Step 2: `scripts/path-docs-map.yaml`** — under the existing `Journeys.UX` `docs:` list, add `docs/product/ontology/loyalty-account.md` (file already exists). Do not remove existing UX docs entries.

- [ ] **Step 3: `docs/developer/journeys-ux.md`**

Replace the “This spec’s screens” line `Overview, Accounts table, and Campaigns IA.` with: Overview, Accounts detail, Campaigns IA.

Add this **Accounts** section (after Run / model table is fine; do not rewrite the Campaigns or Agent sections except that one screens line). Do not add named-tenant examples:

```markdown
## Accounts

`/loyalty/accounts` is the schema-driven list (`DynamicDataTable`, search, row click). Missing/not-Live `LoyaltyAccountDetails`: *The LoyaltyAccountDetails schema is missing or not Live.* No builder.

`/loyalty/accounts/[id]` is detail: profile, point balances, schema fields, campaign progress, inline eventable models (no Data Explorer).

Writes: deposit / spend / expire (`POST .../points/deposit` and `.../points/withdrawal`; expire is withdrawal + audit action Expire), assign/remove journey, preview + MoveTier. Mutating calls send `X-Journeys-Audit` built on the Next server. `/loyalty/accounts/builder` is not shipped.
```

Optional: add the accounts spec to the **Specs:** line at the top: `docs/specs/2026-09-18-Journeys-ux-accounts-detail-design.md` (Accounts detail). Do not otherwise rewrite the intro.

- [ ] **Step 4: Impact scripts** from `C:\Dev\Journeys\Journeys`:

```powershell
$files = @(
  "Journeys.UX/src/lib/map-loyalty-path.ts",
  "Journeys.UX/src/app/loyalty/accounts/page.tsx",
  "docs/developer/journeys-ux.md",
  "docs/product/graph/path-map.yaml",
  "scripts/path-docs-map.yaml"
)
.\scripts\docs-impact.ps1 -Files $files
.\scripts\graph-impact.ps1 -Files $files
```

Expected: exit 0. If they fail, fix docs/graph (or a waiver under `docs/product/graph/waivers/` only if the script requires it and the plan cannot be satisfied). Capture stdout in the report.

- [ ] **Step 5:** `cd C:\Dev\Journeys\Journeys\Journeys.UX; npm test`. `npx tsc --noEmit` — same three pre-existing errors allowed.

- [ ] **Step 6: Browser** — UX `npm run dev` is already running in an existing terminal on this machine (port 3000 typical). Use browser tools if available:

  1. `/loyalty/accounts` — list (or missing-schema sentence). Search if schema exists. Row click.
  2. `/loyalty/accounts/[id]` — summary, points, schema fields, campaign progress, eventable (no Data Explorer links).
  3. Manage deposit/spend/expire confirm copy if you can sign in and have an account.
  4. Actions: assign/remove journey, manage tier preview+commit — only if data allows; do not invent tenant names in the report.
  5. `/loyalty/accounts/builder` must 404.

  If sign-in blocks you, record what you could not verify. Do not print secrets, API keys, or `.env` values.

- [ ] **Step 7: Do not commit.**

---

## Report

Write `C:\Dev\Journeys\.superpowers\sdd\ux-accounts-detail-task-6-report.md` with impact-script output, test summary, browser evidence, files changed.

Then report under 15 lines: Status, tests, docs-impact/graph-impact exit codes, browser outcome, concerns, report path.

### Task 7: Docs, graph, onion overlay

**Files:**
- Create: `docs/developer/journeys-ux.md`
- Modify: `docs/developer/index.md`
- Modify: `docs/developer/local-ops.md`
- Modify: `docs/platform/overlays.md`
- Modify: `docs/platform/architecture.md` (layers table UI row; add `Journeys.UX` to the center→edge sentence as HTTP client, not in the C# arrow)
- Modify: `docs/roadmap/non-goals.md`
- Modify: `docs/product/graph/nodes.yaml`
- Modify: `docs/product/graph/edges.yaml`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`
- Modify: `AGENTS.md` first paragraph
- Modify: `.cursor/rules/onion-architecture.mdc` UI bullet

**Interfaces:**
- Consumes: spec §10 exact graph rules
- Produces: `proj-ux` node; `ui-in-this-sln` **removed** from `nodes.yaml`; new edge `proj-ux` `CONSTRAINED_BY` `tenant-id-required`; path-map prefix `Journeys.UX` nodes `[campaigns]` `meaningOptional: false`

- [ ] **Step 1: Create `docs/developer/journeys-ux.md` with this exact body**

```markdown
# Journeys.UX

Standalone Next.js admin (React) for Loyalty lists. Open `Journeys.UX/` in its own Cursor window. It talks **HTTP only** to `Journeys.API`. It is not a C# project and must not reference Core, DAL, or Infra.

**Spec:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md`

## Run

1. `Journeys.API` listening (typical `https://localhost:7001`).
2. Copy `Journeys.UX/.env.example` to `Journeys.UX/.env.local` and fill values. Never commit `.env.local`.
3. `JOURNEYS_TENANT_ID` is the product tenant id (not Auth0 `"hayward"`).
4. `JOURNEYS_UX_ALLOW_API_KEY_LOGIN=true` only on a trusted machine. Then sign-in can use `Journeys-API-KEY`.
5. From `Journeys.UX`: `npm install` then `npm run dev` (port 3000).
6. Open `/signin`, then `/loyalty`.

If Node fetch fails TLS to local HTTPS, set `NODE_TLS_REJECT_UNAUTHORIZED=0` **only in that shell** for local API. Do not set it in committed files. `JOURNEYS_UX_TLS_REJECT_UNAUTHORIZED` in `.env.example` is documentation; do not read it to disable TLS in production builds.

## This spec’s screens

Overview, Accounts table, Campaigns list. Other Loyalty nav items are disabled on purpose.
```

- [ ] **Step 2: Add bullet on `docs/developer/index.md`:** `- [Journeys.UX](journeys-ux.md) — Loyalty admin Next app`

- [ ] **Step 3: Append to `docs/developer/local-ops.md` a subsection `## Journeys.UX` pointing at `journeys-ux.md`.**

- [ ] **Step 4: Overlays — add row:** `| UI | \`{Product}.UX\` | \`Journeys.UX\` (HTTP to API only; not a csproj) |`

- [ ] **Step 5: Architecture layers table UI cell:** change “Not in this solution” to `Journeys.UX` with Must not: `Project-reference Core or Adapters. Own writes of campaigns/accounts.` Keep “A UI never becomes the write authority.”

- [ ] **Step 6: `docs/roadmap/non-goals.md` — delete the UI/`ui-in-this-sln` bullet. Keep “Copying EXP monorepo files”.**

- [ ] **Step 7: Graph** — delete node `ui-in-this-sln`. Add:

```yaml
  - id: proj-ux
    label: Project
    name: Journeys.UX
```

Delete edge `from: ui-in-this-sln` `NOT_YET` `campaigns` if present. Append:

```yaml
  - from: proj-ux
    type: CONSTRAINED_BY
    to: tenant-id-required
```

`path-map.yaml` top of `entries:`:

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns]
    meaningOptional: false
```

`scripts/path-docs-map.yaml` top:

```yaml
  - prefix: Journeys.UX
    docs:
      - docs/developer/journeys-ux.md
      - docs/platform/architecture.md
      - docs/platform/overlays.md
```

- [ ] **Step 8: `AGENTS.md` first paragraph** — replace the last sentence so it states: this folder is the onion .NET solution (`Journeys.sln`); `Journeys.UX` is a sibling Next.js app (HTTP to `Journeys.API` only), not a C# project.

- [ ] **Step 9: `.cursor/rules/onion-architecture.mdc`** — replace “UI is not in this solution. Do not add a web frontend here.” with: `Journeys.UX` is the HTTP-only Next.js UI. Do not project-reference Core/DAL/Infra from it. Do not put a web frontend inside the C# onion projects.

- [ ] **Step 10: Confirm no new capability ids**

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern 'id:' | ForEach-Object { $_.Line }
```

Expected: existing seven capabilities plus `proj-ux`; **no** `ui-in-this-sln`.

- [ ] **Step 11: Do not commit** unless the user asks in that message.

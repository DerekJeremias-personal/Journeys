### Task 1: Operator docs and local config skeleton

**Files:**
- Create: `docs/developer/linear-aidlc-projection.md`
- Create: `aidlc/spaces/default/linear-projection.example.yaml`
- Modify: `docs/developer/index.md`
- Modify: `docs/developer/local-ops.md`
- Modify: `.gitignore`
- Create: `docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md`

**Interfaces:**
- Consumes: spec Â§2, Â§7, Â§12
- Produces: operator doc and example YAML later tasks must not contradict

- [ ] **Step 1: Create `docs/developer/linear-aidlc-projection.md` with this exact body**

~~~~markdown
# Linear as AI-DLC unit projection

Phase 2 adapter. AI-DLC units stay canon. Linear is the human board.

**Spec:** `docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md`

## Setup (human)

1. Linear team with statuses named exactly as in `aidlc/spaces/default/linear-projection.yaml`: `Todo`, `In Progress`, `In Review` (engine Dev Complete), `Done`.
2. Copy `aidlc/spaces/default/linear-projection.example.yaml` to `aidlc/spaces/default/linear-projection.yaml` and set `teamId` (Linear GraphQL id, not the issue prefix).
3. Set `LINEAR_API_KEY` in the environment. Do not commit it.
4. Optional: Linear GitHub integration + commit-linking webhook on `DerekJeremias-personal/Journeys`.

## Script

From `C:\Dev\Journeys\Journeys`:

```powershell
$env:LINEAR_API_KEY = "<key>"
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>"
.\scripts\linear-aidlc-projection.ps1 -Action pull-back -IntentRecordDir "<record>"
.\scripts\linear-aidlc-projection.ps1 -Action claim -IntentRecordDir "<record>" -Unit "<unit-name>"
.\scripts\linear-aidlc-projection.ps1 -Action comment -IntentRecordDir "<record>" -Unit "<unit-name>" -Body "files: ... tests: ..."
.\scripts\linear-aidlc-projection.ps1 -Action complete -IntentRecordDir "<record>" -Unit "<unit-name>" -CommitSha "<sha>" -CommitUrl "https://github.com/DerekJeremias-personal/Journeys/commit/<sha>"
.\scripts\linear-aidlc-projection.ps1 -Action cancel -IntentRecordDir "<record>" -Unit "<unit-name>"
```

`<record>` is `aidlc/spaces/default/intents/<slug>-<id8>/`.

Exit 0 = success. Non-zero = fail closed (construction must not proceed).

## Freeze

After `claim`, `linear-map.yaml` has `frozen: true` for that unit. `pull-back` must refuse that unit. `upsert` may still push status and comments.

## Commits

Prefer `ref JOU-123` on in-progress commits. `Fixes JOU-123` only when merge should move the issue to Done. Agents do not commit unless asked in that message.
~~~~

- [ ] **Step 2: Add this bullet to `docs/developer/index.md` (keep existing bullets)**

```markdown
- [Linear AI-DLC projection](linear-aidlc-projection.md) â€” board for units; engine stays canon
```

- [ ] **Step 3: Append this subsection to `docs/developer/local-ops.md` after the existing secrets section**

```markdown
## Linear projection (optional)

The AI-DLC Linear board adapter reads `LINEAR_API_KEY` from the environment (not user secrets, not `appsettings`). See [linear-aidlc-projection.md](linear-aidlc-projection.md).
```

- [ ] **Step 4: Create `aidlc/spaces/default/linear-projection.example.yaml`**

```yaml
# Copy to linear-projection.yaml (gitignored) and fill teamId.
teamId: ""
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
```

- [ ] **Step 5: Add this line to `.gitignore` next to the other `aidlc/` ignores**

```
aidlc/spaces/*/linear-projection.yaml
```

- [ ] **Step 6: Create `docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md`**

```markdown
# Waiver: Linear AI-DLC projection adapter

**Reason:** Scripts and developer docs for projecting AI-DLC units onto Linear. No product capability, ontology, or graph edge change. Humans still merge. Agents still claim units, not Linear tickets.

**Nodes touched by path-map (no meaning change):** none (scripts/ and docs/developer/ only).
```

- [ ] **Step 7: Confirm files exist**

Run:

```powershell
Test-Path docs\developer\linear-aidlc-projection.md
Test-Path aidlc\spaces\default\linear-projection.example.yaml
Test-Path docs\product\graph\waivers\2026-09-14-linear-aidlc-projection.md
Select-String -Path .gitignore -Pattern "linear-projection.yaml"
```

Expected: all `True` / a matching gitignore line.

---


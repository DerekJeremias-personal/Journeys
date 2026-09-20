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
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>" -ApproveCreate <n>
.\scripts\linear-aidlc-projection.ps1 -Action pull-back -IntentRecordDir "<record>"
.\scripts\linear-aidlc-projection.ps1 -Action claim -IntentRecordDir "<record>" -Unit "<unit-name>"
.\scripts\linear-aidlc-projection.ps1 -Action comment -IntentRecordDir "<record>" -Unit "<unit-name>" -Body "files: ... tests: ..."
.\scripts\linear-aidlc-projection.ps1 -Action complete -IntentRecordDir "<record>" -Unit "<unit-name>" -CommitSha "<sha>" -CommitUrl "https://github.com/DerekJeremias-personal/Journeys/commit/<sha>"
.\scripts\linear-aidlc-projection.ps1 -Action cancel -IntentRecordDir "<record>" -Unit "<unit-name>"
```

`<record>` is `aidlc/spaces/default/intents/<slug>-<id8>/`.

Exit 0 = success. Non-zero = fail closed (construction must not proceed).

Workflow-state lookup types `teamId` as `ID!`. Issue get/update still use `String!` for issue `id`. Request bodies are UTF-8 JSON (`charset=utf-8`) so titles and unit copy with em dashes do not fail Linear's parser.

`journeys-plan-to-aidlc` and `aidlc-journeys` treat upsert-after-units and pull-back-before-claim as a **hard gate**: no `Journeys.*` edits until every unit has a Linear `issueId` and pull-back has succeeded.

## Create budget

`upsert` will not `issueCreate` until a human confirms the count.

1. Run without `-ApproveCreate`. If new issues would be created, the script exits non-zero and names the units. Zero new creates (map already has `issueId`s) proceeds.
2. Confirm the number, then re-run with `-ApproveCreate <n>` matching that count exactly.
3. `maxCreate` in `linear-projection.yaml` (default 25) is a hard ceiling even when approved. Raise it only in the gitignored local file.

Agents must not invent `<n>` or auto-retry with `-ApproveCreate`. Existing issues are updated without this gate.

## Freeze

After `claim`, `linear-map.yaml` has `frozen: true` for that unit. `pull-back` must refuse that unit. `upsert` may still push status and comments.

## Commits

Prefer `ref JOU-123` on in-progress commits. `Fixes JOU-123` only when merge should move the issue to Done. Agents do not commit unless asked in that message.

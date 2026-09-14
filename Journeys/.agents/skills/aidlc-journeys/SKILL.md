---
name: aidlc-journeys
description: Bind AI-DLC construction in this repo to Journeys canon and agent-verify.
---

# AI-DLC on Journeys

When `/aidlc` (or an AI-DLC construction stage) is running in `C:\Dev\Journeys\Journeys`:

1. Read `AGENTS.md` then `docs/product/` as usual. Do not treat `aidlc/` artifacts as product canon.
2. Do not invent capability ids. Do not edit Auth0 `"hayward"` or ledger types.
3. Before finishing a Construction stage (especially 3.5 / 3.6), run:

```powershell
.\scripts\aidlc-agent-verify-sensor.ps1
```

Add `-RunTests` when tests or `Journeys.Tests` files changed. Exit 0 required. Non-zero means halt — do not open a PR.

4. AWS/CDK/terraform suggestions are out of this tree (`docs/roadmap/non-goals.md`).
5. Linear projection (Phase 2). Units are still the work source. Resolve the intent record as the directory under `aidlc/spaces/default/intents/` that contains `aidlc-state.md`. After `units-generation` completes, run:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>"
```

If that exits non-zero with `would create N Linear issues`, halt and show the human the unit list. Do not pass `-ApproveCreate` until they confirm N. Then:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>" -ApproveCreate N
```

`maxCreate` in `linear-projection.yaml` is a hard ceiling (default 25). Do not raise it without asking.

Before the first construction unit claim, run:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action pull-back -IntentRecordDir "<record>"
```

Non-zero exit means halt. Do not claim a unit. After `aidlc-unit claim` (or equivalent), run `-Action claim -Unit <name>`. During construction, throttle `-Action comment`. When the unit passes `aidlc-agent-verify-sensor.ps1` and a commit SHA exists, run `-Action complete`. Read `<record>/inception/units-generation/unit-linear-copy/<unit>.md` when present — that text is post-review canon for AC.

Do not `list_issues` to pick work. Do not start Linear Agent coding sessions.

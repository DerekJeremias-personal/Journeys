# Ontology: draft vs live

Campaign **status** (`Draft`, `Live`, `Archive`, plus `Pause`) is a **Cosmos partition key**, not a cosmetic field. Fetch, upsert, and list always take `status` because the document lives in that partition (`CampaignAdapter` uses `pk = status.ToLower()`). Event processing loads the **Live** partition. Authoring mutates **Draft**.

This is both logical and physical separation: promoting or archiving is `MoveEntityAsync` into a new `status` partition so hot Live reads stay off Archive (and Draft) documents. Do not query “all campaigns regardless of status” for execution.

Allowed values: `CampaignStatusStrings` (`Live`, `Draft`, `Archive`, `Pause`). Persist lowercase. `ExtCampaignId` is the stable program identity across versions; campaign `Id` is the document in one partition.

## Lifecycle

| Status | Role |
|--------|------|
| **Draft** | Authoring. Prefer draft-first. At most **one Draft per `ExtCampaignId`** in a tenant. Hard-delete allowed only for Draft that was never deployed (`CampaignDeleteGuard`). |
| **Live** | Execution. Event process, rules, MCP resources default here. Do not edit the journey in place (start/end dates are the documented exception). Cannot flip Live → Draft on the same id — open a **new** Draft with a **new Id** and the **same `ExtCampaignId`**. |
| **Archive** | Retired version. Immutable. Live is not deleted; it is moved here. |
| **Pause** | Live document moved to the Pause partition (also implemented; do not invent another paused state). |

**Copy** (`CampaignService.CopyCampaignAsync`) is a **new program**: new Draft, new `Id`, **new** `ExtCampaignId`, name `{source.Name} Copy` unless overridden. **Restore** from Archive (`RestoreArchivedCampaignAsync`) is a new Draft, new `Id`, **same** `ExtCampaignId`; the Archive row is unchanged. Restore fails if a Draft already exists for that ext id. UX **Unpublish** is Pause on the same Live id — not Live → Draft.

**Promote Draft → Live** is an explicit, gated operation — not a side effect of a chat turn (`LivePromotionGuard`: user must approve; verify Draft with `process_event(campaignId)` first). On promote, if another Live already exists for that `ExtCampaignId`, that Live is **archived** (partition move), then the Draft **moves** into the Live partition (`DeployedDate` set). First-time create-as-Live is allowed.

## Governance for agents

- Always pass `status` on get/upsert/delete. Defaulting MCP tools to Live is for execution, not a reason to skip Draft when authoring.
- Do not upsert `status: Live` to “save work.” Save Draft; promote only after approval.
- Do not create a second Draft for an `ExtCampaignId` that already has one — update or delete the existing Draft.
- Do not treat Archive as writable history. Versions are listed by `ExtCampaignId` across partitions (`GetCampaignVersionsByExtId`).
- If this file and `CampaignAdapter.UpsertCampaignAsync` disagree, **code is canonical**.

## Related

`campaign.md`, `tenant.md`, `rule.md`. Agent gate: `Journeys.API/CampaignAgent/Workflow/LivePromotionGuard.cs`.

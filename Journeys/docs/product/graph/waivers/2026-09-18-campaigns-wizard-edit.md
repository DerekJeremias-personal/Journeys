# Waiver: Campaigns IA wizard edit

**Reason:** Task 6 lifts the EXP campaign wizard onto `/loyalty/campaigns/[id]?campaignStatus=`. Live-edit still follows existing `draft-live.md` (new Draft id, same `ExtCampaignId`; do not upsert Live journey in place). No new capability, ontology term, or graph edge.

**Nodes touched by path-map (no meaning change):** `campaigns`, `campaign-agent`.

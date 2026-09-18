import { describe, expect, it } from "vitest";
import {
  AUTHORING_CAMPAIGN_STATUSES,
  mergeCampaignLists
} from "./campaign-authoring-list";

describe("authoring campaign list", () => {
  it("includes live, draft, and pause partitions", () => {
    expect(AUTHORING_CAMPAIGN_STATUSES).toEqual(["live", "draft", "pause"]);
  });

  it("merges partition pages without dropping drafts", () => {
    const merged = mergeCampaignLists([
      [{ id: "live-1", status: "live", name: "Live" }],
      [{ id: "draft-1", status: "draft", name: "Live Copy" }],
      []
    ]);
    expect(merged.map((row) => row.id)).toEqual(["live-1", "draft-1"]);
  });
});

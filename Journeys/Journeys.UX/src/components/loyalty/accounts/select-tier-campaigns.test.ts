import { describe, expect, it } from "vitest";

import type { CampaignListItem } from "@/lib/api-types";

import { selectTierCampaigns } from "./select-tier-campaigns";

const campaign = (
  id: string | undefined,
  name: string,
  status: string
): CampaignListItem => ({ id, name, status });

describe("selectTierCampaigns", () => {
  it("prefers Live campaigns whose names contain tier", () => {
    const liveTier = campaign("tier-live", "Rewards Tier Program", "LIVE");
    const otherLive = campaign("other-live", "Rewards Program", "live");

    expect(selectTierCampaigns([otherLive, liveTier])).toEqual([liveTier]);
  });

  it("excludes paused, draft, and empty-id campaigns", () => {
    const liveTier = campaign("tier-live", "Rewards Tier Program", "Live");

    expect(
      selectTierCampaigns([
        liveTier,
        campaign("tier-paused", "Paused Tier Program", "Pause"),
        campaign("tier-draft", "Draft Tier Program", "Draft"),
        campaign("", "Empty ID Tier Program", "Live"),
      ])
    ).toEqual([liveTier]);
  });

  it("falls back to all Live campaigns when no Live name contains tier", () => {
    const first = campaign("first-live", "Rewards Program", "live");
    const second = campaign("second-live", "VIP Program", "LIVE");

    expect(
      selectTierCampaigns([
        first,
        campaign("paused-tier", "Paused Tier Program", "Pause"),
        second,
      ])
    ).toEqual([first, second]);
  });
});

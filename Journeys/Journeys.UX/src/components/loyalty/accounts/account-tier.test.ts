import { describe, expect, it } from "vitest";

import { resolveCurrentTierLabel } from "./account-tier";

describe("resolveCurrentTierLabel", () => {
  it("resolves the current account journey node to the human tier name", () => {
    const campaigns = [
      {
        id: "campaign-1",
        name: "Tier campaign",
        journey: {
          id: "journey-1",
          rootNodeId: "root-1",
          name: "Tier Program",
          children: [
            { id: "tier-bronze", name: "Bronze Tier" },
            { id: "tier-silver", name: "Silver Tier" },
          ],
        },
      },
    ];

    expect(
      resolveCurrentTierLabel(campaigns, [
        { rootJourneyNodeId: "journey-1", journeyNodeIds: ["tier-bronze", "tier-silver"] },
      ])
    ).toBe("Silver Tier");
  });

  it("falls back to the current tier id when a node label is missing", () => {
    const campaigns = [
      {
        id: "campaign-1",
        journey: {
          rootNodeId: "root-1",
          children: [{ id: "tier-node-without-name" }],
        },
      },
    ];

    expect(
      resolveCurrentTierLabel(campaigns, [{ rootJourneyNodeId: "root-1", journeyNodeIds: ["tier-node-without-name"] }])
    ).toBe("tier-node-without-name");
  });
});

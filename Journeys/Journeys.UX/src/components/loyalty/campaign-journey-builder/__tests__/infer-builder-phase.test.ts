import { describe, expect, it } from "vitest";

import type { Campaign } from "@/lib/campaign-types";

import { inferBuilderPhase, type BuilderPhase } from "../infer-builder-phase";

function campaign(overrides: Partial<Campaign> = {}): Campaign {
  return {
    name: "Test",
    status: "draft",
    ...overrides,
  };
}

describe("inferBuilderPhase", () => {
  it("returns idle when next campaign has no id and no journey", () => {
    expect(inferBuilderPhase({ previous: null, next: campaign() })).toBe("idle");
  });

  it("returns setup when campaign id first appears", () => {
    const next = campaign({ id: "3c4a8633-3a76-491f-b7b3-c592037ec9da" });
    expect(inferBuilderPhase({ previous: null, next })).toBe("setup");
  });

  it("returns review when workflow phase mentions validate", () => {
    expect(
      inferBuilderPhase({
        previous: campaign({ id: "abc" }),
        next: campaign({ id: "abc" }),
        workflowPhase: "validate_campaign",
      })
    ).toBe("review");
  });

  it("maps tool names to builder phases", () => {
    const withId = campaign({ id: "abc" });
    const cases: Array<{ tools: string[]; expected: BuilderPhase }> = [
      { tools: ["create_campaign"], expected: "setup" },
      { tools: ["update_eligibility_rules"], expected: "eligibility" },
      { tools: ["add_purchase_criteria"], expected: "criteria" },
      { tools: ["deposit_points_outcome"], expected: "actions" },
      { tools: ["validate_campaign"], expected: "review" },
    ];

    for (const { tools, expected } of cases) {
      expect(
        inferBuilderPhase({
          previous: withId,
          next: withId,
          toolNames: tools,
        })
      ).toBe(expected);
    }
  });

  it("advances one phase when journey gains a new rule set", () => {
    const previous = campaign({
      id: "abc",
      journey: {
        id: "j1",
        name: "Root",
        rules: [],
        children: [],
      },
    });
    const next = campaign({
      id: "abc",
      journey: {
        id: "j1",
        name: "Root",
        rules: [{ name: "Eligibility", ruleJsonElement: {}, outcomesJsonElement: [] }],
        children: [],
      },
    });

    expect(inferBuilderPhase({ previous, next })).toBe("eligibility");
  });

  it("infers phase from journey content when previous is null", () => {
    const next = campaign({
      id: "abc",
      journey: {
        id: "j1",
        name: "Root",
        rules: [{ name: "Bronze", ruleJsonElement: {}, outcomesJsonElement: [{ $type: "DepositPointsOutcome" }] }],
        children: [],
      },
    });

    expect(inferBuilderPhase({ previous: null, next })).toBe("actions");
  });
});

import { describe, expect, it } from "vitest";

import type { Campaign } from "@/lib/campaign-types";

import { nextBuilderPhase, resolveForwardAdvance } from "../advance-station";
import { validateStationForContinue } from "../validate-station";

function campaign(overrides: Partial<Campaign> = {}): Campaign {
  return {
    name: "Test",
    status: "draft",
    startDate: "2026-01-01T00:00:00.000Z",
    ...overrides,
  };
}

describe("validateStationForContinue", () => {
  it("requires setup fields", () => {
    expect(validateStationForContinue("setup", campaign({ name: "", startDate: "" })).ok).toBe(false);
    expect(validateStationForContinue("setup", campaign()).ok).toBe(true);
  });
});

describe("resolveForwardAdvance", () => {
  it("opens criteria review before actions", () => {
    const intent = resolveForwardAdvance("criteria", { ok: true });
    expect(intent).toEqual({ type: "review_modal", modal: "criteria" });
  });

  it("advances setup to eligibility when valid", () => {
    const intent = resolveForwardAdvance("setup", { ok: true });
    expect(intent).toEqual({ type: "advance", nextPhase: "eligibility" });
  });
});

describe("nextBuilderPhase", () => {
  it("walks the wizard order", () => {
    expect(nextBuilderPhase("setup")).toBe("eligibility");
    expect(nextBuilderPhase("actions")).toBe("review");
  });
});

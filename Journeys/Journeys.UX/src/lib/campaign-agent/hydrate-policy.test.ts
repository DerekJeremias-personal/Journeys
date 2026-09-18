import { describe, expect, it } from "vitest";
import { hydrateDecision } from "./hydrate-policy";

describe("hydrateDecision", () => {
  it("applies to a clean builder", () => {
    expect(hydrateDecision(false)).toBe("apply");
  });

  it("reports a conflict for a dirty builder", () => {
    expect(hydrateDecision(true)).toBe("conflict");
  });
});

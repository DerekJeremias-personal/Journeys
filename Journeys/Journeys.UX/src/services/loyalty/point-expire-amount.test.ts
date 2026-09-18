import { describe, expect, it } from "vitest";

import { resolveExpireAmount } from "./point-expire-amount";

describe("resolveExpireAmount", () => {
  it("uses absolute amount", () => {
    expect(resolveExpireAmount({ amount: 5, isPercent: false })).toBe(5);
  });

  it("uses percent of balance", () => {
    expect(resolveExpireAmount({ amount: 50, isPercent: true, currentBalance: 200 })).toBe(100);
  });

  it("rejects percent without balance", () => {
    expect(() => resolveExpireAmount({ amount: 10, isPercent: true })).toThrow(/currentBalance/);
  });
});

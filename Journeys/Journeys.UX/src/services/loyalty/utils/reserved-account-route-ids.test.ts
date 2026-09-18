import { describe, expect, it } from "vitest";

import { isReservedAccountRouteId } from "./reserved-account-route-ids";

describe("isReservedAccountRouteId", () => {
  it("reserves builder so /loyalty/accounts/builder cannot render a detail page", () => {
    expect(isReservedAccountRouteId("builder")).toBe(true);
    expect(isReservedAccountRouteId("Builder")).toBe(true);
  });

  it("allows account ids", () => {
    expect(isReservedAccountRouteId("acc-1")).toBe(false);
    expect(isReservedAccountRouteId("xref-1")).toBe(false);
  });
});

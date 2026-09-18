import { describe, expect, it } from "vitest";

import { isEventableSchema } from "./eventable-schema";

describe("isEventableSchema", () => {
  it("accepts schemas tagged eventable", () => {
    expect(isEventableSchema({ name: "OrderPlaced", tag: "eventable" })).toBe(true);
  });

  it("accepts the loyalty account details schema regardless of tag", () => {
    expect(isEventableSchema({ name: "LoyaltyAccountDetails" })).toBe(true);
  });

  it("rejects other schemas", () => {
    expect(isEventableSchema({ name: "OrderPlaced", tag: "lookup" })).toBe(false);
    expect(isEventableSchema({})).toBe(false);
  });
});

import { describe, expect, it } from "vitest";
import { mapLoyaltyPath } from "./map-loyalty-path";

describe("mapLoyaltyPath", () => {
  it("maps campaigns getall using session tenant, not path slug", () => {
    expect(mapLoyaltyPath("campaigns/slug-from-exp/getall", "acme")).toBe(
      "/api/Campaign/acme/getall"
    );
  });

  it("maps schemas model/all", () => {
    expect(mapLoyaltyPath("schemas/slug/model/all", "acme")).toBe(
      "/api/Model/acme/GetMany"
    );
  });

  it("maps events admin query and preserves schema name", () => {
    expect(mapLoyaltyPath("events/slug/LoyaltyAccountDetails/admin/query", "acme")).toBe(
      "/api/Events/acme/LoyaltyAccountDetails/admin/query"
    );
  });

  it("rejects unknown paths", () => {
    expect(() => mapLoyaltyPath("campaigns/slug/save", "acme")).toThrow(/not-allowlisted:/);
  });

  it("rejects empty tenantId", () => {
    expect(() => mapLoyaltyPath("campaigns/x/getall", "")).toThrow(/tenant/i);
  });
});

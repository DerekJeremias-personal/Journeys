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
    expect(() => mapLoyaltyPath("campaigns/slug/cid-1/stats", "acme")).toThrow(/not-allowlisted:/);
  });

  it("rejects empty tenantId", () => {
    expect(() => mapLoyaltyPath("campaigns/x/getall", "")).toThrow(/tenant/i);
  });

  const t = "acme";

  it("maps getmany, save, validate, query", () => {
    expect(mapLoyaltyPath("campaigns/session/getmany", t)).toBe("/api/Campaign/acme/getmany");
    expect(mapLoyaltyPath("campaigns/session/save", t)).toBe("/api/Campaign/acme/save");
    expect(mapLoyaltyPath("campaigns/session/validate", t)).toBe("/api/Campaign/acme/validate");
    expect(mapLoyaltyPath("campaigns/session/query", t)).toBe("/api/Campaign/acme");
  });

  it("maps get-by-id, delete, copy, restore", () => {
    expect(mapLoyaltyPath("campaigns/session/cid-1", t)).toBe("/api/Campaign/acme/cid-1");
    expect(mapLoyaltyPath("campaigns/session/cid-1/copy", t)).toBe("/api/Campaign/acme/cid-1/copy");
    expect(mapLoyaltyPath("campaigns/session/cid-1/restore", t)).toBe(
      "/api/Campaign/acme/cid-1/restore"
    );
  });

  it("maps versions, archived, live/draft by ext, PAT getall", () => {
    expect(mapLoyaltyPath("campaigns/session/versions/ext-1", t)).toBe(
      "/api/Campaign/acme/versions/ext-1"
    );
    expect(mapLoyaltyPath("campaigns/session/archived", t)).toBe("/api/Campaign/acme/archived");
    expect(mapLoyaltyPath("campaigns/session/live/ext-1", t)).toBe(
      "/api/Campaign/acme/live/ext-1"
    );
    expect(mapLoyaltyPath("campaigns/session/draft/ext-1", t)).toBe(
      "/api/Campaign/acme/draft/ext-1"
    );
    expect(mapLoyaltyPath("campaigns/session/pointaccounttype/getall", t)).toBe(
      "/api/Campaign/acme/pointaccounttype/getall"
    );
  });

  it("maps campaign-agent conversations JSON", () => {
    expect(mapLoyaltyPath("campaign-agent/conversations", t)).toBe(
      "/api/v1/acme/campaign-agent/conversations"
    );
  });

  it("still rejects unknown paths", () => {
    expect(() => mapLoyaltyPath("campaigns/session/pointaccounttype/upsert", t)).toThrow(
      /not-allowlisted:/
    );
    expect(() => mapLoyaltyPath("campaigns/session/cid-1/stats", t)).toThrow(/not-allowlisted:/);
  });

  it("maps account get, ext, balances, ledgers, deposit, withdrawal", () => {
    expect(mapLoyaltyPath("accounts/session/acc-1", t)).toBe(`/api/Account/${t}/acc-1`);
    expect(mapLoyaltyPath("accounts/session/ext/xref-1", t)).toBe(`/api/Account/${t}/ext/xref-1`);
    expect(mapLoyaltyPath("accounts/session/points/balances/acc-1", t)).toBe(
      `/api/Account/${t}/points/balances/acc-1`
    );
    expect(mapLoyaltyPath("accounts/session/points/acc-1", t)).toBe(`/api/Account/${t}/points/acc-1`);
    expect(mapLoyaltyPath("accounts/session/points/deposit", t)).toBe(`/api/Account/${t}/points/deposit`);
    expect(mapLoyaltyPath("accounts/session/points/withdrawal", t)).toBe(
      `/api/Account/${t}/points/withdrawal`
    );
  });

  it("does not treat points/deposit as an account id", () => {
    expect(mapLoyaltyPath("accounts/session/points/deposit", t)).not.toBe(`/api/Account/${t}/points`);
  });

  it("maps journey enter, exit, preview, move", () => {
    expect(
      mapLoyaltyPath(
        "journey/session/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1",
        t
      )
    ).toBe(`/api/Journey/${t}/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1`);
    expect(
      mapLoyaltyPath(
        "journey/session/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1",
        t
      )
    ).toBe(`/api/Journey/${t}/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1`);
    expect(mapLoyaltyPath("journey/session/PreviewTierMove", t)).toBe(
      `/api/Journey/${t}/PreviewTierMove`
    );
    expect(mapLoyaltyPath("journey/session/MoveTier", t)).toBe(`/api/Journey/${t}/MoveTier`);
  });

  it("rejects account builder, expire path, and points admin", () => {
    expect(() => mapLoyaltyPath("accounts/session/builder", t)).toThrow(/not-allowlisted:/);
    expect(() => mapLoyaltyPath("accounts/session/points/expire", t)).toThrow(/not-allowlisted:/);
    expect(() => mapLoyaltyPath("accounts/session/points/admin", t)).toThrow(/not-allowlisted:/);
  });
});

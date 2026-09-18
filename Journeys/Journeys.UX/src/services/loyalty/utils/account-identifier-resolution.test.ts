import { describe, expect, it, vi } from "vitest";

import { resolveAccountIdentifiers } from "./account-identifier-resolution";

describe("resolveAccountIdentifiers", () => {
  it("falls through a failed identifier query to GET by id and GET by external id", async () => {
    const queryIdentifier = vi.fn(async () => ({ success: false, error: "query failed" }));
    const getById = vi.fn(async () => ({ success: false, error: "not an internal id" }));
    const getByExternalId = vi.fn(async () => ({
      success: true,
      data: { id: "account-1", extAccountId: "external-1" },
    }));

    const result = await resolveAccountIdentifiers("external-1", {
      queryIdentifier,
      getById,
      getByExternalId,
    });

    expect(result).toEqual({
      found: true,
      loyaltyAccountId: "account-1",
      loyaltyAccountXReference: "external-1",
    });
    expect(getById).toHaveBeenCalledWith("external-1");
    expect(getByExternalId).toHaveBeenCalledWith("external-1");
  });

  it("marks an identifier unresolved when every lookup misses", async () => {
    const result = await resolveAccountIdentifiers("missing", {
      queryIdentifier: async () => ({ success: true, data: [] }),
      getById: async () => ({ success: false }),
      getByExternalId: async () => ({ success: false }),
    });

    expect(result).toEqual({
      found: false,
      loyaltyAccountId: "missing",
      loyaltyAccountXReference: "missing",
    });
  });
});

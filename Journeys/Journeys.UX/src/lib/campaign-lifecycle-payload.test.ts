import { describe, expect, it } from "vitest";
import { lifecycleSavePayload } from "./campaign-lifecycle-payload";

describe("lifecycleSavePayload", () => {
  it("maps unpublish to pause without sending journey", () => {
    const payload = lifecycleSavePayload(
      {
        id: "live-1",
        name: "Tier system",
        status: "live",
        extCampaignId: "tiersystem",
        startDate: "2026-01-01T00:00:00Z",
        endDate: "2026-12-31T00:00:00Z",
        journey: { name: "do-not-resend" }
      },
      "unpublish"
    );
    expect(payload.status).toBe("pause");
    expect(payload.id).toBe("live-1");
    expect(payload.journey).toBeUndefined();
    expect((payload as { Journey?: unknown }).Journey).toBeUndefined();
  });
});

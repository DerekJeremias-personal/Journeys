import { describe, expect, it, vi } from "vitest";

import { buildEmptyDraftPayload } from "./use-create-empty-draft";

describe("buildEmptyDraftPayload", () => {
  it("includes startDate so CampaignShellValidator accepts the draft", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-18T17:00:00.000Z"));
    try {
      const payload = buildEmptyDraftPayload();

      expect(payload).toEqual({
        name: "Untitled campaign",
        status: "draft",
        startDate: "2026-09-18T17:00:00.000Z"
      });
      expect(payload.id).toBeUndefined();
    } finally {
      vi.useRealTimers();
    }
  });
});

import { describe, expect, it } from "vitest";

import { canSubmitTierMove } from "./can-submit-tier-move";

const validPreview = {
  isTierDemotion: false,
  isSameJourney: false,
  hasNegativeAdjustment: false,
  validationErrors: [],
};

describe("canSubmitTierMove", () => {
  it("allows submit only after a successful non-blocking preview", () => {
    expect(
      canSubmitTierMove({
        preview: validPreview,
        previewError: null,
        isPreviewLoading: false,
        isMutationPending: false,
        hasTargetJourney: true,
        hasValidComment: true,
      })
    ).toBe(true);
  });

  it.each([
    ["missing", null, null, false],
    ["loading", null, null, true],
    ["failed", null, new Error("required point account types not found"), false],
  ])("blocks submit when preview is %s", (_label, preview, previewError, isPreviewLoading) => {
    expect(
      canSubmitTierMove({
        preview,
        previewError,
        isPreviewLoading,
        isMutationPending: false,
        hasTargetJourney: true,
        hasValidComment: true,
      })
    ).toBe(false);
  });

  it.each([
    { ...validPreview, isTierDemotion: true },
    { ...validPreview, isSameJourney: true },
    { ...validPreview, hasNegativeAdjustment: true },
    { ...validPreview, validationErrors: ["Invalid move"] },
  ])("blocks a preview with a blocking issue", (preview) => {
    expect(
      canSubmitTierMove({
        preview,
        previewError: null,
        isPreviewLoading: false,
        isMutationPending: false,
        hasTargetJourney: true,
        hasValidComment: true,
      })
    ).toBe(false);
  });
});

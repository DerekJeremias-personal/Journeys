type TierMovePreview = {
  isTierDemotion?: boolean;
  isSameJourney?: boolean;
  hasNegativeAdjustment?: boolean;
  validationErrors?: string[];
};

type CanSubmitTierMoveInput = {
  preview: TierMovePreview | null | undefined;
  previewError: unknown;
  isPreviewLoading: boolean;
  isMutationPending: boolean;
  hasTargetJourney: boolean;
  hasValidComment: boolean;
};

export function canSubmitTierMove({
  preview,
  previewError,
  isPreviewLoading,
  isMutationPending,
  hasTargetJourney,
  hasValidComment,
}: CanSubmitTierMoveInput): boolean {
  if (
    isMutationPending ||
    !hasTargetJourney ||
    !hasValidComment ||
    isPreviewLoading ||
    previewError ||
    !preview
  ) {
    return false;
  }

  return !(
    preview.isTierDemotion ||
    preview.isSameJourney ||
    preview.hasNegativeAdjustment ||
    (preview.validationErrors?.length ?? 0) > 0
  );
}

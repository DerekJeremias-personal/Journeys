import type { BuilderPhase } from "./infer-builder-phase";
import { JOURNEY_BUILDER_STEPS, type JourneyBuilderStepKey } from "./journey-builder-steps";
import type { StationValidationResult } from "./validate-station";

export type ReviewModalKind = "criteria" | "actions";

export type AdvanceIntent =
  | { type: "advance"; nextPhase: BuilderPhase }
  | { type: "review_modal"; modal: ReviewModalKind }
  | { type: "blocked" };

const PHASE_ORDER: readonly JourneyBuilderStepKey[] = JOURNEY_BUILDER_STEPS.map((step) => step.key);

export function nextBuilderPhase(phase: BuilderPhase): BuilderPhase | null {
  if (phase === "idle" || phase === "review") {
    return null;
  }
  const index = PHASE_ORDER.indexOf(phase);
  if (index < 0 || index >= PHASE_ORDER.length - 1) {
    return null;
  }
  const nextKey = PHASE_ORDER[index + 1];
  return nextKey ?? null;
}

export function previousBuilderPhase(phase: BuilderPhase): BuilderPhase | null {
  if (phase === "idle" || phase === "setup") {
    return null;
  }
  if (phase === "review") {
    return "actions";
  }
  const index = PHASE_ORDER.indexOf(phase);
  if (index <= 0) {
    return null;
  }
  const prevKey = PHASE_ORDER[index - 1];
  return prevKey ?? null;
}

export function resolveForwardAdvance(
  phase: BuilderPhase,
  validation: StationValidationResult,
  options?: { skipReviewModal?: boolean }
): AdvanceIntent {
  if (!validation.ok) {
    return { type: "blocked" };
  }

  if (phase === "criteria" && !options?.skipReviewModal) {
    return { type: "review_modal", modal: "criteria" };
  }

  if (phase === "actions" && !options?.skipReviewModal) {
    return { type: "review_modal", modal: "actions" };
  }

  const nextPhase = nextBuilderPhase(phase);
  if (!nextPhase) {
    return { type: "blocked" };
  }

  return { type: "advance", nextPhase };
}

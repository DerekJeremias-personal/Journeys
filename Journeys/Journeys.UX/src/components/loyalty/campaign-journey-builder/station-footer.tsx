"use client";

import { ArrowLeft, ArrowRight } from "lucide-react";

import { Button } from "@/components/ui/button";

import type { BuilderPhase } from "./infer-builder-phase";
import { JOURNEY_BUILDER_STEPS } from "./journey-builder-steps";
import type { DraftPersistence } from "./journey-builder-store";
import type { StationValidationResult } from "./validate-station";
import { validationHint } from "./validate-station";

const CONTINUE_LABELS: Partial<Record<BuilderPhase, string>> = {
  setup: "Continue to Eligibility",
  eligibility: "Continue to Criteria",
  criteria: "Continue to Actions",
  actions: "Continue to Review",
};

export interface StationFooterProps {
  readonly phase: BuilderPhase;
  readonly validation: StationValidationResult;
  readonly draftPersistence: DraftPersistence;
  readonly onBack: () => void;
  readonly onContinue: () => void;
  readonly continuing?: boolean;
}

export function StationFooter({
  phase,
  validation,
  draftPersistence,
  onBack,
  onContinue,
  continuing = false,
}: StationFooterProps) {
  if (phase === "idle" || phase === "review") {
    return null;
  }

  const stepIndex = JOURNEY_BUILDER_STEPS.findIndex((step) => step.key === phase);
  const hint = validation.ok
    ? draftPersistence === "local"
      ? "Changes stay local until you save as draft."
      : "Progress saves when you continue."
    : validationHint(validation.reason);

  return (
    <div className="flex flex-col gap-3 border-t bg-muted/10 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-center gap-3">
        {stepIndex > 0 ? (
          <Button type="button" variant="ghost" size="sm" onClick={onBack} disabled={continuing}>
            <ArrowLeft className="mr-1.5 h-4 w-4" aria-hidden />
            Back
          </Button>
        ) : null}
        <span className="text-sm text-muted-foreground">{hint}</span>
      </div>
      <Button type="button" size="lg" className="shrink-0" disabled={!validation.ok || continuing} onClick={onContinue}>
        {continuing ? "Saving…" : (CONTINUE_LABELS[phase] ?? "Continue")}
        {!continuing ? <ArrowRight className="ml-1.5 h-4 w-4" aria-hidden /> : null}
      </Button>
    </div>
  );
}

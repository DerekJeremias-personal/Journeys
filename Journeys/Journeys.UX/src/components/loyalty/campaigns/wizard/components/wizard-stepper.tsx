"use client";

/**
 * WizardStepper — visual horizontal stepper that replaces the flat
 * `<TabsList>` for the campaign wizard. Each step is a clickable circle:
 *
 *   - complete (past steps with no errors): filled primary + checkmark
 *   - current: filled primary + ring
 *   - upcoming: muted bordered circle with the step number
 *   - error: destructive fill + ✕
 *
 * Driven by the wizard's RHF errors so step status reflects validation state.
 */

import { Check, X } from "lucide-react";

import { cn } from "@/lib/utils";

import { WIZARD_STEPS, type WizardStep } from "../wizard-store";

export type StepStatus = "complete" | "current" | "upcoming" | "error";

export interface WizardStepperProps {
  activeStep: WizardStep;
  /** Step ids that have validation errors; rendered as `error` regardless of position. */
  errorSteps?: ReadonlyArray<WizardStep>;
  onStepClick: (step: WizardStep) => void;
}

export function WizardStepper({ activeStep, errorSteps = [], onStepClick }: WizardStepperProps) {
  const activeIndex = WIZARD_STEPS.findIndex((s) => s.id === activeStep);

  return (
    <nav aria-label="Wizard steps" className="w-full">
      <ol className="flex items-center justify-between gap-2">
        {WIZARD_STEPS.map((step, index) => {
          const status = computeStatus(step.id, index, activeIndex, errorSteps);
          const isLast = index === WIZARD_STEPS.length - 1;
          const clickable = status === "complete" || status === "current" || status === "error";
          return (
            <li key={step.id} className="flex flex-1 items-center last:flex-none">
              <button
                type="button"
                onClick={() => clickable && onStepClick(step.id)}
                disabled={!clickable}
                className={cn(
                  "group flex items-center gap-3 text-left",
                  clickable ? "cursor-pointer" : "cursor-not-allowed"
                )}
                aria-current={status === "current" ? "step" : undefined}
              >
                <span
                  className={cn(
                    "flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors",
                    status === "complete" && "bg-primary text-primary-foreground",
                    status === "current" && "bg-primary text-primary-foreground ring-4 ring-primary/20",
                    status === "upcoming" && "border border-border bg-muted text-muted-foreground",
                    status === "error" && "bg-destructive text-destructive-foreground"
                  )}
                  aria-hidden="true"
                >
                  {status === "complete" ? (
                    <Check className="h-4 w-4" />
                  ) : status === "error" ? (
                    <X className="h-4 w-4" />
                  ) : (
                    index + 1
                  )}
                </span>
                <span className="hidden flex-col sm:flex">
                  <span
                    className={cn(
                      "text-sm font-medium leading-tight transition-colors",
                      status === "current" || status === "complete" || status === "error"
                        ? "text-foreground"
                        : "text-muted-foreground"
                    )}
                  >
                    {step.label}
                  </span>
                  <span className="text-xs text-muted-foreground">{step.description}</span>
                </span>
              </button>
              {!isLast && (
                <span
                  aria-hidden="true"
                  className={cn(
                    "mx-3 hidden h-0.5 flex-1 rounded-full sm:block",
                    index < activeIndex ? "bg-primary" : "bg-border"
                  )}
                />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

function computeStatus(
  stepId: WizardStep,
  index: number,
  activeIndex: number,
  errorSteps: ReadonlyArray<WizardStep>
): StepStatus {
  if (errorSteps.includes(stepId)) return "error";
  if (index < activeIndex) return "complete";
  if (index === activeIndex) return "current";
  return "upcoming";
}

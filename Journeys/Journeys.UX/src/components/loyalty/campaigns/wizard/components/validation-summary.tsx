"use client";

/**
 * ValidationSummary — surfaces any RHF `formState.errors` plus structural
 * journey-graph errors (empty journey, untitled steps, …) before submit.
 */

import { CheckCircle2, AlertTriangle } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import type { Journey } from "@/lib/campaign-types";

/** Loose shape of an RHF error node — we only need `message` and the ability to recurse. */
type ErrorNode = { message?: string; type?: unknown };

export interface ValidationSummaryProps {
  /**
   * Loose shape so we don't have to match RHF's deep generic for our parent
   * form. Any nested object with `message: string` leaves are surfaced.
   */
  errors: Record<string, unknown>;
  journey: Journey | null | undefined;
}

interface ValidationIssue {
  field: string;
  message: string;
}

function flattenFormErrors(errors: Record<string, unknown>, prefix = ""): ValidationIssue[] {
  const out: ValidationIssue[] = [];
  for (const [key, value] of Object.entries(errors)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (!value || typeof value !== "object") continue;
    const node = value as ErrorNode & Record<string, unknown>;
    if (typeof node.message === "string") {
      out.push({ field: path, message: node.message });
    } else {
      out.push(...flattenFormErrors(node, path));
    }
  }
  return out;
}

function validateJourney(journey: Journey | null | undefined): ValidationIssue[] {
  const out: ValidationIssue[] = [];
  if (!journey) {
    out.push({ field: "journey", message: "Journey is missing." });
    return out;
  }
  const visit = (node: Journey, path: string) => {
    if (!node.name?.trim()) {
      out.push({ field: path, message: `Step "${node.id ?? path}" has no name.` });
    }
    (node.children ?? []).forEach((child, idx) => visit(child, `${path}.children[${idx}]`));
  };
  visit(journey, "journey");
  return out;
}

export function ValidationSummary({ errors, journey }: ValidationSummaryProps) {
  const issues = [...flattenFormErrors(errors), ...validateJourney(journey)];

  if (issues.length === 0) {
    return (
      <Alert>
        <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
        <AlertTitle>Ready to submit</AlertTitle>
        <AlertDescription>All required fields are valid.</AlertDescription>
      </Alert>
    );
  }

  return (
    <Alert variant="destructive">
      <AlertTriangle className="h-4 w-4" aria-hidden="true" />
      <AlertTitle>
        {issues.length} issue{issues.length === 1 ? "" : "s"} need{issues.length === 1 ? "s" : ""} attention
      </AlertTitle>
      <AlertDescription>
        <ul className="ml-4 mt-2 list-disc space-y-1">
          {issues.map((issue) => (
            <li key={`${issue.field}-${issue.message}`}>
              <span className="font-mono text-xs">{issue.field}</span>: {issue.message}
            </li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  );
}

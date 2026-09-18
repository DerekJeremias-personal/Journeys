"use client";

import { Sparkles } from "lucide-react";

import { Card, CardContent } from "@/components/ui/card";

const STEP_LABELS = [
  "Campaign Setup",
  "Eligibility",
  "Audience Criteria",
  "Resulting Actions",
  "Review & Launch",
] as const;

export function WelcomeCanvas() {
  return (
    <Card className="flex h-full min-h-[520px] flex-col items-center justify-center border-dashed">
      <CardContent className="flex max-w-md flex-col items-center gap-5 py-12 text-center">
        <div className="flex h-16 w-16 items-center justify-center rounded-2xl border border-dashed border-muted-foreground/30">
          <Sparkles className="h-7 w-7 text-muted-foreground" aria-hidden />
        </div>
        <div className="space-y-2">
          <h2 className="text-lg font-semibold">Your campaign will take shape here</h2>
          <p className="text-sm text-muted-foreground">
            Describe your campaign with the Agent, start from a template, or build step by step. The canvas updates as your
            draft takes shape.
          </p>
        </div>
        <div className="flex flex-wrap justify-center gap-2">
          {STEP_LABELS.map((label, index) => (
            <span
              key={label}
              className="rounded-full border border-muted bg-muted/40 px-2.5 py-1 text-xs text-muted-foreground"
            >
              {index + 1}. {label}
            </span>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

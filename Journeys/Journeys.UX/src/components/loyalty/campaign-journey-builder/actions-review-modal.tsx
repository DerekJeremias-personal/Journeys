"use client";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

import type { Campaign } from "@/lib/campaign-types";

import { getActionsTargetNode } from "./journey-builder-journey-utils";
import { labelForOutcomeKind, outcomesFromRuleSet, primaryRuleSet } from "./outcome-utils";

export interface ActionsReviewModalProps {
  readonly open: boolean;
  readonly campaign: Campaign | null;
  readonly onOpenChange: (open: boolean) => void;
  readonly onContinue: () => void;
}

export function ActionsReviewModal({ open, campaign, onOpenChange, onContinue }: ActionsReviewModalProps) {
  const actionsNode = campaign?.journey ? getActionsTargetNode(campaign.journey) : null;
  const outcomes = outcomesFromRuleSet(primaryRuleSet(actionsNode));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Review actions</DialogTitle>
          <DialogDescription>Confirm resulting actions before opening the campaign map.</DialogDescription>
        </DialogHeader>
        <ul className="space-y-2 text-sm">
          {outcomes.map((outcome, index) => (
            <li key={`${outcome.Kind}-${String(index)}`} className="rounded-md border px-3 py-2">
              {labelForOutcomeKind(outcome.Kind)}
            </li>
          ))}
        </ul>
        <DialogFooter>
          <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
            Keep editing
          </Button>
          <Button type="button" onClick={onContinue}>
            Continue to Review
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

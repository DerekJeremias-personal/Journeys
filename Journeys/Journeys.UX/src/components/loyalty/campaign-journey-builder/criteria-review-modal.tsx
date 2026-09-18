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

import {
  getCriteriaTargetNode,
  getEligibilityTargetNode,
  summarizeJourneyRules,
} from "./journey-builder-journey-utils";

export interface CriteriaReviewModalProps {
  readonly open: boolean;
  readonly campaign: Campaign | null;
  readonly onOpenChange: (open: boolean) => void;
  readonly onContinue: () => void;
}

export function CriteriaReviewModal({ open, campaign, onOpenChange, onContinue }: CriteriaReviewModalProps) {
  const eligibility = campaign?.journey ? getEligibilityTargetNode(campaign.journey) : null;
  const criteria = campaign?.journey ? getCriteriaTargetNode(campaign.journey) : null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Review criteria</DialogTitle>
          <DialogDescription>Confirm eligibility and triggers before defining actions.</DialogDescription>
        </DialogHeader>
        <div className="space-y-3 text-sm">
          <div>
            <p className="font-medium">Eligibility — {eligibility?.name || "Audience"}</p>
            <p className="text-muted-foreground">{eligibility ? summarizeJourneyRules(eligibility) : "None"}</p>
          </div>
          <div>
            <p className="font-medium">Triggers — {criteria?.name || "Criteria"}</p>
            <p className="text-muted-foreground">{criteria ? summarizeJourneyRules(criteria) : "None"}</p>
          </div>
        </div>
        <DialogFooter>
          <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
            Keep editing
          </Button>
          <Button type="button" onClick={onContinue}>
            Continue to Actions
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

"use client";

import { useMemo } from "react";

import { Alert, AlertDescription } from "@/components/ui/alert";

import { getActionsTargetNode } from "../journey-builder-journey-utils";
import { selectCampaign, useJourneyBuilderActions, useJourneyBuilderStore } from "../journey-builder-store";
import { OutcomeListEditor } from "./outcome-list-editor";

export function ActionsStationEditor() {
  const campaign = useJourneyBuilderStore(selectCampaign);
  const actions = useJourneyBuilderActions();

  const targetNode = useMemo(
    () => (campaign?.journey ? getActionsTargetNode(campaign.journey) : null),
    [campaign?.journey]
  );

  if (!campaign?.journey || !targetNode?.id) {
    return (
      <Alert>
        <AlertDescription>
          Continue from Criteria to scaffold the actions step, or let the Agent populate it.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <OutcomeListEditor
      targetNode={targetNode}
      onPatchNode={(patch) => actions.patchJourneyNode(targetNode.id!, patch)}
    />
  );
}

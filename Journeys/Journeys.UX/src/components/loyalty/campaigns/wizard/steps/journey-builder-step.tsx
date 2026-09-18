"use client";

/**
 * JourneyBuilderStep — split layout: React Flow journey graph on the left,
 * `<NodeConfigPanel>` for the selected node on the right. Toolbar wires the
 * D13 add/delete-node mutations on the wizard store.
 */

import { Plus, Trash2 } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { selectJourney, selectSelectedNodeId, useWizardActions, useWizardStore } from "../wizard-store";
import type { Journey } from "@/lib/campaign-types";

import { JourneyFlow } from "../components/journey-flow";
import { NodeConfigPanel } from "../components/node-config-panel";

function makeChildNode(rootNodeId: string): Journey {
  const id = crypto.randomUUID();
  return {
    id,
    name: "New step",
    rootNodeId,
    rules: [],
    children: [],
    navigation: {},
    tenantId: null,
  };
}

export function JourneyBuilderStep() {
  const journey = useWizardStore(selectJourney);
  const selectedNodeId = useWizardStore(selectSelectedNodeId);
  const { addChildNode, removeNode } = useWizardActions();

  const canAdd = Boolean(selectedNodeId);
  const canDelete = Boolean(selectedNodeId) && selectedNodeId !== journey.id;

  return (
    <div className="space-y-4">
      <Alert>
        <AlertDescription>
          Click a node to select it. Use the toolbar to add or delete steps. Node positions on the canvas are for layout
          only and aren&apos;t persisted to the campaign.
        </AlertDescription>
      </Alert>

      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={!canAdd}
          onClick={() => {
            if (!selectedNodeId) return;
            addChildNode(selectedNodeId, makeChildNode(journey.rootNodeId ?? journey.id ?? selectedNodeId));
          }}
        >
          <Plus className="mr-1.5 h-3.5 w-3.5" />
          Add node
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={!canDelete}
          onClick={() => {
            if (selectedNodeId) removeNode(selectedNodeId);
          }}
        >
          <Trash2 className="mr-1.5 h-3.5 w-3.5" />
          Delete node
        </Button>
      </div>

      <div className="grid gap-4 lg:grid-cols-[1fr_360px]">
        <JourneyFlow />
        <NodeConfigPanel />
      </div>
    </div>
  );
}

"use client";

import { useMemo } from "react";
import { Badge } from "@/components/ui/badge";
import { Loader2 } from "lucide-react";

import {
  selectAgentConflict,
  selectCampaign,
  selectDraftPersistence,
  selectIsDirty,
  selectLayoutMode,
  useJourneyBuilderStore
} from "./journey-builder-store";

type SyncLabel = {
  readonly text: string;
  readonly variant: "default" | "secondary" | "destructive" | "outline";
  readonly spinning: boolean;
};

export function CampaignSyncStatusPill() {
  const agentConflict = useJourneyBuilderStore(selectAgentConflict);
  const campaign = useJourneyBuilderStore(selectCampaign);
  const isDirty = useJourneyBuilderStore(selectIsDirty);
  const draftPersistence = useJourneyBuilderStore(selectDraftPersistence);
  const layoutMode = useJourneyBuilderStore(selectLayoutMode);

  const label = useMemo((): SyncLabel => {
    if (agentConflict) {
      return { text: "Conflict — resolve below", variant: "destructive", spinning: false };
    }
    if (isDirty) {
      return { text: "Unsaved changes", variant: "outline", spinning: false };
    }
    if (draftPersistence === "local") {
      return { text: "Save draft to sync with Agent", variant: "secondary", spinning: false };
    }
    if (layoutMode === "stream") {
      return { text: "Agent is updating…", variant: "default", spinning: true };
    }
    if (campaign?.journey && !campaign.id) {
      return { text: "Agent preview — not saved yet", variant: "secondary", spinning: false };
    }
    if (draftPersistence === "persisted" && campaign?.id) {
      return { text: "Synced", variant: "secondary", spinning: false };
    }
    return { text: "No draft yet", variant: "outline", spinning: false };
  }, [agentConflict, campaign?.id, campaign?.journey, isDirty, draftPersistence, layoutMode]);

  const spinning = label.spinning || layoutMode === "stream";

  return (
    <Badge variant={label.variant} className="gap-1 font-normal">
      {spinning ? <Loader2 className="h-3 w-3 animate-spin" aria-hidden /> : null}
      {label.text}
    </Badge>
  );
}

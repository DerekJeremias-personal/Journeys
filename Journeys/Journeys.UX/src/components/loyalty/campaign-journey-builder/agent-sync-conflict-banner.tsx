"use client";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

export interface AgentSyncConflictBannerProps {
  readonly onKeepMine: () => void;
  readonly onUseAgentVersion: () => void;
}

export function AgentSyncConflictBanner({ onKeepMine, onUseAgentVersion }: AgentSyncConflictBannerProps) {
  return (
    <Alert variant="destructive">
      <AlertTitle>Agent updated the draft</AlertTitle>
      <AlertDescription className="space-y-3">
        <p>You have unsaved changes in the journey builder. Choose which version to keep.</p>
        <div className="flex flex-wrap gap-2">
          <Button type="button" size="sm" variant="outline" onClick={onKeepMine}>
            Keep local
          </Button>
          <Button type="button" size="sm" onClick={onUseAgentVersion}>
            Apply Agent
          </Button>
        </div>
      </AlertDescription>
    </Alert>
  );
}

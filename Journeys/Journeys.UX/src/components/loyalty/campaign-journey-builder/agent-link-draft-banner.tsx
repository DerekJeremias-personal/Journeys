"use client";

import { Button } from "@/components/ui/button";

export interface AgentLinkDraftBannerProps {
  readonly onLinkDraft: () => void;
  readonly onDismiss: () => void;
}

export function AgentLinkDraftBanner({ onLinkDraft, onDismiss }: AgentLinkDraftBannerProps) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-primary/30 bg-primary/5 px-4 py-3 text-sm">
      <p className="text-foreground">
        The Agent saved a campaign but it is not linked to this workspace yet. Link a draft to show the journey on the
        canvas.
      </p>
      <div className="flex shrink-0 gap-2">
        <Button type="button" size="sm" onClick={onLinkDraft}>
          Link draft…
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onDismiss}>
          Dismiss
        </Button>
      </div>
    </div>
  );
}

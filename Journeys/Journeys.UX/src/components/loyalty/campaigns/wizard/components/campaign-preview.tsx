"use client";

/**
 * CampaignPreview — read-only nested view of the in-progress campaign
 * (form values + journey graph). Uses the shared `JsonTreeView`.
 */

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { JsonTreeView } from "@/components/shared/json-tree-view";

export interface CampaignPreviewProps {
  value: unknown;
}

export function CampaignPreview({ value }: CampaignPreviewProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Campaign preview</CardTitle>
      </CardHeader>
      <CardContent>
        <JsonTreeView value={value} maxInitialExpandLevel={3} />
      </CardContent>
    </Card>
  );
}

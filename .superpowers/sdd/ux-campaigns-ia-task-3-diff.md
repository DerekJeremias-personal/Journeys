# Task 3 review package (working tree; no commit)

## git diff --stat

 .../Journeys.UX/src/app/loyalty/campaigns/page.tsx | 49 ++++++++++------------  1 file changed, 21 insertions(+), 28 deletions(-)

## untracked

Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-card.tsx
Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-filters.tsx
Journeys/Journeys.UX/src/components/loyalty/campaigns/campaigns-client.tsx
Journeys/Journeys.UX/src/components/loyalty/campaigns/index.ts
Journeys/Journeys.UX/src/components/loyalty/status-badge.tsx
Journeys/Journeys.UX/src/lib/campaign-kebab.test.ts
Journeys/Journeys.UX/src/lib/campaign-kebab.ts
Journeys/Journeys.UX/src/lib/campaign-types.ts
Journeys/Journeys.UX/src/services/loyalty/query-keys.ts

## Diff

diff --git a/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
index 63486bd..9adc0c8 100644
--- a/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
+++ b/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
@@ -1,32 +1,25 @@
-import { getCampaigns } from "@/services/loyalty/actions";
-
-function formatFetchError(error?: string): string {
-  const e = error ?? "";
-  if (/401|403|unauthorized|forbidden|nope/i.test(e)) {
-    return "not authorized / check tenant or key";
-  }
-  return e || "Unknown error";
-}
-
-export default async function CampaignsPage() {
-  const result = await getCampaigns();
+import Link from "next/link";
+import { CampaignsClient } from "@/components/loyalty/campaigns";
 
+export default function CampaignsPage() {
   return (
-    <>
-      <h1>Campaigns</h1>
-      {!result.success ? (
-        <p className="error">{formatFetchError(result.error)}</p>
-      ) : result.data!.length === 0 ? (
-        <p className="empty">No campaigns found.</p>
-      ) : (
-        <ul>
-          {result.data!.map((campaign, index) => (
-            <li key={campaign.id ?? index}>
-              {campaign.name} ΓÇö {campaign.status} ΓÇö {campaign.id}
-            </li>
-          ))}
-        </ul>
-      )}
-    </>
+    <div className="space-y-6">
+      <div className="flex items-start justify-between gap-4">
+        <div>
+          <h1 className="text-2xl font-semibold tracking-tight">Campaigns</h1>
+          <p className="mt-1 text-sm text-muted-foreground">
+            Build and manage customer journey campaigns.
+          </p>
+        </div>
+        <Link
+          href="/loyalty/campaigns/agent"
+          className="text-sm font-medium text-primary underline-offset-4 hover:underline"
+        >
+          Agent
+        </Link>
+      </div>
+
+      <CampaignsClient />
+    </div>
   );
 }

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-card.tsx

diff --git a/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-card.tsx b/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-card.tsx
new file mode 100644
index 0000000..38c20f7
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-card.tsx
@@ -0,0 +1,143 @@
+"use client";
+
+import { Calendar, MoreVertical } from "lucide-react";
+import Link from "next/link";
+
+import { StatusBadge } from "@/components/loyalty/status-badge";
+import { Button } from "@/components/ui/button";
+import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
+import {
+  DropdownMenu,
+  DropdownMenuContent,
+  DropdownMenuItem,
+  DropdownMenuSeparator,
+  DropdownMenuTrigger
+} from "@/components/ui/dropdown-menu";
+import { campaignKebabVisibility, normalizeCampaignStatus } from "@/lib/campaign-kebab";
+import type { Campaign } from "@/lib/campaign-types";
+
+export type CampaignAction =
+  | "edit"
+  | "duplicate"
+  | "publish"
+  | "unpublish"
+  | "archive"
+  | "restore"
+  | "versions"
+  | "delete";
+
+export interface CampaignCardProps {
+  campaign: Campaign;
+  onAction: (action: CampaignAction, campaign: Campaign) => void;
+  detailHref?: string;
+}
+
+function formatDate(value?: string | null): string {
+  if (!value) return "ΓÇö";
+  const date = new Date(value);
+  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
+}
+
+export function CampaignCard({ campaign, onAction, detailHref }: CampaignCardProps) {
+  const status = normalizeCampaignStatus(campaign.status);
+  const visibility = campaignKebabVisibility(status, campaign.extCampaignId);
+  const name = campaign.name || "Untitled campaign";
+  const title = detailHref ? (
+    <Link href={detailHref} className="hover:underline">
+      {name}
+    </Link>
+  ) : (
+    name
+  );
+
+  return (
+    <Card>
+      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
+        <div className="min-w-0 space-y-2">
+          <CardTitle className="truncate text-base">{title}</CardTitle>
+          <div className="flex items-center gap-2">
+            <StatusBadge status={status} />
+            {campaign.extCampaignId ? (
+              <span
+                className="truncate font-mono text-xs text-muted-foreground"
+                title={campaign.extCampaignId}
+              >
+                {campaign.extCampaignId}
+              </span>
+            ) : null}
+          </div>
+        </div>
+        <DropdownMenu>
+          <DropdownMenuTrigger asChild>
+            <Button variant="ghost" size="sm" aria-label={`Actions for ${name}`}>
+              <MoreVertical className="h-4 w-4" />
+            </Button>
+          </DropdownMenuTrigger>
+          <DropdownMenuContent align="end">
+            {visibility.edit ? (
+              <DropdownMenuItem onClick={() => onAction("edit", campaign)}>Edit</DropdownMenuItem>
+            ) : null}
+            {visibility.agent && campaign.id ? (
+              <DropdownMenuItem asChild>
+                <Link
+                  href={`/loyalty/campaigns/${campaign.id}/agent?campaignStatus=${encodeURIComponent(status)}`}
+                >
+                  Open in Agent
+                </Link>
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.duplicate ? (
+              <DropdownMenuItem onClick={() => onAction("duplicate", campaign)}>
+                Duplicate
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.versions ? (
+              <DropdownMenuItem onClick={() => onAction("versions", campaign)}>
+                Versions
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.publish ? (
+              <DropdownMenuItem onClick={() => onAction("publish", campaign)}>
+                Publish
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.unpublish ? (
+              <DropdownMenuItem onClick={() => onAction("unpublish", campaign)}>
+                Unpublish
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.archive ? (
+              <DropdownMenuItem onClick={() => onAction("archive", campaign)}>
+                Archive
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.restore ? (
+              <DropdownMenuItem onClick={() => onAction("restore", campaign)}>
+                Restore to draft
+              </DropdownMenuItem>
+            ) : null}
+            {visibility.delete ? (
+              <>
+                <DropdownMenuSeparator />
+                <DropdownMenuItem
+                  onClick={() => onAction("delete", campaign)}
+                  variant="destructive"
+                >
+                  Delete
+                </DropdownMenuItem>
+              </>
+            ) : null}
+          </DropdownMenuContent>
+        </DropdownMenu>
+      </CardHeader>
+      <CardContent className="space-y-2 text-sm">
+        <div className="flex items-center gap-2 text-muted-foreground">
+          <Calendar className="h-4 w-4" aria-hidden="true" />
+          <span>
+            {formatDate(campaign.startDate)} ΓÇô {formatDate(campaign.endDate)}
+          </span>
+        </div>
+      </CardContent>
+    </Card>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-filters.tsx

diff --git a/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-filters.tsx b/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-filters.tsx
new file mode 100644
index 0000000..4cc7eb2
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaign-filters.tsx
@@ -0,0 +1,138 @@
+"use client";
+
+import { Search } from "lucide-react";
+import { useMemo, useState } from "react";
+
+import { Button } from "@/components/ui/button";
+import { Card, CardContent } from "@/components/ui/card";
+
+export interface CampaignFiltersValue {
+  nameFilter: string;
+  fromDate: string | null;
+  toDate: string | null;
+  queryString: string;
+  queryParams: Record<string, unknown>;
+}
+
+export interface CampaignFiltersProps {
+  value: CampaignFiltersValue;
+  onChange: (next: CampaignFiltersValue) => void;
+}
+
+export const EMPTY_CAMPAIGN_FILTERS: CampaignFiltersValue = {
+  nameFilter: "",
+  fromDate: null,
+  toDate: null,
+  queryString: "",
+  queryParams: {}
+};
+
+function buildQuery(
+  input: Pick<CampaignFiltersValue, "nameFilter" | "fromDate" | "toDate">
+): Pick<CampaignFiltersValue, "queryString" | "queryParams"> {
+  const conditions: string[] = [];
+  const parameters: Record<string, unknown> = {};
+
+  if (input.nameFilter.trim()) {
+    conditions.push("STARTSWITH(c.name, @name, true)");
+    parameters["@name"] = input.nameFilter.trim();
+  }
+  if (input.fromDate) {
+    conditions.push("(c.startDate >= @fromDate OR c.startdate >= @fromDate)");
+    parameters["@fromDate"] = new Date(`${input.fromDate}T00:00:00.000Z`).toISOString();
+  }
+  if (input.toDate) {
+    conditions.push("(c.startDate <= @toDate OR c.startdate <= @toDate)");
+    parameters["@toDate"] = new Date(`${input.toDate}T23:59:59.999Z`).toISOString();
+  }
+
+  return { queryString: conditions.join(" and "), queryParams: parameters };
+}
+
+export function CampaignFilters({ value, onChange }: CampaignFiltersProps) {
+  const [nameInput, setNameInput] = useState(value.nameFilter);
+  const [fromDate, setFromDate] = useState(value.fromDate);
+  const [toDate, setToDate] = useState(value.toDate);
+  const hasActiveFilters = useMemo(
+    () => Boolean(nameInput || fromDate || toDate),
+    [nameInput, fromDate, toDate]
+  );
+
+  const apply = () => {
+    onChange({
+      nameFilter: nameInput,
+      fromDate,
+      toDate,
+      ...buildQuery({ nameFilter: nameInput, fromDate, toDate })
+    });
+  };
+
+  const reset = () => {
+    setNameInput("");
+    setFromDate(null);
+    setToDate(null);
+    onChange(EMPTY_CAMPAIGN_FILTERS);
+  };
+
+  const inputClassName =
+    "h-9 rounded-md border border-input bg-transparent px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/50";
+
+  return (
+    <Card>
+      <CardContent className="p-4">
+        <form
+          className="grid gap-4 md:grid-cols-[minmax(0,1fr)_auto_auto_auto] md:items-end"
+          onSubmit={(event) => {
+            event.preventDefault();
+            apply();
+          }}
+        >
+          <label className="space-y-1.5 text-sm font-medium" htmlFor="campaign-name-filter">
+            Name
+            <span className="relative mt-1.5 block">
+              <Search
+                className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
+                aria-hidden="true"
+              />
+              <input
+                id="campaign-name-filter"
+                className={`${inputClassName} w-full pl-9`}
+                placeholder="Search by campaign name"
+                value={nameInput}
+                onChange={(event) => setNameInput(event.target.value)}
+              />
+            </span>
+          </label>
+          <label className="space-y-1.5 text-sm font-medium" htmlFor="campaign-from-date">
+            From
+            <input
+              id="campaign-from-date"
+              type="date"
+              className={`${inputClassName} mt-1.5 block`}
+              value={fromDate ?? ""}
+              onChange={(event) => setFromDate(event.target.value || null)}
+            />
+          </label>
+          <label className="space-y-1.5 text-sm font-medium" htmlFor="campaign-to-date">
+            To
+            <input
+              id="campaign-to-date"
+              type="date"
+              className={`${inputClassName} mt-1.5 block`}
+              value={toDate ?? ""}
+              onChange={(event) => setToDate(event.target.value || null)}
+            />
+          </label>
+          <div className="flex gap-2">
+            <Button type="submit">Search</Button>
+            {hasActiveFilters ? (
+              <Button type="button" variant="outline" onClick={reset}>
+                Clear
+              </Button>
+            ) : null}
+          </div>
+        </form>
+      </CardContent>
+    </Card>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/campaigns/campaigns-client.tsx

diff --git a/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaigns-client.tsx b/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaigns-client.tsx
new file mode 100644
index 0000000..abff25d
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/campaigns/campaigns-client.tsx
@@ -0,0 +1,337 @@
+"use client";
+
+import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
+import { Plus } from "lucide-react";
+import Link from "next/link";
+import { useRouter } from "next/navigation";
+import { useMemo, useState } from "react";
+import { toast } from "react-hot-toast";
+
+import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
+import {
+  AlertDialog,
+  AlertDialogAction,
+  AlertDialogCancel,
+  AlertDialogContent,
+  AlertDialogDescription,
+  AlertDialogFooter,
+  AlertDialogHeader,
+  AlertDialogTitle
+} from "@/components/ui/alert-dialog";
+import { Button } from "@/components/ui/button";
+import { Card, CardContent } from "@/components/ui/card";
+import { Skeleton } from "@/components/ui/skeleton";
+import {
+  campaignKebabVisibility,
+  kebabSaveStatus,
+  normalizeCampaignStatus
+} from "@/lib/campaign-kebab";
+import type { Campaign } from "@/lib/campaign-types";
+import {
+  copyCampaign,
+  deleteCampaign,
+  getCampaigns,
+  getCampaignsByFilters,
+  restoreCampaign,
+  updateCampaign
+} from "@/services/loyalty/actions";
+import { loyaltyKeys } from "@/services/loyalty/query-keys";
+
+import { CampaignCard, type CampaignAction } from "./campaign-card";
+import {
+  CampaignFilters,
+  EMPTY_CAMPAIGN_FILTERS,
+  type CampaignFiltersValue
+} from "./campaign-filters";
+
+function campaignHref(campaign: Campaign): string | undefined {
+  if (!campaign.id) return undefined;
+  const status = normalizeCampaignStatus(campaign.status);
+  return `/loyalty/campaigns/${campaign.id}?campaignStatus=${encodeURIComponent(status)}`;
+}
+
+export function CampaignsClient() {
+  const queryClient = useQueryClient();
+  const router = useRouter();
+  const [filters, setFilters] = useState<CampaignFiltersValue>(EMPTY_CAMPAIGN_FILTERS);
+  const [deleteTarget, setDeleteTarget] = useState<Campaign | null>(null);
+  const [publishTarget, setPublishTarget] = useState<Campaign | null>(null);
+  const useFiltered = filters.queryString.length > 0;
+
+  const listQuery = useQuery({
+    queryKey: useFiltered
+      ? loyaltyKeys.campaigns.list({
+          queryString: filters.queryString,
+          queryParams: filters.queryParams
+        })
+      : loyaltyKeys.campaigns.all,
+    queryFn: async () => {
+      const result = useFiltered
+        ? await getCampaignsByFilters({
+            query: filters.queryString,
+            parameters: filters.queryParams,
+            pageSize: 100,
+            sortOrder: "DESC"
+          })
+        : await getCampaigns();
+      if (!result.success) throw new Error(result.error ?? "Failed to load campaigns");
+      return result.data ?? [];
+    }
+  });
+
+  const invalidateCampaigns = () =>
+    queryClient.invalidateQueries({ queryKey: loyaltyKeys.campaigns.all });
+
+  const lifecycleMutation = useMutation({
+    mutationFn: async ({
+      campaign,
+      action
+    }: {
+      campaign: Campaign;
+      action: "publish" | "unpublish" | "archive";
+    }) => {
+      const result = await updateCampaign({
+        ...campaign,
+        status: kebabSaveStatus(action)
+      });
+      if (!result.success) throw new Error(result.error ?? "Failed to update campaign");
+      if (!result.data) throw new Error("Update returned an empty campaign");
+      return result.data;
+    },
+    onSuccess: (campaign) => {
+      void invalidateCampaigns();
+      setPublishTarget(null);
+      const status = normalizeCampaignStatus(campaign.status);
+      toast.success(
+        status === "archive"
+          ? "Campaign archived"
+          : status === "pause"
+            ? "Campaign paused"
+            : "Campaign published"
+      );
+    },
+    onError: (error) => {
+      toast.error(error instanceof Error ? error.message : "Failed to update campaign");
+    }
+  });
+
+  const duplicateMutation = useMutation({
+    mutationFn: async (campaign: Campaign) => {
+      if (!campaign.id) throw new Error("Campaign id missing");
+      const status = normalizeCampaignStatus(campaign.status) || "draft";
+      const result = await copyCampaign(campaign.id, status);
+      if (!result.success) throw new Error(result.error ?? "Failed to duplicate campaign");
+      return result.data;
+    },
+    onSuccess: () => {
+      void invalidateCampaigns();
+      toast.success("Campaign duplicated");
+    },
+    onError: (error) => {
+      toast.error(error instanceof Error ? error.message : "Failed to duplicate campaign");
+    }
+  });
+
+  const restoreMutation = useMutation({
+    mutationFn: async (campaign: Campaign) => {
+      if (!campaign.id) throw new Error("Campaign id missing");
+      const result = await restoreCampaign(campaign.id);
+      if (!result.success) throw new Error(result.error ?? "Failed to restore campaign");
+      return result.data;
+    },
+    onSuccess: () => {
+      void invalidateCampaigns();
+      toast.success("Campaign restored to draft");
+    },
+    onError: (error) => {
+      toast.error(error instanceof Error ? error.message : "Failed to restore campaign");
+    }
+  });
+
+  const deleteMutation = useMutation({
+    mutationFn: async (campaign: Campaign) => {
+      if (!campaign.id) throw new Error("Campaign id missing");
+      const status = normalizeCampaignStatus(campaign.status) || "draft";
+      if (!campaignKebabVisibility(status, campaign.extCampaignId).delete) {
+        throw new Error("Only draft campaigns can be deleted");
+      }
+      const result = await deleteCampaign(campaign.id, status);
+      if (!result.success) throw new Error(result.error ?? "Failed to delete campaign");
+      return campaign.id;
+    },
+    onSuccess: () => {
+      void invalidateCampaigns();
+      setDeleteTarget(null);
+      toast.success("Campaign deleted");
+    },
+    onError: (error) => {
+      toast.error(error instanceof Error ? error.message : "Failed to delete campaign");
+    }
+  });
+
+  const handleAction = (action: CampaignAction, campaign: Campaign) => {
+    switch (action) {
+      case "edit": {
+        const href = campaignHref(campaign);
+        if (!href) {
+          toast.error("Campaign id missing");
+          return;
+        }
+        router.push(href);
+        return;
+      }
+      case "duplicate":
+        duplicateMutation.mutate(campaign);
+        return;
+      case "publish":
+        setPublishTarget(campaign);
+        return;
+      case "unpublish":
+      case "archive":
+        lifecycleMutation.mutate({ campaign, action });
+        return;
+      case "restore":
+        restoreMutation.mutate(campaign);
+        return;
+      case "versions":
+        if (!campaign.extCampaignId) {
+          toast.error("Campaign has no external id for version history");
+          return;
+        }
+        router.push(
+          `/loyalty/campaigns/versions/${encodeURIComponent(campaign.extCampaignId)}`
+        );
+        return;
+      case "delete":
+        setDeleteTarget(campaign);
+        return;
+    }
+  };
+
+  const campaigns = useMemo(() => listQuery.data ?? [], [listQuery.data]);
+  const sortedCampaigns = useMemo(
+    () =>
+      [...campaigns].sort((left, right) => {
+        const leftTime = new Date(left.startDate ?? 0).getTime();
+        const rightTime = new Date(right.startDate ?? 0).getTime();
+        return rightTime - leftTime;
+      }),
+    [campaigns]
+  );
+
+  return (
+    <div className="space-y-6">
+      <div className="flex flex-wrap items-center justify-between gap-3">
+        <h2 className="text-lg font-semibold">Campaigns</h2>
+        <div className="flex items-center gap-2">
+          <Button asChild variant="outline">
+            <Link href="/loyalty/campaigns/archived">Archived</Link>
+          </Button>
+          <Button asChild>
+            <Link href="/loyalty/campaigns/new">
+              <Plus className="mr-2 h-4 w-4" />
+              New campaign
+            </Link>
+          </Button>
+        </div>
+      </div>
+
+      <CampaignFilters value={filters} onChange={setFilters} />
+
+      {listQuery.isLoading ? (
+        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
+          {Array.from({ length: 6 }).map((_, index) => (
+            <Card key={index}>
+              <CardContent className="h-40 p-6">
+                <Skeleton className="h-full w-full" />
+              </CardContent>
+            </Card>
+          ))}
+        </div>
+      ) : listQuery.isError ? (
+        <Alert variant="destructive">
+          <AlertTitle>Failed to load campaigns</AlertTitle>
+          <AlertDescription>
+            {listQuery.error instanceof Error ? listQuery.error.message : "Unknown error"}
+          </AlertDescription>
+        </Alert>
+      ) : sortedCampaigns.length === 0 ? (
+        <Card>
+          <CardContent className="p-12 text-center text-sm text-muted-foreground">
+            No campaigns match these filters.
+          </CardContent>
+        </Card>
+      ) : (
+        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
+          {sortedCampaigns.map((campaign, index) => (
+            <CampaignCard
+              key={campaign.id ?? campaign.extCampaignId ?? campaign.name ?? index}
+              campaign={campaign}
+              onAction={handleAction}
+              detailHref={campaignHref(campaign)}
+            />
+          ))}
+        </div>
+      )}
+
+      <AlertDialog
+        open={publishTarget !== null}
+        onOpenChange={(open) => {
+          if (!open) setPublishTarget(null);
+        }}
+      >
+        <AlertDialogContent>
+          <AlertDialogHeader>
+            <AlertDialogTitle>Publish to Live?</AlertDialogTitle>
+            <AlertDialogDescription>
+              This campaign will become available for live event processing.
+            </AlertDialogDescription>
+          </AlertDialogHeader>
+          <AlertDialogFooter>
+            <AlertDialogCancel>Cancel</AlertDialogCancel>
+            <AlertDialogAction
+              onClick={() => {
+                if (publishTarget) {
+                  lifecycleMutation.mutate({ campaign: publishTarget, action: "publish" });
+                }
+              }}
+              disabled={lifecycleMutation.isPending}
+            >
+              {lifecycleMutation.isPending ? "PublishingΓÇª" : "Publish"}
+            </AlertDialogAction>
+          </AlertDialogFooter>
+        </AlertDialogContent>
+      </AlertDialog>
+
+      <AlertDialog
+        open={deleteTarget !== null}
+        onOpenChange={(open) => {
+          if (!open) setDeleteTarget(null);
+        }}
+      >
+        <AlertDialogContent>
+          <AlertDialogHeader>
+            <AlertDialogTitle>Delete campaign?</AlertDialogTitle>
+            <AlertDialogDescription>
+              {deleteTarget
+                ? `This will permanently delete "${deleteTarget.name || "Untitled campaign"}".`
+                : null}
+            </AlertDialogDescription>
+          </AlertDialogHeader>
+          <AlertDialogFooter>
+            <AlertDialogCancel>Cancel</AlertDialogCancel>
+            <AlertDialogAction
+              variant="destructive"
+              onClick={() => {
+                if (deleteTarget) deleteMutation.mutate(deleteTarget);
+              }}
+              disabled={deleteMutation.isPending}
+            >
+              {deleteMutation.isPending ? "DeletingΓÇª" : "Delete"}
+            </AlertDialogAction>
+          </AlertDialogFooter>
+        </AlertDialogContent>
+      </AlertDialog>
+    </div>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/campaigns/index.ts

diff --git a/Journeys/Journeys.UX/src/components/loyalty/campaigns/index.ts b/Journeys/Journeys.UX/src/components/loyalty/campaigns/index.ts
new file mode 100644
index 0000000..43c98ab
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/campaigns/index.ts
@@ -0,0 +1,8 @@
+export { CampaignsClient } from "./campaigns-client";
+export { CampaignCard, type CampaignAction, type CampaignCardProps } from "./campaign-card";
+export {
+  CampaignFilters,
+  EMPTY_CAMPAIGN_FILTERS,
+  type CampaignFiltersProps,
+  type CampaignFiltersValue
+} from "./campaign-filters";

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/status-badge.tsx

diff --git a/Journeys/Journeys.UX/src/components/loyalty/status-badge.tsx b/Journeys/Journeys.UX/src/components/loyalty/status-badge.tsx
new file mode 100644
index 0000000..ca90013
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/status-badge.tsx
@@ -0,0 +1,24 @@
+import { Badge } from "@/components/ui/badge";
+import { normalizeCampaignStatus } from "@/lib/campaign-kebab";
+import { cn } from "@/lib/utils";
+
+export interface StatusBadgeProps {
+  status?: string;
+}
+
+const statusStyles: Record<string, string> = {
+  live: "border-emerald-200 bg-emerald-50 text-emerald-700",
+  draft: "border-zinc-200 bg-zinc-100 text-zinc-700",
+  pause: "border-amber-200 bg-amber-50 text-amber-700",
+  archive: "border-slate-200 bg-slate-100 text-slate-600"
+};
+
+export function StatusBadge({ status }: StatusBadgeProps) {
+  const normalized = normalizeCampaignStatus(status) || "draft";
+
+  return (
+    <Badge variant="outline" className={cn("capitalize", statusStyles[normalized])}>
+      {normalized}
+    </Badge>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-kebab.test.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-kebab.test.ts b/Journeys/Journeys.UX/src/lib/campaign-kebab.test.ts
new file mode 100644
index 0000000..31fb77b
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-kebab.test.ts
@@ -0,0 +1,51 @@
+import { describe, expect, it } from "vitest";
+import { campaignKebabVisibility, kebabSaveStatus, normalizeCampaignStatus } from "./campaign-kebab";
+
+describe("normalizeCampaignStatus", () => {
+  it("maps EXP leftovers", () => {
+    expect(normalizeCampaignStatus("active")).toBe("live");
+    expect(normalizeCampaignStatus("archived")).toBe("archive");
+    expect(normalizeCampaignStatus("LIVE")).toBe("live");
+    expect(normalizeCampaignStatus("pause")).toBe("pause");
+  });
+});
+
+describe("campaignKebabVisibility", () => {
+  it("Draft: edit, duplicate, publish, delete; no agent, unpublish, archive, restore", () => {
+    const v = campaignKebabVisibility("draft", "ext");
+    expect(v).toMatchObject({
+      edit: true, agent: false, duplicate: true, versions: true,
+      publish: true, unpublish: false, archive: false, restore: false, delete: true
+    });
+  });
+  it("Live: agent, unpublish, archive; no delete, publish, restore", () => {
+    const v = campaignKebabVisibility("live", "ext");
+    expect(v).toMatchObject({
+      edit: true, agent: true, publish: false, unpublish: true,
+      archive: true, restore: false, delete: false
+    });
+  });
+  it("Pause: publish, archive; no agent", () => {
+    const v = campaignKebabVisibility("pause", "ext");
+    expect(v.agent).toBe(false);
+    expect(v.publish).toBe(true);
+    expect(v.archive).toBe(true);
+  });
+  it("Archive: restore only among mutations; no delete/agent", () => {
+    const v = campaignKebabVisibility("archive", "ext");
+    expect(v).toMatchObject({
+      agent: false, restore: true, delete: false, archive: false, publish: false
+    });
+  });
+  it("hides versions without extCampaignId", () => {
+    expect(campaignKebabVisibility("live", undefined).versions).toBe(false);
+  });
+});
+
+describe("kebabSaveStatus", () => {
+  it("does not map unpublish to draft", () => {
+    expect(kebabSaveStatus("unpublish")).toBe("pause");
+    expect(kebabSaveStatus("publish")).toBe("live");
+    expect(kebabSaveStatus("archive")).toBe("archive");
+  });
+});

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-kebab.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-kebab.ts b/Journeys/Journeys.UX/src/lib/campaign-kebab.ts
new file mode 100644
index 0000000..c523432
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-kebab.ts
@@ -0,0 +1,49 @@
+export type CampaignKebabVisibility = {
+  edit: boolean;
+  agent: boolean;
+  duplicate: boolean;
+  versions: boolean;
+  publish: boolean;
+  unpublish: boolean;
+  archive: boolean;
+  restore: boolean;
+  delete: boolean;
+};
+
+export function normalizeCampaignStatus(raw?: string): string {
+  const status = raw?.trim().toLowerCase() ?? "";
+  if (status === "active") return "live";
+  if (status === "archived") return "archive";
+  return status;
+}
+
+export function campaignKebabVisibility(
+  status: string,
+  extCampaignId?: string
+): CampaignKebabVisibility {
+  const normalized = normalizeCampaignStatus(status);
+  const isDraft = normalized === "draft" || normalized === "";
+  const isLive = normalized === "live";
+  const isArchive = normalized === "archive";
+  const isPause = normalized === "pause";
+
+  return {
+    edit: true,
+    agent: isLive,
+    duplicate: true,
+    versions: Boolean(extCampaignId),
+    publish: isDraft || isPause,
+    unpublish: isLive,
+    archive: !isArchive && !isDraft,
+    restore: isArchive,
+    delete: isDraft
+  };
+}
+
+export function kebabSaveStatus(
+  action: "publish" | "unpublish" | "archive"
+): "live" | "pause" | "archive" {
+  if (action === "publish") return "live";
+  if (action === "unpublish") return "pause";
+  return "archive";
+}

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-types.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-types.ts b/Journeys/Journeys.UX/src/lib/campaign-types.ts
new file mode 100644
index 0000000..0989781
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-types.ts
@@ -0,0 +1,3 @@
+import type { CampaignListItem } from "@/lib/api-types";
+
+export type Campaign = CampaignListItem;

### NEW FILE Journeys/Journeys.UX/src/services/loyalty/query-keys.ts

diff --git a/Journeys/Journeys.UX/src/services/loyalty/query-keys.ts b/Journeys/Journeys.UX/src/services/loyalty/query-keys.ts
new file mode 100644
index 0000000..2483b19
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/query-keys.ts
@@ -0,0 +1,12 @@
+export const loyaltyKeys = {
+  campaigns: {
+    all: ["loyalty", "campaigns"] as const,
+    list: (filters?: Record<string, unknown>) =>
+      ["loyalty", "campaigns", "list", filters] as const,
+    detail: (id: string) => ["loyalty", "campaigns", "detail", id] as const,
+    versions: (extCampaignId: string) =>
+      ["loyalty", "campaigns", "versions", extCampaignId] as const,
+    archived: (filters?: Record<string, unknown>) =>
+      ["loyalty", "campaigns", "archived", filters] as const
+  }
+} as const;

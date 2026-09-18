"use client";

/**
 * ManageTierModal — preview then commit a tier move.
 *
 * The operator picks a target campaign + tier journey, reads the preview, then
 * confirms. Submit calls `moveTier` once: there is no journey enter/exit
 * substitute when the API rejects the move, the API error is surfaced instead.
 * The `moveTier` server action owns the audited request.
 */

import { useEffect, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { toast } from "react-hot-toast";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

import { selectTierCampaigns } from "@/components/loyalty/accounts/select-tier-campaigns";
import { getCampaigns, moveTier, previewTierMove } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { canSubmitTierMove } from "./can-submit-tier-move";

const FormSchema = z.object({
  targetCampaignId: z.string().min(1, "Pick a target campaign"),
  toJourneyId: z.string().min(1, "Pick a target tier"),
  comment: z.string().min(3, "Comment must be at least 3 characters"),
});
type FormValues = z.infer<typeof FormSchema>;

export interface ManageTierModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
  onSuccess?: () => void;
}

interface TierJourneyOption {
  id: string;
  name: string;
  depth: number;
}

type JourneyNode = { id?: string | null; name?: string | null; children?: unknown };

function flattenJourneys(node: JourneyNode | null | undefined, depth = 0): TierJourneyOption[] {
  if (!node || !node.id) return [];
  const current: TierJourneyOption = { id: node.id, name: node.name ?? node.id, depth };
  const children = Array.isArray(node.children) ? (node.children as JourneyNode[]) : [];
  return [current, ...children.flatMap((child) => flattenJourneys(child, depth + 1))];
}

function formatPoints(value: number | undefined): string {
  return typeof value === "number" && Number.isFinite(value) ? value.toLocaleString() : "—";
}

export function ManageTierModal({
  open,
  onOpenChange,
  loyaltyAccountId,
  loyaltyAccountXReference,
  onSuccess,
}: ManageTierModalProps) {
  const [showPreview, setShowPreview] = useState(false);

  const tierCampaignsQuery = useQuery({
    queryKey: loyaltyKeys.campaigns.list({ tierCandidatesOnly: true }),
    queryFn: async () => {
      const result = await getCampaigns();
      if (!result.success) throw new Error(result.error ?? "Failed to load campaigns");
      return selectTierCampaigns(result.data ?? []);
    },
    enabled: open,
  });

  const form = useForm<FormValues>({
    resolver: zodResolver(FormSchema),
    defaultValues: { targetCampaignId: "", toJourneyId: "", comment: "" },
  });

  const selectedTargetCampaignId = useWatch({ control: form.control, name: "targetCampaignId" });
  const selectedTargetCampaign = useMemo(
    () => (tierCampaignsQuery.data ?? []).find((c) => c.id === selectedTargetCampaignId) ?? null,
    [tierCampaignsQuery.data, selectedTargetCampaignId]
  );

  const tierJourneys: TierJourneyOption[] = useMemo(() => {
    const journey = selectedTargetCampaign?.journey;
    if (!journey) return [];
    const root = journey as JourneyNode;
    const rootId = root.id ?? null;
    const flattened = flattenJourneys(root);
    const withoutRoot = flattened.filter((j) => j.id !== rootId);
    return (withoutRoot.length > 0 ? withoutRoot : flattened).filter((j) => Boolean(j.id));
  }, [selectedTargetCampaign]);

  const handleDialogOpenChange = (next: boolean) => {
    if (next) {
      form.reset({ targetCampaignId: "", toJourneyId: "", comment: "" });
      setShowPreview(false);
    }
    onOpenChange(next);
  };

  // One candidate campaign is the common case; pre-select it so the operator
  // only has to choose a tier.
  useEffect(() => {
    if (!open) return;
    if (!form.getValues("targetCampaignId") && (tierCampaignsQuery.data?.length ?? 0) === 1) {
      const only = tierCampaignsQuery.data?.[0];
      if (only?.id) form.setValue("targetCampaignId", only.id);
    }
  }, [open, tierCampaignsQuery.data, form]);

  const watchedToJourneyId = useWatch({ control: form.control, name: "toJourneyId" });
  const watchedComment = useWatch({ control: form.control, name: "comment" }) ?? "";

  const previewQuery = useQuery({
    queryKey: ["loyalty", "tier-preview", loyaltyAccountId, selectedTargetCampaignId, watchedToJourneyId],
    queryFn: async () => {
      const campaignId = form.getValues("targetCampaignId");
      const toJourneyId = form.getValues("toJourneyId");
      if (!campaignId || !toJourneyId) throw new Error("Missing campaign or journey");
      const result = await previewTierMove({
        loyaltyAccountId,
        loyaltyAccountXReference,
        targetCampaignId: campaignId,
        targetJourneyId: toJourneyId,
        comment: form.getValues("comment"),
      });
      if (!result.success) throw new Error(result.error ?? "Preview failed");
      return result.data ?? null;
    },
    enabled: showPreview && Boolean(selectedTargetCampaignId) && Boolean(watchedToJourneyId),
  });

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const result = await moveTier({
        loyaltyAccountId,
        loyaltyAccountXReference,
        targetCampaignId: values.targetCampaignId,
        targetJourneyId: values.toJourneyId,
        comment: values.comment,
      });
      if (!result.success) throw new Error(result.error ?? "Failed to move tier");
    },
    onSuccess: () => {
      toast.success("Tier moved");
      onSuccess?.();
      onOpenChange(false);
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const preview = previewQuery.data;
  const previewErrorMessage = (previewQuery.error as Error | null)?.message ?? "";
  const previewValidationErrors = preview?.validationErrors ?? [];
  const canSubmit = canSubmitTierMove({
    preview,
    previewError: previewQuery.error,
    isPreviewLoading: previewQuery.isLoading,
    isMutationPending: mutation.isPending,
    hasTargetJourney: Boolean(watchedToJourneyId),
    hasValidComment: watchedComment.trim().length >= 3,
  });

  const handleSubmit = form.handleSubmit((values) => {
    if (!canSubmit) return;

    const targetTier = tierJourneys.find((j) => j.id === values.toJourneyId)?.name ?? values.toJourneyId;
    if (!window.confirm(`Move this account to ${targetTier}? This will update the member tier.`)) {
      return;
    }
    mutation.mutate(values);
  });

  const submitDisabled = !canSubmit;

  return (
    <Dialog open={open} onOpenChange={handleDialogOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Manage tier</DialogTitle>
          <DialogDescription>Preview and confirm a tier move for this loyalty account.</DialogDescription>
        </DialogHeader>

        {tierCampaignsQuery.isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : tierCampaignsQuery.error ? (
          <Alert variant="destructive">
            <AlertDescription>{(tierCampaignsQuery.error as Error).message}</AlertDescription>
          </Alert>
        ) : (tierCampaignsQuery.data?.length ?? 0) === 0 ? (
          <Alert>
            <AlertDescription>No campaigns are available for a tier move.</AlertDescription>
          </Alert>
        ) : (
          <Form {...form}>
            <form onSubmit={handleSubmit} className="space-y-4">
              <FormField
                control={form.control}
                name="targetCampaignId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Target campaign</FormLabel>
                    <Select
                      onValueChange={(v) => {
                        field.onChange(v);
                        form.setValue("toJourneyId", "");
                        setShowPreview(false);
                      }}
                      value={field.value}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Choose a campaign" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {(tierCampaignsQuery.data ?? []).map((c) =>
                          c.id ? (
                            <SelectItem key={c.id} value={c.id}>
                              {c.name ?? c.id}
                            </SelectItem>
                          ) : null
                        )}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="toJourneyId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Target tier</FormLabel>
                    <Select
                      onValueChange={(v) => {
                        field.onChange(v);
                        setShowPreview(true);
                      }}
                      value={field.value}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Choose a tier" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {tierJourneys.map((j) => (
                          <SelectItem key={j.id} value={j.id}>
                            {"\u00A0".repeat(j.depth * 2)}
                            {j.depth > 0 ? "↳ " : ""}
                            {j.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {showPreview && (
                <Card>
                  <CardContent className="p-4">
                    <h4 className="mb-2 text-sm font-semibold">Preview</h4>
                    {previewQuery.isLoading ? (
                      <Skeleton className="h-12 w-full" />
                    ) : previewQuery.error ? (
                      <Alert variant="destructive">
                        <AlertDescription>{previewErrorMessage}</AlertDescription>
                      </Alert>
                    ) : preview ? (
                      <div className="space-y-3">
                        {(preview.isTierDemotion || preview.isSameJourney || preview.hasNegativeAdjustment) && (
                          <Alert variant="destructive">
                            <AlertDescription>
                              {preview.isTierDemotion && "Tier level cannot be demoted. "}
                              {preview.isSameJourney && "Selected journey matches current journey. "}
                              {preview.hasNegativeAdjustment && "Move results in a negative point adjustment. "}
                            </AlertDescription>
                          </Alert>
                        )}
                        {previewValidationErrors.length > 0 && (
                          <Alert variant="destructive">
                            <AlertDescription>{previewValidationErrors.join(" ")}</AlertDescription>
                          </Alert>
                        )}
                        <div className="grid grid-cols-1 gap-3 text-xs sm:grid-cols-2">
                          <div className="rounded border p-2">
                            <p className="mb-1 font-medium">Tier qualification</p>
                            <p>Current: {formatPoints(preview.currentTierQualificationBalance)}</p>
                            <p>Adjustment: {formatPoints(preview.tierQualificationAmount)}</p>
                            <p>Projected: {formatPoints(preview.projectedTierQualificationBalance)}</p>
                          </div>
                          <div className="rounded border p-2">
                            <p className="mb-1 font-medium">Dealer spendable</p>
                            <p>Current: {formatPoints(preview.currentDealerSpendableBalance)}</p>
                            <p>Adjustment: {formatPoints(preview.dealerSpendableAmount)}</p>
                            <p>Projected: {formatPoints(preview.projectedDealerSpendableBalance)}</p>
                          </div>
                        </div>
                      </div>
                    ) : (
                      <p className="text-sm text-muted-foreground">Pick a tier to preview the impact.</p>
                    )}
                  </CardContent>
                </Card>
              )}

              {mutation.error instanceof Error && (
                <Alert variant="destructive">
                  <AlertDescription>{mutation.error.message}</AlertDescription>
                </Alert>
              )}

              <FormField
                control={form.control}
                name="comment"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Comment</FormLabel>
                    <FormControl>
                      <Textarea rows={3} placeholder="Reason for tier move" {...field} />
                    </FormControl>
                    <FormDescription>Minimum 3 characters. Recorded with the movement.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => onOpenChange(false)}
                  disabled={mutation.isPending}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={submitDisabled}>
                  {mutation.isPending ? "Moving…" : "Move tier"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  );
}

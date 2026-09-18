"use client";

/**
 * AccountJourneyModal — assign or remove a campaign journey for one loyalty
 * account. Used by `account-actions-dropdown` on the account detail screen.
 *
 * Form: react-hook-form + Zod. Fields: campaign, journey, comment (>=3 chars).
 * Submit calls the `enterJourney` / `exitJourney` server actions, which own the
 * audited request; the browser never supplies audit data.
 */

import { useMemo } from "react";
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
import { Skeleton } from "@/components/ui/skeleton";

import { enterJourney, exitJourney, getCampaigns } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

const FormSchema = z.object({
  campaignId: z.string().min(1, "Campaign is required"),
  journeyId: z.string().min(1, "Journey is required"),
  comment: z.string().min(3, "Comment must be at least 3 characters"),
});
type FormValues = z.infer<typeof FormSchema>;

export type JourneyMode = "assign" | "remove";

export interface AccountJourneyModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: JourneyMode;
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
  onSuccess?: () => void;
}

interface JourneyOption {
  id: string;
  name: string;
  depth: number;
}

function flattenJourneyOptions(node: unknown, depth = 0): JourneyOption[] {
  const current = node as
    | {
        id?: string;
        Id?: string;
        name?: string;
        Name?: string;
        children?: unknown;
        Children?: unknown;
      }
    | undefined;

  const id = current?.id ?? current?.Id ?? "";
  if (!id) return [];

  const options: JourneyOption[] = [{ id, name: current?.name ?? current?.Name ?? id, depth }];
  const children = Array.isArray(current?.children)
    ? current.children
    : Array.isArray(current?.Children)
      ? current.Children
      : [];

  for (const child of children) {
    options.push(...flattenJourneyOptions(child, depth + 1));
  }

  return options;
}

export function AccountJourneyModal({
  open,
  onOpenChange,
  mode,
  loyaltyAccountId,
  loyaltyAccountXReference,
  onSuccess,
}: AccountJourneyModalProps) {
  const isAssign = mode === "assign";

  const campaignsQuery = useQuery({
    queryKey: loyaltyKeys.campaigns.list({ activeOnly: true }),
    queryFn: async () => {
      const response = await getCampaigns();
      if (!response.success) throw new Error(response.error ?? "Failed to load campaigns");
      return (response.data ?? []).filter((c) => {
        const status = (c.status ?? "").toLowerCase();
        return status === "live" || status === "active";
      });
    },
    enabled: open,
  });

  const form = useForm<FormValues>({
    resolver: zodResolver(FormSchema),
    defaultValues: { campaignId: "", journeyId: "", comment: "" },
  });

  // Re-seed the form when the dialog opens rather than in an effect.
  const handleDialogOpenChange = (next: boolean) => {
    if (next) form.reset({ campaignId: "", journeyId: "", comment: "" });
    onOpenChange(next);
  };

  const selectedCampaignId = useWatch({ control: form.control, name: "campaignId" });
  const selectableCampaigns = useMemo(
    () => (campaignsQuery.data ?? []).filter((c) => Boolean(c.id)),
    [campaignsQuery.data]
  );
  const selectedCampaign = useMemo(
    () => selectableCampaigns.find((c) => c.id === selectedCampaignId),
    [selectableCampaigns, selectedCampaignId]
  );

  const journeys = useMemo(() => {
    if (!selectedCampaign?.journey) return [];
    return flattenJourneyOptions(selectedCampaign.journey);
  }, [selectedCampaign]);

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const input = {
        campaignId: values.campaignId,
        journeyId: values.journeyId,
        loyaltyAccountId,
        loyaltyAccountXReference,
        comment: values.comment,
      };
      const response = isAssign ? await enterJourney(input) : await exitJourney(input);
      if (!response.success) {
        throw new Error(response.error ?? `Failed to ${isAssign ? "assign" : "remove"} journey`);
      }
    },
    onSuccess: () => {
      toast.success(isAssign ? "Journey assigned" : "Journey removed");
      onSuccess?.();
      onOpenChange(false);
    },
    onError: (error: Error) => {
      toast.error(error.message);
    },
  });

  const handleSubmit = form.handleSubmit((values) => mutation.mutate(values));

  return (
    <Dialog open={open} onOpenChange={handleDialogOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{isAssign ? "Assign journey" : "Remove journey"}</DialogTitle>
          <DialogDescription>
            {isAssign
              ? "Pick a campaign + journey to manually enter for this account."
              : "Pick a campaign + journey to manually exit for this account."}
          </DialogDescription>
        </DialogHeader>

        {campaignsQuery.isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : campaignsQuery.error ? (
          <Alert variant="destructive">
            <AlertDescription>{(campaignsQuery.error as Error).message}</AlertDescription>
          </Alert>
        ) : selectableCampaigns.length === 0 ? (
          <Alert>
            <AlertDescription>No campaigns available to {isAssign ? "assign" : "remove"}.</AlertDescription>
          </Alert>
        ) : (
          <Form {...form}>
            <form onSubmit={handleSubmit} className="space-y-4">
              {mutation.error instanceof Error && (
                <Alert variant="destructive">
                  <AlertDescription>{mutation.error.message}</AlertDescription>
                </Alert>
              )}

              <FormField
                control={form.control}
                name="campaignId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Campaign</FormLabel>
                    <Select
                      onValueChange={(v) => {
                        field.onChange(v);
                        form.setValue("journeyId", "");
                      }}
                      value={field.value}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select a campaign" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {selectableCampaigns.map((c) =>
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

              {journeys.length > 0 && (
                <FormField
                  control={form.control}
                  name="journeyId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Journey</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value}>
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue placeholder="Select a journey" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {journeys.map((j) => (
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
              )}

              <FormField
                control={form.control}
                name="comment"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Comment</FormLabel>
                    <FormControl>
                      <Textarea rows={3} placeholder="Reason for this journey change…" {...field} />
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
                <Button type="submit" disabled={mutation.isPending}>
                  {mutation.isPending ? "Processing…" : isAssign ? "Assign journey" : "Remove journey"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  );
}

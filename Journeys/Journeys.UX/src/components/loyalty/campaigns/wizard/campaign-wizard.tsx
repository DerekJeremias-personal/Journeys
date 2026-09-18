"use client";

/**
 * CampaignWizard — top-level orchestrator. Owns:
 *   - The single `react-hook-form` instance for the scalar campaign fields.
 *   - The active wizard step (synced into the Zustand wizard-store so other
 *     subtree components can subscribe).
 *   - The submit mutation (`createCampaign` / `updateCampaign`) plus query
 *     invalidation and a success toast.
 *   - Unsaved-changes confirmation when cancelling.
 *
 * The journey graph itself lives in the Zustand store (mutated by the journey
 * builder). At submit time we merge it into the form values and POST.
 */

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm, FormProvider, useWatch, type DefaultValues } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "react-hot-toast";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Tabs, TabsContent } from "@/components/ui/tabs";
import { WizardStepper } from "./components/wizard-stepper";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

import {
  asLoyaltySchemas,
  CampaignStatusSchema,
  LoyaltySegmentSchema,
  type Campaign,
  type Journey,
  type LoyaltySchema,
} from "@/lib/campaign-types";
import { applyWizardSaveIdentity, toWizardStatus, type WizardSaveMode } from "@/lib/campaign-live-edit";
import { deleteCampaign, getAllSchemas, updateCampaign } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

import {
  WIZARD_STEPS,
  createEmptyJourney,
  selectActiveStep,
  useWizardActions,
  useWizardStore,
  type WizardStep,
} from "./wizard-store";
import { CampaignDetailsStep } from "./steps/campaign-details-step";
import { JourneyBuilderStep } from "./steps/journey-builder-step";
import { ReviewStep } from "./steps/review-step";

// ─── Schema ──────────────────────────────────────────────────────────────────

/**
 * Wizard-side schema for the scalar form fields. Built fresh (rather than
 * `.partial().extend()` from `CampaignSchema`) to keep the zodResolver
 * input/output types identical — Zod 4 + `@hookform/resolvers` v5 surface a
 * spurious TS mismatch otherwise. The journey graph lives in the Zustand
 * wizard store and is merged in at submit time.
 */
export const campaignWizardSchema = z.object({
  name: z.string().min(1, "Campaign name is required"),
  extCampaignId: z.string().nullable().optional(),
  status: CampaignStatusSchema,
  startDate: z.string().min(1, "Start date is required"),
  endDate: z.string().nullable().optional(),
  events: z.array(z.string()).optional(),
  segments: z.array(LoyaltySegmentSchema).optional(),
  id: z.string().nullable().optional(),
  tenantId: z.string().nullable().optional(),
  etag: z.string().nullable().optional(),
});

export type CampaignWizardValues = z.infer<typeof campaignWizardSchema>;

export function buildCampaignWizardDefaults(initial?: Partial<Campaign>): DefaultValues<CampaignWizardValues> {
  return {
    name: initial?.name ?? "",
    extCampaignId: initial?.extCampaignId ?? "",
    status: toWizardStatus(initial?.status),
    startDate: initial?.startDate ?? new Date().toISOString(),
    endDate: initial?.endDate ?? null,
    events: initial?.events ?? [],
    segments: initial?.segments ?? [],
    id: initial?.id,
    tenantId: initial?.tenantId,
    etag: initial?.etag,
  };
}

// ─── Component ───────────────────────────────────────────────────────────────

export interface CampaignWizardProps {
  mode: "create" | "edit";
  saveMode?: WizardSaveMode;
  initial?: Partial<Campaign>;
  onClose?: () => void;
}

export function CampaignWizard({ mode, saveMode = "edit", initial, onClose }: CampaignWizardProps) {
  const router = useRouter();
  const queryClient = useQueryClient();

  const activeStep = useWizardStore(selectActiveStep);
  const isJourneyDirty = useWizardStore((s) => s.isJourneyDirty);
  const selectedSchema = useWizardStore((s) => s.selectedSchema);
  const { setActiveStep, reset, setJourney, setJourneyDirty, setSelectedSchema } = useWizardActions();

  const initialJourney = useMemo<Journey>(() => initial?.journey ?? createEmptyJourney(), [initial?.journey]);

  const form = useForm<CampaignWizardValues>({
    resolver: zodResolver(campaignWizardSchema),
    defaultValues: buildCampaignWizardDefaults(initial),
  });

  // Fetch ALL schemas (unfiltered) specifically for schema hydration from saved
  // campaign events (edit mode + create-from-template). We use a distinct query
  // key ("all") so this does NOT collide with CampaignDetailsStep's filtered
  // eventable-only query (key "loyalty") — they share different queryFn shapes
  // and must not corrupt each other's cache entry.
  const allSchemasQuery = useQuery({
    queryKey: loyaltyKeys.schemas.all("all"),
    queryFn: async () => {
      const result = await getAllSchemas();
      if (!result.success) throw new Error(result.error ?? "Failed to load event schemas");
      return asLoyaltySchemas(result.data);
    },
  });

  // Hydrate `selectedSchema` from the form's events list once schemas are
  // available. Covers create-with-template + edit-existing-campaign flows.
  // Only seeds when nothing is cached yet — Details step's `toggleEvent`
  // takes over after that.
  const events = useWatch({ control: form.control, name: "events", defaultValue: [] }) as string[];
  useEffect(() => {
    const schemas = allSchemasQuery.data;
    if (!schemas || selectedSchema || events.length === 0) return;
    for (const event of events) {
      const lowered = event.toLowerCase();
      const match = schemas.find(
        (s: LoyaltySchema) => s.id.toLowerCase() === lowered || s.name.toLowerCase() === lowered
      );
      if (match) {
        setSelectedSchema(match);
        return;
      }
    }
  }, [allSchemasQuery.data, events, selectedSchema, setSelectedSchema]);

  const [confirmCancelOpen, setConfirmCancelOpen] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);

  const deleteMutation = useMutation({
    mutationFn: async () => {
      const id = form.getValues("id");
      const status = form.getValues("status");
      if (!id) throw new Error("No campaign id to delete");
      const response = await deleteCampaign(id, status);
      if (!response.success) throw new Error(response.error ?? "Failed to delete campaign");
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: loyaltyKeys.campaigns.all });
      void queryClient.invalidateQueries({ queryKey: loyaltyKeys.dashboard.data });
      toast.success("Campaign deleted");
      router.push("/loyalty/campaigns");
    },
    onError: (error: Error) => {
      toast.error(error.message);
    },
  });

  // Hydrate the wizard store on first mount + when the `initial` prop swaps.
  const hydratedRef = useRef(false);
  useEffect(() => {
    if (!hydratedRef.current) {
      reset({ journey: initialJourney, activeStep: "details" });
      hydratedRef.current = true;
    } else {
      setJourney(initialJourney);
    }
  }, [initialJourney, reset, setJourney]);

  // Browser-tab close guard for unsaved changes — covers BOTH RHF scalar
  // fields AND journey-graph-only edits (add/remove/patch node).
  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (form.formState.isDirty || isJourneyDirty) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [form.formState.isDirty, isJourneyDirty]);

  const submitMutation = useMutation({
    mutationFn: async (values: CampaignWizardValues): Promise<Campaign> => {
      // Use the journey directly from the Zustand store — the store is the
      // source of truth for structural mutations (add/remove/patch node).
      // The flow-to-journey inverse is used only when React Flow drag-positions
      // are needed; for this wizard, topology changes go through the store actions.
      const journey = useWizardStore.getState().journey;
      const resolvedSaveMode: WizardSaveMode =
        mode === "create" || saveMode === "new-draft-same-ext" ? "new-draft-same-ext" : "edit";
      const payload = applyWizardSaveIdentity(
        { ...values, journey } as Campaign,
        resolvedSaveMode,
        {
          extCampaignId: saveMode === "new-draft-same-ext" ? (initial?.extCampaignId ?? undefined) : undefined,
          sourceStatus: initial?.status,
        }
      );
      const response = await updateCampaign(payload);
      if (!response.success || !response.data) {
        throw new Error(response.error ?? `Failed to ${mode} campaign`);
      }
      return response.data as Campaign;
    },
    onSuccess: (campaign) => {
      void queryClient.invalidateQueries({ queryKey: loyaltyKeys.campaigns.all });
      void queryClient.invalidateQueries({ queryKey: loyaltyKeys.dashboard.data });
      setJourneyDirty(false);
      form.reset(buildCampaignWizardDefaults(campaign));
      toast.success(mode === "create" ? "Campaign created" : "Campaign updated");
      if (campaign.id) {
        const status = toWizardStatus(campaign.status);
        router.push(
          `/loyalty/campaigns/${campaign.id}?campaignStatus=${encodeURIComponent(status)}`
        );
      }
    },
    onError: (error: Error) => {
      toast.error(error.message);
    },
  });

  const handleSubmit = form.handleSubmit((values) => submitMutation.mutate(values));

  const goToStep = (step: WizardStep) => setActiveStep(step);
  const handleNext = async () => {
    if (activeStep === "details") {
      const ok = await form.trigger(["name", "status", "startDate", "events", "segments"]);
      if (!ok) return;
    }
    goToStep(nextStep(activeStep));
  };

  const requestClose = () => {
    if (form.formState.isDirty || isJourneyDirty) {
      setConfirmCancelOpen(true);
    } else {
      if (onClose) {
        onClose();
      } else {
        router.push("/loyalty/campaigns");
      }
    }
  };

  // Map RHF errors → which wizard step the failing field belongs to so the
  // stepper can render the failing step in destructive state.
  const errorSteps = useMemo<ReadonlyArray<WizardStep>>(() => {
    const errors = form.formState.errors as Record<string, unknown>;
    const detailFields = new Set(["name", "extCampaignId", "status", "startDate", "endDate", "events", "segments"]);
    const out: WizardStep[] = [];
    if (Object.keys(errors).some((k) => detailFields.has(k))) out.push("details");
    return out;
  }, [form.formState.errors]);

  return (
    <FormProvider {...form}>
      <form onSubmit={handleSubmit} className="space-y-6">
        <WizardStepper activeStep={activeStep} errorSteps={errorSteps} onStepClick={goToStep} />

        <Tabs value={activeStep} onValueChange={(v) => goToStep(v as WizardStep)}>
          <TabsContent value="details" className="mt-2">
            <CampaignDetailsStep
              lockExtCampaignId={saveMode === "new-draft-same-ext"}
              lockLiveStatus={saveMode === "edit" && toWizardStatus(initial?.status) === "live"}
            />
          </TabsContent>
          <TabsContent value="journey" className="mt-2">
            <JourneyBuilderStep />
          </TabsContent>
          <TabsContent value="review" className="mt-2">
            <ReviewStep />
          </TabsContent>
        </Tabs>

        <div className="flex items-center justify-between border-t border-border pt-4">
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" onClick={requestClose} disabled={submitMutation.isPending}>
              Cancel
            </Button>
            {mode === "edit" && saveMode !== "new-draft-same-ext" && (
              <Button
                type="button"
                variant="destructive"
                onClick={() => setConfirmDeleteOpen(true)}
                disabled={submitMutation.isPending || deleteMutation.isPending}
              >
                Delete campaign
              </Button>
            )}
          </div>
          <div className="flex items-center gap-2">
            {activeStep !== "details" && (
              <Button
                type="button"
                variant="outline"
                onClick={() => goToStep(prevStep(activeStep))}
                disabled={submitMutation.isPending}
              >
                Back
              </Button>
            )}
            {activeStep !== "review" ? (
              <Button type="button" onClick={() => void handleNext()}>
                Next
              </Button>
            ) : (
              <Button type="submit" disabled={submitMutation.isPending}>
                {submitMutation.isPending ? "Saving…" : mode === "create" ? "Create campaign" : "Save changes"}
              </Button>
            )}
          </div>
        </div>
      </form>

      <AlertDialog open={confirmCancelOpen} onOpenChange={setConfirmCancelOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard unsaved changes?</AlertDialogTitle>
            <AlertDialogDescription>
              The campaign hasn&apos;t been saved. Closing the wizard will discard everything you&apos;ve entered.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmCancelOpen(false);
                if (onClose) {
                  onClose();
                } else {
                  router.push("/loyalty/campaigns");
                }
              }}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Discard
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={confirmDeleteOpen} onOpenChange={setConfirmDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this campaign?</AlertDialogTitle>
            <AlertDialogDescription>
              This action cannot be undone. The campaign will be permanently deleted.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => deleteMutation.mutate()}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMutation.isPending ? "Deleting…" : "Delete campaign"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </FormProvider>
  );
}

function prevStep(step: WizardStep): WizardStep {
  const idx = WIZARD_STEPS.findIndex((s) => s.id === step);
  return WIZARD_STEPS[Math.max(0, idx - 1)]?.id ?? "details";
}

function nextStep(step: WizardStep): WizardStep {
  const idx = WIZARD_STEPS.findIndex((s) => s.id === step);
  return WIZARD_STEPS[Math.min(WIZARD_STEPS.length - 1, idx + 1)]?.id ?? "review";
}

"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";

import type { Campaign } from "@/lib/campaign-types";

import {
  buildCampaignWizardDefaults,
  campaignWizardSchema,
  type CampaignWizardValues,
} from "@/components/loyalty/campaigns/wizard/campaign-wizard";

import { selectCampaign, selectHydrateGeneration, useJourneyBuilderActions, useJourneyBuilderStore } from "./journey-builder-store";

export function JourneyBuilderFormProvider({ children }: { children: ReactNode }) {
  const campaign = useJourneyBuilderStore(selectCampaign);
  const hydrateGeneration = useJourneyBuilderStore(selectHydrateGeneration);
  const actions = useJourneyBuilderActions();
  const hydratingRef = useRef(false);

  const form = useForm<CampaignWizardValues>({
    resolver: zodResolver(campaignWizardSchema),
    defaultValues: buildCampaignWizardDefaults(campaign ?? undefined),
  });

  useEffect(() => {
    hydratingRef.current = true;
    form.reset(buildCampaignWizardDefaults(useJourneyBuilderStore.getState().campaign ?? undefined));
    hydratingRef.current = false;
  }, [hydrateGeneration, form]);

  useEffect(() => {
    const subscription = form.watch((values) => {
      if (hydratingRef.current || !values) {
        return;
      }
      actions.patchCampaignScalars(values as Partial<Campaign>);
    });
    return () => subscription.unsubscribe();
  }, [actions, form]);

  return <FormProvider {...form}>{children}</FormProvider>;
}

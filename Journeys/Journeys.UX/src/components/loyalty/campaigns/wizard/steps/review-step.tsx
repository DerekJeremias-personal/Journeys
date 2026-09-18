"use client";

/**
 * ReviewStep — final step. Shows form/journey validation summary above a
 * read-only preview of the assembled campaign payload.
 */

import { useFormContext } from "react-hook-form";

import { selectJourney, useWizardStore } from "../wizard-store";
import type { CampaignWizardValues } from "../campaign-wizard";
import { ValidationSummary } from "../components/validation-summary";
import { CampaignPreview } from "../components/campaign-preview";

export function ReviewStep() {
  const form = useFormContext<CampaignWizardValues>();
  const journey = useWizardStore(selectJourney);

  const formValues = form.getValues();
  const previewValue = { ...formValues, journey };

  return (
    <div className="space-y-4">
      <ValidationSummary errors={form.formState.errors as Record<string, unknown>} journey={journey} />
      <CampaignPreview value={previewValue} />
    </div>
  );
}

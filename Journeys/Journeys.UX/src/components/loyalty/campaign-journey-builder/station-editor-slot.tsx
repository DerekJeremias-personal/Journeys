"use client";

import { JOURNEY_BUILDER_STEPS } from "./journey-builder-steps";
import { CriteriaStationEditor } from "./wizard-station-editors/criteria-station-editor";
import { ActionsStationEditor } from "./wizard-station-editors/actions-station-editor";
import { SetupStationEditor } from "./wizard-station-editors/setup-station-editor";

export function renderJourneyStationEditor(index: number) {
  const step = JOURNEY_BUILDER_STEPS[index];
  if (!step) {
    return null;
  }

  switch (step.key) {
    case "setup":
      return <SetupStationEditor />;
    case "eligibility":
      return <CriteriaStationEditor variant="eligibility" />;
    case "criteria":
      return <CriteriaStationEditor variant="criteria" />;
    case "actions":
      return <ActionsStationEditor />;
    case "review":
      return null;
    default: {
      const exhaustive: never = step.key;
      return exhaustive;
    }
  }
}

/**
 * Barrel for the Phase 07 Campaign Wizard subtree.
 */

export {
  CampaignWizard,
  campaignWizardSchema,
  type CampaignWizardProps,
  type CampaignWizardValues,
} from "./campaign-wizard";
export {
  WIZARD_STEPS,
  createEmptyJourney,
  selectActiveStep,
  selectJourney,
  selectSelectedNodeId,
  selectValidationErrors,
  useWizardActions,
  useWizardStore,
  type WizardJourneyActions,
  type WizardState,
  type WizardStep,
} from "./wizard-store";
export { WIZARD_TEMPLATES, type CampaignTemplate, type TemplateId, type TemplateTone } from "./wizard-templates";
export { TemplatesPicker, type TemplatesPickerProps } from "./components/templates-picker";
export { JourneyFlow } from "./components/journey-flow";
export { NodeConfigPanel } from "./components/node-config-panel";
export { CampaignPreview, type CampaignPreviewProps } from "./components/campaign-preview";
export { ValidationSummary, type ValidationSummaryProps } from "./components/validation-summary";
export {
  useJourneyTransform,
  journeyToFlow,
  type JourneyNodeData,
  type NavigationEdgeData,
} from "./hooks/use-journey-transform";

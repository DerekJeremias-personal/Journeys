/**
 * Zustand store for the Campaign Wizard's non-form state.
 *
 * The form (`react-hook-form`) owns the scalar campaign fields (name, status,
 * dates, events…). This store owns:
 *   - The active wizard step.
 *   - The working `Journey` graph (recursive — kept out of RHF for performance
 *     and so structural mutations can fire without re-rendering every field).
 *   - The currently selected journey node id.
 *   - Journey-graph validation errors keyed by node id (in addition to the RHF
 *     `formState.errors` which the wizard surfaces via `<ValidationSummary>`).
 *
 * Selectors and actions are exported separately so consumers can subscribe to
 * narrow slices without re-rendering on unrelated state changes.
 */

import { create } from "zustand";
import { useShallow } from "zustand/react/shallow";
import type { Journey, LoyaltySchema } from "@/lib/campaign-types";

export type WizardStep = "details" | "journey" | "review";

export const WIZARD_STEPS: ReadonlyArray<{
  id: WizardStep;
  label: string;
  description: string;
}> = [
  { id: "details", label: "Campaign details", description: "Name, schedule, and event types" },
  { id: "journey", label: "Journey builder", description: "Design the journey graph" },
  { id: "review", label: "Review", description: "Validate and submit" },
] as const;

export interface WizardJourneyActions {
  setActiveStep: (step: WizardStep) => void;
  setJourney: (journey: Journey) => void;
  patchJourneyNode: (nodeId: string, patch: Partial<Journey>) => void;
  addChildNode: (parentNodeId: string, child: Journey) => void;
  removeNode: (nodeId: string) => void;
  setSelectedNodeId: (id: string | null) => void;
  setValidationErrors: (errors: Record<string, string[]>) => void;
  /**
   * Set the schema currently driving the rule + navigation editors. When the
   * Details step toggles event types, the *first* selected event's schema is
   * cached here so that downstream editors can render `payload.<symbol>`
   * suggestions for `PathValueProvider` and `TaxonomicRule.itemsPropertyPath`
   * dropdowns. `null` falls back to free-text input.
   */
  setSelectedSchema: (schema: LoyaltySchema | null) => void;
  reset: (next: { journey: Journey; activeStep?: WizardStep; selectedSchema?: LoyaltySchema | null }) => void;
  /**
   * Track journey-graph mutations (add/remove node, patch node, promotions,
   * navigation edits). RHF `formState.isDirty` only covers scalar fields;
   * the unsaved-changes guard also checks this flag so journey-only edits
   * are correctly guarded.
   */
  setJourneyDirty: (dirty: boolean) => void;
}

export interface WizardState {
  activeStep: WizardStep;
  journey: Journey;
  selectedNodeId: string | null;
  /**
   * Schema providing payload-property suggestions for the rule + navigation
   * editors. Resolved by the Details step from the user's event-type pick.
   */
  selectedSchema: LoyaltySchema | null;
  validationErrors: Record<string, string[]>;
  /** True when the journey graph has been modified since the last reset/save. */
  isJourneyDirty: boolean;
  actions: WizardJourneyActions;
}

/** Build an empty single-node Journey graph for the create flow. */
export function createEmptyJourney(): Journey {
  const id = crypto.randomUUID();
  return {
    id,
    rootNodeId: id,
    name: "Root",
    rules: [],
    children: [],
    navigation: {},
    tenantId: null,
  };
}

/**
 * Walks the journey tree and, for every node whose id matches `predicate`,
 * applies `transform`. Returns a structurally-cloned tree. Pure.
 */
function mapJourney(node: Journey, transform: (n: Journey) => Journey): Journey {
  const next = transform(node);
  if (!next.children || next.children.length === 0) return next;
  return {
    ...next,
    children: next.children.map((child) => mapJourney(child, transform)),
  };
}

/**
 * D13 LOCKED: Remove a journey node and splice its children onto the parent
 * that held it. This preserves the journey graph continuity — descendants
 * of the deleted node are NOT lost; they are reconnected.
 *
 * Example: parent → [A, B] where A has [A1, A2]
 *   After removeNode(A): parent → [A1, A2, B]
 *
 * Root guard: callers must ensure `targetId !== root.id`.
 */
function pruneJourney(node: Journey, targetId: string): Journey {
  const children = node.children ?? [];

  // Does this node's children array contain the target?
  const targetIdx = children.findIndex((c) => c.id === targetId);

  let nextChildren: Journey[];
  if (targetIdx !== -1) {
    // Replace the target with its own children (spliced in-place), then recurse
    const target = children[targetIdx]!;
    const targetDescendants = target.children ?? [];
    nextChildren = [...children.slice(0, targetIdx), ...targetDescendants, ...children.slice(targetIdx + 1)].map((c) =>
      pruneJourney(c, targetId)
    );
  } else {
    nextChildren = children.map((c) => pruneJourney(c, targetId));
  }

  return { ...node, children: nextChildren };
}

export const useWizardStore = create<WizardState>((set) => ({
  activeStep: "details",
  journey: createEmptyJourney(),
  selectedNodeId: null,
  selectedSchema: null,
  validationErrors: {},
  isJourneyDirty: false,
  actions: {
    setActiveStep: (step) => set({ activeStep: step }),
    setJourney: (journey) => set({ journey }),
    patchJourneyNode: (nodeId, patch) =>
      set((state) => ({
        journey: mapJourney(state.journey, (n) => (n.id === nodeId ? { ...n, ...patch } : n)),
        isJourneyDirty: true,
      })),
    addChildNode: (parentNodeId, child) =>
      set((state) => ({
        journey: mapJourney(state.journey, (n) =>
          n.id === parentNodeId ? { ...n, children: [...(n.children ?? []), child] } : n
        ),
        isJourneyDirty: true,
      })),
    removeNode: (nodeId) =>
      set((state) => {
        if (state.journey.id === nodeId) return state;
        return {
          journey: pruneJourney(state.journey, nodeId),
          selectedNodeId: state.selectedNodeId === nodeId ? null : state.selectedNodeId,
          isJourneyDirty: true,
        };
      }),
    setSelectedNodeId: (id) => set({ selectedNodeId: id }),
    setSelectedSchema: (schema) => set({ selectedSchema: schema }),
    setValidationErrors: (errors) => set({ validationErrors: errors }),
    setJourneyDirty: (dirty) => set({ isJourneyDirty: dirty }),
    reset: (next) =>
      set({
        activeStep: next.activeStep ?? "details",
        journey: next.journey,
        selectedNodeId: null,
        selectedSchema: next.selectedSchema ?? null,
        validationErrors: {},
        isJourneyDirty: false,
      }),
  },
}));

// ─── Selectors ───────────────────────────────────────────────────────────────

export const selectActiveStep = (s: WizardState): WizardStep => s.activeStep;
export const selectJourney = (s: WizardState): Journey => s.journey;
export const selectSelectedNodeId = (s: WizardState): string | null => s.selectedNodeId;
export const selectSelectedSchema = (s: WizardState): LoyaltySchema | null => s.selectedSchema;
export const selectValidationErrors = (s: WizardState): Record<string, string[]> => s.validationErrors;

/**
 * Stable actions selector — Zustand returns the same object identity each call
 * because `actions` was constructed once inside `create`. Wrap callers with
 * `useShallow` only when selecting non-action slices.
 */
export const useWizardActions = (): WizardJourneyActions => useWizardStore((s) => s.actions);

/** Re-export `useShallow` so callers don't need to import it directly. */
export { useShallow };

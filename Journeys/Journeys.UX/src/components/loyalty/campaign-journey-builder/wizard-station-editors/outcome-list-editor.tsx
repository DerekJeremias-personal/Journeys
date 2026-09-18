"use client";

import { useMemo, useState } from "react";
import { Pencil, Plus, Trash2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  OutcomeEditorModal,
  outcomeToFormValues,
} from "@/components/loyalty/campaigns/wizard/modals/outcome-editor-modal";
import type { Journey, Outcome } from "@/lib/campaign-types";

import {
  labelForOutcomeKind,
  outcomesFromRuleSet,
  primaryRuleSet,
  upsertPrimaryRuleSetOutcomes,
} from "../outcome-utils";

export interface OutcomeListEditorProps {
  readonly targetNode: Journey;
  readonly onPatchNode: (patch: Partial<Journey>) => void;
}

export function OutcomeListEditor({ targetNode, onPatchNode }: OutcomeListEditorProps) {
  const [modalOpen, setModalOpen] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);

  const ruleSet = useMemo(() => primaryRuleSet(targetNode), [targetNode]);
  const outcomes = useMemo(() => outcomesFromRuleSet(ruleSet), [ruleSet]);

  const persistOutcomes = (nextOutcomes: Outcome[]) => {
    onPatchNode({
      rules: upsertPrimaryRuleSetOutcomes(targetNode, nextOutcomes),
    });
  };

  const editingOutcome = editingIndex != null ? (outcomes[editingIndex] ?? null) : null;

  return (
    <div className="space-y-3">
      {outcomes.length === 0 ? (
        <p className="text-sm text-muted-foreground">No actions yet. Add what happens when criteria are met.</p>
      ) : (
        <ul className="space-y-2">
          {outcomes.map((outcome, index) => (
            <li
              key={`${outcome.Kind}-${String(index)}`}
              className="flex items-center justify-between rounded-lg border px-3 py-2"
            >
              <span className="text-sm font-medium">{labelForOutcomeKind(outcome.Kind)}</span>
              <div className="flex gap-1">
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Edit action"
                  onClick={() => {
                    setEditingIndex(index);
                    setModalOpen(true);
                  }}
                >
                  <Pencil className="h-4 w-4" />
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Remove action"
                  onClick={() => persistOutcomes(outcomes.filter((_, i) => i !== index))}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <Button
        type="button"
        variant="outline"
        onClick={() => {
          setEditingIndex(null);
          setModalOpen(true);
        }}
      >
        <Plus className="mr-1.5 h-4 w-4" aria-hidden />
        Add action
      </Button>

      <OutcomeEditorModal
        open={modalOpen}
        onOpenChange={(open) => {
          setModalOpen(open);
          if (!open) {
            setEditingIndex(null);
          }
        }}
        initial={editingOutcome ? outcomeToFormValues(editingOutcome) : undefined}
        initialOutcome={editingOutcome}
        onSave={(outcome: Outcome) => {
          if (editingIndex != null) {
            persistOutcomes(outcomes.map((entry, index) => (index === editingIndex ? outcome : entry)));
          } else {
            persistOutcomes([...outcomes, outcome]);
          }
          setModalOpen(false);
          setEditingIndex(null);
        }}
      />
    </div>
  );
}

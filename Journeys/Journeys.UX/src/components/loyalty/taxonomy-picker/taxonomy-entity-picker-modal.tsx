"use client";

/**
 * TaxonomyEntityPickerModal — Dialog wrapper around `TaxonomyEntityPicker`.
 *
 * Internal staging selection (uncontrolled) committed on Apply, discarded on
 * Cancel. The parent receives the staged selection only when Apply is clicked.
 */

import { useState } from "react";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

import { TaxonomyEntityPicker, type TaxonomyEntityPickerProps } from "./taxonomy-entity-picker";
import type { TaxonomyPickerSelection } from "./state";

export interface TaxonomyEntityPickerModalProps extends Omit<TaxonomyEntityPickerProps, "value" | "onChange"> {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialSelection?: TaxonomyPickerSelection;
  onApply: (selection: TaxonomyPickerSelection) => void;
  title?: string;
  description?: string;
  applyLabel?: string;
  cancelLabel?: string;
}

const EMPTY_SELECTION: TaxonomyPickerSelection = {
  categories: new Map(),
  entities: new Map(),
};

export function TaxonomyEntityPickerModal({
  open,
  onOpenChange,
  initialSelection,
  onApply,
  title = "Select taxonomies and items",
  description = "Pick categories and individual items to apply.",
  applyLabel = "Apply",
  cancelLabel = "Cancel",
  ...pickerProps
}: TaxonomyEntityPickerModalProps) {
  const [staged, setStaged] = useState<TaxonomyPickerSelection>(initialSelection ?? EMPTY_SELECTION);

  const handleOpenChange = (next: boolean) => {
    // Re-seed staging state on each open so the dialog reflects the latest
    // `initialSelection` from the parent. Avoids `set-state-in-effect`.
    if (next) setStaged(initialSelection ?? EMPTY_SELECTION);
    onOpenChange(next);
  };

  const handleApply = () => {
    onApply(staged);
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-5xl max-h-[90vh]">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <TaxonomyEntityPicker {...pickerProps} value={staged} onChange={setStaged} />

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {cancelLabel}
          </Button>
          <Button onClick={handleApply} disabled={staged.categories.size === 0 && staged.entities.size === 0}>
            {applyLabel} ({staged.categories.size + staged.entities.size})
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

"use client";

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

import { ProductSelector, type ProductSelection, type ProductSelectorProps } from "./product-selector";

export interface ProductSelectorModalProps extends Omit<ProductSelectorProps, "initialSelection" | "onChange"> {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialSelection?: ProductSelection;
  onApply: (selection: ProductSelection) => void;
  title?: string;
  description?: string;
}

const DEFAULT_SELECTION: ProductSelection = { mode: "include", categories: [], entities: [] };

export function ProductSelectorModal({
  open,
  onOpenChange,
  initialSelection = DEFAULT_SELECTION,
  onApply,
  title = "Select products",
  description = "Choose categories or individual SKUs.",
  ...props
}: ProductSelectorModalProps) {
  const [staged, setStaged] = useState<ProductSelection>(initialSelection);

  const handleOpenChange = (next: boolean) => {
    if (next) setStaged(initialSelection);
    onOpenChange(next);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-h-[95vh] w-[min(95vw,90rem)] max-w-none overflow-y-auto sm:max-w-none">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <ProductSelector {...props} initialSelection={staged} onChange={setStaged} />

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={() => {
              onApply(staged);
              onOpenChange(false);
            }}
            disabled={staged.categories.length === 0 && staged.entities.length === 0}
          >
            Apply ({staged.categories.length + staged.entities.length})
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

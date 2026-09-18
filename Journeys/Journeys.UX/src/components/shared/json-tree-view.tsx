"use client";

import { useMemo } from "react";

import { cn } from "@/lib/utils";

export interface JsonTreeViewProps {
  value: unknown;
  className?: string;
  maxInitialExpandLevel?: number;
}

export function JsonTreeView({ value, className }: JsonTreeViewProps) {
  const text = useMemo(() => JSON.stringify(value, null, 2), [value]);

  return (
    <pre
      className={cn(
        "max-h-96 overflow-auto rounded-md border border-border bg-muted/30 p-2 text-[11px] leading-snug",
        className
      )}
    >
      {text}
    </pre>
  );
}

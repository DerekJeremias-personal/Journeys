"use client";

/**
 * Custom React Flow node for a journey step. Displays the step name, a small
 * badge row with rule-set + outcome counts, and direction badges (E / T / X)
 * for any navigation criteria configured on the node.
 */

import { memo } from "react";
import { Handle, Position, type NodeProps } from "reactflow";
import { Layers, Target } from "lucide-react";

import { cn } from "@/lib/utils";
import type { JourneyNodeData } from "../hooks/use-journey-transform";

const NAVIGATION_BADGES: ReadonlyArray<{
  symbol: "E" | "T" | "X";
  /** lowercased navigation type matched against `data.navigationTypes`. */
  match: string;
  className: string;
  label: string;
}> = [
  {
    symbol: "E",
    match: "entry",
    className: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
    label: "Entry criteria configured",
  },
  {
    symbol: "T",
    match: "transition",
    className: "bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400",
    label: "Transition criteria configured",
  },
  {
    symbol: "X",
    match: "exit",
    className: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400",
    label: "Exit criteria configured",
  },
] as const;

function JourneyNodeImpl({ data }: NodeProps<JourneyNodeData>) {
  const navTypes = (data.navigationTypes ?? []).map((t) => t.toLowerCase());
  const activeBadges = NAVIGATION_BADGES.filter((b) => navTypes.includes(b.match));

  return (
    <div
      className={cn(
        "min-w-[200px] rounded-md border bg-card px-3 py-2 text-card-foreground shadow-sm transition-colors",
        data.isSelected ? "border-primary ring-2 ring-primary/30" : "border-border hover:border-primary/40"
      )}
    >
      <Handle type="target" position={Position.Left} className="!bg-muted-foreground" />
      <div className="flex flex-col gap-1">
        <span className="text-sm font-medium leading-tight">{data.label}</span>
        <div className="flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
          <span className="inline-flex items-center gap-1">
            <Layers className="h-3 w-3" aria-hidden="true" />
            {data.ruleSetsCount} rule set{data.ruleSetsCount === 1 ? "" : "s"}
          </span>
          <span className="inline-flex items-center gap-1">
            <Target className="h-3 w-3" aria-hidden="true" />
            {data.outcomesCount} outcome{data.outcomesCount === 1 ? "" : "s"}
          </span>
        </div>
        {activeBadges.length > 0 && (
          <div className="mt-1 flex items-center gap-1">
            {activeBadges.map((badge) => (
              <span
                key={badge.symbol}
                title={badge.label}
                className={cn(
                  "inline-flex h-4 min-w-[16px] items-center justify-center rounded px-1 text-[10px] font-semibold",
                  badge.className
                )}
                aria-label={badge.label}
              >
                {badge.symbol}
              </span>
            ))}
          </div>
        )}
      </div>
      <Handle type="source" position={Position.Right} className="!bg-muted-foreground" />
    </div>
  );
}

export const JourneyNode = memo(JourneyNodeImpl);

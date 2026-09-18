"use client";

/**
 * Custom React Flow edge for journey navigation. Renders a smooth-stepped SVG
 * path color-coded by navigation type:
 *   - Transition → solid sky-blue (animated upstream)
 *   - Entry      → dashed emerald
 *   - Exit       → dashed amber
 *   - none       → dashed gray (default)
 *
 * The label badge near the midpoint shows colored E / T / X pills for any
 * navigation criteria configured on the destination node.
 */

import { memo } from "react";
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, type EdgeProps } from "reactflow";

import { cn } from "@/lib/utils";
import type { NavigationEdgeData } from "../hooks/use-journey-transform";

interface NavigationStyle {
  stroke: string;
  /** SVG `stroke-dasharray`. `"0"` = solid. */
  dasharray: string;
}

function pickStyle(types: string[]): NavigationStyle {
  const lowered = types.map((t) => t.toLowerCase());
  if (lowered.includes("transition")) return { stroke: "var(--color-sky-500, #0ea5e9)", dasharray: "0" };
  if (lowered.includes("entry")) return { stroke: "var(--color-emerald-500, #10b981)", dasharray: "5 5" };
  if (lowered.includes("exit")) return { stroke: "var(--color-amber-500, #f59e0b)", dasharray: "5 5" };
  return { stroke: "var(--color-muted-foreground, #6b7280)", dasharray: "5 5" };
}

const BADGES: ReadonlyArray<{ symbol: "E" | "T" | "X"; match: string; className: string; title: string }> = [
  {
    symbol: "E",
    match: "entry",
    className: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
    title: "Entry",
  },
  {
    symbol: "T",
    match: "transition",
    className: "bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400",
    title: "Transition",
  },
  {
    symbol: "X",
    match: "exit",
    className: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400",
    title: "Exit",
  },
] as const;

function NavigationEdgeImpl({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  data,
  markerEnd,
  selected,
}: EdgeProps<NavigationEdgeData>) {
  const [edgePath, labelX, labelY] = getSmoothStepPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition,
  });

  const types = data?.navigationTypes ?? [];
  const lowered = types.map((t) => t.toLowerCase());
  const style = pickStyle(types);
  const activeBadges = BADGES.filter((b) => lowered.includes(b.match));

  return (
    <>
      <BaseEdge
        id={id}
        path={edgePath}
        markerEnd={markerEnd}
        style={{
          stroke: style.stroke,
          strokeWidth: selected ? 3 : 2,
          strokeDasharray: style.dasharray,
          fill: "none",
        }}
      />
      {activeBadges.length > 0 && (
        <EdgeLabelRenderer>
          <div
            className="pointer-events-none absolute flex items-center gap-1 rounded-full border border-border bg-background px-1.5 py-0.5 shadow-sm"
            style={{ transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)` }}
          >
            {activeBadges.map((badge) => (
              <span
                key={badge.symbol}
                title={badge.title}
                className={cn(
                  "inline-flex h-4 min-w-[16px] items-center justify-center rounded px-1 text-[10px] font-semibold",
                  badge.className
                )}
                aria-label={badge.title}
              >
                {badge.symbol}
              </span>
            ))}
          </div>
        </EdgeLabelRenderer>
      )}
    </>
  );
}

export const NavigationEdge = memo(NavigationEdgeImpl);

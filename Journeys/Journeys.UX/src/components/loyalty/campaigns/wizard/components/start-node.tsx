"use client";

/**
 * Custom React Flow node rendered for the campaign entry (root). Visually
 * differentiated from regular journey steps so authors can quickly identify
 * the entry point.
 */

import { memo } from "react";
import { Handle, Position, type NodeProps } from "reactflow";
import { Play } from "lucide-react";

import { cn } from "@/lib/utils";
import type { JourneyNodeData } from "../hooks/use-journey-transform";

function StartNodeImpl({ data }: NodeProps<JourneyNodeData>) {
  return (
    <div
      className={cn(
        "min-w-[160px] rounded-md border-2 bg-primary/5 px-3 py-2 shadow-sm transition-colors",
        data.isSelected ? "border-primary ring-2 ring-primary/30" : "border-primary/60"
      )}
    >
      <div className="flex items-center gap-2">
        <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-primary text-primary-foreground">
          <Play className="h-3 w-3" aria-hidden="true" />
        </span>
        <div className="flex flex-col">
          <span className="text-[10px] font-medium uppercase tracking-wider text-primary">Entry</span>
          <span className="text-sm font-semibold leading-tight">{data.label}</span>
        </div>
      </div>
      <Handle type="source" position={Position.Right} className="!bg-primary" />
    </div>
  );
}

export const StartNode = memo(StartNodeImpl);

"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { cn } from "@/lib/utils";

import type { CampaignMapBranch, CampaignMapNode, CampaignMapViewModel } from "./campaign-to-map-view-model";

const KIND_STYLES: Record<CampaignMapNode["kind"], string> = {
  Campaign: "border-primary/50 bg-primary/5",
  Eligibility: "border-violet-500/40 bg-violet-500/5",
  Qualification: "border-sky-500/40 bg-sky-500/5",
  Outcome: "border-emerald-500/40 bg-emerald-500/5",
};

function MapNodeCard({
  node,
  selected,
  onSelect,
  nodeRef,
}: {
  node: CampaignMapNode;
  selected: boolean;
  onSelect: (nodeId: string) => void;
  nodeRef: (element: HTMLButtonElement | null) => void;
}) {
  return (
    <button
      ref={nodeRef}
      type="button"
      onClick={() => onSelect(node.id)}
      className={cn(
        "mnode w-full max-w-xs rounded-xl border px-4 py-3 text-left transition-shadow",
        KIND_STYLES[node.kind],
        selected ? "ring-2 ring-primary shadow-md" : "hover:shadow-sm"
      )}
    >
      <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{node.kind}</p>
      <p className="mt-1 text-sm font-semibold">{node.title}</p>
      {node.summary ? <p className="mt-1 text-xs text-muted-foreground">{node.summary}</p> : null}
    </button>
  );
}

function Column({
  nodes,
  selectedNodeId,
  onSelect,
  registerNodeRef,
}: {
  nodes: readonly CampaignMapNode[];
  selectedNodeId: string | null;
  onSelect: (nodeId: string) => void;
  registerNodeRef: (nodeId: string, element: HTMLButtonElement | null) => void;
}) {
  return (
    <div className="flex flex-col items-center gap-3">
      {nodes.map((node, index) => (
        <div key={node.id} className="flex flex-col items-center gap-3">
          <MapNodeCard
            node={node}
            selected={selectedNodeId === node.id}
            onSelect={onSelect}
            nodeRef={(element) => registerNodeRef(node.id, element)}
          />
          {index < nodes.length - 1 ? <div className="h-8 w-px bg-border" aria-hidden /> : null}
        </div>
      ))}
    </div>
  );
}

export interface CampaignMapCanvasProps {
  readonly viewModel: CampaignMapViewModel;
  readonly selectedNodeId: string | null;
  readonly onSelectNode: (nodeId: string) => void;
  readonly onBackgroundClick?: () => void;
  readonly panTarget?: number;
}

function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    if (typeof window.matchMedia !== "function") {
      return;
    }
    const media = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = (): void => setReduced(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  return reduced;
}

export function CampaignMapCanvas({
  viewModel,
  selectedNodeId,
  onSelectNode,
  onBackgroundClick,
  panTarget = 48,
}: CampaignMapCanvasProps) {
  const wrapRef = useRef<HTMLDivElement>(null);
  const nodeRefs = useRef<Record<string, HTMLButtonElement | null>>({});
  const prefersReducedMotion = usePrefersReducedMotion();

  const registerNodeRef = useCallback((nodeId: string, element: HTMLButtonElement | null) => {
    nodeRefs.current[nodeId] = element;
  }, []);

  useEffect(() => {
    if (!selectedNodeId || !wrapRef.current) {
      return;
    }
    const wrap = wrapRef.current;
    const node = nodeRefs.current[selectedNodeId];
    if (!node) {
      return;
    }

    const scrollToTarget = (): void => {
      const wrapRect = wrap.getBoundingClientRect();
      const nodeRect = node.getBoundingClientRect();
      wrap.scrollLeft += nodeRect.left - (wrapRect.left + panTarget);
      wrap.scrollTop += nodeRect.top + nodeRect.height / 2 - (wrapRect.top + wrapRect.height / 2);
    };

    if (prefersReducedMotion) {
      scrollToTarget();
      return;
    }

    const fromLeft = wrap.scrollLeft;
    const fromTop = wrap.scrollTop;
    const wrapRect = wrap.getBoundingClientRect();
    const nodeRect = node.getBoundingClientRect();
    const toLeft = fromLeft + (nodeRect.left - (wrapRect.left + panTarget));
    const toTop = fromTop + (nodeRect.top + nodeRect.height / 2 - (wrapRect.top + wrapRect.height / 2));
    const duration = 460;
    const start = performance.now();
    const easeOutCubic = (value: number): number => 1 - Math.pow(1 - value, 3);

    let frame = 0;
    const tick = (now: number): void => {
      const progress = Math.min(1, (now - start) / duration);
      const eased = easeOutCubic(progress);
      wrap.scrollLeft = fromLeft + (toLeft - fromLeft) * eased;
      wrap.scrollTop = fromTop + (toTop - fromTop) * eased;
      if (progress < 1) {
        frame = requestAnimationFrame(tick);
      }
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [panTarget, prefersReducedMotion, selectedNodeId]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === "Escape") {
        onBackgroundClick?.();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onBackgroundClick]);

  const root = viewModel.coreNodes[0];
  const primaryColumn = viewModel.coreNodes.slice(1);

  if (!root) {
    return (
      <div className="flex h-full min-h-[520px] items-center justify-center rounded-xl border bg-muted/20 p-8 text-center text-sm text-muted-foreground">
        Campaign map will appear once the campaign has a journey path.
      </div>
    );
  }

  return (
    <div
      ref={wrapRef}
      className="relative h-full min-h-[520px] overflow-auto rounded-xl border bg-muted/10 p-8"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onBackgroundClick?.();
        }
      }}
    >
      <div className="mx-auto flex min-w-max flex-col items-center gap-6">
        <MapNodeCard
          node={root}
          selected={selectedNodeId === root.id}
          onSelect={onSelectNode}
          nodeRef={(element) => registerNodeRef(root.id, element)}
        />

        {(primaryColumn.length > 0 || viewModel.branches.length > 0) && (
          <>
            <div className="h-10 w-px bg-border" aria-hidden />
            <div className="flex flex-wrap items-start justify-center gap-10">
              {primaryColumn.length > 0 ? (
                <Column
                  nodes={primaryColumn}
                  selectedNodeId={selectedNodeId}
                  onSelect={onSelectNode}
                  registerNodeRef={registerNodeRef}
                />
              ) : null}
              {viewModel.branches.map((branch: CampaignMapBranch) => (
                <Column
                  key={branch.id}
                  nodes={branch.seq}
                  selectedNodeId={selectedNodeId}
                  onSelect={onSelectNode}
                  registerNodeRef={registerNodeRef}
                />
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

/**
 * Pure transforms between the ELP `Journey` DTO (recursive parent → children
 * graph) and the React Flow `Nodes[] + Edges[]` shape rendered by the canvas.
 *
 * The transform is deliberately position-derivative: we lay nodes out by depth
 * (X) and sibling-index (Y) so that the journey DTO never has to carry visual
 * coordinates. React Flow's user drags update React Flow internal state only —
 * they do NOT round-trip back to the journey DTO (per spec D13).
 */

import { useMemo } from "react";
import type { Edge, Node } from "reactflow";
import type { Journey } from "@/lib/campaign-types";

export interface JourneyNodeData {
  label: string;
  /** Number of rule sets attached to this journey node. */
  ruleSetsCount: number;
  /** Number of outcomes attached across all rule sets on this node. */
  outcomesCount: number;
  /** Set on the root node so the canvas can render a different visual. */
  isStart: boolean;
  /** Selection state — drives a highlight border in the node component. */
  isSelected: boolean;
  /**
   * Navigation directions configured on this node (`Entry` / `Transition` / `Exit`).
   * Drives the colored E/T/X badges on the node.
   */
  navigationTypes: string[];
}

export interface NavigationEdgeData {
  /** Short labels for any navigation criteria configured on the child node. */
  navigationTypes: string[];
}

const X_STEP = 280;
const Y_STEP = 160;
const X_PAD = 40;
const Y_PAD = 40;

function countOutcomes(node: Journey): number {
  if (!node.rules) return 0;
  return node.rules.reduce((acc, ruleSet) => {
    const outcomes = ruleSet.outcomesJsonElement;
    if (Array.isArray(outcomes)) return acc + outcomes.length;
    return acc;
  }, 0);
}

function summarizeNavigation(node: Journey): string[] {
  const nav = node.navigation;
  if (!nav || typeof nav !== "object") return [];
  return Object.keys(nav as Record<string, unknown>).filter((k) => Boolean((nav as Record<string, unknown>)[k]));
}

function walkJourney(
  node: Journey,
  parentId: string | null,
  depth: number,
  siblingIndex: number,
  selectedNodeId: string | null,
  acc: { nodes: Node<JourneyNodeData>[]; edges: Edge<NavigationEdgeData>[] }
): void {
  const nodeId = node.id ?? `__missing__${depth}-${siblingIndex}`;
  const navigationTypes = summarizeNavigation(node);
  acc.nodes.push({
    id: nodeId,
    type: parentId === null ? "startNode" : "journeyNode",
    position: { x: X_PAD + depth * X_STEP, y: Y_PAD + siblingIndex * Y_STEP },
    data: {
      label: node.name ?? "Untitled step",
      ruleSetsCount: node.rules?.length ?? 0,
      outcomesCount: countOutcomes(node),
      isStart: parentId === null,
      isSelected: selectedNodeId === nodeId,
      navigationTypes,
    },
    selected: selectedNodeId === nodeId,
  });

  if (parentId) {
    acc.edges.push({
      id: `${parentId}->${nodeId}`,
      source: parentId,
      target: nodeId,
      type: "navigation",
      data: { navigationTypes },
      // Animated edges hint at "transition" navigation. Matches legacy.
      animated: navigationTypes.some((t) => t.toLowerCase() === "transition"),
    });
  }

  const children = node.children ?? [];
  children.forEach((child, idx) => {
    walkJourney(child, nodeId, depth + 1, siblingIndex + idx, selectedNodeId, acc);
  });
}

/** Pure DTO → React Flow shape. Stable for the same `(journey, selectedNodeId)`. */
export function journeyToFlow(
  journey: Journey | null | undefined,
  selectedNodeId: string | null
): { nodes: Node<JourneyNodeData>[]; edges: Edge<NavigationEdgeData>[] } {
  if (!journey) return { nodes: [], edges: [] };
  const acc = { nodes: [] as Node<JourneyNodeData>[], edges: [] as Edge<NavigationEdgeData>[] };
  walkJourney(journey, null, 0, 0, selectedNodeId, acc);
  return acc;
}

/** Hook wrapper that memoizes on `(journey, selectedNodeId)`. */
export function useJourneyTransform(
  journey: Journey | null | undefined,
  selectedNodeId: string | null
): { nodes: Node<JourneyNodeData>[]; edges: Edge<NavigationEdgeData>[] } {
  return useMemo(() => journeyToFlow(journey, selectedNodeId), [journey, selectedNodeId]);
}

// ─────────────────────────────────────────────────────────────────────────────
// Flow → Journey (inverse)
//
// Visual positions are NOT round-tripped (per spec). The inverse reconstructs
// the tree topology from the edges list.
//
// Algorithm:
//   1. Build a parent → children adjacency map from `edges`.
//   2. Find the root node (no incoming edge).
//   3. Walk depth-first and reconstruct each `Journey` node from the matching
//      `Node<JourneyNodeData>` + the existing `Journey` DTO (for rules, navigation,
//      etag, tenantId, etc.) which we look up from the source journey.
// ─────────────────────────────────────────────────────────────────────────────

function indexJourney(root: Journey, acc: Map<string, Journey>): void {
  if (root.id) acc.set(root.id, root);
  for (const child of root.children ?? []) {
    indexJourney(child, acc);
  }
}

/**
 * Reconstruct a `Journey` DTO from React Flow nodes + edges.
 *
 * The original `sourceJourney` is required so we can preserve non-visual fields
 * (rules, navigation, etag, tenantId, etc.) that the canvas doesn't model.
 * Visual-only React Flow mutations (node renames via `label`, new nodes added
 * via addChildNode, node removals) are reflected back.
 *
 * Returns `null` if the graph has no nodes (degenerate state).
 */
export function flowToJourney(
  nodes: Node<JourneyNodeData>[],
  edges: Edge<NavigationEdgeData>[],
  sourceJourney: Journey
): Journey | null {
  if (nodes.length === 0) return null;

  // Index source nodes by id for property lookup
  const sourceIndex = new Map<string, Journey>();
  indexJourney(sourceJourney, sourceIndex);

  // Build adjacency: parent id → child ids (ordered by edge creation which preserves
  // sibling order for reconnected children)
  const childrenMap = new Map<string, string[]>();
  const incomingCount = new Map<string, number>();

  for (const node of nodes) {
    childrenMap.set(node.id, []);
    incomingCount.set(node.id, 0);
  }
  for (const edge of edges) {
    childrenMap.get(edge.source)?.push(edge.target);
    incomingCount.set(edge.target, (incomingCount.get(edge.target) ?? 0) + 1);
  }

  // Root = node with no incoming edges
  const rootNode = nodes.find((n) => (incomingCount.get(n.id) ?? 0) === 0);
  if (!rootNode) return null;

  function buildNode(nodeId: string): Journey {
    const flowNode = nodes.find((n) => n.id === nodeId);
    const source = sourceIndex.get(nodeId);
    const childIds = childrenMap.get(nodeId) ?? [];

    return {
      // Preserve all DTO fields from the source, falling back to defaults
      ...(source ?? {}),
      id: nodeId,
      name: flowNode?.data.label ?? source?.name ?? "Untitled step",
      children: childIds.map(buildNode),
    } as Journey;
  }

  return buildNode(rootNode.id);
}

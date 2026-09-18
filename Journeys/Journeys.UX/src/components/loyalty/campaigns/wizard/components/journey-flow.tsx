"use client";

/**
 * JourneyFlow — React Flow canvas wrapping the wizard's working `Journey`.
 *
 * The journey DTO is the source of truth (lives in the Zustand `wizard-store`).
 * On every render we project it through `useJourneyTransform` into React Flow
 * nodes/edges. User drag-to-reposition mutates React Flow's internal state
 * only and is intentionally NOT persisted to the journey DTO (per spec D13).
 */

import { useCallback, useEffect } from "react";
import ReactFlow, {
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  type Edge,
  type EdgeTypes,
  type Node,
  type NodeTypes,
  type OnNodesChange,
} from "reactflow";
import "reactflow/dist/style.css";

import { selectJourney, selectSelectedNodeId, useWizardActions, useWizardStore } from "../wizard-store";
import { useJourneyTransform, type JourneyNodeData, type NavigationEdgeData } from "../hooks/use-journey-transform";
import { JourneyNode } from "./journey-node";
import { StartNode } from "./start-node";
import { NavigationEdge } from "./navigation-edge";

const nodeTypes: NodeTypes = {
  journeyNode: JourneyNode,
  startNode: StartNode,
};

const edgeTypes: EdgeTypes = {
  navigation: NavigationEdge,
};

function JourneyFlowInner() {
  const journey = useWizardStore(selectJourney);
  const selectedNodeId = useWizardStore(selectSelectedNodeId);
  const { setSelectedNodeId } = useWizardActions();

  const { nodes: derivedNodes, edges: derivedEdges } = useJourneyTransform(journey, selectedNodeId);
  const [nodes, setNodes, onNodesChange] = useNodesState<JourneyNodeData>(derivedNodes);
  const [edges, setEdges] = useEdgesState<NavigationEdgeData>(derivedEdges);

  useEffect(() => {
    setNodes(derivedNodes);
    setEdges(derivedEdges);
  }, [derivedNodes, derivedEdges, setNodes, setEdges]);

  const handleNodeClick = useCallback(
    (_evt: React.MouseEvent, node: Node) => setSelectedNodeId(node.id),
    [setSelectedNodeId]
  );

  const handlePaneClick = useCallback(() => setSelectedNodeId(null), [setSelectedNodeId]);

  // Position-only changes are kept local to React Flow per spec D13.
  const handleNodesChange: OnNodesChange = useCallback((changes) => onNodesChange(changes), [onNodesChange]);

  return (
    <div className="h-[600px] w-full overflow-hidden rounded-md border border-border bg-muted/30">
      <ReactFlow
        nodes={nodes as Node[]}
        edges={edges as Edge[]}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onNodesChange={handleNodesChange}
        onNodeClick={handleNodeClick}
        onPaneClick={handlePaneClick}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        proOptions={{ hideAttribution: true }}
      >
        <Background variant={BackgroundVariant.Dots} gap={20} size={1} />
        <Controls className="rounded-md border border-border bg-background" />
        <MiniMap
          className="!bg-background"
          nodeColor={(node) => (node.type === "startNode" ? "var(--color-primary)" : "var(--color-muted-foreground)")}
        />
      </ReactFlow>
    </div>
  );
}

export function JourneyFlow() {
  return (
    <ReactFlowProvider>
      <JourneyFlowInner />
    </ReactFlowProvider>
  );
}

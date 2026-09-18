"use client";

/**
 * AccountCampaignProgress — journey progress for the campaigns an account is
 * enrolled in.
 *
 * Data sources:
 *   - `getLoyaltyAccountById` — `journeys[]` with `journeyNodeIds`, which tells
 *     us the node the account currently sits on.
 *   - `getCampaigns` — each campaign carries its `journey` tree.
 *   - `getAccountPoints` — current balance per point account type.
 *   - `getPointAccountTypes` — names for labels.
 *
 * Progress %: for each enrolled campaign, find the current node, read the
 * node's navigation transition `navConstraint` (NumericPropertyRule or
 * AndRule) to extract the point threshold, then compare it against the
 * account's balance for that point account type.
 */

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Award, CheckCircle2, ChevronDown, ChevronUp, CornerDownRight, Info } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Command, CommandEmpty, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

import {
  getAccountPoints,
  getCampaigns,
  getLoyaltyAccountById,
  getPointAccountTypes,
} from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import {
  asLoose,
  flattenJourneyNodes,
  getJourneyRootId,
  getNodeId,
  journeyMatchesAccountJourney,
  normalizeId,
  resolveCurrentTierLabel,
  type JourneyRef,
  type Loose,
} from "./account-tier";

export interface AccountCampaignProgressProps {
  loyaltyAccountId: string;
  className?: string;
}

/** A point ledger/balance row as the progress math reads it. */
type PointRow = { pointAccountTypeId?: string | null; currentBalance?: number | null };

type ProgressTarget = { patId: string | null; lowerBound: number; upperBound: number | null; isRange: boolean };

function getCampaignKey(
  campaign: { id?: string | null; name?: string | null; journey?: unknown },
  index: number
): string {
  const idPart = normalizeId(campaign.id);
  const namePart = normalizeId(campaign.name);
  const journeyPart = getJourneyRootId(asLoose(campaign.journey));
  return `${idPart || "no-id"}|${namePart || "no-name"}|${journeyPart || "no-journey"}|${index}`;
}

function getNumericValue(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string") {
    const parsed = Number(value.trim());
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

function matchesComparison(comparison: unknown, expectedNumeric: number, expectedLabel: string): boolean {
  if (typeof comparison === "number") return comparison === expectedNumeric;
  if (typeof comparison === "string") return comparison.toLowerCase() === expectedLabel.toLowerCase();
  return false;
}

function getNavigationConstraint(node: Loose): Loose | null {
  const nav = asLoose(node["navigation"] ?? node["Navigation"]);
  const transition = asLoose(nav["transition"] ?? nav["Transition"]);
  const rawConstraint = transition["navConstraint"] ?? transition["NavConstraint"];
  if (!rawConstraint || typeof rawConstraint !== "object") return null;
  return asLoose(rawConstraint);
}

function extractProgressTargetFromConstraint(constraint: Loose): ProgressTarget | null {
  const kind = String(constraint["Kind"] ?? constraint["kind"] ?? "").trim();
  if (!kind) return null;

  if (kind === "NumericPropertyRule") {
    const right = asLoose(constraint["RightProvider"] ?? constraint["rightProvider"]);
    const left = asLoose(constraint["LeftProvider"] ?? constraint["leftProvider"]);
    const target = getNumericValue(right["Value"]) ?? getNumericValue(right["value"]);
    const patId = ((left["PointAccountTypeId"] ?? left["pointAccountTypeId"]) as string | undefined) ?? null;
    if (target === null) return null;
    return { patId, lowerBound: Math.max(0, target), upperBound: null, isRange: false };
  }

  if (kind === "AndRule") {
    const children = Array.isArray(constraint["Children"])
      ? (constraint["Children"] as Loose[])
      : Array.isArray(constraint["children"])
        ? (constraint["children"] as Loose[])
        : [];

    const lower = children.find((c) => {
      const ck = asLoose(c);
      const ek = asLoose(ck["Evaluator"] ?? ck["evaluator"]);
      const comparison = ek["Comparison"] ?? ek["comparison"];
      return (
        (ck["Kind"] === "NumericPropertyRule" || ck["kind"] === "NumericPropertyRule") &&
        matchesComparison(comparison, 3, "GreaterThanOrEqual")
      );
    });

    const upper = children.find((c) => {
      const ck = asLoose(c);
      const ek = asLoose(ck["Evaluator"] ?? ck["evaluator"]);
      const comparison = ek["Comparison"] ?? ek["comparison"];
      return (
        (ck["Kind"] === "NumericPropertyRule" || ck["kind"] === "NumericPropertyRule") &&
        matchesComparison(comparison, 5, "LessThanOrEqual")
      );
    });

    if (!lower) return null;

    const lowerRule = asLoose(lower);
    const lowerRight = asLoose(lowerRule["RightProvider"] ?? lowerRule["rightProvider"]);
    const lowerLeft = asLoose(lowerRule["LeftProvider"] ?? lowerRule["leftProvider"]);
    const lowerValue = getNumericValue(lowerRight["Value"]) ?? getNumericValue(lowerRight["value"]);
    if (lowerValue === null) return null;

    const patId = ((lowerLeft["PointAccountTypeId"] ?? lowerLeft["pointAccountTypeId"]) as string | undefined) ?? null;
    const upperRule = asLoose(upper);
    const upperRight = asLoose(upperRule["RightProvider"] ?? upperRule["rightProvider"]);
    const upperValue = getNumericValue(upperRight["Value"]) ?? getNumericValue(upperRight["value"]);

    return {
      patId,
      lowerBound: Math.max(0, lowerValue),
      upperBound: upperValue ?? null,
      isRange: true,
    };
  }

  return null;
}

function extractProgressTarget(node: Loose): ProgressTarget | null {
  const constraint = getNavigationConstraint(node);
  if (!constraint) return null;
  return extractProgressTargetFromConstraint(constraint);
}

function isProgressVisibleForNode(node: Loose): boolean {
  const constraint = getNavigationConstraint(node);
  if (!constraint) return false;

  const left = asLoose(constraint["leftProvider"] ?? constraint["LeftProvider"]);
  const right = asLoose(constraint["rightProvider"] ?? constraint["RightProvider"]);
  const leftValue = left["value"] ?? left["Value"];
  const rightValue = right["value"] ?? right["Value"];

  return !(leftValue === false && rightValue === true);
}

function getCurrentBalance(points: PointRow[], patId: string): number {
  const pointEntry = points.find((p) => normalizeId(p.pointAccountTypeId) === normalizeId(patId));
  return pointEntry?.currentBalance ?? 0;
}

function getProgressPercentage(node: Loose, points: PointRow[]): number {
  const target = extractProgressTarget(node);
  if (!target?.patId) return 0;

  const currentBalance = getCurrentBalance(points, target.patId);
  if (target.isRange) {
    const min = target.lowerBound;
    const max = target.upperBound ?? (min > 0 ? min * 2 : 1);
    if (max <= min) return currentBalance >= min ? 100 : 0;
    return Math.min(100, Math.max(0, ((currentBalance - min) / (max - min)) * 100));
  }
  if (target.lowerBound <= 0) return 100;
  return Math.min(100, (currentBalance / target.lowerBound) * 100);
}

function findNextNode(node: Loose, campaignId: string, flatNodesByCampaign: Record<string, Loose[]>): Loose | null {
  const nodeId = normalizeId(getNodeId(node));
  if (!nodeId) return null;
  const flat = flatNodesByCampaign[campaignId] ?? [];
  const idx = flat.findIndex((n) => normalizeId(getNodeId(n)) === nodeId);
  if (idx < 0 || idx >= flat.length - 1) return null;
  return flat[idx + 1] ?? null;
}

function getProgressText(
  node: Loose,
  campaignId: string,
  flatNodesByCampaign: Record<string, Loose[]>,
  points: PointRow[]
): string {
  const nextNode = findNextNode(node, campaignId, flatNodesByCampaign);
  if (!nextNode) return "N/A";
  const nextTarget = extractProgressTarget(nextNode);
  const currentTarget = extractProgressTarget(node);
  const currentBalance = currentTarget?.patId ? getCurrentBalance(points, currentTarget.patId) : 0;
  const requiredLower = nextTarget?.lowerBound ?? 0;
  const remaining = Math.max(requiredLower - currentBalance, 0);
  return `${currentBalance.toLocaleString()} / ${requiredLower.toLocaleString()} points (${remaining.toLocaleString()} remaining)`;
}

function getRuleCalculationText(node: Loose, points: PointRow[], patNames: Map<string, string>): string {
  const target = extractProgressTarget(node);
  if (!target?.patId) return "N/A";

  const currentBalance = getCurrentBalance(points, target.patId);
  const accountName = patNames.get(normalizeId(target.patId)) ?? "Points";
  if (target.isRange) {
    if (target.upperBound === null) return "N/A";
    return `${accountName} Requirements\n${target.lowerBound.toLocaleString()} ≤ ${currentBalance.toLocaleString()} < ${target.upperBound.toLocaleString()}`;
  }
  return `${accountName} Requirements\n${target.lowerBound.toLocaleString()} ≤ ${currentBalance.toLocaleString()}`;
}

interface JourneyProgressNodeProps {
  node: Loose;
  level: number;
  points: PointRow[];
  campaignId: string;
  flatNodesByCampaign: Record<string, Loose[]>;
  patNames: Map<string, string>;
}

function JourneyProgressNode({
  node,
  level,
  points,
  campaignId,
  flatNodesByCampaign,
  patNames,
}: JourneyProgressNodeProps) {
  const pct = getProgressPercentage(node, points);
  const pctLabel = `${pct.toFixed(2)}%`;
  const nodeName = String(node["name"] ?? "Unnamed node");
  const achievedDate = (node["achievedDate"] ?? node["AchievedDate"]) as string | undefined;
  const ruleText = getRuleCalculationText(node, points, patNames);
  const progressText = getProgressText(node, campaignId, flatNodesByCampaign, points);
  const children = Array.isArray(node["children"])
    ? (node["children"] as Loose[])
    : Array.isArray(node["Children"])
      ? (node["Children"] as Loose[])
      : [];

  const fillWidth = pct > 0 ? `${Math.min(100, Math.max(0, pct))}%` : "0%";

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        {level > 0 && <CornerDownRight className="h-4 w-4 text-muted-foreground" />}
        <div className="relative h-8 flex-1 overflow-hidden rounded-full border bg-muted/50">
          {pct > 0 && (
            <div
              className="h-full rounded-full bg-primary/90 transition-all duration-300"
              style={{ width: fillWidth }}
            />
          )}
          <div className="absolute inset-0 flex items-center justify-between px-3 text-xs">
            <div className={cn("font-semibold", pct > 0 ? "text-primary-foreground" : "text-foreground")}>
              {pct >= 100 ? (
                <span className="inline-flex items-center gap-1.5">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  {nodeName}
                </span>
              ) : (
                nodeName
              )}
            </div>
            {pct > 0 && <span className="font-semibold text-primary-foreground">{pctLabel}</span>}
          </div>
        </div>

        <Popover>
          <PopoverTrigger asChild>
            <Button type="button" variant="ghost" size="icon" className="h-7 w-7">
              <Info className="h-4 w-4" />
            </Button>
          </PopoverTrigger>
          <PopoverContent align="end" className="w-[320px] space-y-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Rule calculation</p>
              <p className="mt-1 whitespace-pre-line text-sm">{ruleText}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Progress</p>
              <p className="mt-1 text-sm">{progressText}</p>
            </div>
          </PopoverContent>
        </Popover>
      </div>

      {achievedDate && <p className="pl-2 text-xs text-muted-foreground">Achieved {achievedDate}</p>}

      {children.length > 0 && (
        <div className="space-y-2 pl-5">
          {children.map((child, index) => (
            <JourneyProgressNode
              key={getNodeId(child) ?? `${level}-${index}`}
              node={asLoose(child)}
              level={level + 1}
              points={points}
              campaignId={campaignId}
              flatNodesByCampaign={flatNodesByCampaign}
              patNames={patNames}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export function AccountCampaignProgress({ loyaltyAccountId, className }: AccountCampaignProgressProps) {
  const [isExpanded, setIsExpanded] = useState(true);
  const [selectedCampaignKey, setSelectedCampaignKey] = useState<string>("");
  const [campaignPickerOpen, setCampaignPickerOpen] = useState(false);

  const campaignsQuery = useQuery({
    queryKey: loyaltyKeys.campaigns.list({}),
    queryFn: async () => {
      const r = await getCampaigns();
      if (!r.success) throw new Error(r.error ?? "Failed to load campaigns");
      return r.data ?? [];
    },
  });

  const accountQuery = useQuery({
    queryKey: loyaltyKeys.accounts.loyaltyDetail(loyaltyAccountId),
    queryFn: async () => {
      const r = await getLoyaltyAccountById(loyaltyAccountId);
      if (!r.success) throw new Error(r.error ?? "Failed to load account");
      return r.data ?? null;
    },
  });

  const pointsQuery = useQuery({
    queryKey: loyaltyKeys.points.ledgersByAccount(loyaltyAccountId),
    queryFn: async () => {
      const r = await getAccountPoints(loyaltyAccountId);
      if (!r.success) throw new Error(r.error ?? "Failed to load points");
      return (r.data ?? []) as PointRow[];
    },
  });

  const patsQuery = useQuery({
    queryKey: loyaltyKeys.pointAccountTypes.all,
    queryFn: async () => {
      const r = await getPointAccountTypes();
      if (!r.success) throw new Error(r.error ?? "Failed to load point types");
      return r.data ?? [];
    },
    staleTime: 5 * 60 * 1000,
  });

  const isLoading = campaignsQuery.isLoading || accountQuery.isLoading || pointsQuery.isLoading;

  const accountJourneys = useMemo<JourneyRef[]>(() => {
    const account = accountQuery.data;
    if (!account) return [];
    return (account as { journeys?: JourneyRef[] }).journeys ?? [];
  }, [accountQuery.data]);

  const enrolledCampaigns = useMemo(() => {
    const allCampaigns = campaignsQuery.data ?? [];
    return allCampaigns.filter((campaign) =>
      accountJourneys.some((accountJourney) => journeyMatchesAccountJourney(asLoose(campaign.journey), accountJourney))
    );
  }, [campaignsQuery.data, accountJourneys]);

  const keyedCampaigns = useMemo(
    () => enrolledCampaigns.map((campaign, index) => ({ key: getCampaignKey(campaign, index), campaign })),
    [enrolledCampaigns]
  );

  const flatNodesByCampaign = useMemo<Record<string, Loose[]>>(() => {
    return keyedCampaigns.reduce<Record<string, Loose[]>>((acc, entry) => {
      acc[entry.key] = flattenJourneyNodes(asLoose(entry.campaign.journey));
      return acc;
    }, {});
  }, [keyedCampaigns]);

  const campaignCurrentNodeMap = useMemo<Record<string, Loose | null>>(() => {
    return keyedCampaigns.reduce<Record<string, Loose | null>>((acc, entry) => {
      const journey = asLoose(entry.campaign.journey);
      const accountJourney = accountJourneys.find((candidate) => journeyMatchesAccountJourney(journey, candidate));
      const currentNodeId = accountJourney?.journeyNodeIds?.length
        ? accountJourney.journeyNodeIds[accountJourney.journeyNodeIds.length - 1]
        : null;
      const allNodes = flatNodesByCampaign[entry.key] ?? [];
      acc[entry.key] = currentNodeId
        ? (allNodes.find((n) => normalizeId(getNodeId(n)) === normalizeId(currentNodeId)) ?? null)
        : null;
      return acc;
    }, {});
  }, [accountJourneys, keyedCampaigns, flatNodesByCampaign]);

  const selectedCampaignKeyResolved = useMemo(() => {
    if (keyedCampaigns.length === 0) return "";
    if (selectedCampaignKey && keyedCampaigns.some((entry) => entry.key === selectedCampaignKey)) {
      return selectedCampaignKey;
    }
    const firstAccountJourney = accountJourneys[0];
    const preferred =
      keyedCampaigns.find((entry) =>
        firstAccountJourney ? journeyMatchesAccountJourney(asLoose(entry.campaign.journey), firstAccountJourney) : false
      ) ?? keyedCampaigns[0];
    return preferred?.key ?? "";
  }, [accountJourneys, keyedCampaigns, selectedCampaignKey]);

  const selectedCampaignEntry = useMemo(
    () => keyedCampaigns.find((entry) => entry.key === selectedCampaignKeyResolved) ?? null,
    [keyedCampaigns, selectedCampaignKeyResolved]
  );
  const selectedCampaign = selectedCampaignEntry?.campaign ?? null;
  const selectedCampaignEntryKey = selectedCampaignEntry?.key ?? "";

  const selectedCurrentNode = selectedCampaignEntry
    ? (campaignCurrentNodeMap[selectedCampaignEntry.key] ?? null)
    : null;
  const currentTierLabel = useMemo(
    () => resolveCurrentTierLabel(selectedCampaign ? [selectedCampaign] : [], accountJourneys),
    [selectedCampaign, accountJourneys]
  );

  const pointRows = useMemo(() => pointsQuery.data ?? [], [pointsQuery.data]);
  const pointAccountTypeNameMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const pat of patsQuery.data ?? []) {
      const id = typeof pat.id === "string" ? pat.id : null;
      if (!id) continue;
      map.set(normalizeId(id), typeof pat.name === "string" ? pat.name : id);
    }
    return map;
  }, [patsQuery.data]);

  if (isLoading) {
    return <Skeleton className={cn("h-32 w-full", className)} />;
  }

  if (campaignsQuery.error) {
    return (
      <Alert variant="destructive" className={className}>
        <AlertDescription>{(campaignsQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }
  if (accountQuery.error) {
    return (
      <Alert variant="destructive" className={className}>
        <AlertDescription>{(accountQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }
  if (pointsQuery.error) {
    return (
      <Alert variant="destructive" className={className}>
        <AlertDescription>{(pointsQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  return (
    <Card className={className}>
      <CardContent className="p-4 space-y-4">
        <h3 className="text-base font-semibold">Campaign progress</h3>

        {enrolledCampaigns.length === 0 && (
          <p className="text-sm font-medium text-muted-foreground">No campaigns for this account.</p>
        )}

        {selectedCampaign && selectedCampaignEntry && (
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0">
              {currentTierLabel ? (
                <div className="flex items-center gap-3">
                  <Award className="h-6 w-6 text-primary" />
                  <div>
                    <p className="text-xs uppercase tracking-wide text-muted-foreground">Current tier</p>
                    <p className="truncate text-xl font-bold sm:text-2xl">{currentTierLabel}</p>
                  </div>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">Not currently in a journey tier.</p>
              )}
            </div>

            <Popover open={campaignPickerOpen} onOpenChange={setCampaignPickerOpen}>
              <PopoverTrigger asChild>
                <Button variant="outline" className="justify-between gap-2 sm:min-w-[220px]">
                  <span className="truncate">{selectedCampaign.name ?? selectedCampaign.id ?? "Select campaign"}</span>
                  <ChevronDown className="h-4 w-4 opacity-70" />
                </Button>
              </PopoverTrigger>
              <PopoverContent align="end" className="w-[320px] p-0">
                <Command>
                  <CommandInput placeholder="Search campaigns..." />
                  <CommandList>
                    <CommandEmpty>No campaign found.</CommandEmpty>
                    {keyedCampaigns.map((entry) => (
                      <CommandItem
                        key={entry.key}
                        onSelect={() => {
                          setSelectedCampaignKey(entry.key);
                          setCampaignPickerOpen(false);
                        }}
                      >
                        <span className="truncate">{entry.campaign.name ?? entry.campaign.id ?? "Untitled"}</span>
                      </CommandItem>
                    ))}
                  </CommandList>
                </Command>
              </PopoverContent>
            </Popover>
          </div>
        )}

        {selectedCampaign && selectedCurrentNode && isProgressVisibleForNode(selectedCurrentNode) && (
          <>
            <div className="flex items-center gap-3">
              <div className="h-px flex-1 bg-border" />
              <Button type="button" size="sm" variant="secondary" onClick={() => setIsExpanded((v) => !v)}>
                {isExpanded ? "Collapse" : "Expand"}
                {isExpanded ? <ChevronUp className="ml-1 h-4 w-4" /> : <ChevronDown className="ml-1 h-4 w-4" />}
              </Button>
            </div>

            {isExpanded && (
              <JourneyProgressNode
                node={selectedCurrentNode}
                level={0}
                points={pointRows}
                campaignId={selectedCampaignEntryKey}
                flatNodesByCampaign={flatNodesByCampaign}
                patNames={pointAccountTypeNameMap}
              />
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

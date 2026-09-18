"use client";

import { AlertTriangle, Lightbulb, XCircle } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

import type { CampaignHealthIssue, CampaignHealthReport } from "./compute-campaign-health";

export interface CampaignHealthSidebarProps {
  readonly report: CampaignHealthReport;
  readonly mode?: "review" | "build";
  readonly onFix?: (nodeId: string) => void;
  readonly className?: string;
}

function issueIcon(kind: CampaignHealthIssue["kind"]) {
  switch (kind) {
    case "error":
      return XCircle;
    case "warning":
      return AlertTriangle;
    case "rec":
      return Lightbulb;
    default: {
      const exhaustive: never = kind;
      return exhaustive;
    }
  }
}

function issueStyles(kind: CampaignHealthIssue["kind"]): string {
  switch (kind) {
    case "error":
      return "border-destructive/30 bg-destructive/5";
    case "warning":
      return "border-amber-500/30 bg-amber-500/5";
    case "rec":
      return "border-sky-500/30 bg-sky-500/5";
    default: {
      const exhaustive: never = kind;
      return exhaustive;
    }
  }
}

function HealthIssueCard({ issue, onFix }: { issue: CampaignHealthIssue; onFix?: (nodeId: string) => void }) {
  const Icon = issueIcon(issue.kind);
  return (
    <div className={cn("rounded-xl border p-3", issueStyles(issue.kind))}>
      <div className="flex items-start gap-2">
        <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
        <div className="min-w-0 space-y-2">
          <p className="text-sm font-medium">{issue.title}</p>
          <ul className="space-y-1 text-xs text-muted-foreground">
            <li>{issue.why}</li>
            <li>{issue.fix}</li>
          </ul>
          {issue.nodeId && onFix ? (
            <Button type="button" size="sm" variant="outline" onClick={() => onFix(issue.nodeId!)}>
              {issue.fixLabel ?? "Resolve"}
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  );
}

export function CampaignHealthSidebar({ report, mode = "review", onFix, className }: CampaignHealthSidebarProps) {
  const total = report.errors.length + report.warnings.length + report.recs.length;

  return (
    <aside className={cn("flex h-full min-h-[520px] flex-col rounded-xl border bg-background p-4", className)}>
      <div className="space-y-1 border-b pb-4">
        <p className="text-sm font-semibold">{mode === "review" ? "Campaign health" : "Build checklist"}</p>
        <p className="text-xs text-muted-foreground">
          {mode === "review"
            ? "Select a step on the map to edit it, or resolve issues below."
            : "Resolve these items before launch."}
        </p>
      </div>

      <div className="flex-1 space-y-4 overflow-auto py-4">
        {total === 0 ? (
          <p className="text-sm text-muted-foreground">No issues detected for the current draft.</p>
        ) : null}

        {report.errors.length > 0 ? (
          <section className="space-y-2">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-destructive">Errors</h3>
            {report.errors.map((issue) => (
              <HealthIssueCard key={issue.id} issue={issue} onFix={onFix} />
            ))}
          </section>
        ) : null}

        {report.warnings.length > 0 ? (
          <section className="space-y-2">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-400">
              Warnings
            </h3>
            {report.warnings.map((issue) => (
              <HealthIssueCard key={issue.id} issue={issue} onFix={onFix} />
            ))}
          </section>
        ) : null}

        {report.recs.length > 0 ? (
          <section className="space-y-2">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-sky-700 dark:text-sky-400">
              Recommendations
            </h3>
            {report.recs.map((issue) => (
              <HealthIssueCard key={issue.id} issue={issue} onFix={onFix} />
            ))}
          </section>
        ) : null}
      </div>
    </aside>
  );
}

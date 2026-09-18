"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const LONG_STRING_THRESHOLD = 480;

/** Turn camelCase / snake_case keys into short titles for non-technical readers. */
function formatFieldLabel(key: string): string {
  const spaced = key
    .replace(/_/g, " ")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .trim();
  if (!spaced) return key;
  return spaced.replace(/\b\w/g, (c) => c.toUpperCase());
}

function tryParseJsonObjectString(s: string): unknown | null {
  const t = s.trim();
  if (!((t.startsWith("{") && t.endsWith("}")) || (t.startsWith("[") && t.endsWith("]")))) {
    return null;
  }
  try {
    return JSON.parse(t) as unknown;
  } catch {
    return null;
  }
}

function LongString({ text }: { text: string }) {
  const [open, setOpen] = useState(false);
  const needsTruncate = text.length > LONG_STRING_THRESHOLD;
  const shown = needsTruncate && !open ? `${text.slice(0, LONG_STRING_THRESHOLD)}…` : text;

  return (
    <div className="space-y-1">
      <p className="text-sm whitespace-pre-wrap break-words text-foreground">{shown}</p>
      {needsTruncate ? (
        <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs" onClick={() => setOpen((o) => !o)}>
          {open ? "Show less" : "Show full text"}
        </Button>
      ) : null}
    </div>
  );
}

export interface FriendlyDataViewProps {
  value: unknown;
  /** Nesting depth (internal). */
  depth?: number;
  className?: string;
}

/**
 * Renders structured data as labeled sections — no JSON brackets, quotes, or colons as syntax.
 * For objects: field labels + values; for arrays: numbered items; primitives read as plain content.
 */
export function FriendlyDataView({ value, depth = 0, className }: FriendlyDataViewProps) {
  if (value === null || value === undefined) {
    return <span className="text-sm text-muted-foreground">—</span>;
  }

  if (typeof value === "boolean") {
    return value ? (
      <Badge variant="secondary" className="font-normal">
        Yes
      </Badge>
    ) : (
      <Badge variant="outline" className="font-normal text-muted-foreground">
        No
      </Badge>
    );
  }

  if (typeof value === "number") {
    return <span className="text-sm tabular-nums text-foreground">{Number.isFinite(value) ? String(value) : "—"}</span>;
  }

  if (typeof value === "string") {
    const parsed = tryParseJsonObjectString(value);
    if (parsed !== null && typeof parsed === "object") {
      return <FriendlyDataView value={parsed} depth={depth} className={className} />;
    }
    if (value.length > LONG_STRING_THRESHOLD) {
      return <LongString text={value} />;
    }
    return <p className="text-sm whitespace-pre-wrap break-words text-foreground">{value}</p>;
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return <span className="text-sm text-muted-foreground">No items</span>;
    }
    return (
      <ul className={cn("list-none space-y-3", className)}>
        {value.map((item, i) => (
          <li key={i} className="rounded-md border border-border/80 bg-card/40 px-3 py-2.5 shadow-sm">
            <p className="mb-2 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Item {i + 1}</p>
            <FriendlyDataView value={item} depth={depth + 1} />
          </li>
        ))}
      </ul>
    );
  }

  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      return <span className="text-sm text-muted-foreground">No details</span>;
    }
    return (
      <dl className={cn("space-y-3", depth > 0 && "border-l-2 border-primary/15 pl-3", className)}>
        {entries.map(([k, v]) => (
          <div key={k}>
            <dt className="text-xs font-medium text-muted-foreground">{formatFieldLabel(k)}</dt>
            <dd className="mt-1.5">
              <FriendlyDataView value={v} depth={depth + 1} />
            </dd>
          </div>
        ))}
      </dl>
    );
  }

  return <span className="text-sm text-foreground">{String(value)}</span>;
}

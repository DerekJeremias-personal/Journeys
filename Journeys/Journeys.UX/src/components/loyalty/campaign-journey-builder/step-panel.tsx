"use client";

import { X } from "lucide-react";
import type { ReactNode } from "react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export interface StepPanelProps {
  readonly stepLabel?: string;
  readonly title: string;
  readonly subtitle?: string;
  readonly editor: ReactNode;
  readonly summary?: ReactNode;
  readonly footer?: ReactNode;
  readonly onClose?: () => void;
  readonly className?: string;
}

export function StepPanel({ stepLabel, title, subtitle, editor, summary, footer, onClose, className }: StepPanelProps) {
  return (
    <div
      className={cn(
        "relative flex h-full min-h-[520px] overflow-hidden rounded-xl border bg-background shadow-lg",
        className
      )}
    >
      <span className="pointer-events-none absolute -left-3 top-16 z-10 hidden lg:block" aria-hidden>
        <svg width="24" height="32" viewBox="0 0 46 52" fill="none">
          <path
            d="M43 5 L43 47 L7 26 Z"
            className="fill-background stroke-background"
            strokeWidth="7"
            strokeLinejoin="round"
            strokeLinecap="round"
          />
        </svg>
      </span>

      <div className="flex min-w-0 flex-1 flex-col border-r">
        <div className="relative border-b bg-muted/20 px-5 py-4">
          {onClose ? (
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="absolute right-3 top-3 h-8 w-8"
              onClick={onClose}
              aria-label="Close editor"
            >
              <X className="h-4 w-4" />
            </Button>
          ) : null}
          {stepLabel ? (
            <p className="text-[11px] font-bold uppercase tracking-wider text-primary">{stepLabel}</p>
          ) : null}
          <h3 className="pr-10 text-lg font-semibold tracking-tight">{title}</h3>
          {subtitle ? <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p> : null}
        </div>
        <div className="flex-1 overflow-auto px-5 py-4">{editor}</div>
        {footer ? <div className="border-t px-5 py-4">{footer}</div> : null}
      </div>

      {summary ? (
        <aside className="hidden w-[240px] shrink-0 overflow-auto bg-muted/10 p-4 xl:block">{summary}</aside>
      ) : null}
    </div>
  );
}

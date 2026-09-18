"use client";

import { Check, Pencil } from "lucide-react";
import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";

import { cn } from "@/lib/utils";

import { JOURNEY_BUILDER_STEPS, type JourneyBuilderStepMeta } from "./journey-builder-steps";
import type { CanvasPhase } from "./journey-builder-store";

export interface JourneyCanvasHeading {
  readonly title: string;
  readonly subtitle: string;
  readonly mini?: string;
}

export interface JourneyCanvasProps {
  readonly activeIndex: number;
  readonly canvasPhase?: CanvasPhase;
  readonly direction?: "forward" | "backward";
  readonly steps?: readonly JourneyBuilderStepMeta[];
  readonly headings?: readonly JourneyCanvasHeading[];
  readonly zoom?: number;
  readonly renderStationEditor?: (index: number) => ReactNode;
  readonly renderFooter?: ReactNode;
  readonly renderSummary?: ReactNode;
  readonly onJumpBack?: (index: number) => void;
}

function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    if (typeof window.matchMedia !== "function") {
      return;
    }
    const media = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = (): void => {
      setReduced(media.matches);
    };
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  return reduced;
}

function defaultHeadings(steps: readonly JourneyBuilderStepMeta[]): JourneyCanvasHeading[] {
  return steps.map((step) => ({
    title: step.title,
    subtitle: step.description,
    mini: step.description,
  }));
}

function StationConnector({
  isDone,
  isActive,
  reverseCue,
  nextTitle,
  backTitle,
}: {
  isDone: boolean;
  isActive: boolean;
  reverseCue: boolean;
  nextTitle: string;
  backTitle: string;
}) {
  const stroke = isDone ? "stroke-emerald-500" : isActive ? "stroke-primary" : "stroke-muted-foreground/40";

  return (
    <div className={cn("flex flex-col items-center gap-1 py-1 text-muted-foreground/70", isActive && "text-primary")}>
      <svg viewBox="0 0 26 56" className="h-14 w-6" aria-hidden>
        <path
          d="M13 2 V44"
          className={cn(stroke, isActive ? "stroke-[2]" : "stroke-[2] [stroke-dasharray:5_5]")}
          fill="none"
        />
        {reverseCue ? (
          <path
            d="M5 18 L13 8 L21 18"
            className="stroke-primary stroke-[2.4] fill-none [stroke-linecap:round] [stroke-linejoin:round]"
          />
        ) : (
          <path
            d="M5 38 L13 48 L21 38"
            className={cn(
              isDone ? "stroke-emerald-500" : isActive ? "stroke-primary" : "stroke-current",
              "stroke-[2.4] fill-none [stroke-linecap:round] [stroke-linejoin:round]"
            )}
          />
        )}
      </svg>
      <span className="text-[11px] font-medium tracking-wide">
        {reverseCue ? `Back to ${backTitle}` : `Next: ${nextTitle}`}
      </span>
    </div>
  );
}

export function JourneyCanvas({
  activeIndex,
  canvasPhase = "idle",
  direction = "forward",
  steps = JOURNEY_BUILDER_STEPS,
  headings,
  zoom = 1,
  renderStationEditor,
  renderFooter,
  renderSummary,
  onJumpBack,
}: JourneyCanvasProps) {
  const prefersReducedMotion = usePrefersReducedMotion();
  const stageRef = useRef<HTMLDivElement>(null);
  const cardRefs = useRef<Array<HTMLDivElement | null>>([]);
  const [center, setCenter] = useState({ y: 0, origin: 300 });
  const resolvedHeadings = headings ?? defaultHeadings(steps);

  const recenter = useCallback(() => {
    const stage = stageRef.current;
    const card = cardRefs.current[activeIndex];
    if (!stage || !card) {
      return;
    }
    const stageHeight = stage.clientHeight;
    const midpoint = card.offsetTop + card.offsetHeight / 2;
    setCenter({ y: stageHeight / 2 - midpoint, origin: midpoint });
  }, [activeIndex]);

  useLayoutEffect(() => {
    recenter();
  }, [activeIndex, recenter]);

  useEffect(() => {
    const timeout = window.setTimeout(recenter, prefersReducedMotion ? 0 : 60);
    window.addEventListener("resize", recenter);
    return () => {
      window.clearTimeout(timeout);
      window.removeEventListener("resize", recenter);
    };
  }, [prefersReducedMotion, recenter]);

  const trackStyle = prefersReducedMotion
    ? undefined
    : {
        transform: `translateY(${center.y}px) scale(${zoom})`,
        transformOrigin: `50% ${center.origin}px`,
      };

  return (
    <div ref={stageRef} className="relative h-full min-h-[520px] overflow-hidden rounded-xl border bg-muted/10">
      <div
        className={cn(
          "mx-auto flex w-full max-w-2xl flex-col px-4 py-8",
          !prefersReducedMotion && zoom !== 1 && "transition-transform duration-300 ease-out"
        )}
        style={trackStyle}
      >
        {steps.map((step, index) => {
          const state = index < activeIndex ? "done" : index === activeIndex ? "active" : "upcoming";
          const heading = resolvedHeadings[index] ?? {
            title: step.title,
            subtitle: step.description,
            mini: step.description,
          };
          const reverseCue =
            index === activeIndex &&
            direction === "backward" &&
            (canvasPhase === "traveling" || canvasPhase === "entering");

          return (
            <div key={step.key}>
              <div
                ref={(element) => {
                  cardRefs.current[index] = element;
                }}
                className="w-full"
              >
                {state === "active" ? (
                  <>
                    <div
                      className={cn(
                        "overflow-hidden rounded-2xl border-2 border-primary bg-background shadow-sm",
                        canvasPhase === "exiting" && "opacity-60"
                      )}
                    >
                      <div className="flex items-start gap-4 border-b bg-primary/5 px-6 py-5">
                        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary to-violet-600 text-sm font-bold text-primary-foreground">
                          {step.number}
                        </div>
                        <div>
                          <p className="text-[11px] font-bold uppercase tracking-wider text-primary">
                            Step {step.number} of {steps.length}
                          </p>
                          <h2 className="mt-0.5 text-xl font-semibold tracking-tight">{heading.title}</h2>
                        </div>
                      </div>
                      <p className="px-6 pt-3 text-sm text-muted-foreground">{heading.subtitle}</p>
                      {renderStationEditor ? (
                        <div className="px-6 py-4">{renderStationEditor(index)}</div>
                      ) : (
                        <div className="px-6 py-8 text-sm text-muted-foreground">
                          Station editor will appear here as the Agent builds this step.
                        </div>
                      )}
                      {renderFooter ? <div className="border-t">{renderFooter}</div> : null}
                    </div>
                    {renderSummary ? (
                      <aside className="mt-4 rounded-xl border bg-muted/10 p-4 xl:hidden">{renderSummary}</aside>
                    ) : null}
                  </>
                ) : (
                  <button
                    type="button"
                    disabled={state !== "done"}
                    onClick={() => state === "done" && onJumpBack?.(index)}
                    className={cn(
                      "flex w-full items-center gap-3 rounded-xl border bg-background px-4 py-3 text-left transition-colors",
                      state === "done" && "cursor-pointer hover:border-emerald-500/50 hover:bg-emerald-500/5",
                      state === "upcoming" && "cursor-default opacity-60"
                    )}
                  >
                    <div
                      className={cn(
                        "flex h-9 w-9 shrink-0 items-center justify-center rounded-full border text-sm font-semibold",
                        state === "done"
                          ? "border-emerald-500 bg-emerald-500 text-white"
                          : "border-muted bg-muted/40 text-muted-foreground"
                      )}
                    >
                      {state === "done" ? <Check className="h-4 w-4" aria-hidden /> : step.number}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium">{step.title}</div>
                      <div className="truncate text-xs text-muted-foreground">{heading.mini ?? step.description}</div>
                    </div>
                    <span
                      className={cn(
                        "text-xs font-medium",
                        state === "done" ? "text-emerald-600" : "text-muted-foreground"
                      )}
                    >
                      {state === "done" ? "Complete" : "Upcoming"}
                    </span>
                    {state === "done" ? (
                      <Pencil className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                    ) : null}
                  </button>
                )}
              </div>

              {index < steps.length - 1 ? (
                <StationConnector
                  isDone={index < activeIndex}
                  isActive={index === activeIndex}
                  reverseCue={reverseCue}
                  nextTitle={steps[index + 1]?.title ?? ""}
                  backTitle={step.title}
                />
              ) : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}

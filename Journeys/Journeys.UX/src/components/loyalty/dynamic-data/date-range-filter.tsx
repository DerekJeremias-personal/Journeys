"use client";

/**
 * Date range filter for the dynamic data table.
 *
 * Emits ISO date strings (`fromDate`, `toDate`) suitable for use in the
 * event query filters built by `DynamicDataTable`.
 */

import { useCallback } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

export interface DateRangeFilterProps {
  fromDate: string | null;
  toDate: string | null;
  onChange: (fromDate: string | null, toDate: string | null) => void;
  className?: string;
  placeholder?: string;
}

export function DateRangeFilter({
  fromDate,
  toDate,
  onChange,
  className,
  placeholder = "Filter by date",
}: DateRangeFilterProps) {
  const handleFromChange = useCallback(
    (value: string) => onChange(value || null, toDate),
    [onChange, toDate]
  );

  const handleToChange = useCallback(
    (value: string) => onChange(fromDate, value || null),
    [onChange, fromDate]
  );

  return (
    <div className={cn("flex items-center gap-2", className)}>
      <Input
        type="date"
        value={fromDate ?? ""}
        onChange={(event) => handleFromChange(event.target.value)}
        aria-label={`${placeholder} from`}
        className="h-8 w-[150px]"
      />
      <span className="text-sm text-muted-foreground">to</span>
      <Input
        type="date"
        value={toDate ?? ""}
        onChange={(event) => handleToChange(event.target.value)}
        aria-label={`${placeholder} to`}
        className="h-8 w-[150px]"
      />
      {(fromDate || toDate) && (
        <Button size="sm" variant="ghost" onClick={() => onChange(null, null)}>
          Clear dates
        </Button>
      )}
    </div>
  );
}

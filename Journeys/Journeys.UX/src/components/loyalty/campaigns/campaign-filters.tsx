"use client";

import { Search } from "lucide-react";
import { useMemo, useState } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export interface CampaignFiltersValue {
  nameFilter: string;
  fromDate: string | null;
  toDate: string | null;
  queryString: string;
  queryParams: Record<string, unknown>;
}

export interface CampaignFiltersProps {
  value: CampaignFiltersValue;
  onChange: (next: CampaignFiltersValue) => void;
}

export const EMPTY_CAMPAIGN_FILTERS: CampaignFiltersValue = {
  nameFilter: "",
  fromDate: null,
  toDate: null,
  queryString: "",
  queryParams: {}
};

function buildQuery(
  input: Pick<CampaignFiltersValue, "nameFilter" | "fromDate" | "toDate">
): Pick<CampaignFiltersValue, "queryString" | "queryParams"> {
  const conditions: string[] = [];
  const parameters: Record<string, unknown> = {};

  if (input.nameFilter.trim()) {
    conditions.push("STARTSWITH(c.name, @name, true)");
    parameters["@name"] = input.nameFilter.trim();
  }
  if (input.fromDate) {
    conditions.push("(c.startDate >= @fromDate OR c.startdate >= @fromDate)");
    parameters["@fromDate"] = new Date(`${input.fromDate}T00:00:00.000Z`).toISOString();
  }
  if (input.toDate) {
    conditions.push("(c.startDate <= @toDate OR c.startdate <= @toDate)");
    parameters["@toDate"] = new Date(`${input.toDate}T23:59:59.999Z`).toISOString();
  }

  return { queryString: conditions.join(" and "), queryParams: parameters };
}

export function CampaignFilters({ value, onChange }: CampaignFiltersProps) {
  const [nameInput, setNameInput] = useState(value.nameFilter);
  const [fromDate, setFromDate] = useState(value.fromDate);
  const [toDate, setToDate] = useState(value.toDate);
  const hasActiveFilters = useMemo(
    () => Boolean(nameInput || fromDate || toDate),
    [nameInput, fromDate, toDate]
  );

  const apply = () => {
    onChange({
      nameFilter: nameInput,
      fromDate,
      toDate,
      ...buildQuery({ nameFilter: nameInput, fromDate, toDate })
    });
  };

  const reset = () => {
    setNameInput("");
    setFromDate(null);
    setToDate(null);
    onChange(EMPTY_CAMPAIGN_FILTERS);
  };

  const inputClassName =
    "h-9 rounded-md border border-input bg-transparent px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/50";

  return (
    <Card>
      <CardContent className="p-4">
        <form
          className="grid gap-4 md:grid-cols-[minmax(0,1fr)_auto_auto_auto] md:items-end"
          onSubmit={(event) => {
            event.preventDefault();
            apply();
          }}
        >
          <label className="space-y-1.5 text-sm font-medium" htmlFor="campaign-name-filter">
            Name
            <span className="relative mt-1.5 block">
              <Search
                className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden="true"
              />
              <input
                id="campaign-name-filter"
                className={`${inputClassName} w-full pl-9`}
                placeholder="Search by campaign name"
                value={nameInput}
                onChange={(event) => setNameInput(event.target.value)}
              />
            </span>
          </label>
          <label className="space-y-1.5 text-sm font-medium" htmlFor="campaign-from-date">
            From
            <input
              id="campaign-from-date"
              type="date"
              className={`${inputClassName} mt-1.5 block`}
              value={fromDate ?? ""}
              onChange={(event) => setFromDate(event.target.value || null)}
            />
          </label>
          <label className="space-y-1.5 text-sm font-medium" htmlFor="campaign-to-date">
            To
            <input
              id="campaign-to-date"
              type="date"
              className={`${inputClassName} mt-1.5 block`}
              value={toDate ?? ""}
              onChange={(event) => setToDate(event.target.value || null)}
            />
          </label>
          <div className="flex gap-2">
            <Button type="submit">Search</Button>
            {hasActiveFilters ? (
              <Button type="button" variant="outline" onClick={reset}>
                Clear
              </Button>
            ) : null}
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

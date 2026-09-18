"use client";

/**
 * DynamicDataTable — schema-driven data grid for loyalty events/entities.
 *
 * Generates columns from a `LoyaltySchema`, queries `queryData` with continuation-
 * token pagination, supports optional account-selection gating, date-range filter,
 * sortable columns (`SchemaAttribute.isGridSortable`/`defaultSortDirection`), CSV
 * export, and row-click navigation.
 *
 * Row navigation is opt-in: pass `detailRoutePath` to make rows clickable.
 */

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Download, Plus, Search, Users, X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { DataTable, type ColumnDef } from "@/components/shared/data-table";
import { cn } from "@/lib/utils";

import { queryData } from "@/services/loyalty/actions";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import {
  formatValueAsString,
  generateColumnsFromSchema,
  getInitialSortFromSchema,
  getNestedValue,
  getSchemaFieldValue,
} from "@/services/loyalty/utils/grid-columns";
import { normalizeEntityWithEvent } from "@/services/loyalty/utils/account-identifiers";
import {
  AccountsSelectionModal,
  type SelectedLoyaltyAccount,
} from "@/components/loyalty/accounts/accounts-selection-modal";
import { DateRangeFilter } from "./date-range-filter";

interface EventRow {
  id?: string;
  event?: Record<string, unknown>;
}

export interface DynamicDataTableProps<T extends EventRow = EventRow> {
  schema: LoyaltySchema;
  /** Pre-loaded data (used only when account selection is not required). */
  initialData?: T[];
  /** Bypass account-selection requirement (e.g. for nested grids in entity details). */
  skipAccountSelection?: boolean;
  /**
   * Route prefix for row clicks, e.g. `/loyalty/accounts`. Rows are not
   * clickable unless this is set.
   */
  detailRoutePath?: string | null;
  emptyMessage?: string;
  countLabel?: { singular: string; plural: string };
  exportFilePrefix?: string;
  fallbackIdColumn?: {
    label: string;
    getValue: (row: T) => unknown;
  };
  extraSearchFields?: string[];
  getDetailId?: (row: T) => string | undefined;
  className?: string;
}

interface QueryFilters {
  selectedAccountIds: string[];
  fromDate: string | null;
  toDate: string | null;
  sortKey: string;
  sortOrder: "asc" | "desc";
  searchText: string;
  searchField: string;
  columnFilters: AppliedFilter[];
}

interface QueryResult<T> {
  rows: T[];
  continuationToken: string | null;
}

type NormalizedDataType = "string" | "number" | "date" | "boolean";
type FilterOperator = "contains" | "equals" | "startsWith" | "gt" | "gte" | "lt" | "lte" | "between";

interface FilterableColumn {
  key: string;
  label: string;
  dataType: NormalizedDataType;
}

interface AppliedFilter {
  id: string;
  key: string;
  label: string;
  dataType: NormalizedDataType;
  operator: FilterOperator;
  value: string;
  valueTo?: string;
}

const ALL_FIELDS_SEARCH = "__all_fields__";
const EMPTY_EXTRA_SEARCH_FIELDS: string[] = [];

function normalizeDataType(dataType: string | null | undefined): NormalizedDataType {
  switch ((dataType ?? "string").toLowerCase()) {
    case "int":
    case "double":
    case "number":
      return "number";
    case "date":
    case "datetime":
      return "date";
    case "boolean":
    case "bool":
      return "boolean";
    default:
      return "string";
  }
}

function getOperatorsForDataType(dataType: NormalizedDataType): FilterOperator[] {
  switch (dataType) {
    case "string":
      return ["contains", "startsWith", "equals"];
    case "number":
      return ["equals", "gt", "gte", "lt", "lte", "between"];
    case "date":
      return ["equals", "gt", "gte", "lt", "lte", "between"];
    case "boolean":
      return ["equals"];
    default:
      return ["equals"];
  }
}

function getFieldExpression(symbol: string): { rootField: string; eventField: string } | null {
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(symbol)) return null;
  return {
    rootField: `c.${symbol}`,
    eventField: `c.event.${symbol}`,
  };
}

function compareValues(a: unknown, b: unknown, dataType: string | null | undefined): number {
  if (a == null && b == null) return 0;
  if (a == null) return 1;
  if (b == null) return -1;

  if (typeof a === "number" && typeof b === "number") return a - b;

  const normalizedDataType = (dataType ?? "string").toLowerCase();
  if (normalizedDataType === "date" || normalizedDataType === "datetime") {
    const aDate = a instanceof Date ? a.getTime() : typeof a === "string" ? Date.parse(a) : Number.NaN;
    const bDate = b instanceof Date ? b.getTime() : typeof b === "string" ? Date.parse(b) : Number.NaN;
    if (!Number.isNaN(aDate) && !Number.isNaN(bDate)) return aDate - bDate;
  }

  if (typeof a === "boolean" && typeof b === "boolean") return Number(a) - Number(b);

  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" });
}

function buildQuery(
  schemaName: string,
  dateAttributeSymbol: string | null,
  filters: QueryFilters,
  filterableColumns: FilterableColumn[],
  extraSearchFields: string[],
  continuationToken?: string
): Parameters<typeof queryData>[0] {
  const conditions: string[] = [];
  const args: Record<string, unknown> = {};
  let paramIndex = 0;

  const nextParam = () => `@p${paramIndex++}`;

  if (filters.selectedAccountIds.length > 0) {
    conditions.push(`ARRAY_CONTAINS(${JSON.stringify(filters.selectedAccountIds)}, c.accountid)`);
  }

  if (dateAttributeSymbol && (filters.fromDate || filters.toDate)) {
    const fieldExpr = getFieldExpression(dateAttributeSymbol);
    if (fieldExpr) {
      if (filters.fromDate) {
        args["@fromDate"] = new Date(`${filters.fromDate}T00:00:00.000Z`).toISOString();
        conditions.push(`(${fieldExpr.eventField} >= @fromDate OR ${fieldExpr.rootField} >= @fromDate)`);
      }
      if (filters.toDate) {
        args["@toDate"] = new Date(`${filters.toDate}T23:59:59.999Z`).toISOString();
        conditions.push(`(${fieldExpr.eventField} <= @toDate OR ${fieldExpr.rootField} <= @toDate)`);
      }
    }
  }

  if (filters.searchText) {
    const searchParam = nextParam();
    args[searchParam] = filters.searchText;

    if (filters.searchField === ALL_FIELDS_SEARCH) {
      const searchableFieldKeys = Array.from(
        new Set([
          ...filterableColumns.filter((col) => col.dataType === "string").map((col) => col.key),
          ...extraSearchFields,
        ])
      );
      const searchableClauses = searchableFieldKeys
        .map((key) => getFieldExpression(key))
        .filter((fieldExpr): fieldExpr is { rootField: string; eventField: string } => Boolean(fieldExpr))
        .map(
          (fieldExpr) =>
            `CONTAINS(${fieldExpr.eventField}, ${searchParam}, true) OR CONTAINS(${fieldExpr.rootField}, ${searchParam}, true)`
        );
      if (searchableClauses.length > 0) {
        conditions.push(`(${searchableClauses.join(" OR ")})`);
      }
    } else {
      const fieldExpr = getFieldExpression(filters.searchField);
      if (fieldExpr) {
        conditions.push(
          `(CONTAINS(${fieldExpr.eventField}, ${searchParam}, true) OR CONTAINS(${fieldExpr.rootField}, ${searchParam}, true))`
        );
      }
    }
  }

  for (const filter of filters.columnFilters) {
    const fieldExpr = getFieldExpression(filter.key);
    if (!fieldExpr) continue;

    const makeDualField = (expr: string) =>
      `(${expr.replaceAll("__FIELD__", fieldExpr.eventField)} OR ${expr.replaceAll("__FIELD__", fieldExpr.rootField)})`;

    const parseFilterValue = (value: string): unknown => {
      if (filter.dataType === "number") {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : undefined;
      }
      if (filter.dataType === "boolean") {
        if (value === "true") return true;
        if (value === "false") return false;
        return undefined;
      }
      if (filter.dataType === "date") {
        const parsedDate = new Date(value);
        if (Number.isNaN(parsedDate.getTime())) return undefined;
        return parsedDate.toISOString();
      }
      return value;
    };

    if (filter.operator === "between") {
      if (!filter.value || !filter.valueTo) continue;
      const fromParam = nextParam();
      const toParam = nextParam();
      const parsedFrom = parseFilterValue(filter.value);
      let parsedTo = parseFilterValue(filter.valueTo);
      if (filter.dataType === "date" && typeof parsedTo === "string") {
        const endDate = new Date(parsedTo);
        if (Number.isNaN(endDate.getTime())) continue;
        endDate.setUTCHours(23, 59, 59, 999);
        parsedTo = endDate.toISOString();
      }
      if (parsedFrom == null || parsedTo == null) continue;
      args[fromParam] = parsedFrom;
      args[toParam] = parsedTo;
      conditions.push(makeDualField(`(__FIELD__ >= ${fromParam} AND __FIELD__ <= ${toParam})`));
      continue;
    }

    if (!filter.value) continue;
    const valueParam = nextParam();
    const parsedValue = parseFilterValue(filter.value);
    if (parsedValue == null) continue;
    args[valueParam] = parsedValue;

    switch (filter.operator) {
      case "contains":
        conditions.push(makeDualField(`CONTAINS(__FIELD__, ${valueParam}, true)`));
        break;
      case "startsWith":
        conditions.push(makeDualField(`STARTSWITH(__FIELD__, ${valueParam}, true)`));
        break;
      case "equals":
        if (filter.dataType === "date") {
          const date = new Date(filter.value);
          if (Number.isNaN(date.getTime())) break;
          const endOfDay = new Date(date);
          endOfDay.setUTCHours(23, 59, 59, 999);
          const endParam = nextParam();
          args[valueParam] = date.toISOString();
          args[endParam] = endOfDay.toISOString();
          conditions.push(makeDualField(`(__FIELD__ >= ${valueParam} AND __FIELD__ <= ${endParam})`));
          break;
        }
        conditions.push(makeDualField(`__FIELD__ = ${valueParam}`));
        break;
      case "gt":
        conditions.push(makeDualField(`__FIELD__ > ${valueParam}`));
        break;
      case "gte":
        conditions.push(makeDualField(`__FIELD__ >= ${valueParam}`));
        break;
      case "lt":
        conditions.push(makeDualField(`__FIELD__ < ${valueParam}`));
        break;
      case "lte":
        conditions.push(makeDualField(`__FIELD__ <= ${valueParam}`));
        break;
      default:
        break;
    }
  }

  return {
    schemaName,
    queryString: conditions.join(" AND "),
    queryArgs: args,
    pageSize: 50,
    continuationToken: continuationToken ?? undefined,
    sortBy: filters.sortKey || undefined,
    sortOrder: filters.sortOrder.toUpperCase() as "ASC" | "DESC",
  };
}

export function DynamicDataTable<T extends EventRow = EventRow>({
  schema,
  initialData,
  skipAccountSelection = false,
  detailRoutePath,
  emptyMessage,
  countLabel,
  exportFilePrefix,
  fallbackIdColumn,
  extraSearchFields = EMPTY_EXTRA_SEARCH_FIELDS,
  getDetailId,
  className,
}: DynamicDataTableProps<T>) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const schemaName = schema.name ?? "";
  const schemaLabel = schemaName.toLowerCase();
  // gridRequiresAccount must be explicitly true to gate the grid.
  // `undefined` / `null` (fields not yet configured) should NOT block data access.
  const requiresAccountSelection = !skipAccountSelection && schema.gridRequiresAccount === true;

  const initialSort = useMemo(() => getInitialSortFromSchema(schema), [schema]);
  const [sortKey, setSortKey] = useState("");
  const [sortOrder, setSortOrder] = useState<"asc" | "desc">(initialSort.order);
  const [fromDate, setFromDate] = useState<string | null>(null);
  const [toDate, setToDate] = useState<string | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [searchText, setSearchText] = useState("");
  const [searchField, setSearchField] = useState(ALL_FIELDS_SEARCH);
  const [draftFilterKey, setDraftFilterKey] = useState("");
  const [draftFilterOperator, setDraftFilterOperator] = useState<FilterOperator>("contains");
  const [draftFilterValue, setDraftFilterValue] = useState("");
  const [draftFilterValueTo, setDraftFilterValueTo] = useState("");
  const [columnFilters, setColumnFilters] = useState<AppliedFilter[]>([]);
  const accountIdsFromUrl = useMemo(() => {
    const direct = searchParams.getAll("account");
    const split = direct.flatMap((value) => value.split(","));
    return Array.from(new Set(split.map((v) => v.trim()).filter((v) => v.length > 0)));
  }, [searchParams]);

  const [selectedAccounts, setSelectedAccounts] = useState<SelectedLoyaltyAccount[]>(() =>
    accountIdsFromUrl.map((id) => ({
      loyaltyAccountId: id,
      displayLabel: id,
      eventData: {},
    }))
  );
  const [accountsModalOpen, setAccountsModalOpen] = useState(false);
  const [continuationToken, setContinuationToken] = useState<string | null>(null);
  const [allRows, setAllRows] = useState<T[]>([]);

  useEffect(() => {
    setSortKey("");
    setSortOrder(initialSort.order);
  }, [schema.id, initialSort.order]);

  useEffect(() => {
    if (!requiresAccountSelection || accountIdsFromUrl.length === 0) return;
    setSelectedAccounts((prev) => {
      const prevIds = new Set(prev.map((a) => a.loyaltyAccountId));
      const sameLength = prev.length === accountIdsFromUrl.length;
      const sameValues = sameLength && accountIdsFromUrl.every((id) => prevIds.has(id));
      if (sameValues) return prev;
      return accountIdsFromUrl.map((id) => ({
        loyaltyAccountId: id,
        displayLabel: id,
        eventData: {},
      }));
    });
  }, [requiresAccountSelection, accountIdsFromUrl]);

  const dateAttribute = useMemo(() => {
    return (schema.attributes ?? []).find(
      (a) => a.type === "Primitive" && a.status === "Live" && (a.dataType ?? "").toLowerCase() === "date"
    );
  }, [schema]);

  const filterableColumns = useMemo<FilterableColumn[]>(() => {
    return (schema.attributes ?? [])
      .filter((a) => a.type === "Primitive" && a.status === "Live" && Boolean(a.symbol))
      .map((a) => ({
        key: a.symbol as string,
        label: a.displayName ?? (a.symbol as string),
        dataType: normalizeDataType(a.dataType),
      }))
      .filter((column) => Boolean(getFieldExpression(column.key)));
  }, [schema]);

  const selectedDraftColumn = useMemo(
    () => filterableColumns.find((column) => column.key === draftFilterKey) ?? null,
    [filterableColumns, draftFilterKey]
  );

  const availableOperators = useMemo<FilterOperator[]>(() => {
    if (!selectedDraftColumn) return ["contains", "equals"];
    return getOperatorsForDataType(selectedDraftColumn.dataType);
  }, [selectedDraftColumn]);

  useEffect(() => {
    if (!availableOperators.includes(draftFilterOperator)) {
      setDraftFilterOperator(availableOperators[0] ?? "equals");
    }
  }, [availableOperators, draftFilterOperator]);

  const filters = useMemo<QueryFilters>(
    () => ({
      selectedAccountIds: selectedAccounts.map((a) => a.loyaltyAccountId),
      fromDate,
      toDate,
      sortKey: sortKey === initialSort.key ? "" : sortKey,
      sortOrder: sortKey === initialSort.key ? initialSort.order : sortOrder,
      searchText,
      searchField,
      columnFilters,
    }),
    [
      selectedAccounts,
      fromDate,
      toDate,
      initialSort.key,
      initialSort.order,
      sortKey,
      sortOrder,
      searchText,
      searchField,
      columnFilters,
    ]
  );

  const isQueryEnabled = !requiresAccountSelection || selectedAccounts.length > 0;

  const queryResult = useQuery<QueryResult<T>>({
    queryKey: [
      "loyalty",
      "dynamic-data",
      schema.id,
      schemaName,
      filters.selectedAccountIds,
      filters.fromDate,
      filters.toDate,
      filters.sortKey,
      filters.sortOrder,
      filters.searchText,
      filters.searchField,
      filters.columnFilters,
      extraSearchFields,
      // Note: we intentionally exclude `continuationToken` from the key so that
      // appending more pages doesn't re-fetch from page 1.
    ],
    queryFn: async () => {
      const response = await queryData<T>(
        buildQuery(schemaName, dateAttribute?.symbol ?? null, filters, filterableColumns, extraSearchFields)
      );
      if (!response.success) throw new Error(response.error ?? "Failed to load data");
      const rawData: T[] = response.data ?? [];
      const rows = rawData
        .map((item) => (normalizeEntityWithEvent(item as Record<string, unknown>) ?? item) as T)
        .filter(Boolean);
      const newToken = response.meta?.continuationToken ?? null;
      // On a fresh query (no existing token), reset accumulated rows
      setAllRows(rows);
      setContinuationToken(newToken);
      return {
        rows,
        continuationToken: newToken,
      };
    },
    enabled: isQueryEnabled,
    placeholderData: (previous) => previous,
  });

  const [isLoadingMore, setIsLoadingMore] = useState(false);

  const handleLoadMore = useCallback(async () => {
    if (!continuationToken || isLoadingMore) return;
    setIsLoadingMore(true);
    try {
      const response = await queryData<T>(
        buildQuery(
          schemaName,
          dateAttribute?.symbol ?? null,
          filters,
          filterableColumns,
          extraSearchFields,
          continuationToken
        )
      );
      if (!response.success) return;
      const rawData: T[] = response.data ?? [];
      const newRows = rawData
        .map((item) => (normalizeEntityWithEvent(item as Record<string, unknown>) ?? item) as T)
        .filter(Boolean);
      setAllRows((prev) => [...prev, ...newRows]);
      setContinuationToken(response.meta?.continuationToken ?? null);
    } finally {
      setIsLoadingMore(false);
    }
  }, [
    continuationToken,
    isLoadingMore,
    schemaName,
    dateAttribute?.symbol,
    filters,
    filterableColumns,
    extraSearchFields,
  ]);

  const rawData = useMemo<T[]>(() => {
    const queryRows = queryResult.data?.rows ?? [];
    if (allRows.length > 0) return allRows;
    if (queryRows.length > 0) return queryRows;
    return requiresAccountSelection ? [] : (initialData ?? []);
  }, [allRows, queryResult.data, requiresAccountSelection, initialData]);
  const schemaColumns = useMemo(() => generateColumnsFromSchema(schema), [schema]);
  const columns = useMemo<ColumnDef<T>[]>(() => {
    return schemaColumns.map((col) => ({
      id: col.key,
      header: col.key === "id" && fallbackIdColumn ? fallbackIdColumn.label : col.label,
      sortable: col.sortable,
      cell: (row: T) => {
        const value =
          col.key === "id" && fallbackIdColumn ? fallbackIdColumn.getValue(row) : getSchemaFieldValue(row, col.key);
        return <span className="text-sm">{formatValueAsString(value, col.dataType)}</span>;
      },
    }));
  }, [schemaColumns, fallbackIdColumn]);
  const sortColumn = useMemo(
    () => schemaColumns.find((column) => column.key === sortKey) ?? null,
    [schemaColumns, sortKey]
  );
  const data = useMemo<T[]>(() => {
    if (!sortKey || sortKey !== initialSort.key) return rawData;

    return [...rawData].sort((left, right) => {
      const leftValue = getNestedValue(left, sortKey);
      const rightValue = getNestedValue(right, sortKey);
      const compared = compareValues(leftValue, rightValue, sortColumn?.dataType);
      return sortOrder === "asc" ? compared : -compared;
    });
  }, [initialSort.key, rawData, sortColumn?.dataType, sortKey, sortOrder]);
  const isLoading = queryResult.isLoading;
  const errorMessage = (queryResult.error as Error | undefined)?.message;

  const handleSort = useCallback((columnId: string, direction: "asc" | "desc") => {
    setSortKey(columnId);
    setSortOrder(direction);
  }, []);

  const handleSearch = useCallback(() => {
    setSearchText(searchInput.trim());
  }, [searchInput]);

  const handleClearSearch = useCallback(() => {
    setSearchInput("");
    setSearchText("");
  }, []);

  const handleAddFilter = useCallback(() => {
    if (!selectedDraftColumn) return;
    const value = draftFilterValue.trim();
    const valueTo = draftFilterValueTo.trim();
    if (!value) return;
    if (draftFilterOperator === "between" && !valueTo) return;

    setColumnFilters((prev) => [
      ...prev,
      {
        id: crypto.randomUUID(),
        key: selectedDraftColumn.key,
        label: selectedDraftColumn.label,
        dataType: selectedDraftColumn.dataType,
        operator: draftFilterOperator,
        value,
        valueTo: draftFilterOperator === "between" ? valueTo : undefined,
      },
    ]);

    setDraftFilterValue("");
    setDraftFilterValueTo("");
  }, [selectedDraftColumn, draftFilterOperator, draftFilterValue, draftFilterValueTo]);

  const handleRemoveFilter = useCallback((id: string) => {
    setColumnFilters((prev) => prev.filter((filter) => filter.id !== id));
  }, []);

  const handleClearFilters = useCallback(() => {
    setColumnFilters([]);
  }, []);

  const handleRowClick = useCallback(
    (row: T) => {
      if (!detailRoutePath) return;
      const id = getDetailId?.(row) || row.id || (getNestedValue(row, "id") as string | undefined);
      if (!id) return;
      const target = new URL(`${detailRoutePath}/${encodeURIComponent(id)}`, "http://localhost");
      for (const accountId of filters.selectedAccountIds) {
        target.searchParams.append("account", accountId);
      }
      router.push(`${target.pathname}${target.search}`);
    },
    [router, detailRoutePath, getDetailId, filters.selectedAccountIds]
  );

  const handleExport = useCallback(() => {
    if (data.length === 0) return;
    const headerRow = columns.map((c) => c.header).join(",");
    const dataRows = data.map((row) =>
      columns
        .map((col) => {
          const value = getSchemaFieldValue(row, col.id);
          const stringValue = String(value ?? "").replace(/"/g, '""');
          return `"${stringValue}"`;
        })
        .join(",")
    );
    const csv = [headerRow, ...dataRows].join("\n");
    const blob = new Blob([csv], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${exportFilePrefix ?? schemaName}-${new Date().toISOString()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }, [data, columns, exportFilePrefix, schemaName]);

  if (requiresAccountSelection && selectedAccounts.length === 0) {
    return (
      <>
        <Card>
          <CardContent className="p-12">
            <div className="flex flex-col items-center justify-center text-center space-y-3">
              <Users className="h-10 w-10 text-muted-foreground" aria-hidden="true" />
              <div>
                <h3 className="text-lg font-semibold">No data to display</h3>
                <p className="text-sm text-muted-foreground mt-1">
                  Select one or more loyalty accounts to view their {schemaLabel}.
                </p>
              </div>
              <Button onClick={() => setAccountsModalOpen(true)}>Select loyalty accounts</Button>
            </div>
          </CardContent>
        </Card>
        <AccountsSelectionModal
          open={accountsModalOpen}
          onOpenChange={setAccountsModalOpen}
          onApply={setSelectedAccounts}
          initialSelections={selectedAccounts}
        />
      </>
    );
  }

  return (
    <div className={cn("space-y-4", className)}>
      <Card>
        <CardContent className="p-4 space-y-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              {requiresAccountSelection && (
                <Button size="sm" variant="outline" onClick={() => setAccountsModalOpen(true)}>
                  Change accounts ({selectedAccounts.length})
                </Button>
              )}
              <div className="flex items-center gap-2">
                <Select value={searchField} onValueChange={setSearchField}>
                  <SelectTrigger className="h-8 w-[180px]">
                    <SelectValue placeholder="Search field" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_FIELDS_SEARCH}>All text fields</SelectItem>
                    {filterableColumns
                      .filter((column) => column.dataType === "string")
                      .map((column) => (
                        <SelectItem key={column.key} value={column.key}>
                          {column.label}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                <div className="relative w-[220px]">
                  <Search className="pointer-events-none absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        handleSearch();
                      }
                    }}
                    placeholder="Search"
                    className="h-8 pl-8"
                  />
                </div>
                <Button size="sm" variant="outline" onClick={handleSearch}>
                  Search
                </Button>
                {(searchText || searchInput) && (
                  <Button size="sm" variant="ghost" onClick={handleClearSearch}>
                    Clear
                  </Button>
                )}
              </div>
              {dateAttribute && (
                <DateRangeFilter
                  fromDate={fromDate}
                  toDate={toDate}
                  onChange={(from, to) => {
                    setFromDate(from);
                    setToDate(to);
                  }}
                />
              )}
            </div>
            <Button
              size="sm"
              variant="outline"
              onClick={handleExport}
              disabled={data.length === 0}
              title="Export currently loaded rows to CSV (server-side pagination not included)"
            >
              <Download className="mr-2 h-4 w-4" />
              Export
            </Button>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Select value={draftFilterKey} onValueChange={setDraftFilterKey}>
              <SelectTrigger className="h-8 w-[190px]">
                <SelectValue placeholder="Filter field" />
              </SelectTrigger>
              <SelectContent>
                {filterableColumns.map((column) => (
                  <SelectItem key={column.key} value={column.key}>
                    {column.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={draftFilterOperator}
              onValueChange={(value) => setDraftFilterOperator(value as FilterOperator)}
              disabled={!selectedDraftColumn}
            >
              <SelectTrigger className="h-8 w-[130px]">
                <SelectValue placeholder="Operator" />
              </SelectTrigger>
              <SelectContent>
                {availableOperators.map((operator) => (
                  <SelectItem key={operator} value={operator}>
                    {operator}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {selectedDraftColumn?.dataType !== "boolean" && (
              <Input
                value={draftFilterValue}
                onChange={(event) => setDraftFilterValue(event.target.value)}
                className="h-8 w-[170px]"
                placeholder="Value"
                type={
                  selectedDraftColumn?.dataType === "number"
                    ? "number"
                    : selectedDraftColumn?.dataType === "date"
                      ? "date"
                      : "text"
                }
              />
            )}
            {draftFilterOperator === "between" && (
              <Input
                value={draftFilterValueTo}
                onChange={(event) => setDraftFilterValueTo(event.target.value)}
                className="h-8 w-[170px]"
                placeholder="And value"
                type={
                  selectedDraftColumn?.dataType === "number"
                    ? "number"
                    : selectedDraftColumn?.dataType === "date"
                      ? "date"
                      : "text"
                }
              />
            )}
            {selectedDraftColumn?.dataType === "boolean" && (
              <Select value={draftFilterValue} onValueChange={setDraftFilterValue}>
                <SelectTrigger className="h-8 w-[130px]">
                  <SelectValue placeholder="True / False" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="true">true</SelectItem>
                  <SelectItem value="false">false</SelectItem>
                </SelectContent>
              </Select>
            )}
            <Button
              size="sm"
              variant="outline"
              onClick={handleAddFilter}
              disabled={
                !selectedDraftColumn || !draftFilterValue || (draftFilterOperator === "between" && !draftFilterValueTo)
              }
            >
              <Plus className="mr-1 h-3 w-3" />
              Add filter
            </Button>
            {columnFilters.length > 0 && (
              <Button size="sm" variant="ghost" onClick={handleClearFilters}>
                Clear filters
              </Button>
            )}
          </div>

          {columnFilters.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {columnFilters.map((filter) => (
                <Badge key={filter.id} variant="secondary" className="gap-1">
                  {filter.label} {filter.operator} {filter.value}
                  {filter.valueTo ? ` - ${filter.valueTo}` : ""}
                  <button
                    type="button"
                    aria-label={`Remove filter ${filter.label}`}
                    onClick={() => handleRemoveFilter(filter.id)}
                    className="ml-1 hover:text-destructive"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          )}

          {errorMessage && (
            <Alert variant="destructive">
              <AlertTitle>Failed to load data</AlertTitle>
              <AlertDescription>{errorMessage}</AlertDescription>
            </Alert>
          )}

          <DataTable<T>
            columns={columns}
            data={data}
            isLoading={isLoading}
            emptyMessage={emptyMessage ?? `No ${schemaLabel} found.`}
            onRowClick={detailRoutePath ? handleRowClick : undefined}
            getRowId={(row) => row.id ?? ""}
            onSort={handleSort}
            sortColumnId={sortKey}
            sortOrder={sortOrder}
            allowClearSort
          />

          <p className="text-xs text-muted-foreground text-center">
            Showing {data.length}{" "}
            {data.length === 1 ? (countLabel?.singular ?? schemaLabel) : (countLabel?.plural ?? `${schemaLabel}s`)}
            {continuationToken && (
              <>
                {" — "}
                <button
                  type="button"
                  onClick={() => void handleLoadMore()}
                  disabled={isLoadingMore}
                  className="underline hover:text-foreground disabled:opacity-50"
                >
                  {isLoadingMore ? "Loading…" : "Load more"}
                </button>
              </>
            )}
          </p>
        </CardContent>
      </Card>

      {requiresAccountSelection && (
        <AccountsSelectionModal
          open={accountsModalOpen}
          onOpenChange={setAccountsModalOpen}
          onApply={setSelectedAccounts}
          initialSelections={selectedAccounts}
        />
      )}
    </div>
  );
}

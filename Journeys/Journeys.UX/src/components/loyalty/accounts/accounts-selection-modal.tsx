"use client";

/**
 * AccountsSelectionModal — loyalty account picker used by grids that are gated
 * on an account selection.
 *
 *   - schema provided → schema-driven dynamic columns + rich `eventData`/`displayLabel` payload
 *   - schema omitted → the modal loads `LoyaltyAccountDetails` itself, and falls
 *     back to a fixed column set if that fetch fails
 *
 * Selection state persists across search/filter changes (selected accounts NOT
 * in the current filter remain in the chip strip and the apply payload). Lookup
 * is keyed by `loyaltyAccountId`.
 */

import { useCallback, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, X } from "lucide-react";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { DataTable, type ColumnDef } from "@/components/shared/data-table";
import { Skeleton } from "@/components/ui/skeleton";

import { getSchemaByName, queryData } from "@/services/loyalty/actions";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { formatValueAsString, generateColumnsFromSchema, getNestedValue } from "@/services/loyalty/utils/grid-columns";
import {
  LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
  resolveLoyaltyAccountXReference,
} from "@/services/loyalty/utils/account-identifiers";

interface AccountRecord {
  loyaltyAccountId: string;
  event: Record<string, unknown>;
}

export interface SelectedLoyaltyAccount {
  loyaltyAccountId: string;
  displayLabel: string;
  eventData: Record<string, unknown>;
}

export interface AccountsSelectionModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Called with the selected accounts when the user clicks Apply. */
  onApply: (selected: SelectedLoyaltyAccount[]) => void;
  /** Pre-selected accounts on open. */
  initialSelections?: SelectedLoyaltyAccount[];
  /**
   * Optional schema to drive columns and label. When omitted, the modal:
   *   - fetches `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` from `getSchemaByName`
   *   - falls back to a fixed column set if the schema fetch fails
   */
  schema?: LoyaltySchema | null;
  /** Title shown in the dialog header. */
  title?: string;
  /** Description shown in the dialog header. */
  description?: string;
  /** Search field placeholder. Defaults to "Search by account ID". */
  searchPlaceholder?: string;
}

const FIXED_COLUMNS_FALLBACK = [
  { key: "customerid", label: "Customer ID", dataType: "String", sortable: true },
  { key: "region", label: "Region", dataType: "String", sortable: true },
] as const;

function getAccountDisplayId(row: AccountRecord): string {
  return resolveLoyaltyAccountXReference(row as unknown as Record<string, unknown>, row.loyaltyAccountId);
}

export function AccountsSelectionModal({
  open,
  onOpenChange,
  onApply,
  initialSelections = [],
  schema: providedSchema = null,
  title = "Select Loyalty Accounts",
  description = "Choose one or more loyalty accounts to view their data.",
  searchPlaceholder = "Search by account ID",
}: AccountsSelectionModalProps) {
  const [filterText, setFilterText] = useState("");
  const [activeFilter, setActiveFilter] = useState("");
  const [selected, setSelected] = useState<Map<string, SelectedLoyaltyAccount>>(
    () => new Map(initialSelections.map((s) => [s.loyaltyAccountId, s]))
  );

  // Re-seed on open via onOpenChange, not useEffect (avoids react-hooks/set-state-in-effect)
  const handleOpenChange = (next: boolean) => {
    if (next) {
      setSelected(new Map(initialSelections.map((s) => [s.loyaltyAccountId, s])));
      setFilterText("");
      setActiveFilter("");
    }
    onOpenChange(next);
  };

  const schemaQuery = useQuery({
    queryKey: loyaltyKeys.schemas.byName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME),
    queryFn: async () => {
      const result = await getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME);
      if (!result.success) throw new Error(result.error ?? "Failed to load schema");
      return result.data ?? null;
    },
    enabled: open && !providedSchema,
    staleTime: 5 * 60 * 1000,
  });

  const schema: LoyaltySchema | null = providedSchema ?? schemaQuery.data ?? null;

  const dataQuery = useQuery({
    queryKey: ["loyalty-accounts-modal", LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME, activeFilter],
    queryFn: async () => {
      const queryString = activeFilter ? LOYALTY_ACCOUNT_IDENTIFIER_QUERY : "";
      const queryArgs = activeFilter ? { "@id": activeFilter } : {};

      const response = await queryData<AccountRecord>({
        schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME,
        queryString,
        queryArgs,
        pageSize: 50,
      });

      if (!response.success) throw new Error(response.error ?? "Failed to load accounts");
      return response.data ?? [];
    },
    enabled: open,
  });

  const firstColumnKey = useMemo<string | null>(() => {
    if (schema) {
      const cols = generateColumnsFromSchema(schema);
      return cols[0]?.key ?? null;
    }
    return FIXED_COLUMNS_FALLBACK[0]?.key ?? null;
  }, [schema]);

  const buildDisplayLabel = useCallback(
    (row: AccountRecord): string => {
      const displayId = getAccountDisplayId(row);
      if (displayId) return displayId;

      if (firstColumnKey) {
        const value = getNestedValue(row.event, firstColumnKey);
        if (value !== null && value !== undefined) return String(value);
      }
      return row.loyaltyAccountId;
    },
    [firstColumnKey]
  );

  const toggleSelection = useCallback(
    (row: AccountRecord) => {
      setSelected((prev) => {
        const next = new Map(prev);
        if (next.has(row.loyaltyAccountId)) {
          next.delete(row.loyaltyAccountId);
        } else {
          next.set(row.loyaltyAccountId, {
            loyaltyAccountId: row.loyaltyAccountId,
            displayLabel: buildDisplayLabel(row),
            eventData: row.event,
          });
        }
        return next;
      });
    },
    [buildDisplayLabel]
  );

  // `columns` recomputes when `schema`, `selected`, or `toggleSelection` changes.
  // `selected` is a Map and changes on every selection toggle, but columns are
  // cheap to recompute inside a modal. The cell renderers for the checkbox column
  // need the current `selected` state and `toggleSelection` — both listed in deps.
  const columns: ColumnDef<AccountRecord>[] = useMemo(() => {
    const selectColumn: ColumnDef<AccountRecord> = {
      id: "select",
      header: "",
      cell: (row) => (
        <span
          className="inline-flex"
          onClick={(event) => event.stopPropagation()}
          onKeyDown={(event) => event.stopPropagation()}
        >
          <Checkbox
            checked={selected.has(row.loyaltyAccountId)}
            aria-label={`Select account ${row.loyaltyAccountId}`}
            onCheckedChange={() => toggleSelection(row)}
          />
        </span>
      ),
    };
    const accountIdColumn: ColumnDef<AccountRecord> = {
      id: "extAccountId",
      header: "External account ID",
      cell: (row) => (
        <div className="space-y-0.5">
          <code className="text-xs">{getAccountDisplayId(row)}</code>
          {row.loyaltyAccountId !== getAccountDisplayId(row) && (
            <p className="text-[11px] text-muted-foreground break-all">{row.loyaltyAccountId}</p>
          )}
        </div>
      ),
    };

    if (schema) {
      const schemaCols = generateColumnsFromSchema(schema)
        .filter(
          (col) => !["id", "profileid", "profileId", "customerid", "customerId", "extAccountId"].includes(col.key)
        )
        .map((col) => ({
          id: col.key,
          header: col.label,
          cell: (row: AccountRecord) => (
            <span className="text-sm">{formatValueAsString(getNestedValue(row.event, col.key), col.dataType)}</span>
          ),
        }));
      return [selectColumn, accountIdColumn, ...schemaCols];
    }

    return [
      selectColumn,
      accountIdColumn,
      ...FIXED_COLUMNS_FALLBACK.map((col) => ({
        id: col.key,
        header: col.label,
        cell: (row: AccountRecord) => (
          <span className="text-sm">{formatValueAsString(getNestedValue(row.event, col.key), col.dataType)}</span>
        ),
      })),
    ];
  }, [schema, selected, toggleSelection]);

  const removeBadge = useCallback((id: string) => {
    setSelected((prev) => {
      const next = new Map(prev);
      next.delete(id);
      return next;
    });
  }, []);

  const handleSearch = useCallback(() => {
    setActiveFilter(filterText.trim());
  }, [filterText]);

  const handleReset = useCallback(() => {
    setFilterText("");
    setActiveFilter("");
    setSelected(new Map());
  }, []);

  const handleApply = useCallback(() => {
    onApply(Array.from(selected.values()));
    onOpenChange(false);
  }, [selected, onApply, onOpenChange]);

  const data = dataQuery.data ?? [];
  const isLoading = dataQuery.isLoading || (!providedSchema && schemaQuery.isLoading);
  const error = (dataQuery.error as Error | undefined)?.message ?? (schemaQuery.error as Error | undefined)?.message;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="flex h-[calc(100vh-2rem)] max-h-[760px] w-[calc(100vw-2rem)] max-w-[calc(100vw-2rem)] flex-col overflow-hidden sm:max-w-[calc(100vw-2rem)] xl:max-w-7xl">
        <DialogHeader className="min-w-0">
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        {selected.size > 0 && (
          <div className="max-h-20 overflow-y-auto pr-1">
            <div className="flex flex-wrap gap-2">
              {Array.from(selected.values()).map((account) => (
                <Badge key={account.loyaltyAccountId} variant="secondary" className="gap-1">
                  {account.displayLabel}
                  <button
                    type="button"
                    onClick={() => removeBadge(account.loyaltyAccountId)}
                    className="hover:text-destructive ml-1"
                    aria-label={`Remove ${account.displayLabel}`}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          </div>
        )}

        <div className="flex shrink-0 items-center gap-2">
          <div className="relative flex-1 max-w-sm">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              type="text"
              placeholder={searchPlaceholder}
              value={filterText}
              onChange={(e) => setFilterText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.preventDefault();
                  handleSearch();
                }
              }}
              className="pl-9"
              aria-label={searchPlaceholder}
            />
          </div>
          <Button size="sm" onClick={handleSearch}>
            Search
          </Button>
          <Button size="sm" variant="outline" onClick={handleReset}>
            Reset
          </Button>
        </div>

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <div className="min-h-0 flex-1 overflow-y-auto rounded-md">
          {isLoading && data.length === 0 ? (
            <div className="space-y-2">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : (
            <div className="max-w-full overflow-x-auto pb-2">
              <DataTable<AccountRecord>
                columns={columns}
                data={data}
                isLoading={dataQuery.isLoading}
                emptyMessage="No accounts found. Try adjusting your search."
                getRowId={(row) => row.loyaltyAccountId}
                onRowClick={toggleSelection}
              />
            </div>
          )}
        </div>

        <DialogFooter className="shrink-0 items-center border-t pt-3">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleApply} disabled={selected.size === 0 && initialSelections.length === 0}>
            Apply ({selected.size})
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

"use client";

import React from "react";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import { ArrowUp, ArrowDown, ArrowUpDown } from "lucide-react";
import { cn } from "@/lib/utils";
import type { JsonValue, PaginationMeta, PaginationParams } from "@/lib/loyalty-schema-types";

export interface ColumnDef<T> {
  id: string;
  header: string;
  accessorFn?: (row: T) => JsonValue;
  cell?: (row: T) => React.ReactNode;
  sortable?: boolean;
}

type SortOrder = NonNullable<PaginationParams["sortOrder"]>;

interface DataTableProps<T> {
  columns: ColumnDef<T>[];
  data: T[];
  pagination?: PaginationMeta;
  onPageChange?: (page: number) => void;
  onSort?: (columnId: string, direction: SortOrder) => void;
  sortColumnId?: string;
  sortOrder?: SortOrder;
  allowClearSort?: boolean;
  defaultSortColumnId?: string;
  defaultSortOrder?: SortOrder;
  isLoading?: boolean;
  emptyMessage?: string;
  onRowClick?: (row: T) => void;
  getRowId?: (row: T) => string;
  selectedRowId?: string | null;
}

function SortIcon({
  columnId,
  activeId,
  activeOrder,
}: {
  columnId: string;
  activeId?: string;
  activeOrder?: SortOrder;
}) {
  if (columnId !== activeId) return <ArrowUpDown className="ml-1 inline-block size-3 opacity-40" />;
  if (activeOrder === "asc") return <ArrowUp className="ml-1 inline-block size-3" />;
  return <ArrowDown className="ml-1 inline-block size-3" />;
}

export function DataTable<T>({
  columns,
  data,
  pagination,
  onPageChange,
  onSort,
  sortColumnId,
  sortOrder,
  allowClearSort = false,
  defaultSortColumnId,
  defaultSortOrder = "desc",
  isLoading = false,
  emptyMessage = "No results found.",
  onRowClick,
  getRowId,
  selectedRowId,
}: DataTableProps<T>) {
  const handleHeaderClick = (col: ColumnDef<T>) => {
    if (!col.sortable || !onSort) return;
    const defaultColumnId = defaultSortColumnId ?? col.id;
    const isActiveColumn = col.id === sortColumnId;
    const isAlreadyDefaultSort = sortColumnId === defaultColumnId && sortOrder === defaultSortOrder;
    if (allowClearSort && isActiveColumn && sortOrder === "desc" && !isAlreadyDefaultSort) {
      onSort(defaultColumnId, defaultSortOrder);
      return;
    }
    const nextOrder: SortOrder = isActiveColumn && sortOrder === "asc" ? "desc" : "asc";
    onSort(col.id, nextOrder);
  };

  const ariaSortForColumn = (col: ColumnDef<T>): "none" | "ascending" | "descending" | undefined => {
    if (!col.sortable || !onSort) return undefined;
    if (col.id !== sortColumnId) return "none";
    return sortOrder === "asc" ? "ascending" : "descending";
  };

  const renderHeader = (col: ColumnDef<T>) => {
    if (!col.sortable || !onSort) return col.header;
    return (
      <button
        type="button"
        className="-ml-1 inline-flex items-center gap-0.5 rounded px-1 py-0.5 transition-colors hover:text-foreground"
        onClick={() => handleHeaderClick(col)}
      >
        {col.header}
        <SortIcon columnId={col.id} activeId={sortColumnId} activeOrder={sortOrder} />
      </button>
    );
  };

  const tableShell = (body: React.ReactNode) => (
    <div className="overflow-hidden rounded-xl border bg-card">
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-muted/40">
            {columns.map((col) => (
              <TableHead key={col.id} aria-sort={ariaSortForColumn(col)}>
                {renderHeader(col)}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>{body}</TableBody>
      </Table>
    </div>
  );

  if (isLoading) {
    return tableShell(
      Array.from({ length: 5 }).map((_, i) => (
        <TableRow key={i} className="hover:bg-transparent">
          {columns.map((col) => (
            <TableCell key={col.id}>
              <Skeleton className={cn("h-4", col.id === columns[0]?.id ? "w-32" : "w-full max-w-24")} />
            </TableCell>
          ))}
        </TableRow>
      ))
    );
  }

  // ── Record count ───────────────────────────────────────────────────────────
  const recordCount = (() => {
    if (!pagination) return null;
    const { page, limit, totalPages } = pagination;
    const total = (pagination as { total?: number }).total;
    if (total != null) {
      const from = (page - 1) * limit + 1;
      const to = Math.min(page * limit, total);
      return `${from.toLocaleString()}–${to.toLocaleString()} of ${total.toLocaleString()}`;
    }
    if (totalPages > 1) return `Page ${page} of ${totalPages}`;
    return null;
  })();

  return (
    <div className="space-y-3">
      {tableShell(
        data.length === 0 ? (
          <TableRow className="hover:bg-transparent">
            <TableCell colSpan={columns.length} className="px-4 py-10 text-center">
              <p className="text-sm text-muted-foreground">{emptyMessage}</p>
            </TableCell>
          </TableRow>
        ) : (
          data.map((row, i) => {
            const rowKey = getRowId?.(row) ?? String(i);
            const isSelected = selectedRowId != null && getRowId?.(row) === selectedRowId;
            return (
              <TableRow
                key={rowKey}
                data-state={isSelected ? "selected" : undefined}
                tabIndex={onRowClick ? 0 : undefined}
                className={cn(
                  "transition-colors",
                  onRowClick && "cursor-pointer hover:bg-muted/50",
                  isSelected && "border-l-2 border-primary bg-primary/5 hover:bg-primary/8"
                )}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                onKeyDown={
                  onRowClick
                    ? (e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          onRowClick(row);
                        }
                      }
                    : undefined
                }
              >
                {columns.map((col) => (
                  <TableCell key={col.id}>
                    {col.cell ? col.cell(row) : col.accessorFn ? String(col.accessorFn(row) ?? "") : null}
                  </TableCell>
                ))}
              </TableRow>
            );
          })
        )
      )}

      {pagination && pagination.totalPages > 1 && (
        <div className="flex items-center justify-between gap-4">
          {recordCount && <p className="text-xs text-muted-foreground">{recordCount}</p>}
          <Pagination className="mx-0 w-auto flex-1 justify-end">
            <PaginationContent>
              {pagination.hasPrev && (
                <PaginationItem>
                  <PaginationPrevious
                    href="#"
                    onClick={(e) => {
                      e.preventDefault();
                      onPageChange?.(pagination.page - 1);
                    }}
                  />
                </PaginationItem>
              )}
              {Array.from({ length: pagination.totalPages }, (_, i) => i + 1)
                .filter((p) => p === 1 || p === pagination.totalPages || Math.abs(p - pagination.page) <= 1)
                .map((p, idx, arr) => {
                  const prev = arr[idx - 1];
                  return (
                    <React.Fragment key={p}>
                      {prev != null && p - prev > 1 && (
                        <PaginationItem>
                          <span className="px-2 text-sm text-muted-foreground">…</span>
                        </PaginationItem>
                      )}
                      <PaginationItem>
                        <PaginationLink
                          href="#"
                          isActive={p === pagination.page}
                          onClick={(e) => {
                            e.preventDefault();
                            onPageChange?.(p);
                          }}
                        >
                          {p}
                        </PaginationLink>
                      </PaginationItem>
                    </React.Fragment>
                  );
                })}
              {pagination.hasNext && (
                <PaginationItem>
                  <PaginationNext
                    href="#"
                    onClick={(e) => {
                      e.preventDefault();
                      onPageChange?.(pagination.page + 1);
                    }}
                  />
                </PaginationItem>
              )}
            </PaginationContent>
          </Pagination>
        </div>
      )}
    </div>
  );
}

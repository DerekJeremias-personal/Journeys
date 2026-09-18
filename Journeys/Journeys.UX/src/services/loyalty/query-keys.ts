export const loyaltyKeys = {
  campaigns: {
    all: ["loyalty", "campaigns"] as const,
    list: (filters?: Record<string, unknown>) =>
      ["loyalty", "campaigns", "list", filters] as const,
    detail: (id: string) => ["loyalty", "campaigns", "detail", id] as const,
    versions: (extCampaignId: string) =>
      ["loyalty", "campaigns", "versions", extCampaignId] as const,
    archived: (filters?: Record<string, unknown>) =>
      ["loyalty", "campaigns", "archived", filters] as const
  },
  schemas: {
    all: (modelType?: string) => ["loyalty", "schemas", modelType ?? "loyalty"] as const,
    byId: (id: string) => ["loyalty", "schemas", "by-id", id] as const,
    byName: (name: string) => ["loyalty", "schemas", "by-name", name] as const
  },
  pointAccountTypes: {
    all: ["loyalty", "point-account-types"] as const
  },
  dashboard: {
    data: ["loyalty", "dashboard", "data"] as const
  },
  accounts: {
    all: ["loyalty", "accounts"] as const,
    detail: (id: string) => ["loyalty", "accounts", "detail", id] as const,
    detailByExt: (ext: string) => ["loyalty", "accounts", "detail-ext", ext] as const,
    loyaltyDetail: (id: string) => ["loyalty", "accounts", "loyalty", id] as const
  },
  points: {
    balancesByAccount: (id: string) => ["loyalty", "points", "balances", id] as const,
    ledgersByAccount: (id: string) => ["loyalty", "points", "ledgers", id] as const
  },
  eventable: {
    bySchemaAccount: (schemaName: string, accountId: string) =>
      ["loyalty", "eventable", schemaName, accountId] as const
  }
} as const;

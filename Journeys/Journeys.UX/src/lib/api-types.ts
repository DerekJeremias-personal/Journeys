export type ApiResponse<T> = {
  success: boolean;
  data?: T;
  error?: string;
  timestamp: string;
  meta?: { continuationToken?: string | null; pageSize?: number };
};

export type CampaignListItem = {
  id?: string;
  name?: string;
  status?: string;
  extCampaignId?: string;
  startDate?: string;
  endDate?: string;
  events?: string[];
  journey?: Record<string, unknown>;
  [key: string]: unknown;
};

export type AgentConversationListItem = {
  conversationId: string;
};

export type PointAccountTypeListItem = Record<string, unknown>;

export type SchemaListItem = {
  id?: string;
  name?: string;
  status?: string;
  modelType?: string;
  tag?: string;
  modelMetaData?: Record<string, string>;
  attributes?: { name?: string; displayName?: string }[];
};

export type AccountPointBalance = {
  accountId?: string;
  pointAccountTypeId?: string;
  currentBalance?: number;
  lifetimeTotal?: number;
};

export type QueryDataParams = {
  schemaName: string;
  queryString?: string;
  queryArgs?: Record<string, unknown>;
  loyaltyAccountId?: string;
  pageSize?: number;
  continuationToken?: string | null;
  sortBy?: string | null;
  sortOrder?: "ASC" | "DESC";
};

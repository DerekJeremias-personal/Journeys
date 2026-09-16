export type ApiResponse<T> = {
  success: boolean;
  data?: T;
  error?: string;
  timestamp: string;
};

export type CampaignListItem = {
  id?: string;
  name?: string;
  status?: string;
  extCampaignId?: string;
};

export type SchemaListItem = {
  id?: string;
  name?: string;
  status?: string;
  modelType?: string;
  attributes?: { name?: string; displayName?: string }[];
};

export async function duplicateCampaign(args: {
  id: string;
  status: string;
  copyCampaign: (
    id: string,
    status: string
  ) => Promise<{ success: boolean; error?: string; data?: unknown }>;
  updateCampaign: (c: unknown) => Promise<unknown>;
}) {
  return args.copyCampaign(args.id, args.status);
}

export async function restoreArchived(args: {
  id: string;
  restoreCampaign: (id: string) => Promise<{ success: boolean; error?: string }>;
  updateCampaign: (c: unknown) => Promise<unknown>;
}) {
  return args.restoreCampaign(args.id);
}

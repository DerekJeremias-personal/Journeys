export function isCampaignAgentWritable(status: string | null | undefined): boolean {
  return (status ?? "").toLowerCase() === "draft";
}

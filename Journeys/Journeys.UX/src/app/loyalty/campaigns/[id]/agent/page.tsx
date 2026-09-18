import { AgentChat } from "@/components/loyalty/agent-chat";
import { normalizeCampaignStatus } from "@/lib/campaign-kebab";
import { redirect } from "next/navigation";

export default async function LiveCampaignAgentPage({
  params,
  searchParams
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ campaignStatus?: string | string[] }>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  const rawStatus =
    typeof query.campaignStatus === "string" ? query.campaignStatus : "";
  const status = normalizeCampaignStatus(rawStatus);

  if (status !== "live") {
    if (status) {
      redirect(
        `/loyalty/campaigns/${encodeURIComponent(id)}?campaignStatus=${encodeURIComponent(status)}`
      );
    }
    redirect("/loyalty/campaigns");
  }

  return (
    <>
      <h1>Agent</h1>
      <AgentChat linkedCampaignId={id} />
    </>
  );
}

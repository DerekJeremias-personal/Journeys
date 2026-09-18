import { AgentChat } from "@/components/loyalty/agent-chat";

export default async function CampaignAgentPage({
  searchParams
}: {
  searchParams: Promise<{ conversationId?: string | string[] }>;
}) {
  const query = await searchParams;
  const conversationId =
    typeof query.conversationId === "string" ? query.conversationId : null;

  return (
    <>
      <h1>Agent</h1>
      <AgentChat conversationId={conversationId} />
    </>
  );
}

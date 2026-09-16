import { getCampaigns } from "@/services/loyalty/actions";

function formatFetchError(error?: string): string {
  const e = error ?? "";
  if (/401|403|unauthorized|forbidden|nope/i.test(e)) {
    return "not authorized / check tenant or key";
  }
  return e || "Unknown error";
}

export default async function CampaignsPage() {
  const result = await getCampaigns();

  return (
    <>
      <h1>Campaigns</h1>
      {!result.success ? (
        <p className="error">{formatFetchError(result.error)}</p>
      ) : result.data!.length === 0 ? (
        <p className="empty">No campaigns found.</p>
      ) : (
        <ul>
          {result.data!.map((campaign, index) => (
            <li key={campaign.id ?? index}>
              {campaign.name} — {campaign.status} — {campaign.id}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

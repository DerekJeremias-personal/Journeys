import Link from "next/link";

export default function LoyaltyOverviewPage() {
  return (
    <>
      <h1>Loyalty</h1>
      <div className="card-row">
        <Link className="card" href="/loyalty/accounts">
          Accounts
        </Link>
        <Link className="card" href="/loyalty/campaigns">
          Campaigns
        </Link>
      </div>
    </>
  );
}

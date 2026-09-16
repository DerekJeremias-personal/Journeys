import Link from "next/link";

export function LoyaltyNav() {
  return (
    <nav className="loyalty-nav">
      <h1>Loyalty</h1>
      <Link href="/loyalty">Overview</Link>
      <Link href="/loyalty/accounts">Accounts</Link>
      <Link href="/loyalty/campaigns">Campaigns</Link>
      <span className="disabled">Promotions</span>
      <span className="disabled">Analytics</span>
      <span className="disabled">Action Log</span>
      <span className="disabled">Notifications</span>
      <span className="disabled">File Ingestion</span>
      <span className="disabled">Settings</span>
      <span className="disabled">Data Explorer</span>
      <span className="disabled">Model Builder</span>
    </nav>
  );
}

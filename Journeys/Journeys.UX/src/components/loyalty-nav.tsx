import Link from "next/link";
import { modelBuilderNavHref, modelBuilderUxBaseUrl } from "@/lib/model-builder-handoff";

export function LoyaltyNav() {
  const linkClass = "block rounded-md px-3 py-2 text-sm text-zinc-700 hover:bg-zinc-200 hover:text-zinc-950";
  const disabledClass = "block px-3 py-2 text-sm text-zinc-400";
  const modelBuilderHref = modelBuilderNavHref(modelBuilderUxBaseUrl());

  return (
    <nav className="w-60 shrink-0 border-r border-zinc-200 bg-zinc-100 p-4">
      <h1 className="mb-4 px-3 text-base font-semibold text-zinc-950">Loyalty</h1>
      <Link className={linkClass} href="/loyalty">Overview</Link>
      <Link className={linkClass} href="/loyalty/accounts">Accounts</Link>
      <Link className={linkClass} href="/loyalty/campaigns">Campaigns</Link>
      <span className={disabledClass}>Promotions</span>
      <span className={disabledClass}>Analytics</span>
      <span className={disabledClass}>Action Log</span>
      <span className={disabledClass}>Notifications</span>
      <span className={disabledClass}>File Ingestion</span>
      <span className={disabledClass}>Settings</span>
      <span className={disabledClass}>Data Explorer</span>
      {modelBuilderHref ? (
        <Link className={linkClass} href={modelBuilderHref}>
          Model Builder
        </Link>
      ) : (
        <span className={disabledClass}>Model Builder</span>
      )}
    </nav>
  );
}

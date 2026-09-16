import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { LoyaltyNav } from "@/components/loyalty-nav";

export default async function LoyaltyLayout({ children }: { children: ReactNode }) {
  const session = await auth();
  if (!session?.user) redirect("/signin");

  return (
    <div className="layout">
      <LoyaltyNav />
      <main>{children}</main>
    </div>
  );
}

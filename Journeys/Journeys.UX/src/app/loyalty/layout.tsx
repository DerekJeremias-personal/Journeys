import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { LoyaltyNav } from "@/components/loyalty-nav";

export default async function LoyaltyLayout({ children }: { children: ReactNode }) {
  const session = await auth();
  if (!session?.user) redirect("/signin");

  return (
    <div className="flex min-h-screen bg-white text-zinc-950">
      <LoyaltyNav />
      <main className="min-w-0 flex-1 p-6">{children}</main>
    </div>
  );
}

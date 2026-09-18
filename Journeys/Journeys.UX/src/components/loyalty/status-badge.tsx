import { Badge } from "@/components/ui/badge";
import { normalizeCampaignStatus } from "@/lib/campaign-kebab";
import { cn } from "@/lib/utils";

export interface StatusBadgeProps {
  status?: string;
}

const statusStyles: Record<string, string> = {
  live: "border-emerald-200 bg-emerald-50 text-emerald-700",
  draft: "border-zinc-200 bg-zinc-100 text-zinc-700",
  pause: "border-amber-200 bg-amber-50 text-amber-700",
  archive: "border-slate-200 bg-slate-100 text-slate-600"
};

export function StatusBadge({ status }: StatusBadgeProps) {
  const normalized = normalizeCampaignStatus(status) || "draft";

  return (
    <Badge variant="outline" className={cn("capitalize", statusStyles[normalized])}>
      {normalized}
    </Badge>
  );
}

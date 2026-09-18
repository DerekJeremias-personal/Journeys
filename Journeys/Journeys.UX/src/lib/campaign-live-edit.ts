export async function resolveWizardCampaign(args: {
  requested: { id: string; status: string };
  getCampaign: (id: string, status?: string) => Promise<{ success: boolean; data?: { id?: string; extCampaignId?: string; status?: string } }>;
  getDraftByExt: (ext: string) => Promise<{ success: boolean; data?: { id?: string } | null }>;
}): Promise<{ mode: "edit"; id: string; status: string } | { mode: "new-draft-same-ext"; extCampaignId: string }> {
  const status = args.requested.status.toLowerCase();
  if (status !== "live") {
    return { mode: "edit", id: args.requested.id, status };
  }
  const live = await args.getCampaign(args.requested.id, "live");
  const ext = live.data?.extCampaignId;
  if (!ext) return { mode: "edit", id: args.requested.id, status: "live" };
  const draft = await args.getDraftByExt(ext);
  if (draft.success && draft.data?.id) {
    return { mode: "edit", id: draft.data.id, status: "draft" };
  }
  return { mode: "new-draft-same-ext", extCampaignId: ext };
}

export type WizardSaveMode = "edit" | "new-draft-same-ext";

export type WizardSaveIdentityOptions = {
  extCampaignId?: string;
  sourceStatus?: string | null;
};

export function toWizardStatus(raw?: string | null): "draft" | "live" | "pause" | "archive" {
  const status = raw?.trim().toLowerCase() ?? "";
  if (status === "active") return "live";
  if (status === "paused") return "pause";
  if (status === "archived") return "archive";
  if (status === "live" || status === "draft" || status === "pause" || status === "archive") return status;
  return "draft";
}

function coerceLiveEditStatus(status?: string | null): "live" | "pause" {
  const next = status?.trim().toLowerCase();
  if (next === "pause" || next === "paused") return "pause";
  return "live";
}

export function applyWizardSaveIdentity<
  T extends { id?: string | null; extCampaignId?: string | null; status?: string; etag?: string | null }
>(payload: T, saveMode: WizardSaveMode, options?: WizardSaveIdentityOptions): T {
  if (saveMode === "new-draft-same-ext") {
    const next = { ...payload, status: "draft" as const };
    delete next.id;
    delete next.etag;
    if (options?.extCampaignId) {
      next.extCampaignId = options.extCampaignId;
    }
    return next;
  }

  if (toWizardStatus(options?.sourceStatus) === "live") {
    return { ...payload, status: coerceLiveEditStatus(payload.status) };
  }

  return payload;
}

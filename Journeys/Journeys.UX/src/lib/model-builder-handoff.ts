export function modelBuilderUxBaseUrl(): string {
  return (process.env.BACKEND_MODEL_UX_BASE_URL ?? "").trim().replace(/\/+$/, "");
}

export function modelBuilderNavHref(baseUrl: string): string | null {
  const trimmed = baseUrl.trim().replace(/\/+$/, "");
  return trimmed ? "/loyalty/models/handoff" : null;
}

export function modelBuilderSeedActionUrl(baseUrl: string): string {
  return `${baseUrl.replace(/\/+$/, "")}/api/model-builder/seed`;
}

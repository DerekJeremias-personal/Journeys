import { auth } from "@/auth";
import { redirect } from "next/navigation";
import { buildModelBuilderBrief } from "@/lib/model-builder-brief";
import { modelBuilderSeedActionUrl, modelBuilderUxBaseUrl } from "@/lib/model-builder-handoff";
import { resolveTenantId } from "@/lib/resolve-tenant-id";
import { getAllSchemas } from "@/services/loyalty/actions";

export default async function ModelBuilderHandoffPage() {
  const session = await auth();
  if (!session?.user) redirect("/signin");

  const base = modelBuilderUxBaseUrl();
  if (!base) {
    return (
      <p className="text-sm text-zinc-600">
        Model Builder is not configured. Set BACKEND_MODEL_UX_BASE_URL.
      </p>
    );
  }

  const tenantId = resolveTenantId((session as { tenantId?: string }).tenantId);
  const catalog = await getAllSchemas();
  const brief = buildModelBuilderBrief({
    tenantId,
    schemas: catalog.success ? catalog.data ?? [] : [],
    catalogUnavailable: !catalog.success
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight">Model Builder</h1>
      <p className="text-sm text-zinc-600">
        Opens Backend Modeler in a new tab for this tenant, with a one-time Journeys event-signal brief.
      </p>
      <form
        action={modelBuilderSeedActionUrl(base)}
        method="post"
        target="_blank"
        rel="noopener"
      >
        <input type="hidden" name="tenantId" value={tenantId} />
        <input type="hidden" name="source" value="journeys" />
        <input type="hidden" name="brief" value={brief} />
        <button
          type="submit"
          className="rounded-md bg-zinc-900 px-3 py-2 text-sm text-white"
        >
          Open Model Builder
        </button>
      </form>
    </div>
  );
}

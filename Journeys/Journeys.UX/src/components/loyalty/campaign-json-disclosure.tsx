"use client";

import { useEffect, useState } from "react";
import { copyText } from "@/lib/campaign-agent/copy-text";
import { getCampaign } from "@/services/loyalty/actions";

type CampaignJsonDisclosureProps = {
  campaignId: string;
};

export function CampaignJsonDisclosure({ campaignId }: CampaignJsonDisclosureProps) {
  const [json, setJson] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadCampaign() {
      setJson(null);
      setError(null);
      const draft = await getCampaign(campaignId, "draft");
      const result = draft.success && draft.data ? draft : await getCampaign(campaignId);
      if (cancelled) return;
      if (!result.success || !result.data) {
        setError(result.error ?? "Failed to load campaign JSON");
        return;
      }
      setJson(JSON.stringify(result.data, null, 2));
    }

    void loadCampaign().catch((reason: unknown) => {
      if (!cancelled) {
        setError(reason instanceof Error ? reason.message : "Failed to load campaign JSON");
      }
    });

    return () => {
      cancelled = true;
    };
  }, [campaignId]);

  async function onCopy() {
    if (!json || !(await copyText(json))) return;
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  }

  return (
    <details className="rounded-md border border-border p-3">
      <summary className="cursor-pointer font-medium">Campaign JSON</summary>
      <div className="mt-3">
        {error ? <p className="error">{error}</p> : null}
        {!error && !json ? <p className="text-sm text-muted-foreground">Loading…</p> : null}
        {json ? (
          <>
            <button type="button" className="mb-2 text-sm underline" onClick={onCopy}>
              {copied ? "Copied" : "Copy"}
            </button>
            <pre className="max-h-96 overflow-auto rounded bg-muted p-3 text-xs">{json}</pre>
          </>
        ) : null}
      </div>
    </details>
  );
}

export function mapLoyaltyPath(path: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const trimmed = path.replace(/^\/+/, "");

  if (/^campaigns\/[^/]+\/getall$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/getall`;
  }
  if (/^campaigns\/[^/]+\/getmany$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/getmany`;
  }
  if (/^campaigns\/[^/]+\/save$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/save`;
  }
  if (/^campaigns\/[^/]+\/validate$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/validate`;
  }
  if (/^campaigns\/[^/]+\/query$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}`;
  }
  if (/^campaigns\/[^/]+\/archived$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/archived`;
  }
  if (/^campaigns\/[^/]+\/pointaccounttype\/getall$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/pointaccounttype/getall`;
  }
  const versions = /^campaigns\/[^/]+\/versions\/([^/]+)$/i.exec(trimmed);
  if (versions) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/versions/${encodeURIComponent(versions[1])}`;
  }
  const live = /^campaigns\/[^/]+\/live\/([^/]+)$/i.exec(trimmed);
  if (live) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/live/${encodeURIComponent(live[1])}`;
  }
  const draft = /^campaigns\/[^/]+\/draft\/([^/]+)$/i.exec(trimmed);
  if (draft) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/draft/${encodeURIComponent(draft[1])}`;
  }
  const copy = /^campaigns\/[^/]+\/([^/]+)\/copy$/i.exec(trimmed);
  if (copy) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(copy[1])}/copy`;
  }
  const restore = /^campaigns\/[^/]+\/([^/]+)\/restore$/i.exec(trimmed);
  if (restore) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(restore[1])}/restore`;
  }
  const one = /^campaigns\/[^/]+\/([^/]+)$/i.exec(trimmed);
  if (one && !/^(getall|getmany|save|validate|archived|query)$/i.test(one[1])) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(one[1])}`;
  }
  if (/^campaign-agent\/conversations$/i.test(trimmed)) {
    return `/api/v1/${encodeURIComponent(tenant)}/campaign-agent/conversations`;
  }
  if (/^schemas\/[^/]+\/model\/all$/i.test(trimmed)) {
    return `/api/Model/${encodeURIComponent(tenant)}/GetMany`;
  }
  const events = /^events\/[^/]+\/([^/]+)\/admin\/query$/i.exec(trimmed);
  if (events) {
    return `/api/Events/${encodeURIComponent(tenant)}/${encodeURIComponent(events[1])}/admin/query`;
  }

  const enc = (s: string) => encodeURIComponent(s);

  if (/^accounts\/[^/]+\/points\/deposit$/i.test(trimmed)) {
    return `/api/Account/${enc(tenant)}/points/deposit`;
  }
  if (/^accounts\/[^/]+\/points\/withdrawal$/i.test(trimmed)) {
    return `/api/Account/${enc(tenant)}/points/withdrawal`;
  }
  const balances = /^accounts\/[^/]+\/points\/balances\/([^/]+)$/i.exec(trimmed);
  if (balances) {
    return `/api/Account/${enc(tenant)}/points/balances/${enc(balances[1])}`;
  }
  const ledgers = /^accounts\/[^/]+\/points\/([^/]+)$/i.exec(trimmed);
  if (ledgers && !/^(deposit|withdrawal|balances|admin|expire)$/i.test(ledgers[1])) {
    return `/api/Account/${enc(tenant)}/points/${enc(ledgers[1])}`;
  }
  const ext = /^accounts\/[^/]+\/ext\/([^/]+)$/i.exec(trimmed);
  if (ext) {
    return `/api/Account/${enc(tenant)}/ext/${enc(ext[1])}`;
  }
  const accountOne = /^accounts\/[^/]+\/([^/]+)$/i.exec(trimmed);
  if (accountOne && !/^(points|ext|builder)$/i.test(accountOne[1])) {
    return `/api/Account/${enc(tenant)}/${enc(accountOne[1])}`;
  }

  const enter =
    /^journey\/[^/]+\/ManuallyEnter\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
      trimmed
    );
  if (enter) {
    return `/api/Journey/${enc(tenant)}/ManuallyEnter/${enc(enter[1])}/Journey/${enc(enter[2])}/ForAccount/${enc(enter[3])}`;
  }
  const exit =
    /^journey\/[^/]+\/ManuallyExit\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
      trimmed
    );
  if (exit) {
    return `/api/Journey/${enc(tenant)}/ManuallyExit/${enc(exit[1])}/Journey/${enc(exit[2])}/ForAccount/${enc(exit[3])}`;
  }
  if (/^journey\/[^/]+\/PreviewTierMove$/i.test(trimmed)) {
    return `/api/Journey/${enc(tenant)}/PreviewTierMove`;
  }
  if (/^journey\/[^/]+\/MoveTier$/i.test(trimmed)) {
    return `/api/Journey/${enc(tenant)}/MoveTier`;
  }

  throw new Error(`not-allowlisted: ${trimmed}`);
}

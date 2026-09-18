export const LOYALTY_ACCOUNT_IDENTIFIER_QUERY =
  "c.event.id = @id OR c.id = @id OR c.event.loyaltyAccountId = @id OR c.loyaltyAccountId = @id OR c.event.profileid = @id OR c.profileid = @id OR c.event.profileId = @id OR c.profileId = @id OR c.event.customerid = @id OR c.customerid = @id OR c.event.customerId = @id OR c.customerId = @id OR c.event.extAccountId = @id OR c.extAccountId = @id";

export const LOYALTY_ACCOUNT_IDENTIFIER_SEARCH_FIELDS = [
  "id",
  "loyaltyAccountId",
  "loyaltyMemberId",
  "sourceRecordId",
  "profileid",
  "profileId",
  "customerid",
  "customerId",
  "extAccountId",
];

type LooseRecord = Record<string, unknown>;

function asLooseRecord(value: unknown): LooseRecord | null {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as LooseRecord) : null;
}

function asNonEmptyString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

export function normalizeEntityWithEvent(entity: LooseRecord | null | undefined): LooseRecord | null {
  const record = asLooseRecord(entity);
  if (!record) return null;

  const event = asLooseRecord(record["event"]);
  if (!event) return record;

  const { event: _event, ...rootFields } = record;
  void _event;

  // Preserve root-level fields while letting event payload shape drive display fields.
  // Keep document id stable for downstream APIs that key by account id.
  const documentId = asNonEmptyString(rootFields["id"]);
  const eventId = asNonEmptyString(event["id"]);
  const merged: LooseRecord = { ...rootFields, ...event };
  if (documentId) {
    merged["id"] = documentId;
    merged["_documentId"] = documentId;
  }
  if (eventId && eventId !== documentId && !asNonEmptyString(merged["eventId"])) {
    merged["eventId"] = eventId;
  }
  return merged;
}

export function resolveLoyaltyAccountId(entity: LooseRecord | null | undefined, fallbackId: string): string {
  const record = asLooseRecord(entity);
  if (!record) return fallbackId;

  const event = asLooseRecord(record["event"]);

  return (
    asNonEmptyString(record["loyaltyAccountId"]) ??
    asNonEmptyString(event?.["loyaltyAccountId"]) ??
    asNonEmptyString(record["customerid"]) ??
    asNonEmptyString(event?.["customerid"]) ??
    asNonEmptyString(record["customerId"]) ??
    asNonEmptyString(event?.["customerId"]) ??
    asNonEmptyString(record["_documentId"]) ??
    asNonEmptyString(record["id"]) ??
    asNonEmptyString(record["extAccountId"]) ??
    asNonEmptyString(event?.["extAccountId"]) ??
    asNonEmptyString(event?.["id"]) ??
    fallbackId
  );
}

export function resolveLoyaltyAccountXReference(entity: LooseRecord | null | undefined, fallbackId: string): string {
  const record = asLooseRecord(entity);
  if (!record) return fallbackId;

  const event = asLooseRecord(record["event"]);
  const loyaltyAccountId = resolveLoyaltyAccountId(record, fallbackId);

  return (
    asNonEmptyString(event?.["profileid"]) ??
    asNonEmptyString(record["profileid"]) ??
    asNonEmptyString(event?.["profileId"]) ??
    asNonEmptyString(record["profileId"]) ??
    asNonEmptyString(event?.["customerid"]) ??
    asNonEmptyString(record["customerid"]) ??
    asNonEmptyString(event?.["customerId"]) ??
    asNonEmptyString(record["customerId"]) ??
    asNonEmptyString(event?.["extAccountId"]) ??
    asNonEmptyString(record["extAccountId"]) ??
    loyaltyAccountId
  );
}

export function hydrateDecision(isDirty: boolean): "apply" | "conflict" {
  return isDirty ? "conflict" : "apply";
}

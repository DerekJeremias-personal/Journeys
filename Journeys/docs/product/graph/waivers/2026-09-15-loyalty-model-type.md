# Waiver: loyalty modelType on model/all

**Reason:** Backend `/model/all` and Journeys.UX GetMany now always send `modelType: loyalty` and omit model ids. An empty/unknown model id is invalid on that Backend contract. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `campaigns`, `backend-dll-external`.

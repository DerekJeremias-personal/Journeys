# Neo4j adapter (not implemented)

YAML in this folder is the **canon** until a Bolt client exists.

When Neo4j is added:

- Connection: `Journeys_NEO4J_URI`, `Journeys_NEO4J_USER`, `Journeys_NEO4J_PASSWORD` (or equivalent).
- Labels map 1:1 from `nodes.yaml` `label` (`Capability`, `Domain`, `Persona`, `UseCase`, `Project`, `Constraint`, `NonGoal`).
- Merge on `id` (stable). Do not create a second id scheme.
- Relationship types map 1:1 from `edges.yaml` `type`.
- `graph-impact` may switch to querying Neo4j; YAML becomes a governed export/mirror that expires and resyncs.
- Do not keep a private coding-team graph that diverges from this model.

This spec does **not** stand up Neo4j or add a NuGet Bolt driver.

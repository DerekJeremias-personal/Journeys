# Product meaning (semantic plane)

This folder is the local product brain. Agents reuse these labels and edges instead of inventing synonyms.

| Folder | Load when |
|--------|-----------|
| `taxonomies/` | You need allowed names (capabilities, domains, personas, lifecycle) |
| `ontology/` | You need what a term means *here* |
| `graph/` | You need relationships (exists? for whom? implemented as which project?) |
| `use-cases/` | You need jobs the product performs |
| `market-and-positioning.md` | You need who it is for and why now |

Canon for the graph is YAML in `graph/` until a Neo4j adapter is wired (`graph/neo4j-adapter.md`). Do not keep a second silent graph.

**Extend vs invent:** if the request maps to an existing capability node, extend it. Inventing a new capability requires a graph node + human-approved spec.

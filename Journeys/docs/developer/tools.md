# Tools (sibling folder)

`C:\Dev\Journeys\tools\` (CampaignContextAudit, tests, DumpTranscript) sits beside this solution folder. There is no second `AGENTS.md` there.

When changing tools, still follow onion references to `Journeys.*` and run those projects' `dotnet build`. Product meaning nodes are usually `campaign-agent` if you touch linked CampaignAgent sources.

`Journeys.Agent` Anthropic and MCP credentials belong in user secrets or environment variables — see [local ops](local-ops.md). Campaign Agent LLM provider switch (Anthropic vs Ollama) is [campaign-agent-llm.md](campaign-agent-llm.md). Do not commit live keys in `Journeys.Agent/appsettings.json`.

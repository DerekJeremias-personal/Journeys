# Ollama Task 3 review-fix report

## Critical
Factory wrap order is now AsIChatClient -> RequestOptions -> ConnectRetry (if ConnectRetrySeconds > 0) -> UseFunctionInvocation -> Build. Outermost stays FunctionInvoking when retry is on so CampaignAgentChatClientStackBuilder peels one FI layer and does not add a second. OpenAICompatibleConnectRetryChatClient exposes public IChatClient Inner.

## Important
IsUnreachable is true only for HttpRequestException ConnectionError or NameResolutionError; HttpRequestException message containing refused/reset/unreachable (not 4xx); SocketException; IOException with those words. 4xx StatusCode and message 401 stay false. InvalidResponse and 5xx are false.

## Tests
Added streaming ConnectionError then recovered (two inner calls, delay Zero, window 5s). Added InvalidResponse 500 GetResponseAsync: one call, exception bubbles, not Ollama unreachable. Skipped live CreateChatClient wrap-type assertion. Existing 8 tests unchanged. Isolated dotnet test was not run (Shell blocked).

## Docs
Updated docs/platform/runtime.md, architecture.md, security.md, and docs/developer/testing.md. No commit.

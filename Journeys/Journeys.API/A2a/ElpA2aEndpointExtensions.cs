using System.Text.Json;

namespace Journeys.API.A2a;

internal static class JourneysA2aEndpointExtensions
{
    private static readonly JsonDocument s_emptyParams = JsonDocument.Parse("{}");

    internal static WebApplication MapJourneysA2a(this WebApplication app)
    {
        app.MapGet("/.well-known/agent-card.json", (HttpContext http) =>
        {
            var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            var card = JourneysA2aAgentCardBuilder.Build(baseUrl);
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Json(card, A2aJsonOptions.Instance);
        }).ExcludeFromDescription();

        app.MapPost("/a2a/rpc", async (HttpContext http, JourneysA2aService a2a) =>
        {
            JsonRpcRequest? req;
            try
            {
                req = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(http.Request.Body, A2aJsonOptions.Instance, http.RequestAborted);
            }
            catch (JsonException)
            {
                var err = new JsonRpcResponse
                {
                    Id = null,
                    Error = A2aRpcErrors.Error(A2aRpcErrors.ParseError, "Invalid JSON body.")
                };
                http.Response.ContentType = "application/json";
                await http.Response.WriteAsync(JsonSerializer.Serialize(err, A2aJsonOptions.Instance), http.RequestAborted);
                return;
            }

            if (req == null)
            {
                var err = new JsonRpcResponse
                {
                    Id = null,
                    Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidRequest, "Empty request body.")
                };
                http.Response.ContentType = "application/json";
                await http.Response.WriteAsync(JsonSerializer.Serialize(err, A2aJsonOptions.Instance), http.RequestAborted);
                return;
            }

            if (req.Params.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                req.Params = s_emptyParams.RootElement.Clone();

            var version = http.Request.Headers.TryGetValue("A2A-Version", out var hv) ? hv.FirstOrDefault()
                : http.Request.Query.TryGetValue("A2A-Version", out var qv) ? qv.FirstOrDefault() : null;

            var response = await a2a.HandleAsync(req, version);
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync(JsonSerializer.Serialize(response, A2aJsonOptions.Instance), http.RequestAborted);
        }).ExcludeFromDescription();

        return app;
    }
}

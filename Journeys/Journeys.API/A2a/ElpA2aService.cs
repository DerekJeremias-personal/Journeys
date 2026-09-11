using System.Text.Json;

namespace Journeys.API.A2a;

internal sealed class JourneysA2aService
{
    private static readonly JsonDocument EmptyArgs = JsonDocument.Parse("{}");

    private readonly A2aTaskMemoryStore _tasks;
    private readonly JourneysA2aToolDispatcher _dispatcher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JourneysA2aService(
        A2aTaskMemoryStore tasks,
        JourneysA2aToolDispatcher dispatcher,
        IHttpContextAccessor httpContextAccessor)
    {
        _tasks = tasks;
        _dispatcher = dispatcher;
        _httpContextAccessor = httpContextAccessor;
    }

    private CancellationToken RequestAborted => _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    internal async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest req, string? a2aVersion)
    {
        if (req.Id == null || req.Id.Value.ValueKind == JsonValueKind.Null || req.Id.Value.ValueKind == JsonValueKind.Undefined)
            return new JsonRpcResponse { Id = null, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidRequest, "Request id is required for A2A.") };

        var id = req.Id.Value;
        var paramsElement = req.Params;

        if (!IsSupportedA2aVersion(a2aVersion))
        {
            return new JsonRpcResponse
            {
                Id = id,
                Error = A2aRpcErrors.Error(A2aRpcErrors.VersionNotSupported, $"A2A-Version '{a2aVersion}' is not supported. Use 1.0, 0.3, or omit the header.")
            };
        }

        var method = req.Method ?? "";
        try
        {
            return method switch
            {
                "SendMessage" => await HandleSendMessageAsync(id, paramsElement, RequestAborted),
                "GetTask" => HandleGetTask(id, paramsElement),
                "CancelTask" => HandleCancelTask(id, paramsElement),
                "ListTasks" => HandleListTasks(id),
                "SendStreamingMessage" or "SubscribeToTask" or "GetExtendedAgentCard" or "CreateTaskPushNotificationConfig" or "GetTaskPushNotificationConfig" or "ListTaskPushNotificationConfigs" or "DeleteTaskPushNotificationConfig" =>
                    new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.UnsupportedOperation, $"{method} is not enabled on this server. Use MCP or enable streaming/push in a future release.") },
                _ => new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.MethodNotFound, $"Method '{method}' not found.") }
            };
        }
        catch (OperationCanceledException)
        {
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InternalError, "Request canceled.") };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InternalError, ex.Message) };
        }
    }

    private static bool IsSupportedA2aVersion(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return true;
        return string.Equals(v, "1.0", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "0.3", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonRpcResponse> HandleSendMessageAsync(JsonElement id, JsonElement paramsElement, CancellationToken ct)
    {
        SendMessageRequestDto? send;
        try
        {
            send = paramsElement.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<SendMessageRequestDto>(paramsElement.GetRawText(), A2aJsonOptions.Instance)
                : null;
        }
        catch (JsonException)
        {
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "Invalid SendMessage params.") };
        }

        if (send?.Message == null || string.IsNullOrWhiteSpace(send.Message.MessageId))
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "message with messageId is required.") };

        if (send.Message.Parts.Count == 0)
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "message.parts must not be empty.") };

        if (!TryParseToolInvocation(send.Message, out var tool, out var arguments))
        {
            return new JsonRpcResponse
            {
                Id = id,
                Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "Include a message part with {\"data\":{\"tool\":\"<McpToolName>\",\"arguments\":{...}}}. See Journeys-A2A-Contract.md.")
            };
        }

        var taskId = Guid.NewGuid().ToString("N");
        var contextId = send.Message.ContextId ?? Guid.NewGuid().ToString("N");

        var working = new TaskDto
        {
            Id = taskId,
            ContextId = contextId,
            Status = new TaskStatusDto { State = "TASK_STATE_WORKING", Timestamp = DateTime.UtcNow.ToString("O") }
        };
        _tasks.Put(working);

        try
        {
            var resultJson = await _dispatcher.DispatchAsync(tool, arguments, send.Tenant, ct);
            var artifactId = Guid.NewGuid().ToString("N");
            JsonElement dataElement;
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                dataElement = doc.RootElement.Clone();
            }
            catch
            {
                dataElement = JsonSerializer.SerializeToElement(new { text = resultJson }, A2aJsonOptions.Instance);
            }

            var completed = new TaskDto
            {
                Id = taskId,
                ContextId = contextId,
                Status = new TaskStatusDto { State = "TASK_STATE_COMPLETED", Timestamp = DateTime.UtcNow.ToString("O") },
                Artifacts = new List<ArtifactDto>
                {
                    new()
                    {
                        ArtifactId = artifactId,
                        Name = "toolResult",
                        Parts = new List<PartDto>
                        {
                            new() { Data = dataElement, MediaType = "application/json" }
                        }
                    }
                }
            };
            _tasks.Put(completed);

            var response = new SendMessageResponseDto { Task = completed };
            var result = JsonSerializer.SerializeToElement(response, A2aJsonOptions.Instance);
            return new JsonRpcResponse { Id = id, Result = result };
        }
        catch (Exception ex)
        {
            var failed = new TaskDto
            {
                Id = taskId,
                ContextId = contextId,
                Status = new TaskStatusDto
                {
                    State = "TASK_STATE_FAILED",
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    Message = new MessageDto
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        Role = "ROLE_AGENT",
                        Parts = new List<PartDto> { new() { Text = ex.Message } }
                    }
                }
            };
            _tasks.Put(failed);
            var response = new SendMessageResponseDto { Task = failed };
            var result = JsonSerializer.SerializeToElement(response, A2aJsonOptions.Instance);
            return new JsonRpcResponse { Id = id, Result = result };
        }
    }

    private static bool TryParseToolInvocation(MessageDto message, out string tool, out JsonElement arguments)
    {
        tool = "";
        arguments = EmptyArgs.RootElement;
        foreach (var part in message.Parts)
        {
            if (!part.Data.HasValue || part.Data.Value.ValueKind != JsonValueKind.Object)
                continue;
            var data = part.Data.Value;
            if (!data.TryGetProperty("tool", out var toolEl) || toolEl.ValueKind != JsonValueKind.String)
                continue;
            tool = toolEl.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(tool))
                continue;
            if (data.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
                arguments = argsEl.Clone();
            else
                arguments = EmptyArgs.RootElement.Clone();
            return true;
        }

        return false;
    }

    private JsonRpcResponse HandleGetTask(JsonElement id, JsonElement paramsElement)
    {
        GetTaskParamsDto? p;
        try
        {
            p = JsonSerializer.Deserialize<GetTaskParamsDto>(paramsElement.GetRawText(), A2aJsonOptions.Instance);
        }
        catch (JsonException)
        {
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "Invalid GetTask params.") };
        }

        if (string.IsNullOrWhiteSpace(p?.Id))
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "id is required.") };

        if (!_tasks.TryGet(p.Id, out var task) || task == null)
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.TaskNotFound, "Task not found.") };

        var result = JsonSerializer.SerializeToElement(task, A2aJsonOptions.Instance);
        return new JsonRpcResponse { Id = id, Result = result };
    }

    private JsonRpcResponse HandleCancelTask(JsonElement id, JsonElement paramsElement)
    {
        CancelTaskParamsDto? p;
        try
        {
            p = JsonSerializer.Deserialize<CancelTaskParamsDto>(paramsElement.GetRawText(), A2aJsonOptions.Instance);
        }
        catch (JsonException)
        {
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "Invalid CancelTask params.") };
        }

        if (string.IsNullOrWhiteSpace(p?.Id))
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.InvalidParams, "id is required.") };

        if (!_tasks.TryGet(p.Id, out var task) || task == null)
            return new JsonRpcResponse { Id = id, Error = A2aRpcErrors.Error(A2aRpcErrors.TaskNotFound, "Task not found.") };

        if (task.Status.State is "TASK_STATE_COMPLETED" or "TASK_STATE_FAILED" or "TASK_STATE_CANCELED" or "TASK_STATE_REJECTED")
            return new JsonRpcResponse { Id = id, Result = JsonSerializer.SerializeToElement(task, A2aJsonOptions.Instance) };

        task.Status = new TaskStatusDto { State = "TASK_STATE_CANCELED", Timestamp = DateTime.UtcNow.ToString("O") };
        _tasks.Put(task);
        return new JsonRpcResponse { Id = id, Result = JsonSerializer.SerializeToElement(task, A2aJsonOptions.Instance) };
    }

    private JsonRpcResponse HandleListTasks(JsonElement id)
    {
        var result = JsonSerializer.SerializeToElement(new { tasks = Array.Empty<TaskDto>(), nextPageToken = (string?)null }, A2aJsonOptions.Instance);
        return new JsonRpcResponse { Id = id, Result = result };
    }
}

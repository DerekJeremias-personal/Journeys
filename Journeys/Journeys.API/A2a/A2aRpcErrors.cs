using System.Text.Json;

namespace Journeys.API.A2a;

internal static class A2aRpcErrors
{
    internal const int ParseError = -32700;
    internal const int InvalidRequest = -32600;
    internal const int MethodNotFound = -32601;
    internal const int InvalidParams = -32602;
    internal const int InternalError = -32603;
    internal const int TaskNotFound = -32001;
    internal const int UnsupportedOperation = -32004;
    internal const int VersionNotSupported = -32009;

    internal static JsonRpcError Error(int code, string message, JsonElement? data = null) =>
        new() { Code = code, Message = message, Data = data };
}

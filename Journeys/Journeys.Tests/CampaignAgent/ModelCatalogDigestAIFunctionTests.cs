using System.Text.Json;
using Journeys.API.CampaignAgent;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.Tests.CampaignAgent;

public sealed class ModelCatalogDigestAIFunctionTests
{
    private const string CatalogJson =
        """
        {
          "models": [
            { "id": "m-order", "name": "Order", "modelType": "Custom", "tag": "eventable", "isContainer": false, "attributes": [ { "name": "ordertotal", "type": "number" } ] }
          ]
        }
        """;

    [Fact]
    public async Task Invoke_digests_result_and_delegates_metadata()
    {
        var inner = new FakeStringFunction(CatalogJson);
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: null, enabled: true, logger: null);

        Assert.Equal("get_all_models", fn.Name);
        Assert.Equal("lists models", fn.Description);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        var json = Assert.IsType<string>(result);
        using var doc = JsonDocument.Parse(json);
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("total").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("models").GetArrayLength());
    }

    [Fact]
    public async Task Invoke_injects_forced_tag_when_missing()
    {
        var inner = new TagCapturingFunction(CatalogJson);
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: "eventable", enabled: true, logger: null);

        await fn.InvokeAsync(new AIFunctionArguments { ["tenantId"] = "t1" }, CancellationToken.None);

        Assert.Equal("eventable", inner.CapturedTag);
    }

    [Fact]
    public async Task Invoke_does_not_override_existing_tag()
    {
        var inner = new TagCapturingFunction(CatalogJson);
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: "eventable", enabled: true, logger: null);

        await fn.InvokeAsync(new AIFunctionArguments { ["tag"] = "custom" }, CancellationToken.None);

        Assert.Equal("custom", inner.CapturedTag);
    }

    [Fact]
    public async Task Invoke_passthrough_when_disabled()
    {
        var inner = new FakeStringFunction(CatalogJson);
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: null, enabled: false, logger: null);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        Assert.Equal(CatalogJson, result);
    }

    [Fact]
    public async Task Invoke_rewraps_text_content_result()
    {
        var inner = new FakeTextContentFunction(CatalogJson);
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: null, enabled: true, logger: null);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        var content = Assert.IsType<TextContent>(result);
        using var doc = JsonDocument.Parse(content.Text!);
        Assert.Equal(1, doc.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Invoke_digests_json_element_result()
    {
        var inner = new FakeObjectFunction(JsonDocument.Parse(CatalogJson).RootElement.Clone());
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: null, enabled: true, logger: null);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        var json = Assert.IsType<string>(result);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Invoke_passthrough_when_result_not_extractable()
    {
        var notExtractable = new object();
        var inner = new FakeObjectFunction(notExtractable);
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: null, enabled: true, logger: null);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        Assert.Same(notExtractable, result);
    }

    [Fact]
    public async Task Invoke_warns_and_passes_through_when_shape_unrecognized()
    {
        const string unrecognized = "{\"unexpected\":true}";
        var inner = new FakeStringFunction(unrecognized);
        var logger = new CapturingLogger();
        var fn = new ModelCatalogDigestAIFunction(inner, forcedTag: null, enabled: true, logger: logger);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        Assert.Equal(unrecognized, result);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Levels.Add(logLevel);
    }

    private sealed class TagCapturingFunction : AIFunction
    {
        public string? CapturedTag { get; private set; }
        private readonly string _result;
        public TagCapturingFunction(string result) => _result = result;
        public override string Name => "get_all_models";
        public override string Description => "lists models";
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            CapturedTag = arguments.TryGetValue("tag", out var v) ? v?.ToString() : null;
            return ValueTask.FromResult<object?>(_result);
        }
    }

    private sealed class FakeStringFunction : AIFunction
    {
        private readonly string _result;
        public FakeStringFunction(string result) => _result = result;
        public override string Name => "get_all_models";
        public override string Description => "lists models";
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>(_result);
    }

    private sealed class FakeTextContentFunction : AIFunction
    {
        private readonly string _result;
        public FakeTextContentFunction(string result) => _result = result;
        public override string Name => "list_models";
        public override string Description => "lists models";
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>(new TextContent(_result));
    }

    private sealed class FakeObjectFunction : AIFunction
    {
        private readonly object? _result;
        public FakeObjectFunction(object? result) => _result = result;
        public override string Name => "get_all_models";
        public override string Description => "lists models";
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult(_result);
    }
}

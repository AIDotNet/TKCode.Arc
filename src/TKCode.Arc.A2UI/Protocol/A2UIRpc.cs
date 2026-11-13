using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>
/// Function calls across the wire: renderer to agent (<c>callAgentFunction</c>, answered by
/// <c>agentFunctionResponse</c>) and agent to renderer (<c>callRendererFunction</c>, answered by
/// <c>rendererFunctionResponse</c>).
/// </summary>
internal sealed class A2UIRpc : IDisposable
{
    private const int CacheSize = 256;

    private readonly A2UIRenderer _renderer;
    private readonly Dictionary<string, Pending> _byCallId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Pending> _byKey = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, JsonNode? Value)> _settled = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, JsonNode? Value)>> _settledIndex = new(StringComparer.Ordinal);
    private long _nextId;
    private bool _disposed;

    public A2UIRpc(A2UIRenderer renderer)
    {
        _renderer = renderer;
    }

    public int PendingCount => _byCallId.Count;

    /// <summary>
    /// The value of an agent-side function for a binding or check: the settled answer when there is one, else null
    /// (pending; the call is sent once and the binding re-evaluates when the answer arrives). Actions always call anew.
    /// </summary>
    public JsonNode? GetAgentValue(A2UISurface surface, A2UIFunctionDefinition definition, A2UICatalog catalog,
                                   IReadOnlyDictionary<string, JsonNode?> args, A2UIEvaluation ev)
    {
        var argsObject = new JsonObject();
        foreach (var (key, value) in args.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            argsObject[key] = value?.DeepClone();
        }

        if (ev.UserActivation)
        {
            Send(surface.Id, definition.Name, catalog, argsObject, cacheKey: null, completion: null);
            return null;
        }

        var cacheKey = surface.Id + "|" + catalog.Id + "|" + definition.Name + "|" + A2UIJson.ToJson(argsObject);
        if (_settledIndex.TryGetValue(cacheKey, out var settled))
        {
            _settled.Remove(settled);
            _settled.AddFirst(settled);
            return settled.Value.Value;
        }

        ev.PendingCalls.Add(cacheKey);
        if (!_byKey.ContainsKey(cacheKey))
        {
            Send(surface.Id, definition.Name, catalog, argsObject, cacheKey, completion: null);
        }

        return null;
    }

    /// <summary>Calls an agent function from code and waits for the answer.</summary>
    public Task<JsonNode?> CallAsync(string surfaceId, string name, A2UICatalog? catalog, JsonObject? args, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Send(surfaceId, name, catalog, args ?? new JsonObject(), cacheKey: null, completion);
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => _renderer.Post(() => Cancel(pending, notify: true)));
        }

        return completion.Task;
    }

    private Pending Send(string surfaceId, string name, A2UICatalog? catalog, JsonObject args, string? cacheKey, TaskCompletionSource<JsonNode?>? completion)
    {
        if (_disposed)
        {
            throw A2UIException.State("The renderer is disposed.");
        }

        var callId = "r-" + (++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var pending = new Pending(callId, surfaceId, cacheKey, completion);
        _byCallId[callId] = pending;
        if (cacheKey is not null)
        {
            _byKey[cacheKey] = pending;
        }

        var call = new JsonObject { ["@call"] = name };
        if (catalog is not null)
        {
            call["catalogId"] = catalog.Id;
        }

        call["args"] = args;
        _renderer.SendOutbound(A2UIOutboundKind.CallAgentFunction, surfaceId, new JsonObject
        {
            ["version"] = "1.0",
            ["callAgentFunction"] = new JsonObject { ["surfaceId"] = surfaceId, ["functionCallId"] = callId, ["callFunction"] = call }
        });

        var timeout = _renderer.Options.FunctionTimeout;
        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            pending.Timer = new Timer(_ => _renderer.Post(() => TimeOut(pending)), null, timeout, Timeout.InfiniteTimeSpan);
        }

        return pending;
    }

    private void TimeOut(Pending pending)
    {
        if (!_byCallId.ContainsKey(pending.CallId))
        {
            return;
        }

        Settle(pending, null, new A2UIException(A2UIErrorCategory.Expression, A2UIErrorCodes.ExpressionError,
                                                 $"Agent function call '{pending.CallId}' timed out.", functionCallId: pending.CallId));
    }

    /// <summary>Handles <c>agentFunctionResponse</c>. Unknown or late call ids are ignored.</summary>
    public void HandleAgentResponse(JsonObject response)
    {
        var callId = A2UIJson.AsString(response["functionCallId"]) ?? throw A2UIException.Validation("'functionCallId' must be a string.");
        var hasValue = response.ContainsKey("value");
        var hasError = response.ContainsKey("error");
        if (hasValue == hasError)
        {
            throw A2UIException.Validation("'agentFunctionResponse' needs exactly one of 'value' and 'error'.");
        }

        if (!_byCallId.TryGetValue(callId, out var pending))
        {
            return;
        }

        if (hasError)
        {
            var error = response["error"] as JsonObject;
            var message = A2UIJson.AsString(error?["message"]) ?? "The agent function failed.";
            Settle(pending, null, new A2UIException(A2UIErrorCategory.Expression, A2UIErrorCodes.ExpressionError,
                                                     message, functionCallId: callId));
        }
        else
        {
            Settle(pending, response["value"]?.DeepClone(), null);
        }
    }

    private void Settle(Pending pending, JsonNode? value, A2UIException? error)
    {
        _byCallId.Remove(pending.CallId);
        pending.Timer?.Dispose();
        if (pending.CacheKey is { } key)
        {
            _byKey.Remove(key);
            Cache(key, value);
        }

        if (error is not null)
        {
            _renderer.ReportError(error, pending.SurfaceId);
            pending.Completion?.TrySetException(error);
        }
        else
        {
            pending.Completion?.TrySetResult(value);
        }

        if (pending.CacheKey is { } settledKey && _renderer.GetSurface(pending.SurfaceId) is { } surface)
        {
            surface.Nodes.OnRpcSettled(settledKey);
        }
    }

    private void Cache(string key, JsonNode? value)
    {
        if (_settledIndex.Remove(key, out var old))
        {
            _settled.Remove(old);
        }

        _settledIndex[key] = _settled.AddFirst((key, value));
        while (_settled.Count > CacheSize)
        {
            var last = _settled.Last!;
            _settledIndex.Remove(last.Value.Key);
            _settled.RemoveLast();
        }
    }

    /// <summary>Cancels the calls of one surface without reporting them.</summary>
    public void CancelSurface(string surfaceId)
    {
        foreach (var pending in _byCallId.Values.Where(p => p.SurfaceId == surfaceId).ToList())
        {
            Cancel(pending, notify: false);
        }

        foreach (var key in _settledIndex.Keys.Where(k => k.StartsWith(surfaceId + "|", StringComparison.Ordinal)).ToList())
        {
            _settled.Remove(_settledIndex[key]);
            _settledIndex.Remove(key);
        }
    }

    private void Cancel(Pending pending, bool notify)
    {
        if (!_byCallId.Remove(pending.CallId))
        {
            return;
        }

        pending.Timer?.Dispose();
        if (pending.CacheKey is { } key)
        {
            _byKey.Remove(key);
        }

        if (notify)
        {
            pending.Completion?.TrySetCanceled();
        }
        else
        {
            pending.Completion?.TrySetException(new A2UIException(A2UIErrorCategory.State, "CANCELLED", "The call was cancelled.", pending.SurfaceId));
        }
    }

    /// <summary>
    /// Handles <c>callRendererFunction</c> and returns the <c>rendererFunctionResponse</c> object (value or error).
    /// </summary>
    public JsonObject HandleRendererCall(JsonObject message)
    {
        var callId = A2UIJson.AsString(message["functionCallId"]) ?? throw A2UIException.Validation("'functionCallId' must be a string.");
        A2UIException Structural(string text) => new(A2UIErrorCategory.Validation, A2UIErrorCodes.InvalidFunctionCall, text, functionCallId: callId);
        if (message["callFunction"] is not JsonObject callFunction)
        {
            throw Structural("'callFunction' is missing or not an object.");
        }

        if (callFunction.TryGetPropertyValue("args", out var argsNode) && argsNode is not JsonObject)
        {
            throw Structural("'args' must be an object.");
        }

        if (string.IsNullOrEmpty(A2UIJson.AsString(callFunction["catalogId"])))
        {
            throw Structural("'callRendererFunction' needs a 'catalogId' in callFunction.");
        }

        var response = new JsonObject { ["functionCallId"] = callId };
        try
        {
            if (message["callFunction"] is not JsonObject call)
            {
                throw Invalid("'callFunction' must be an object.");
            }

            var name = A2UIJson.AsString(call["@call"]);
            if (string.IsNullOrEmpty(name))
            {
                throw Invalid("'@call' must be a non-empty string.");
            }

            var catalogId = A2UIJson.AsString(call["catalogId"]);
            if (string.IsNullOrEmpty(catalogId))
            {
                throw Invalid("'callRendererFunction' needs a 'catalogId'.");
            }

            A2UISurface? surface = null;
            if (A2UIJson.AsString(message["surfaceId"]) is { } surfaceId)
            {
                surface = _renderer.GetSurface(surfaceId) ?? throw Invalid($"Unknown surface '{surfaceId}'.");
            }

            var catalog = (surface?.AvailableCatalogs ?? _renderer.Catalogs).FirstOrDefault(c => c.Id == catalogId)
                          ?? throw Invalid($"Unknown catalog '{catalogId}'.");
            if (!catalog.Functions.TryGetValue(name, out var definition))
            {
                throw Invalid($"Unknown function '{name}'.");
            }

            if (definition.AllowedCallers == A2UIAllowedCallers.RendererOnly)
            {
                throw Invalid($"Function '{name}' may not be called by the agent.");
            }

            if (definition.RequiresUserActivation)
            {
                throw Invalid($"Function '{name}' requires a user gesture.");
            }

            if (definition.Implementation is null)
            {
                throw Invalid($"Function '{name}' has no renderer implementation.");
            }

            var scope = new A2UIScope(surface, surface?.DataModel ?? new A2UIDataModel(), "/");
            var ev = new A2UIEvaluation { CalledByAgent = true };
            var value = _renderer.Evaluator.Invoke(definition, call["args"] as JsonObject, scope, ev, catalog);
            response["value"] = value?.DeepClone();
        }
        catch (A2UIException ex)
        {
            response["error"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message };
        }

        return response;

        static A2UIException Invalid(string text) => new(A2UIErrorCategory.Expression, A2UIErrorCodes.InvalidFunctionCall, text);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var pending in _byCallId.Values.ToList())
        {
            Cancel(pending, notify: false);
        }
    }

    private sealed class Pending(string callId, string surfaceId, string? cacheKey, TaskCompletionSource<JsonNode?>? completion)
    {
        public string CallId { get; } = callId;
        public string SurfaceId { get; } = surfaceId;
        public string? CacheKey { get; } = cacheKey;
        public TaskCompletionSource<JsonNode?>? Completion { get; } = completion;
        public Timer? Timer { get; set; }
    }
}

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>
/// The A2UI 1.0 renderer state: reads agent messages, keeps the surfaces, and produces the messages for the agent
/// (<see cref="Outbound"/>). It is not tied to any UI: <c>A2UISurfaceView</c> draws its surfaces with Arc controls.
/// Use it from one thread (the UI thread); <see cref="ProcessAsync(Stream, CancellationToken)"/> reads in the
/// background and applies each message on the thread that created the renderer.
/// </summary>
public sealed class A2UIRenderer : IDisposable
{
    private static readonly string[] s_messageKeys =
        ["createSurface", "updateComponents", "updateDataModel", "deleteSurface", "callRendererFunction", "agentFunctionResponse"];

    private readonly Dictionary<string, A2UISurface> _surfaces = new(StringComparer.Ordinal);
    private readonly List<A2UISurface> _order = new();
    private readonly A2UIMessageValidator _validator;
    private readonly SynchronizationContext? _context;
    private bool _disposed;
    private int _activationDepth;

    public A2UIRenderer(A2UIRendererOptions? options = null)
    {
        Options = options ?? new A2UIRendererOptions();
        _context = SynchronizationContext.Current;
        Evaluator = new A2UIEvaluator(this, Options.Culture ?? CultureInfo.CurrentCulture);
        Evaluator.FallbackCatalogs = Catalogs;
        Rpc = new A2UIRpc(this);
        _validator = new A2UIMessageValidator(this);
    }

    /// <summary>The settings.</summary>
    public A2UIRendererOptions Options { get; }

    /// <summary>The supported catalogs.</summary>
    public IReadOnlyList<A2UICatalog> Catalogs => (IReadOnlyList<A2UICatalog>)(Options.Catalogs as List<A2UICatalog> ?? Options.Catalogs.ToList());

    /// <summary>The live surfaces, in creation order.</summary>
    public IReadOnlyList<A2UISurface> Surfaces => _order;

    internal A2UIEvaluator Evaluator { get; }

    internal A2UIRpc Rpc { get; }

    /// <summary>Raised for every message to the agent: actions, function calls and answers, errors.</summary>
    public event EventHandler<A2UIOutboundEventArgs>? Outbound;

    /// <summary>Raised for every error the renderer finds (also sent to the agent as an <c>error</c> message).</summary>
    public event EventHandler<A2UIErrorEventArgs>? ErrorReported;

    /// <summary>Raised before a URL is opened (<c>openUrl</c>, links); the host opens it unless cancelled.</summary>
    public event EventHandler<A2UIUrlOpeningEventArgs>? UrlOpening;

    /// <summary>Raised when a surface is created.</summary>
    public event EventHandler<A2UISurfaceEventArgs>? SurfaceCreated;

    /// <summary>Raised when a surface is deleted.</summary>
    public event EventHandler<A2UISurfaceEventArgs>? SurfaceDeleted;

    /// <summary>The surface with the id, or null.</summary>
    public A2UISurface? GetSurface(string surfaceId) => _surfaces.TryGetValue(surfaceId, out var s) ? s : null;

    // ---- Inbound ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Applies one envelope (JSON text). A message that is invalid is reported (<see cref="ErrorReported"/> and an
    /// outbound <c>error</c>) and leaves the surfaces as they were. Returns false in that case.
    /// </summary>
    public bool Process(string envelopeJson)
    {
        if (Encoding.UTF8.GetByteCount(envelopeJson) > Options.Limits.MaxEnvelopeBytes)
        {
            ReportError(new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.LimitExceeded,
                                          $"The message is larger than {Options.Limits.MaxEnvelopeBytes} bytes."), null);
            return false;
        }

        try
        {
            Apply(A2UIJson.Parse(envelopeJson));
            return true;
        }
        catch (A2UIException ex)
        {
            ReportError(ex, ex.SurfaceId);
            return false;
        }
    }

    /// <summary>Applies one envelope given as UTF-8 JSON.</summary>
    public bool Process(ReadOnlySpan<byte> utf8Envelope)
    {
        if (utf8Envelope.Length > Options.Limits.MaxEnvelopeBytes)
        {
            ReportError(new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.LimitExceeded,
                                          $"The message is larger than {Options.Limits.MaxEnvelopeBytes} bytes."), null);
            return false;
        }

        try
        {
            Apply(A2UIJson.Parse(utf8Envelope));
            return true;
        }
        catch (A2UIException ex)
        {
            ReportError(ex, ex.SurfaceId);
            return false;
        }
    }

    /// <summary>Applies JSON Lines: one envelope per non-empty line. Returns the number of lines that failed.</summary>
    public int ProcessJsonl(string jsonl)
    {
        var failed = 0;
        foreach (var line in jsonl.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !Process(trimmed))
            {
                failed++;
            }
        }

        return failed;
    }

    /// <summary>Reads JSON Lines from a stream and applies each envelope on the renderer's thread.</summary>
    public async Task ProcessAsync(Stream jsonl, CancellationToken cancellationToken = default)
    {
        await ProcessAsync(A2UIJsonl.ReadLinesAsync(jsonl, Options.Limits.MaxEnvelopeBytes, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies envelopes as they arrive, each on the renderer's thread.</summary>
    public async Task ProcessAsync(IAsyncEnumerable<string> envelopes, CancellationToken cancellationToken = default)
    {
        await foreach (var envelope in envelopes.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            await InvokeAsync(() => Process(envelope)).ConfigureAwait(false);
        }
    }

    /// <summary>Applies one parsed envelope (or a list / <c>{"messages": [...]}</c> wrapper). Throws on invalid input.</summary>
    internal void Apply(JsonNode? message)
    {
        ThrowIfDisposed();
        switch (message)
        {
            case JsonArray list:
                foreach (var item in list)
                {
                    Apply(item);
                }

                return;
            case JsonObject wrapper when wrapper.Count == 1 && wrapper["messages"] is JsonArray messages:
                foreach (var item in messages)
                {
                    Apply(item);
                }

                return;
            case JsonObject envelope:
                ApplyEnvelope(envelope);
                return;
            default:
                throw A2UIException.Validation("A message must be a JSON object.");
        }
    }

    private void ApplyEnvelope(JsonObject envelope)
    {
        if (!envelope.TryGetPropertyValue("version", out var versionNode))
        {
            throw A2UIException.Validation("Missing required version field in the message.");
        }

        var version = A2UIJson.AsString(versionNode);
        if (version != "1.0")
        {
            throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.UnsupportedVersion,
                                    $"Unsupported protocol version '{version ?? A2UIJson.ToJson(versionNode)}'; this renderer reads 1.0.");
        }

        string? key = null;
        foreach (var (name, _) in envelope)
        {
            if (name == "version")
            {
                continue;
            }

            if (!s_messageKeys.Contains(name))
            {
                throw A2UIException.Validation(name is "beginRendering" or "surfaceUpdate" or "dataModelUpdate"
                    ? $"The action '{name}' is not supported in protocol version 1.0."
                    : $"Unknown message type '{name}'.");
            }

            if (key is not null)
            {
                throw A2UIException.Validation($"Message contains multiple conflicting update actions ('{key}' and '{name}').");
            }

            key = name;
        }

        if (key is null || envelope[key] is not JsonObject body)
        {
            throw A2UIException.Validation("The message carries no message object.");
        }

        switch (key)
        {
            case "createSurface":
                CreateSurface(body);
                break;
            case "updateComponents":
                UpdateComponents(body);
                break;
            case "updateDataModel":
                UpdateDataModel(body);
                break;
            case "deleteSurface":
                DeleteSurface(body);
                break;
            case "callRendererFunction":
                var response = Rpc.HandleRendererCall(body);
                SendOutbound(A2UIOutboundKind.RendererFunctionResponse, A2UIJson.AsString(body["surfaceId"]),
                             new JsonObject { ["version"] = "1.0", ["rendererFunctionResponse"] = response });
                break;
            case "agentFunctionResponse":
                Rpc.HandleAgentResponse(body);
                break;
        }
    }

    private static string SurfaceIdOf(JsonObject body, string message)
    {
        if (!body.TryGetPropertyValue("surfaceId", out var node) || A2UIJson.AsString(node) is not { Length: > 0 } id)
        {
            throw A2UIException.Validation($"surfaceId must be a string ('{message}').", path: "/surfaceId");
        }

        return id;
    }

    private void CreateSurface(JsonObject body)
    {
        var id = SurfaceIdOf(body, "createSurface");
        foreach (var (key, _) in body)
        {
            if (key is not ("surfaceId" or "catalogId" or "sendDataModel" or "components" or "dataModel" or "metadata"))
            {
                throw A2UIException.Validation($"'createSurface' has no property '{key}'" + (key == "theme" ? " (theme was removed in 1.0)." : "."),
                                               id, "/createSurface/" + A2UIJsonPointer.Escape(key));
            }
        }

        if (_surfaces.ContainsKey(id))
        {
            throw A2UIException.Integrity($"Surface '{id}' already exists.", id, "/createSurface/surfaceId");
        }

        if (_surfaces.Count >= Options.Limits.MaxSurfaces)
        {
            throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.LimitExceeded, $"More than {Options.Limits.MaxSurfaces} surfaces.", id);
        }

        A2UICatalog? catalog = null;
        if (body.TryGetPropertyValue("catalogId", out var catalogNode))
        {
            var catalogId = A2UIJson.AsString(catalogNode) ?? throw A2UIException.Validation("'catalogId' must be a string.", id, "/createSurface/catalogId");
            catalog = Catalogs.FirstOrDefault(c => c.Id == catalogId)
                      ?? throw A2UIException.Catalog($"Catalog not found: {catalogId}", id, "/catalogId");
            if (catalog.ProtocolVersion != "1.0")
            {
                throw A2UIException.Catalog($"Catalog '{catalogId}' is for protocol version {catalog.ProtocolVersion}, not 1.0.", id, "/catalogId");
            }
        }

        var sendDataModel = false;
        if (body.TryGetPropertyValue("sendDataModel", out var sendNode))
        {
            sendDataModel = A2UIJson.AsBoolean(sendNode) ?? throw A2UIException.Validation("'sendDataModel' must be a boolean.", id, "/createSurface/sendDataModel");
        }

        JsonObject? metadata = null;
        if (body.TryGetPropertyValue("metadata", out var metadataNode))
        {
            metadata = metadataNode as JsonObject ?? throw A2UIException.Validation("'metadata' must be an object.", id, "/createSurface/metadata");
            A2UIMessageValidator.CheckMetadata(metadata, id, "/createSurface/metadata");
            metadata = (JsonObject)metadata.DeepClone();
        }

        var available = Catalogs.Where(c => c.ProtocolVersion == "1.0").ToList();
        var surface = new A2UISurface(this, id, catalog, available, sendDataModel, metadata);

        List<A2UIComponentModel>? models = null;
        if (body["components"] is { } componentsNode)
        {
            if (componentsNode is not JsonArray components || components.Count == 0)
            {
                throw A2UIException.Validation("'components' must be a non-empty list.", id, "/createSurface/components");
            }

            models = ReadBatch(surface, components, "/createSurface/components");
        }

        _surfaces[id] = surface;
        _order.Add(surface);
        if (body["dataModel"] is { } data)
        {
            if (data is JsonObject dataObject)
            {
                foreach (var (key, value) in dataObject)
                {
                    surface.DataModel.Set("/" + A2UIJsonPointer.Escape(key), value);
                }
            }
            else
            {
                surface.DataModel.Set("/", data);
            }
        }

        if (models is not null)
        {
            surface.ApplyComponents(models);
        }

        SurfaceCreated?.Invoke(this, new A2UISurfaceEventArgs(surface));
    }

    private List<A2UIComponentModel> ReadBatch(A2UISurface surface, JsonArray components, string path)
    {
        var strict = Options.ValidationMode == A2UIValidationMode.Strict;
        var models = _validator.ReadComponents(surface, components, path, strict);
        if (surface.Components.Count + models.Count(m => !surface.Components.ContainsKey(m.Id)) > Options.Limits.MaxComponentsPerSurface)
        {
            throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.LimitExceeded,
                                    $"Surface '{surface.Id}' would hold more than {Options.Limits.MaxComponentsPerSurface} components.", surface.Id);
        }

        _validator.CheckGraph(surface, models, path, strict, Options.Limits.MaxDepth);
        return models;
    }

    private void UpdateComponents(JsonObject body)
    {
        var id = SurfaceIdOf(body, "updateComponents");
        var surface = GetSurface(id) ?? throw A2UIException.Integrity($"Surface not found for message: {id}", id, "/updateComponents/surfaceId");
        if (body["components"] is not JsonArray components || components.Count == 0)
        {
            throw A2UIException.Validation("'components' must be a non-empty list.", id, "/updateComponents/components");
        }

        surface.ApplyComponents(ReadBatch(surface, components, "/updateComponents/components"));
    }

    private void UpdateDataModel(JsonObject body)
    {
        var id = SurfaceIdOf(body, "updateDataModel");
        var surface = GetSurface(id) ?? throw A2UIException.Integrity($"Surface not found for message: {id}", id, "/updateDataModel/surfaceId");
        var path = "/";
        if (body.TryGetPropertyValue("path", out var pathNode))
        {
            path = A2UIJson.AsString(pathNode) ?? throw A2UIException.Validation("'path' must be a string.", id, "/path");
            if (path.Length > 0 && path[0] != '/')
            {
                throw A2UIException.Validation($"Invalid path '{path}': a data model path is a JSON Pointer starting with '/'.", id, "/path");
            }
        }

        if (!body.TryGetPropertyValue("value", out var value))
        {
            throw A2UIException.Validation("'updateDataModel' needs a 'value' (null deletes).", id, "/value");
        }

        if (A2UIMessageValidator.Depth(value) > A2UIMessageValidator.MaxJsonDepth)
        {
            throw new A2UIException(A2UIErrorCategory.Recursion, A2UIErrorCodes.ValidationFailed,
                                    $"Global recursion limit exceeded: the value nests deeper than {A2UIMessageValidator.MaxJsonDepth}.", id, "/value");
        }

        try
        {
            if (value is null)
            {
                surface.DataModel.Delete(path);
            }
            else
            {
                surface.DataModel.Set(path, value);
            }
        }
        catch (A2UIException ex) when (ex.SurfaceId is null)
        {
            throw new A2UIException(ex.Category, ex.Code, ex.Message, id, "/path", innerException: ex);
        }
    }

    private void DeleteSurface(JsonObject body)
    {
        var id = SurfaceIdOf(body, "deleteSurface");
        if (!_surfaces.Remove(id, out var surface))
        {
            return;
        }

        _order.Remove(surface);
        Rpc.CancelSurface(id);
        surface.Delete();
        SurfaceDeleted?.Invoke(this, new A2UISurfaceEventArgs(surface));
    }

    // ---- Outbound -----------------------------------------------------------------------------------------------

    internal void SendOutbound(A2UIOutboundKind kind, string? surfaceId, JsonObject envelope)
        => Outbound?.Invoke(this, new A2UIOutboundEventArgs(kind, surfaceId, envelope));

    /// <summary>Reports an error: raises <see cref="ErrorReported"/> and sends an <c>error</c> message to the agent.</summary>
    internal void ReportError(A2UIException error, string? surfaceId)
    {
        surfaceId ??= error.SurfaceId;
        ErrorReported?.Invoke(this, new A2UIErrorEventArgs(error, surfaceId));
        var body = new JsonObject { ["code"] = error.Code };
        if (A2UIErrorCodes.IsValidationShape(error.Code))
        {
            body["surfaceId"] = surfaceId ?? string.Empty;
            body["path"] = error.Path ?? "/";
        }
        else if (error.FunctionCallId is { } callId)
        {
            body["functionCallId"] = callId;
        }
        else
        {
            body["surfaceId"] = surfaceId ?? string.Empty;
        }

        body["message"] = error.Message;
        SendOutbound(A2UIOutboundKind.Error, surfaceId, new JsonObject { ["version"] = "1.0", ["error"] = body });
    }

    /// <summary>
    /// The renderer's capabilities for transport metadata (<c>a2uiRendererCapabilities</c>):
    /// <c>{"1.0": {"supportedCatalogIds": [...], "inlineCatalogs": [...]?}}</c>.
    /// </summary>
    public JsonObject GetRendererCapabilities(bool includeInlineCatalogs = false)
    {
        var ids = new JsonArray();
        foreach (var catalog in Catalogs.Where(c => c.ProtocolVersion == "1.0"))
        {
            ids.Add((JsonNode?)(JsonValue.Create(catalog.Id)));
        }

        var v1 = new JsonObject { ["supportedCatalogIds"] = ids };
        if (includeInlineCatalogs)
        {
            var inline = new JsonArray();
            foreach (var catalog in Catalogs.Where(c => c.ProtocolVersion == "1.0"))
            {
                inline.Add(catalog.Document.DeepClone());
            }

            v1["inlineCatalogs"] = inline;
        }

        return new JsonObject { ["1.0"] = v1 };
    }

    /// <summary>
    /// The data models of the surfaces created with <c>sendDataModel</c>, for transport metadata
    /// (<c>a2uiRendererDataModel</c>): <c>{"version": "1.0", "surfaces": {id: data}}</c>; null when there are none.
    /// </summary>
    public JsonObject? GetRendererDataModel()
    {
        var surfaces = new JsonObject();
        foreach (var surface in _order.Where(s => s.SendDataModel))
        {
            surfaces[surface.Id] = surface.DataModel.Get("/");
        }

        return surfaces.Count == 0 ? null : new JsonObject { ["version"] = "1.0", ["surfaces"] = surfaces };
    }

    /// <summary>Calls an agent-side function and waits for its answer (<c>callAgentFunction</c>).</summary>
    public async Task<JsonNode?> CallAgentFunctionAsync(string surfaceId, string name, JsonObject? args = null, string? catalogId = null,
                                                       CancellationToken cancellationToken = default)
    {
        var catalog = catalogId is null ? GetSurface(surfaceId)?.DefaultCatalog : Catalogs.FirstOrDefault(c => c.Id == catalogId);
        return await Rpc.CallAsync(surfaceId, name, catalog, args, cancellationToken).ConfigureAwait(false);
    }

    // ---- Actions and writes -------------------------------------------------------------------------------------

    internal bool DispatchAction(A2UISurface surface, A2UINode node, string property, bool userActivation)
    {
        if (node.IsDisposed || surface.IsDeleted || node.GetRaw(property) is not JsonObject action)
        {
            return false;
        }

        if (!node.IsValid)
        {
            return false;
        }

        var scope = new A2UIScope(surface, surface.DataModel, node.DataPath, node.Index ?? IndexOf(node));
        var ev = new A2UIEvaluation { UserActivation = userActivation };
        _activationDepth += userActivation ? 1 : 0;
        try
        {
            if (action["event"] is JsonObject evt)
            {
                var name = A2UIJson.AsString(evt["name"]) ?? throw A2UIException.Validation("An action event needs a 'name'.", surface.Id);
                var context = new JsonObject();
                if (evt["context"] is JsonObject contextNode)
                {
                    foreach (var (key, value) in contextNode)
                    {
                        context[key] = Evaluator.ResolveDynamic(value, scope, ev, deep: true)?.DeepClone();
                    }
                }

                var actionBody = new JsonObject
                {
                    ["name"] = name,
                    ["surfaceId"] = surface.Id,
                    ["sourceComponentId"] = node.ComponentId,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                    ["context"] = context
                };
                if (evt["userMessage"] is { } userMessage)
                {
                    actionBody["userMessage"] = A2UIJson.ToDisplayString(Evaluator.Resolve(userMessage,
                        new A2UIPropertyShape(A2UIPropertyKind.DynamicString), scope, ev));
                }

                SendOutbound(A2UIOutboundKind.Action, surface.Id, new JsonObject { ["version"] = "1.0", ["action"] = actionBody });
                return true;
            }

            if (action["functionCall"] is JsonObject call)
            {
                Evaluator.Call(call, scope, ev);
                return true;
            }

            return false;
        }
        catch (A2UIException ex)
        {
            ReportError(ex, surface.Id);
            return false;
        }
        finally
        {
            _activationDepth -= userActivation ? 1 : 0;
        }
    }

    private static int? IndexOf(A2UINode node)
    {
        for (var p = node; p is not null; p = p.Parent)
        {
            if (p.Index is { } i)
            {
                return i;
            }
        }

        return null;
    }

    internal void WriteData(A2UISurface surface, string path, JsonNode? value)
    {
        try
        {
            surface.DataModel.Set(path, value);
        }
        catch (A2UIException ex)
        {
            ReportError(ex, surface.Id);
        }
    }

    internal void OpenUrl(Uri url, A2UISurface? surface)
    {
        if (!Options.UrlPolicy.AllowsLink(url))
        {
            throw A2UIException.Expression($"Opening '{url}' is not allowed.", surface?.Id);
        }

        var args = new A2UIUrlOpeningEventArgs(url, surface?.Id);
        UrlOpening?.Invoke(this, args);
        if (!args.Cancel)
        {
            UrlOpener?.Invoke(args.Url);
        }
    }

    /// <summary>Opens URLs after <see cref="UrlOpening"/>; set by the view layer (the top level's launcher).</summary>
    public Action<Uri>? UrlOpener { get; set; }


    internal void Post(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
        }
        else
        {
            _context.Post(static state => ((Action)state!)(), action);
        }
    }

    private Task InvokeAsync(Func<bool> action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _context.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw A2UIException.State("The renderer is disposed.");
        }
    }

    /// <summary>Cancels pending calls and deletes every surface.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Rpc.Dispose();
        foreach (var surface in _order.ToList())
        {
            surface.Delete();
        }

        _surfaces.Clear();
        _order.Clear();
    }
}

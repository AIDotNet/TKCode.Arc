// JSON-RPC 2.0 (https://www.jsonrpc.org/specification) over the LSP base protocol: requests with ids, notifications,
// responses and errors, and the protocol's cancellation ($/cancelRequest). Written for this package; vscode's src/vs
// has no language client (vscode-jsonrpc is a separate repository and was not consulted).

using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;

namespace TKCode.Arc.Editor.Lsp.JsonRpc;

/// <summary>Error codes of JSON-RPC and of the Language Server Protocol.</summary>
internal static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ServerNotInitialized = -32002;
    public const int UnknownErrorCode = -32001;
    public const int RequestFailed = -32803;
    public const int ServerCancelled = -32802;
    public const int ContentModified = -32801;
    public const int RequestCancelled = -32800;
}

/// <summary>An error response, or an error a request handler throws to answer with one.</summary>
internal sealed class JsonRpcException : Exception
{
    public JsonRpcException(int code, string message, JsonElement? data = null)
        : base(message)
    {
        Code = code;
        Data = data;
    }

    public int Code { get; }

    public new JsonElement? Data { get; }

    /// <summary>The server gave up on the request (cancelled, content modified): a feature shows nothing.</summary>
    public bool IsCancellation => Code is JsonRpcErrorCodes.RequestCancelled or JsonRpcErrorCodes.ContentModified or JsonRpcErrorCodes.ServerCancelled;
}

/// <summary>Writes a JSON value (params or a result) into a message.</summary>
internal delegate void JsonValueWriter(Utf8JsonWriter writer);

/// <summary>Handles a request from the other side: the result's writer, or null for a null result.</summary>
internal delegate Task<JsonValueWriter?> JsonRpcRequestHandler(JsonElement parameters, CancellationToken cancellationToken);

/// <summary>
/// A JSON-RPC connection over a stream pair. Messages are written in the order they are sent (one writer loop), so a
/// notification sent before a request reaches the other side first, as document sync needs. Incoming requests and
/// notifications run on the synchronization context given (an editor's UI thread), in order; responses complete their
/// tasks without it (continuations run where their awaits were).
/// </summary>
internal sealed class JsonRpcConnection : IDisposable
{
    private static readonly JsonWriterOptions s_writerOptions = new() { SkipValidation = false };

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly SynchronizationContext? _context;
    private readonly Channel<byte[]> _outgoing = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<int, PendingRequest> _pending = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _incoming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonRpcRequestHandler> _requestHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _writeLoop = Task.CompletedTask;
    private int _nextId;
    private int _started;
    private int _disposed;

    public JsonRpcConnection(Stream input, Stream output, SynchronizationContext? context = null)
    {
        _input = input;
        _output = output;
        _context = context;
    }

    /// <summary>Raised once when the connection closes: the input ended, a stream failed, or it was disposed.</summary>
    public event Action<Exception?>? Closed;

    /// <summary>Raised for a message that could not be read or handled (for a log).</summary>
    public event Action<string>? Trace;

    /// <summary>Completes when the connection has closed.</summary>
    public Task Completion => _closed.Task;

    public bool IsClosed => _closed.Task.IsCompleted;

    /// <summary>Completes when every message queued before the connection closed has been written.</summary>
    public Task WriteCompletion => _writeLoop;

    /// <summary>Handles requests of a method (register before <see cref="Start"/>).</summary>
    public void OnRequest(string method, JsonRpcRequestHandler handler) => _requestHandlers[method] = handler;

    /// <summary>Handles notifications of a method (register before <see cref="Start"/>).</summary>
    public void OnNotification(string method, Action<JsonElement> handler) => _notificationHandlers[method] = handler;

    /// <summary>Starts reading and writing.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The connection has already started.");
        }
        _writeLoop = Task.Run(WriteLoopAsync);
        _ = Task.Run(ReadLoopAsync);
    }

    /// <summary>Sends a notification.</summary>
    public void SendNotification(string method, JsonValueWriter? parameters)
    {
        Enqueue(WriteMessage(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteString("method", method);
            if (parameters is not null)
            {
                writer.WritePropertyName("params");
                parameters(writer);
            }
        }));
    }

    /// <summary>Sends a notification with typed params.</summary>
    public void SendNotification<T>(string method, T parameters, JsonTypeInfo<T> typeInfo) =>
        SendNotification(method, writer => JsonSerializer.Serialize(writer, parameters, typeInfo));

    /// <summary>
    /// Sends a request; the task gives its result (a clone, valid after the message is gone), fails with a
    /// <see cref="JsonRpcException"/> on an error response and is cancelled when <paramref name="cancellationToken"/> is
    /// (the other side is told with $/cancelRequest).
    /// </summary>
    public Task<JsonElement> SendRequest(string method, JsonValueWriter? parameters, CancellationToken cancellationToken = default)
    {
        if (IsClosed)
        {
            return Task.FromException<JsonElement>(new IOException("The connection is closed."));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var id = Interlocked.Increment(ref _nextId);
        var pending = new PendingRequest(method);
        _pending[id] = pending;
        if (cancellationToken.CanBeCanceled)
        {
            pending.Registration = cancellationToken.Register(() =>
            {
                if (_pending.TryRemove(id, out var removed))
                {
                    removed.Registration.Dispose();
                    removed.Completion.TrySetCanceled(cancellationToken);
                    if (!IsClosed)
                    {
                        SendNotification("$/cancelRequest", writer =>
                        {
                            writer.WriteStartObject();
                            writer.WriteNumber("id", id);
                            writer.WriteEndObject();
                        });
                    }
                }
            });
        }
        Enqueue(WriteMessage(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteNumber("id", id);
            writer.WriteString("method", method);
            if (parameters is not null)
            {
                writer.WritePropertyName("params");
                parameters(writer);
            }
        }));
        return pending.Completion.Task;
    }

    /// <summary>Sends a request with typed params.</summary>
    public Task<JsonElement> SendRequest<T>(string method, T parameters, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default) =>
        SendRequest(method, writer => JsonSerializer.Serialize(writer, parameters, typeInfo), cancellationToken);

    /// <summary>Closes the connection: pending requests fail, the writer stops after what is queued.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        Close(null);
    }

    private void Enqueue(byte[] message)
    {
        if (!_outgoing.Writer.TryWrite(message))
        {
            Trace?.Invoke("A message was dropped: the connection is closed.");
        }
    }

    private static byte[] WriteMessage(Action<Utf8JsonWriter> writeMembers)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, s_writerOptions))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }
        return MessageWriter.Frame(buffer);
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var message in _outgoing.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await _output.WriteAsync(message).ConfigureAwait(false);
                // Flush per batch: the other side waits for whole messages.
                if (!_outgoing.Reader.TryPeek(out _))
                {
                    await _output.FlushAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Close(e);
        }
    }

    private async Task ReadLoopAsync()
    {
        var reader = new MessageReader(_input);
        Exception? error = null;
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var body = await reader.ReadAsync(_lifetime.Token).ConfigureAwait(false);
                if (body is null)
                {
                    break;
                }
                Dispatch(body);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidDataException)
        {
            error = e;
        }
        Close(error);
    }

    private void Dispatch(byte[] body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException e)
        {
            Trace?.Invoke("A message is not JSON: " + e.Message);
            return;
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Trace?.Invoke("A message is not a JSON object.");
                return;
            }
            var hasMethod = root.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String;
            var hasId = root.TryGetProperty("id", out var idElement) && idElement.ValueKind is JsonValueKind.Number or JsonValueKind.String;
            if (!hasMethod)
            {
                if (hasId)
                {
                    HandleResponse(root, idElement);
                }
                return;
            }
            var method = methodElement.GetString()!;
            var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;
            if (hasId)
            {
                HandleRequest(method, idElement.Clone(), parameters);
            }
            else
            {
                HandleNotification(method, parameters);
            }
        }
    }

    private void HandleResponse(JsonElement root, JsonElement idElement)
    {
        if (idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt32(out var id) || !_pending.TryRemove(id, out var pending))
        {
            // A response to a request that was cancelled (or never sent): nothing waits for it.
            return;
        }
        pending.Registration.Dispose();
        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var code = error.TryGetProperty("code", out var c) && c.TryGetInt32(out var value) ? value : JsonRpcErrorCodes.UnknownErrorCode;
            var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()! : "Request failed.";
            JsonElement? data = error.TryGetProperty("data", out var d) ? d.Clone() : null;
            pending.Completion.TrySetException(new JsonRpcException(code, message, data));
            return;
        }
        pending.Completion.TrySetResult(root.TryGetProperty("result", out var result) ? result.Clone() : default);
    }

    private void HandleNotification(string method, JsonElement parameters)
    {
        if (method == "$/cancelRequest")
        {
            if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("id", out var id) && _incoming.TryGetValue(IdKey(id), out var cts))
            {
                cts.Cancel();
            }
            return;
        }
        if (!_notificationHandlers.TryGetValue(method, out var handler))
        {
            // Unknown notifications are ignored ($/ ones by the specification, others because a client may not know them).
            return;
        }
        Post(() =>
        {
            try
            {
                handler(parameters);
            }
            catch (Exception e)
            {
                Trace?.Invoke($"The handler of '{method}' failed: {e.Message}");
            }
        });
    }

    private void HandleRequest(string method, JsonElement id, JsonElement parameters)
    {
        if (!_requestHandlers.TryGetValue(method, out var handler))
        {
            SendError(id, JsonRpcErrorCodes.MethodNotFound, $"Unhandled method {method}");
            return;
        }
        var key = IdKey(id);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _incoming[key] = cts;
        Post(async () =>
        {
            try
            {
                var result = await handler(parameters, cts.Token);
                SendResult(id, result);
            }
            catch (OperationCanceledException)
            {
                SendError(id, JsonRpcErrorCodes.RequestCancelled, "Request cancelled.");
            }
            catch (JsonRpcException e)
            {
                SendError(id, e.Code, e.Message);
            }
            catch (Exception e)
            {
                SendError(id, JsonRpcErrorCodes.InternalError, e.Message);
            }
            finally
            {
                _incoming.TryRemove(key, out _);
                cts.Dispose();
            }
        });
    }

    private void SendResult(JsonElement id, JsonValueWriter? result)
    {
        if (IsClosed)
        {
            return;
        }
        Enqueue(WriteMessage(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            id.WriteTo(writer);
            writer.WritePropertyName("result");
            if (result is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                result(writer);
            }
        }));
    }

    private void SendError(JsonElement id, int code, string message)
    {
        if (IsClosed)
        {
            return;
        }
        Enqueue(WriteMessage(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            id.WriteTo(writer);
            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        }));
    }

    private void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(static state => ((Action)state!)(), action);
        }
    }

    private static string IdKey(JsonElement id) => id.ValueKind == JsonValueKind.String ? "s:" + id.GetString() : "n:" + id.GetRawText();

    private void Close(Exception? error)
    {
        if (!_closed.TrySetResult())
        {
            return;
        }
        _outgoing.Writer.TryComplete();
        _lifetime.Cancel();
        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.Registration.Dispose();
                pending.Completion.TrySetException(new IOException($"The connection closed before '{pending.Method}' was answered.", error));
            }
        }
        Closed?.Invoke(error);
    }

    private sealed class PendingRequest(string method)
    {
        public string Method { get; } = method;

        public TaskCompletionSource<JsonElement> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration Registration { get; set; }
    }
}

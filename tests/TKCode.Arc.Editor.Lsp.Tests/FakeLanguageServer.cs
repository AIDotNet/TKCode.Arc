// An in-process language server for the tests: the client talks to it over two operating system pipes, it speaks
// JSON-RPC with the package's own connection, keeps the text of the documents it was sent (applying incremental
// changes as a server would) and answers requests from handlers the test gives.

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using TKCode.Arc.Editor.Lsp.JsonRpc;

namespace TKCode.Arc.Editor.Lsp.Tests;

internal sealed class FakeLanguageServer : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream _toClient = new(PipeDirection.Out);
    private readonly AnonymousPipeServerStream _toServer = new(PipeDirection.Out);
    private readonly AnonymousPipeClientStream _clientInput;
    private readonly AnonymousPipeClientStream _serverInput;
    private readonly Dictionary<string, Func<JsonElement, string?>> _handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _documents = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(string Method, JsonElement Params)> _messages = new();
    private readonly string _capabilities;

    public FakeLanguageServer(string capabilities)
    {
        _capabilities = capabilities;
        _clientInput = new AnonymousPipeClientStream(PipeDirection.In, _toClient.ClientSafePipeHandle);
        _serverInput = new AnonymousPipeClientStream(PipeDirection.In, _toServer.ClientSafePipeHandle);
        Connection = new JsonRpcConnection(_serverInput, _toClient);
    }

    /// <summary>The server side of the connection (to send notifications and requests to the client).</summary>
    public JsonRpcConnection Connection { get; }

    /// <summary>What the client reads (the server's output).</summary>
    public Stream ClientInput => _clientInput;

    /// <summary>What the client writes (the server's input).</summary>
    public Stream ClientOutput => _toServer;

    /// <summary>The initialize params the client sent.</summary>
    public JsonElement InitializeParams { get; private set; }

    /// <summary>Every request and notification the server got, in order.</summary>
    public IReadOnlyList<(string Method, JsonElement Params)> Messages => _messages.ToList();

    public IEnumerable<JsonElement> Received(string method) => _messages.Where(m => m.Method == method).Select(m => m.Params);

    public bool ShutdownReceived { get; private set; }

    public bool ExitReceived { get; private set; }

    /// <summary>The text the server has for a document (after the client's changes).</summary>
    public string? Document(string uri) => _documents.TryGetValue(uri, out var text) ? text : null;

    /// <summary>Answers a request method with raw JSON (null answers JSON null).</summary>
    public FakeLanguageServer On(string method, Func<JsonElement, string?> handler)
    {
        _handlers[method] = handler;
        return this;
    }

    public FakeLanguageServer Start()
    {
        Connection.OnRequest("initialize", (parameters, _) =>
        {
            InitializeParams = parameters;
            Record("initialize", parameters);
            var result = "{\"capabilities\":" + _capabilities + ",\"serverInfo\":{\"name\":\"fake\",\"version\":\"1.0\"}}";
            return Task.FromResult<JsonValueWriter?>(writer => writer.WriteRawValue(result));
        });
        Connection.OnRequest("shutdown", (parameters, _) =>
        {
            ShutdownReceived = true;
            Record("shutdown", parameters);
            return Task.FromResult<JsonValueWriter?>(null);
        });
        foreach (var notification in new[] { "initialized", "exit", "textDocument/didOpen", "textDocument/didChange", "textDocument/didClose", "$/setTrace" })
        {
            var method = notification;
            Connection.OnNotification(method, parameters => OnNotification(method, parameters));
        }
        foreach (var (method, handler) in _handlers)
        {
            var h = handler;
            var m = method;
            Connection.OnRequest(m, (parameters, _) =>
            {
                Record(m, parameters);
                var json = h(parameters);
                return Task.FromResult<JsonValueWriter?>(json is null ? null : writer => writer.WriteRawValue(json));
            });
        }
        Connection.Start();
        return this;
    }

    /// <summary>Sends a notification to the client, the params as raw JSON.</summary>
    public void Notify(string method, string parameters) => Connection.SendNotification(method, writer => writer.WriteRawValue(parameters));

    /// <summary>Sends a request to the client and returns its result.</summary>
    public Task<JsonElement> Request(string method, string parameters) => Connection.SendRequest(method, writer => writer.WriteRawValue(parameters));

    public async ValueTask DisposeAsync()
    {
        Connection.Dispose();
        await Task.WhenAny(Connection.WriteCompletion, Task.Delay(1000));
        _toClient.Dispose();
        _toServer.Dispose();
        _clientInput.Dispose();
        _serverInput.Dispose();
    }

    private void Record(string method, JsonElement parameters) => _messages.Enqueue((method, parameters.ValueKind == JsonValueKind.Undefined ? default : parameters.Clone()));

    private void OnNotification(string method, JsonElement parameters)
    {
        switch (method)
        {
            case "textDocument/didOpen":
                var item = parameters.GetProperty("textDocument");
                _documents[item.GetProperty("uri").GetString()!] = item.GetProperty("text").GetString()!;
                break;
            case "textDocument/didChange":
                var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
                var text = _documents.TryGetValue(uri, out var current) ? current : string.Empty;
                foreach (var change in parameters.GetProperty("contentChanges").EnumerateArray())
                {
                    text = change.TryGetProperty("range", out var range)
                        ? Apply(text, range, change.GetProperty("text").GetString()!)
                        : change.GetProperty("text").GetString()!;
                }
                _documents[uri] = text;
                break;
            case "textDocument/didClose":
                _documents.TryRemove(parameters.GetProperty("textDocument").GetProperty("uri").GetString()!, out _);
                break;
            case "exit":
                ExitReceived = true;
                break;
        }
        Record(method, parameters);
    }

    // Applies one incremental change (0-based line / UTF-16 character) to the text.
    private static string Apply(string text, JsonElement range, string newText)
    {
        var start = Offset(text, range.GetProperty("start"));
        var end = Offset(text, range.GetProperty("end"));
        return new StringBuilder(text).Remove(start, end - start).Insert(start, newText).ToString();
    }

    private static int Offset(string text, JsonElement position)
    {
        var line = position.GetProperty("line").GetInt32();
        var character = position.GetProperty("character").GetInt32();
        var offset = 0;
        for (var i = 0; i < line; i++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0)
            {
                return text.Length;
            }
            offset = next + 1;
        }
        return Math.Min(text.Length, offset + character);
    }
}

internal static class TestWait
{
    /// <summary>Waits until <paramref name="condition"/> holds (messages travel through pipes and threads).</summary>
    public static async Task Until(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold in time.");
            }
            await Task.Delay(10);
        }
    }
}

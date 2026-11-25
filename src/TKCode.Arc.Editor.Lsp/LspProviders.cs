// The language feature providers a language server backs: each provider method finds the model's attached document,
// sends the request and converts the answer. The features are split by concern: LspProviders.Completion.cs (completion,
// hover, signature help), .Navigation.cs (definitions, references, highlights, symbols, folding, selection ranges,
// linked editing), .Editing.cs (code actions, rename, formatting, colors) and .Info.cs (code lens, semantic tokens,
// inlay hints).

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.JsonRpc;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Lsp;

internal sealed partial class LspProviders
{
    private static readonly LspJsonContext s_json = LspJsonContext.Default;

    private readonly LanguageClient _client;
    private readonly LspServerCapabilities _capabilities;

    // The protocol values of results that can be resolved later (the server needs them back as it sent them). Keyed by
    // the engine object's identity; the values go with the objects.
    private readonly ConditionalWeakTable<CompletionItem, CompletionResolveState> _completionItems = new();
    private readonly ConditionalWeakTable<CodeAction, RawValue> _codeActions = new();
    private readonly ConditionalWeakTable<CodeLens, RawValue> _codeLenses = new();
    private readonly ConditionalWeakTable<InlayHint, RawValue> _inlayHints = new();

    public LspProviders(LanguageClient client, LspServerCapabilities capabilities)
    {
        _client = client;
        _capabilities = capabilities;
    }

    /// <summary>The document and the connection for a request, or null when the model is not attached or the session ended.</summary>
    private (LspDocument Document, JsonRpcConnection Connection)? Target(TextModel model)
    {
        if (_client.GetDocument(model) is not { } document || _client.Connection is not { IsClosed: false } connection)
        {
            return null;
        }
        return (document, connection);
    }

    /// <summary>
    /// Sends a request and gives its result; null when the result is null, the server gave up on it (cancelled, content
    /// modified) or failed (logged). A cancelled <paramref name="token"/> cancels the request and throws, as providers do.
    /// </summary>
    private async Task<JsonElement?> Request<T>(JsonRpcConnection connection, string method, T parameters, JsonTypeInfo<T> typeInfo, CancellationToken token)
    {
        try
        {
            var result = await connection.SendRequest(method, parameters, typeInfo, token).ConfigureAwait(true);
            return LspConvert.IsNull(result) ? null : result;
        }
        catch (JsonRpcException e) when (e.IsCancellation)
        {
            return null;
        }
        catch (JsonRpcException e)
        {
            _client.Log(LanguageClientMessageType.Error, $"Request {method} failed: {e.Message}");
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>A request whose params are a protocol value sent back as it came (a resolve).</summary>
    private async Task<JsonElement?> RequestRaw(JsonRpcConnection connection, string method, JsonElement parameters, CancellationToken token)
    {
        try
        {
            var result = await connection.SendRequest(method, writer => parameters.WriteTo(writer), token).ConfigureAwait(true);
            return LspConvert.IsNull(result) ? null : result;
        }
        catch (JsonRpcException e) when (e.IsCancellation)
        {
            return null;
        }
        catch (JsonRpcException e)
        {
            _client.Log(LanguageClientMessageType.Error, $"Request {method} failed: {e.Message}");
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static TextDocumentPositionParams PositionParams(LspDocument document, TextPosition position) =>
        new(document.Identifier, LspConvert.FromPosition(position));

    private static T? Read<T>(JsonElement? element, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (element is not { } value)
        {
            return null;
        }
        try
        {
            return value.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static T? ReadStruct<T>(JsonElement? element, JsonTypeInfo<T> typeInfo)
        where T : struct
    {
        if (element is not { } value)
        {
            return null;
        }
        try
        {
            return value.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class RawValue(JsonElement value)
    {
        public JsonElement Value { get; } = value;
    }

    private sealed class CompletionResolveState(JsonElement item, LspCompletionItemDefaults? defaults)
    {
        public JsonElement Item { get; } = item;

        public LspCompletionItemDefaults? Defaults { get; } = defaults;
    }
}

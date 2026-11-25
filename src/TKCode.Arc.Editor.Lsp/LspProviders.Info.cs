// Code lenses (+ resolve), semantic tokens (full, delta, range) and inlay hints (+ resolve) from a language server,
// with the server's refresh requests as the providers' change events.

using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Lsp;

internal sealed partial class LspProviders :
    ICodeLensProvider, IDocumentSemanticTokensProvider, IDocumentRangeSemanticTokensProvider, IInlayHintsProvider
{
    private SemanticTokensLegend? _legend;

    private event Action<ICodeLensProvider>? CodeLensesChanged;

    private event Action? SemanticTokensChanged;

    private event Action? InlayHintsChanged;

    /// <summary>The server asked for code lenses again (workspace/codeLens/refresh).</summary>
    public void RefreshCodeLenses() => CodeLensesChanged?.Invoke(this);

    /// <summary>The server asked for semantic tokens again (workspace/semanticTokens/refresh).</summary>
    public void RefreshSemanticTokens() => SemanticTokensChanged?.Invoke();

    /// <summary>The server asked for inlay hints again (workspace/inlayHint/refresh).</summary>
    public void RefreshInlayHints() => InlayHintsChanged?.Invoke();

    // ---- code lens ----

    event Action<ICodeLensProvider>? ICodeLensProvider.DidChange
    {
        add => CodeLensesChanged += value;
        remove => CodeLensesChanged -= value;
    }

    bool ICodeLensProvider.SupportsResolveCodeLens => _capabilities.CodeLensResolve;

    public async Task<CodeLensList?> ProvideCodeLenses(TextModel model, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        if (await Request(connection, "textDocument/codeLens", new TextDocumentParams(document.Identifier), s_json.TextDocumentParams, token) is not { ValueKind: JsonValueKind.Array } result)
        {
            return null;
        }
        var lenses = new List<CodeLens>();
        foreach (var element in result.EnumerateArray())
        {
            if (Read(element, s_json.LspCodeLens) is not { } lspLens)
            {
                continue;
            }
            var lens = LspConvert.ToCodeLens(lspLens);
            if (_capabilities.CodeLensResolve && lens.Command is null)
            {
                _codeLenses.AddOrUpdate(lens, new RawValue(element.Clone()));
            }
            lenses.Add(lens);
        }
        return new CodeLensList(lenses);
    }

    public async Task<CodeLens?> ResolveCodeLens(TextModel model, CodeLens codeLens, CancellationToken token)
    {
        if (!_capabilities.CodeLensResolve || !_codeLenses.TryGetValue(codeLens, out var raw) || Target(model) is not ({ }, { } connection))
        {
            return null;
        }
        var resolved = Read(await RequestRaw(connection, "codeLens/resolve", raw.Value, token), s_json.LspCodeLens);
        return resolved is null ? null : LspConvert.ToCodeLens(resolved);
    }

    // ---- semantic tokens ----

    event Action? IDocumentSemanticTokensProvider.DidChange
    {
        add => SemanticTokensChanged += value;
        remove => SemanticTokensChanged -= value;
    }

    event Action? IDocumentRangeSemanticTokensProvider.DidChange
    {
        add => SemanticTokensChanged += value;
        remove => SemanticTokensChanged -= value;
    }

    public SemanticTokensLegend GetLegend()
    {
        if (_legend is null)
        {
            var legend = _capabilities.SemanticTokensLegend;
            _legend = new SemanticTokensLegend(legend?.TokenTypes ?? [], legend?.TokenModifiers ?? []);
        }
        return _legend;
    }

    public async Task<SemanticTokensOrEdits?> ProvideDocumentSemanticTokens(TextModel model, string? lastResultId, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        JsonElement? result;
        if (lastResultId is not null && _capabilities.SemanticTokensDelta)
        {
            var parameters = new SemanticTokensDeltaParams(document.Identifier, lastResultId);
            result = await Request(connection, "textDocument/semanticTokens/full/delta", parameters, s_json.SemanticTokensDeltaParams, token);
        }
        else
        {
            result = await Request(connection, "textDocument/semanticTokens/full", new TextDocumentParams(document.Identifier), s_json.TextDocumentParams, token);
        }
        return ReadStruct(result, s_json.LspSemanticTokensResult) switch
        {
            LspSemanticTokensDelta delta => LspConvert.ToSemanticTokensEdits(delta),
            LspSemanticTokens tokens => LspConvert.ToSemanticTokens(tokens),
            _ => null,
        };
    }

    public void ReleaseDocumentSemanticTokens(string? resultId)
    {
        // The protocol has no release: a server keeps one previous result per document.
    }

    public async Task<SemanticTokens?> ProvideDocumentRangeSemanticTokens(TextModel model, TextRange range, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new SemanticTokensRangeParams(document.Identifier, LspConvert.FromRange(range));
        var result = Read(await Request(connection, "textDocument/semanticTokens/range", parameters, s_json.SemanticTokensRangeParams, token), s_json.LspSemanticTokens);
        return result is null ? null : LspConvert.ToSemanticTokens(result);
    }

    // ---- inlay hints ----

    string? IInlayHintsProvider.DisplayName => _client.ServerName;

    event Action? IInlayHintsProvider.DidChangeInlayHints
    {
        add => InlayHintsChanged += value;
        remove => InlayHintsChanged -= value;
    }

    bool IInlayHintsProvider.SupportsResolveInlayHint => _capabilities.InlayHintResolve;

    public async Task<InlayHintList?> ProvideInlayHints(TextModel model, TextRange range, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new InlayHintParams(document.Identifier, LspConvert.FromRange(range));
        if (await Request(connection, "textDocument/inlayHint", parameters, s_json.InlayHintParams, token) is not { ValueKind: JsonValueKind.Array } result)
        {
            return null;
        }
        var hints = new List<InlayHint>();
        foreach (var element in result.EnumerateArray())
        {
            if (Read(element, s_json.LspInlayHint) is not { } lspHint)
            {
                continue;
            }
            var hint = LspConvert.ToInlayHint(lspHint);
            if (_capabilities.InlayHintResolve)
            {
                _inlayHints.AddOrUpdate(hint, new RawValue(element.Clone()));
            }
            hints.Add(hint);
        }
        return new InlayHintList(hints);
    }

    public async Task<InlayHint?> ResolveInlayHint(InlayHint hint, CancellationToken token)
    {
        if (!_capabilities.InlayHintResolve || !_inlayHints.TryGetValue(hint, out var raw) || _client.Connection is not { IsClosed: false } connection)
        {
            return null;
        }
        var resolved = Read(await RequestRaw(connection, "inlayHint/resolve", raw.Value, token), s_json.LspInlayHint);
        return resolved is null ? null : LspConvert.ToInlayHint(resolved);
    }
}

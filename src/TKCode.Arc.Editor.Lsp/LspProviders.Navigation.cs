// Definitions, declarations, implementations, type definitions, references, document highlights, document symbols,
// folding ranges, selection ranges and linked editing ranges from a language server.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Lsp;

internal sealed partial class LspProviders :
    IDefinitionProvider, IDeclarationProvider, IImplementationProvider, ITypeDefinitionProvider, IReferenceProvider,
    IDocumentHighlightProvider, IDocumentSymbolProvider, IFoldingRangeProvider, ISelectionRangeProvider, ILinkedEditingRangeProvider
{
    private event Action<IFoldingRangeProvider>? FoldingRangesChanged;

    public Task<IReadOnlyList<LocationLink>?> ProvideDefinition(TextModel model, TextPosition position, CancellationToken token) =>
        Locations(model, position, "textDocument/definition", token);

    public Task<IReadOnlyList<LocationLink>?> ProvideDeclaration(TextModel model, TextPosition position, CancellationToken token) =>
        Locations(model, position, "textDocument/declaration", token);

    public Task<IReadOnlyList<LocationLink>?> ProvideImplementation(TextModel model, TextPosition position, CancellationToken token) =>
        Locations(model, position, "textDocument/implementation", token);

    public Task<IReadOnlyList<LocationLink>?> ProvideTypeDefinition(TextModel model, TextPosition position, CancellationToken token) =>
        Locations(model, position, "textDocument/typeDefinition", token);

    private async Task<IReadOnlyList<LocationLink>?> Locations(TextModel model, TextPosition position, string method, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = await Request(connection, method, PositionParams(document, position), s_json.TextDocumentPositionParams, token);
        return LspConvert.ToLocationLinks(ReadStruct(result, s_json.LspLocationResult));
    }

    public async Task<IReadOnlyList<Location>?> ProvideReferences(TextModel model, TextPosition position, ReferenceContext context, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new ReferenceParams(document.Identifier, LspConvert.FromPosition(position), new LspReferenceContext(context.IncludeDeclaration));
        var result = Read(await Request(connection, "textDocument/references", parameters, s_json.ReferenceParams, token), s_json.LspLocationArray);
        return result?.Select(LspConvert.ToLocation).ToList();
    }

    public async Task<IReadOnlyList<DocumentHighlight>?> ProvideDocumentHighlights(TextModel model, TextPosition position, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = Read(
            await Request(connection, "textDocument/documentHighlight", PositionParams(document, position), s_json.TextDocumentPositionParams, token),
            s_json.LspDocumentHighlightArray);
        return result is null ? null : LspConvert.ToDocumentHighlights(result);
    }

    string? IDocumentSymbolProvider.DisplayName => _client.ServerName;

    public async Task<IReadOnlyList<DocumentSymbol>?> ProvideDocumentSymbols(TextModel model, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = await Request(connection, "textDocument/documentSymbol", new TextDocumentParams(document.Identifier), s_json.TextDocumentParams, token);
        return LspConvert.ToDocumentSymbols(Read(result, s_json.LspDocumentSymbolItemArray));
    }

    string? IFoldingRangeProvider.Id => "lsp:" + _client.MarkerOwner;

    event Action<IFoldingRangeProvider>? IFoldingRangeProvider.DidChange
    {
        add => FoldingRangesChanged += value;
        remove => FoldingRangesChanged -= value;
    }

    /// <summary>The server asked for the folding ranges to be computed again (workspace/foldingRange/refresh).</summary>
    public void RefreshFoldingRanges() => FoldingRangesChanged?.Invoke(this);

    public async Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = Read(
            await Request(connection, "textDocument/foldingRange", new TextDocumentParams(document.Identifier), s_json.TextDocumentParams, token),
            s_json.LspFoldingRangeArray);
        return result is null ? null : LspConvert.ToFoldingRanges(result);
    }

    public async Task<IReadOnlyList<IReadOnlyList<SelectionRange>>?> ProvideSelectionRanges(TextModel model, IReadOnlyList<TextPosition> positions, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new SelectionRangeParams(document.Identifier, positions.Select(LspConvert.FromPosition).ToArray());
        var result = Read(await Request(connection, "textDocument/selectionRange", parameters, s_json.SelectionRangeParams, token), s_json.LspSelectionRangeArray);
        return result is null ? null : LspConvert.ToSelectionRanges(result);
    }

    public async Task<LinkedEditingRanges?> ProvideLinkedEditingRanges(TextModel model, TextPosition position, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = Read(
            await Request(connection, "textDocument/linkedEditingRange", PositionParams(document, position), s_json.TextDocumentPositionParams, token),
            s_json.LspLinkedEditingRanges);
        return result is null ? null : LspConvert.ToLinkedEditingRanges(result);
    }
}

// Completion (+ resolve), hover and signature help from a language server.

using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Lsp;

internal sealed partial class LspProviders : ICompletionItemProvider, IHoverProvider, ISignatureHelpProvider
{
    // ---- completion ----

    string ICompletionItemProvider.DebugDisplayName => "lsp:" + _client.MarkerOwner;

    IReadOnlyList<string>? ICompletionItemProvider.TriggerCharacters => _capabilities.CompletionTriggerCharacters;

    bool ICompletionItemProvider.SupportsResolveCompletionItem => _capabilities.CompletionResolve;

    public async Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new CompletionParams(document.Identifier, LspConvert.FromPosition(position))
        {
            Context = new LspCompletionContext(LspConvert.FromCompletionTriggerKind(context.TriggerKind)) { TriggerCharacter = context.TriggerCharacter },
        };
        if (await Request(connection, "textDocument/completion", parameters, s_json.CompletionParams, token) is not { } result)
        {
            return null;
        }

        var defaultRange = DefaultCompletionRange(model, position);
        JsonElement items;
        LspCompletionItemDefaults? defaults = null;
        var incomplete = false;
        switch (result.ValueKind)
        {
            case JsonValueKind.Array:
                items = result;
                break;
            case JsonValueKind.Object:
                incomplete = LspConvert.GetBool(result, "isIncomplete");
                if (!result.TryGetProperty("items", out items) || items.ValueKind != JsonValueKind.Array)
                {
                    return new CompletionList([]) { Incomplete = incomplete };
                }
                if (result.TryGetProperty("itemDefaults", out var defaultsElement) && defaultsElement.ValueKind == JsonValueKind.Object)
                {
                    defaults = Read(defaultsElement, s_json.LspCompletionItemDefaults);
                }
                break;
            default:
                return null;
        }

        var suggestions = new List<CompletionItem>(items.GetArrayLength());
        foreach (var element in items.EnumerateArray())
        {
            if (Read(element, s_json.LspCompletionItem) is not { } lspItem)
            {
                continue;
            }
            var item = LspConvert.ToCompletionItem(lspItem, defaults, defaultRange);
            if (_capabilities.CompletionResolve)
            {
                _completionItems.AddOrUpdate(item, new CompletionResolveState(element, defaults));
            }
            suggestions.Add(item);
        }
        return new CompletionList(suggestions) { Incomplete = incomplete };
    }

    public async Task<CompletionItem?> ResolveCompletionItem(CompletionItem item, CancellationToken token)
    {
        if (!_capabilities.CompletionResolve || !_completionItems.TryGetValue(item, out var state)
            || _client.Connection is not { IsClosed: false } connection)
        {
            return null;
        }
        // An item that left its data to the list's defaults gets it back for the resolve.
        var raw = state.Item;
        if (state.Defaults?.Data is { } data && !raw.TryGetProperty("data", out _) && Read(raw, s_json.LspCompletionItem) is { } withoutData)
        {
            raw = JsonSerializer.SerializeToElement(withoutData with { Data = data }, s_json.LspCompletionItem);
        }
        if (await RequestRaw(connection, "completionItem/resolve", raw, token) is not { } result
            || Read(result, s_json.LspCompletionItem) is not { } resolved)
        {
            return null;
        }
        // The resolved item keeps the original's range unless it brings its own edit.
        return LspConvert.ToCompletionItem(resolved, state.Defaults is { } defaults ? defaults with { EditRange = null } : null, item.Range);
    }

    /// <summary>The range an item without an edit replaces: the word before the position (insert) or around it (replace).</summary>
    private static CompletionItemRanges DefaultCompletionRange(TextModel model, TextPosition position)
    {
        var validated = model.ValidatePosition(position);
        var line = validated.LineNumber;
        var word = WordHelper.GetWordAtText(validated.Column, WordHelper.DefaultWordRegExp, model.GetLineContent(line), 0);
        if (word is null || word.StartColumn > validated.Column)
        {
            var empty = new TextRange(line, validated.Column, line, validated.Column);
            return new CompletionItemRanges(empty, empty);
        }
        return new CompletionItemRanges(
            new TextRange(line, word.StartColumn, line, validated.Column),
            new TextRange(line, word.StartColumn, line, Math.Max(word.EndColumn, validated.Column)));
    }

    // ---- hover ----

    public async Task<Hover?> ProvideHover(TextModel model, TextPosition position, CancellationToken token, HoverContext? context = null)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = await Request(connection, "textDocument/hover", PositionParams(document, position), s_json.TextDocumentPositionParams, token);
        return LspConvert.ToHover(Read(result, s_json.LspHover));
    }

    // ---- signature help ----

    IReadOnlyList<string>? ISignatureHelpProvider.SignatureHelpTriggerCharacters => _capabilities.SignatureHelpTriggerCharacters;

    IReadOnlyList<string>? ISignatureHelpProvider.SignatureHelpRetriggerCharacters => _capabilities.SignatureHelpRetriggerCharacters;

    public async Task<SignatureHelpResult?> ProvideSignatureHelp(TextModel model, TextPosition position, CancellationToken token, SignatureHelpContext context)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new SignatureHelpParams(document.Identifier, LspConvert.FromPosition(position))
        {
            Context = new LspSignatureHelpContext((int)context.TriggerKind, context.IsRetrigger)
            {
                TriggerCharacter = context.TriggerCharacter,
                ActiveSignatureHelp = context.ActiveSignatureHelp is { } active ? LspConvert.FromSignatureHelp(active) : null,
            },
        };
        var result = Read(await Request(connection, "textDocument/signatureHelp", parameters, s_json.SignatureHelpParams, token), s_json.LspSignatureHelp);
        if (result is null || result.Signatures.Length == 0)
        {
            return null;
        }
        return new SignatureHelpResult(LspConvert.ToSignatureHelp(result));
    }
}

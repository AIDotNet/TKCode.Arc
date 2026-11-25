// Code actions (+ resolve), rename (+ prepare), document / range / on-type formatting and document colors from a
// language server.

using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.JsonRpc;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Lsp;

internal sealed partial class LspProviders :
    ICodeActionProvider, IRenameProvider, IDocumentFormattingEditProvider, IDocumentRangeFormattingEditProvider,
    IOnTypeFormattingEditProvider, IDocumentColorProvider
{
    // ---- code actions ----

    string? ICodeActionProvider.DisplayName => _client.ServerName;

    IReadOnlyList<string>? ICodeActionProvider.ProvidedCodeActionKinds => _capabilities.CodeActionKinds;

    bool ICodeActionProvider.SupportsResolveCodeAction => _capabilities.CodeActionResolve;

    public async Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new CodeActionParams(
            document.Identifier,
            LspConvert.FromRange(range),
            new LspCodeActionContext(document.GetDiagnosticsIn(range).ToArray())
            {
                Only = context.Only is { } only ? [only] : null,
                TriggerKind = (int)context.Trigger,
            });
        if (await Request(connection, "textDocument/codeAction", parameters, s_json.CodeActionParams, token) is not { ValueKind: JsonValueKind.Array } result)
        {
            return null;
        }
        if (!_capabilities.CodeActionResolve)
        {
            var entries = Read(result, s_json.LspCodeActionOrCommandArray);
            if (entries is null)
            {
                return null;
            }
            var converted = new List<CodeAction>(entries.Length);
            foreach (var entry in entries)
            {
                if (LspConvert.ToCodeAction(entry) is { } action)
                {
                    converted.Add(action);
                }
            }
            return new CodeActionList(converted);
        }
        var actions = new List<CodeAction>();
        foreach (var element in result.EnumerateArray())
        {
            if (ReadStruct(element, s_json.LspCodeActionOrCommand) is not { } entry
                || LspConvert.ToCodeAction(entry) is not { } action)
            {
                continue;
            }
            if (entry is LspCodeAction)
            {
                _codeActions.AddOrUpdate(action, new RawValue(element.Clone()));
            }
            actions.Add(action);
        }
        return new CodeActionList(actions);
    }

    public async Task<CodeAction?> ResolveCodeAction(CodeAction codeAction, CancellationToken token)
    {
        if (!_capabilities.CodeActionResolve || !_codeActions.TryGetValue(codeAction, out var raw)
            || _client.Connection is not { IsClosed: false } connection)
        {
            return null;
        }
        return ReadStruct(await RequestRaw(connection, "codeAction/resolve", raw.Value, token), s_json.LspCodeActionOrCommand) is { } entry
            ? LspConvert.ToCodeAction(entry)
            : null;
    }

    // ---- rename ----

    bool IRenameProvider.SupportsResolveRenameLocation => _capabilities.RenamePrepare;

    public async Task<RenameEdits?> ProvideRenameEdits(TextModel model, TextPosition position, string newName, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new RenameParams(document.Identifier, LspConvert.FromPosition(position), newName);
        try
        {
            var result = await connection.SendRequest("textDocument/rename", parameters, s_json.RenameParams, token);
            if (LspConvert.IsNull(result))
            {
                return null;
            }
            return new RenameEdits(LspConvert.ToWorkspaceEdit(Read(result, s_json.LspWorkspaceEdit)));
        }
        catch (JsonRpcException e) when (!e.IsCancellation)
        {
            // The server's reason (a name that is taken, a symbol it cannot rename) is the message the user sees.
            return new RenameEdits(new WorkspaceEdit([])) { RejectReason = e.Message };
        }
        catch (JsonRpcException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task<RenameLocationResult?> ResolveRenameLocation(TextModel model, TextPosition position, CancellationToken token)
    {
        if (!_capabilities.RenamePrepare || Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        JsonElement result;
        try
        {
            result = await connection.SendRequest("textDocument/prepareRename", PositionParams(document, position), s_json.TextDocumentPositionParams, token);
        }
        catch (JsonRpcException e) when (!e.IsCancellation)
        {
            return Rejected(position, e.Message);
        }
        catch (JsonRpcException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        if (LspConvert.IsNull(result) || ReadStruct(result, s_json.LspPrepareRenameResult) is not { } prepared)
        {
            return Rejected(position, "The element can't be renamed.");
        }
        switch (prepared)
        {
            case LspPrepareRenameDefaultBehavior { DefaultBehavior: true }:
                var validated = model.ValidatePosition(position);
                var word = WordHelper.GetWordAtText(validated.Column, WordHelper.DefaultWordRegExp, model.GetLineContent(validated.LineNumber), 0);
                if (word is null)
                {
                    return Rejected(position, "The element can't be renamed.");
                }
                return new RenameLocationResult(new TextRange(validated.LineNumber, word.StartColumn, validated.LineNumber, word.EndColumn), word.Word);
            case LspPrepareRenamePlaceholder withPlaceholder:
                var placeholderRange = model.ValidateRange(LspConvert.ToRange(withPlaceholder.Range));
                return new RenameLocationResult(placeholderRange, withPlaceholder.Placeholder ?? model.GetValueInRange(placeholderRange));
            case LspRange plain:
                var plainRange = model.ValidateRange(LspConvert.ToRange(plain));
                return new RenameLocationResult(plainRange, model.GetValueInRange(plainRange));
            default:
                return Rejected(position, "The element can't be renamed.");
        }
    }

    private static RenameLocationResult Rejected(TextPosition position, string reason) =>
        new(new TextRange(position, position), string.Empty) { RejectReason = reason };

    // ---- formatting ----

    string? IDocumentFormattingEditProvider.DisplayName => _client.ServerName;

    string? IDocumentRangeFormattingEditProvider.DisplayName => _client.ServerName;

    bool IDocumentRangeFormattingEditProvider.SupportsRangesFormatting => _capabilities.DocumentRangesFormatting;

    IReadOnlyList<string> IOnTypeFormattingEditProvider.AutoFormatTriggerCharacters => _capabilities.OnTypeFormattingTriggerCharacters ?? [];

    public async Task<IReadOnlyList<Languages.TextEdit>?> ProvideDocumentFormattingEdits(TextModel model, FormattingOptions options, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new DocumentFormattingParams(document.Identifier, new LspFormattingOptions(options.TabSize, options.InsertSpaces));
        return Edits(await Request(connection, "textDocument/formatting", parameters, s_json.DocumentFormattingParams, token));
    }

    public async Task<IReadOnlyList<Languages.TextEdit>?> ProvideDocumentRangeFormattingEdits(TextModel model, TextRange range, FormattingOptions options, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new DocumentRangeFormattingParams(document.Identifier, LspConvert.FromRange(range), new LspFormattingOptions(options.TabSize, options.InsertSpaces));
        return Edits(await Request(connection, "textDocument/rangeFormatting", parameters, s_json.DocumentRangeFormattingParams, token));
    }

    public async Task<IReadOnlyList<Languages.TextEdit>?> ProvideDocumentRangesFormattingEdits(TextModel model, IReadOnlyList<TextRange> ranges, FormattingOptions options, CancellationToken token)
    {
        if (!_capabilities.DocumentRangesFormatting || Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new DocumentRangesFormattingParams(document.Identifier, ranges.Select(LspConvert.FromRange).ToArray(), new LspFormattingOptions(options.TabSize, options.InsertSpaces));
        return Edits(await Request(connection, "textDocument/rangesFormatting", parameters, s_json.DocumentRangesFormattingParams, token));
    }

    public async Task<IReadOnlyList<Languages.TextEdit>?> ProvideOnTypeFormattingEdits(TextModel model, TextPosition position, string ch, FormattingOptions options, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new DocumentOnTypeFormattingParams(document.Identifier, LspConvert.FromPosition(position), ch, new LspFormattingOptions(options.TabSize, options.InsertSpaces));
        return Edits(await Request(connection, "textDocument/onTypeFormatting", parameters, s_json.DocumentOnTypeFormattingParams, token));
    }

    private static IReadOnlyList<Languages.TextEdit>? Edits(JsonElement? result) =>
        Read(result, s_json.LspTextEditArray) is { } edits ? LspConvert.ToTextEdits(edits) : null;

    // ---- colors ----

    public async Task<IReadOnlyList<ColorInformation>?> ProvideDocumentColors(TextModel model, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var result = Read(
            await Request(connection, "textDocument/documentColor", new TextDocumentParams(document.Identifier), s_json.TextDocumentParams, token),
            s_json.LspColorInformationArray);
        return result?.Select(static color => new ColorInformation(LspConvert.ToRange(color.Range), LspConvert.ToColor(color.Color))).ToList();
    }

    public async Task<IReadOnlyList<ColorPresentation>?> ProvideColorPresentations(TextModel model, ColorInformation colorInfo, CancellationToken token)
    {
        if (Target(model) is not ({ } document, { } connection))
        {
            return null;
        }
        var parameters = new ColorPresentationParams(document.Identifier, LspConvert.FromColor(colorInfo.Color), LspConvert.FromRange(colorInfo.Range));
        var result = Read(await Request(connection, "textDocument/colorPresentation", parameters, s_json.ColorPresentationParams, token), s_json.LspColorPresentationArray);
        return result?.Select(LspConvert.ToColorPresentation).ToList();
    }
}

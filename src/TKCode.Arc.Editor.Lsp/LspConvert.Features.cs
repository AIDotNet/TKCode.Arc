// Conversions of the language feature results of the Language Server Protocol into the engine's languages.ts types.
// The basics (positions, markup, edits) are in LspConvert.cs.

using System.Text.Json;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;

namespace TKCode.Arc.Editor.Lsp;

internal static partial class LspConvert
{
    // ---- completion ----

    // LSP CompletionItemKind (1-based, Text = 1) in the engine's order.
    private static readonly CompletionItemKind[] s_completionKinds =
    [
        CompletionItemKind.Text, CompletionItemKind.Method, CompletionItemKind.Function, CompletionItemKind.Constructor,
        CompletionItemKind.Field, CompletionItemKind.Variable, CompletionItemKind.Class, CompletionItemKind.Interface,
        CompletionItemKind.Module, CompletionItemKind.Property, CompletionItemKind.Unit, CompletionItemKind.Value,
        CompletionItemKind.Enum, CompletionItemKind.Keyword, CompletionItemKind.Snippet, CompletionItemKind.Color,
        CompletionItemKind.File, CompletionItemKind.Reference, CompletionItemKind.Folder, CompletionItemKind.EnumMember,
        CompletionItemKind.Constant, CompletionItemKind.Struct, CompletionItemKind.Event, CompletionItemKind.Operator,
        CompletionItemKind.TypeParameter,
    ];

    /// <summary>An LSP completion item kind; an unknown or missing one is a property, as upstream's language client.</summary>
    public static CompletionItemKind ToCompletionItemKind(int? kind) =>
        kind is >= 1 and <= 25 ? s_completionKinds[kind.Value - 1] : CompletionItemKind.Property;

    public static int FromCompletionTriggerKind(CompletionTriggerKind kind) => kind switch
    {
        CompletionTriggerKind.TriggerCharacter => 2,
        CompletionTriggerKind.TriggerForIncompleteCompletions => 3,
        _ => 1,
    };

    /// <summary>
    /// A completion item; <paramref name="defaultRange"/> applies when neither the item nor the list's defaults give an
    /// edit range (the word before the position).
    /// </summary>
    public static CompletionItem ToCompletionItem(LspCompletionItem item, LspCompletionItemDefaults? defaults, CompletionItemRanges defaultRange)
    {
        var label = new CompletionItemLabel(item.Label)
        {
            Detail = item.LabelDetails?.Detail,
            Description = item.LabelDetails?.Description,
        };
        var deprecated = item.Deprecated == true || (item.Tags is not null && item.Tags.Contains(1));
        var format = item.InsertTextFormat ?? defaults?.InsertTextFormat;

        CompletionItemRanges range;
        string insertText;
        if (item.TextEdit is { ValueKind: JsonValueKind.Object } textEdit)
        {
            insertText = GetString(textEdit, "newText") ?? item.Label;
            range = ToEditRange(textEdit) ?? defaultRange;
        }
        else if (defaults?.EditRange is { ValueKind: JsonValueKind.Object } editRange)
        {
            insertText = item.TextEditText ?? item.InsertText ?? item.Label;
            range = ToEditRange(editRange) ?? defaultRange;
        }
        else
        {
            insertText = item.InsertText ?? item.Label;
            range = defaultRange;
        }

        return new CompletionItem
        {
            Label = label,
            Kind = ToCompletionItemKind(item.Kind),
            Tags = deprecated ? [CompletionItemTag.Deprecated] : null,
            Detail = item.Detail,
            Documentation = ToDocumentation(item.Documentation),
            SortText = item.SortText,
            FilterText = item.FilterText,
            Preselect = item.Preselect == true,
            InsertText = insertText,
            InsertTextRules = format == 2 ? CompletionItemInsertTextRule.InsertAsSnippet : CompletionItemInsertTextRule.None,
            Range = range,
            CommitCharacters = item.CommitCharacters ?? defaults?.CommitCharacters,
            AdditionalTextEdits = item.AdditionalTextEdits?.Select(ToEditOperation).ToList(),
            Command = ToCommand(item.Command),
        };
    }

    // TextEdit { range } | InsertReplaceEdit { insert, replace } | Range | { insert, replace }.
    private static CompletionItemRanges? ToEditRange(JsonElement element)
    {
        if (element.TryGetProperty("insert", out var insert) && element.TryGetProperty("replace", out var replace))
        {
            var insertRange = insert.Deserialize(s_json.LspRange);
            var replaceRange = replace.Deserialize(s_json.LspRange);
            if (insertRange is not null && replaceRange is not null)
            {
                return new CompletionItemRanges(ToRange(insertRange), ToRange(replaceRange));
            }
            return null;
        }
        if (element.TryGetProperty("range", out var rangeElement))
        {
            return rangeElement.Deserialize(s_json.LspRange) is { } range ? ToRange(range) : null;
        }
        if (element.TryGetProperty("start", out _))
        {
            return element.Deserialize(s_json.LspRange) is { } range ? ToRange(range) : null;
        }
        return null;
    }

    // ---- hover, signature help ----

    public static Hover? ToHover(LspHover? hover)
    {
        if (hover is null)
        {
            return null;
        }
        var contents = ToMarkdownStrings(hover.Contents);
        if (contents.Count == 0)
        {
            return null;
        }
        return new Hover(contents) { Range = hover.Range is { } range ? ToRange(range) : null };
    }

    public static SignatureHelp ToSignatureHelp(LspSignatureHelp help)
    {
        var signatures = help.Signatures.Select(static signature => new SignatureInformation(
            signature.Label,
            signature.Parameters?.Select(static parameter =>
            {
                var info = new ParameterInformation { Documentation = ToDocumentation(parameter.Documentation) };
                if (parameter.Label.ValueKind == JsonValueKind.String)
                {
                    return info with { Label = parameter.Label.GetString() };
                }
                if (parameter.Label.ValueKind == JsonValueKind.Array && parameter.Label.GetArrayLength() == 2
                    && parameter.Label[0].TryGetInt32(out var start) && parameter.Label[1].TryGetInt32(out var end))
                {
                    return info with { LabelOffsets = (start, end) };
                }
                return info with { Label = string.Empty };
            }).ToList() ?? [])
        {
            Documentation = ToDocumentation(signature.Documentation),
            ActiveParameter = signature.ActiveParameter,
        }).ToList();
        return new SignatureHelp(signatures, Math.Max(0, help.ActiveSignature ?? 0), Math.Max(0, help.ActiveParameter ?? 0));
    }

    public static LspSignatureHelp FromSignatureHelp(SignatureHelp help) =>
        new(help.Signatures.Select(static signature => new LspSignatureInformation(signature.Label)
        {
            Documentation = FromDocumentation(signature.Documentation),
            ActiveParameter = signature.ActiveParameter,
            Parameters = signature.Parameters.Select(static parameter => new LspParameterInformation(
                parameter.LabelOffsets is { } offsets
                    ? JsonSerializer.SerializeToElement(new[] { offsets.Start, offsets.End }, s_json.Int32Array)
                    : JsonSerializer.SerializeToElement(parameter.Label ?? string.Empty, s_json.String))
            {
                Documentation = FromDocumentation(parameter.Documentation),
            }).ToArray(),
        }).ToArray())
        {
            ActiveSignature = help.ActiveSignature,
            ActiveParameter = help.ActiveParameter,
        };

    // ---- navigation ----

    /// <summary>Location | Location[] | LocationLink[] | null as links.</summary>
    public static IReadOnlyList<LocationLink>? ToLocationLinks(JsonElement result)
    {
        if (IsNull(result))
        {
            return null;
        }
        var links = new List<LocationLink>();
        if (result.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in result.EnumerateArray())
            {
                if (ToLocationLink(item) is { } link)
                {
                    links.Add(link);
                }
            }
        }
        else if (ToLocationLink(result) is { } single)
        {
            links.Add(single);
        }
        return links;
    }

    private static LocationLink? ToLocationLink(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (element.TryGetProperty("targetUri", out _))
        {
            var link = element.Deserialize(s_json.LspLocationLink);
            if (link is null)
            {
                return null;
            }
            return new LocationLink(ToUri(link.TargetUri), ToRange(link.TargetRange))
            {
                TargetSelectionRange = ToRange(link.TargetSelectionRange),
                OriginSelectionRange = link.OriginSelectionRange is { } origin ? ToRange(origin) : null,
            };
        }
        if (element.Deserialize(s_json.LspLocation) is not { } location)
        {
            return null;
        }
        return ToLocation(location);
    }

    public static Location ToLocation(LspLocation location) => new(ToUri(location.Uri), ToRange(location.Range));

    public static IReadOnlyList<DocumentHighlight> ToDocumentHighlights(LspDocumentHighlight[] highlights) =>
        highlights.Select(static highlight => new DocumentHighlight(ToRange(highlight.Range))
        {
            Kind = highlight.Kind switch
            {
                2 => DocumentHighlightKind.Read,
                3 => DocumentHighlightKind.Write,
                _ => DocumentHighlightKind.Text,
            },
        }).ToList();

    /// <summary>DocumentSymbol[] (a tree) or SymbolInformation[] (flat, with container names).</summary>
    public static IReadOnlyList<DocumentSymbol>? ToDocumentSymbols(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var symbols = new List<DocumentSymbol>();
        foreach (var item in result.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (item.TryGetProperty("location", out _))
            {
                if (item.Deserialize(s_json.LspSymbolInformation) is { } information)
                {
                    var range = ToRange(information.Location.Range);
                    symbols.Add(new DocumentSymbol(information.Name, string.Empty, ToSymbolKind(information.Kind), range, range)
                    {
                        Tags = ToSymbolTags(information.Tags, information.Deprecated),
                        ContainerName = information.ContainerName,
                    });
                }
            }
            else if (item.Deserialize(s_json.LspDocumentSymbol) is { } symbol)
            {
                symbols.Add(ToDocumentSymbol(symbol));
            }
        }
        return symbols;
    }

    private static DocumentSymbol ToDocumentSymbol(LspDocumentSymbol symbol) =>
        new(symbol.Name, symbol.Detail ?? string.Empty, ToSymbolKind(symbol.Kind), ToRange(symbol.Range), ToRange(symbol.SelectionRange))
        {
            Tags = ToSymbolTags(symbol.Tags, symbol.Deprecated),
            Children = symbol.Children?.Select(ToDocumentSymbol).ToList(),
        };

    // LSP SymbolKind is 1-based (File = 1); the engine's is 0-based in the same order.
    public static SymbolKind ToSymbolKind(int kind) => kind is >= 1 and <= 26 ? (SymbolKind)(kind - 1) : SymbolKind.Property;

    private static IReadOnlyList<SymbolTag> ToSymbolTags(int[]? tags, bool? deprecated) =>
        deprecated == true || (tags is not null && tags.Contains(1)) ? [SymbolTag.Deprecated] : [];

    public static IReadOnlyList<FoldingRange> ToFoldingRanges(LspFoldingRange[] ranges) =>
        ranges
            .Where(static range => range.EndLine >= range.StartLine && range.StartLine >= 0)
            .Select(static range => new FoldingRange(range.StartLine + 1, range.EndLine + 1)
            {
                Kind = range.Kind is { } kind ? FoldingRangeKind.FromValue(kind) : null,
            })
            .ToList();

    /// <summary>One selection range chain per position, innermost first.</summary>
    public static IReadOnlyList<IReadOnlyList<SelectionRange>> ToSelectionRanges(LspSelectionRange[] ranges)
    {
        var result = new List<IReadOnlyList<SelectionRange>>(ranges.Length);
        foreach (var range in ranges)
        {
            var chain = new List<SelectionRange>();
            for (var current = range; current is not null; current = current.Parent)
            {
                chain.Add(new SelectionRange(ToRange(current.Range)));
            }
            result.Add(chain);
        }
        return result;
    }

    public static LinkedEditingRanges ToLinkedEditingRanges(LspLinkedEditingRanges ranges)
    {
        Regex? wordPattern = null;
        if (!string.IsNullOrEmpty(ranges.WordPattern))
        {
            try
            {
                wordPattern = new Regex(ranges.WordPattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                // A JavaScript pattern .NET does not read: the language's word definition applies.
            }
        }
        return new LinkedEditingRanges(ranges.Ranges.Select(ToRange).ToList()) { WordPattern = wordPattern };
    }

    // ---- editing ----

    /// <summary>A code action, or a bare command (which the engine shows as an action that runs it).</summary>
    public static CodeAction? ToCodeAction(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (element.TryGetProperty("command", out var command) && command.ValueKind == JsonValueKind.String)
        {
            var bare = element.Deserialize(s_json.LspCommand);
            return bare is null ? null : new CodeAction(bare.Title) { Command = ToCommand(bare) };
        }
        var action = element.Deserialize(s_json.LspCodeAction);
        if (action is null)
        {
            return null;
        }
        return new CodeAction(action.Title)
        {
            Kind = action.Kind,
            IsPreferred = action.IsPreferred == true,
            Disabled = action.Disabled?.Reason,
            Diagnostics = action.Diagnostics?.Select(ToMarkerData).ToList(),
            Edit = action.Edit is null ? null : ToWorkspaceEdit(action.Edit),
            Command = ToCommand(action.Command),
        };
    }

    public static CodeLens ToCodeLens(LspCodeLens lens) => new(ToRange(lens.Range)) { Command = ToCommand(lens.Command) };

    public static LanguageColor ToColor(LspColor color) => new(color.Red, color.Green, color.Blue, color.Alpha);

    public static LspColor FromColor(LanguageColor color) => new(color.Red, color.Green, color.Blue, color.Alpha);

    public static ColorPresentation ToColorPresentation(LspColorPresentation presentation) =>
        new(presentation.Label)
        {
            TextEdit = presentation.TextEdit is { } edit ? ToTextEdit(edit) : null,
            AdditionalTextEdits = presentation.AdditionalTextEdits?.Select(ToTextEdit).ToList(),
        };

    // ---- semantic tokens, inlay hints ----

    public static SemanticTokens ToSemanticTokens(LspSemanticTokens tokens) => new(tokens.Data) { ResultId = tokens.ResultId };

    public static SemanticTokensEdits ToSemanticTokensEdits(LspSemanticTokensDelta delta) =>
        new(delta.Edits.Select(static edit => new SemanticTokensEdit(edit.Start, edit.DeleteCount) { Data = edit.Data }).ToList())
        {
            ResultId = delta.ResultId,
        };

    public static InlayHint ToInlayHint(LspInlayHint hint)
    {
        string? label = null;
        IReadOnlyList<InlayHintLabelPart>? parts = null;
        if (hint.Label.ValueKind == JsonValueKind.String)
        {
            label = hint.Label.GetString();
        }
        else if (hint.Label.ValueKind == JsonValueKind.Array)
        {
            parts = hint.Label.Deserialize(s_json.LspInlayHintLabelPartArray)?.Select(static part => new InlayHintLabelPart(part.Value)
            {
                Tooltip = ToDocumentation(part.Tooltip),
                Command = ToCommand(part.Command),
                Location = part.Location is { } location ? ToLocation(location) : null,
            }).ToList();
        }
        return new InlayHint(ToPosition(hint.Position))
        {
            Label = label ?? (parts is null ? string.Empty : null),
            LabelParts = parts,
            Tooltip = ToDocumentation(hint.Tooltip),
            TextEdits = hint.TextEdits?.Select(ToTextEdit).ToList(),
            Kind = hint.Kind switch
            {
                1 => InlayHintKind.Type,
                2 => InlayHintKind.Parameter,
                _ => null,
            },
            PaddingLeft = hint.PaddingLeft == true,
            PaddingRight = hint.PaddingRight == true,
        };
    }
}

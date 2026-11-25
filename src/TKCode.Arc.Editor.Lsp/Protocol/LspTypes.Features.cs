// The Language Server Protocol's language feature structures (specification 3.17) this client sends or reads. The
// conventions are in LspTypes.cs.

using System.Text.Json;

namespace TKCode.Arc.Editor.Lsp.Protocol;

// ---- completion ----

internal sealed class LspCompletionContext(int triggerKind)
{
    public int TriggerKind { get; } = triggerKind;

    public string? TriggerCharacter { get; init; }
}

internal sealed class CompletionParams(TextDocumentIdentifier textDocument, LspPosition position)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition Position { get; } = position;

    public LspCompletionContext? Context { get; init; }
}

internal sealed class LspCompletionItemLabelDetails
{
    public string? Detail { get; init; }

    public string? Description { get; init; }
}

internal sealed record LspCompletionItem(string Label)
{
    public LspCompletionItemLabelDetails? LabelDetails { get; init; }

    public int? Kind { get; init; }

    public int[]? Tags { get; init; }

    public string? Detail { get; init; }

    /// <summary>string | MarkupContent.</summary>
    public LspStringOrMarkup? Documentation { get; init; }

    public bool? Deprecated { get; init; }

    public bool? Preselect { get; init; }

    public string? SortText { get; init; }

    public string? FilterText { get; init; }

    public string? InsertText { get; init; }

    public int? InsertTextFormat { get; init; }

    /// <summary>TextEdit | InsertReplaceEdit.</summary>
    public LspCompletionTextEdit? TextEdit { get; init; }

    public string? TextEditText { get; init; }

    public LspTextEdit[]? AdditionalTextEdits { get; init; }

    public string[]? CommitCharacters { get; init; }

    public LspCommand? Command { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed record LspCompletionItemDefaults
{
    public string[]? CommitCharacters { get; init; }

    /// <summary>Range | { insert, replace }.</summary>
    public LspEditRange? EditRange { get; init; }

    public int? InsertTextFormat { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed class LspCompletionList(bool isIncomplete, LspCompletionItem[] items)
{
    public bool IsIncomplete { get; } = isIncomplete;

    public LspCompletionItem[] Items { get; } = items;

    public LspCompletionItemDefaults? ItemDefaults { get; init; }
}

// ---- hover, signature help ----

internal sealed class LspHover(LspHoverContents contents)
{
    public LspHoverContents Contents { get; } = contents;

    public LspRange? Range { get; init; }
}

internal sealed class LspParameterInformation(LspParameterLabel label)
{
    public LspParameterLabel Label { get; } = label;

    public LspStringOrMarkup? Documentation { get; init; }
}

internal sealed class LspSignatureInformation(string label)
{
    public string Label { get; } = label;

    public LspStringOrMarkup? Documentation { get; init; }

    public LspParameterInformation[]? Parameters { get; init; }

    public int? ActiveParameter { get; init; }
}

internal sealed class LspSignatureHelp(LspSignatureInformation[] signatures)
{
    public LspSignatureInformation[] Signatures { get; } = signatures;

    public int? ActiveSignature { get; init; }

    public int? ActiveParameter { get; init; }
}

internal sealed class LspSignatureHelpContext(int triggerKind, bool isRetrigger)
{
    public int TriggerKind { get; } = triggerKind;

    public bool IsRetrigger { get; } = isRetrigger;

    public string? TriggerCharacter { get; init; }

    public LspSignatureHelp? ActiveSignatureHelp { get; init; }
}

internal sealed class SignatureHelpParams(TextDocumentIdentifier textDocument, LspPosition position)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition Position { get; } = position;

    public LspSignatureHelpContext? Context { get; init; }
}

// ---- navigation ----

internal sealed class LspReferenceContext(bool includeDeclaration)
{
    public bool IncludeDeclaration { get; } = includeDeclaration;
}

internal sealed class ReferenceParams(TextDocumentIdentifier textDocument, LspPosition position, LspReferenceContext context)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition Position { get; } = position;

    public LspReferenceContext Context { get; } = context;
}

internal sealed class LspDocumentHighlight(LspRange range)
{
    public LspRange Range { get; } = range;

    public int? Kind { get; init; }
}

internal sealed class LspLinkedEditingRanges(LspRange[] ranges)
{
    public LspRange[] Ranges { get; } = ranges;

    public string? WordPattern { get; init; }
}

internal sealed class LspDocumentSymbol(string name, int kind, LspRange range, LspRange selectionRange)
{
    public string Name { get; } = name;

    public int Kind { get; } = kind;

    public LspRange Range { get; } = range;

    public LspRange SelectionRange { get; } = selectionRange;

    public string? Detail { get; init; }

    public int[]? Tags { get; init; }

    public bool? Deprecated { get; init; }

    public LspDocumentSymbol[]? Children { get; init; }
}

internal sealed class LspSymbolInformation(string name, int kind, LspLocation location)
{
    public string Name { get; } = name;

    public int Kind { get; } = kind;

    public LspLocation Location { get; } = location;

    public int[]? Tags { get; init; }

    public bool? Deprecated { get; init; }

    public string? ContainerName { get; init; }
}

internal sealed class SelectionRangeParams(TextDocumentIdentifier textDocument, LspPosition[] positions)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition[] Positions { get; } = positions;
}

internal sealed class LspSelectionRange(LspRange range)
{
    public LspRange Range { get; } = range;

    public LspSelectionRange? Parent { get; init; }
}

internal sealed class LspFoldingRange(int startLine, int endLine)
{
    public int StartLine { get; } = startLine;

    public int EndLine { get; } = endLine;

    public int? StartCharacter { get; init; }

    public int? EndCharacter { get; init; }

    public string? Kind { get; init; }
}

// ---- editing ----

internal sealed class LspCodeActionContext(JsonElement[] diagnostics)
{
    public JsonElement[] Diagnostics { get; } = diagnostics;

    public string[]? Only { get; init; }

    public int? TriggerKind { get; init; }
}

internal sealed class CodeActionParams(TextDocumentIdentifier textDocument, LspRange range, LspCodeActionContext context)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspRange Range { get; } = range;

    public LspCodeActionContext Context { get; } = context;
}

internal sealed class LspCodeActionDisabled(string reason)
{
    public string Reason { get; } = reason;
}

internal sealed class LspCodeAction(string title)
{
    public string Title { get; } = title;

    public string? Kind { get; init; }

    public LspDiagnostic[]? Diagnostics { get; init; }

    public bool? IsPreferred { get; init; }

    public LspCodeActionDisabled? Disabled { get; init; }

    public LspWorkspaceEdit? Edit { get; init; }

    public LspCommand? Command { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed class RenameParams(TextDocumentIdentifier textDocument, LspPosition position, string newName)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition Position { get; } = position;

    public string NewName { get; } = newName;
}

internal sealed class LspPrepareRenamePlaceholder(LspRange range, string? placeholder)
{
    public LspRange Range { get; } = range;

    public string? Placeholder { get; } = placeholder;
}

internal sealed class LspPrepareRenameDefaultBehavior(bool defaultBehavior)
{
    public bool DefaultBehavior { get; } = defaultBehavior;
}

internal sealed class LspFormattingOptions(int tabSize, bool insertSpaces)
{
    public int TabSize { get; } = tabSize;

    public bool InsertSpaces { get; } = insertSpaces;
}

internal sealed class DocumentFormattingParams(TextDocumentIdentifier textDocument, LspFormattingOptions options)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspFormattingOptions Options { get; } = options;
}

internal sealed class DocumentRangeFormattingParams(TextDocumentIdentifier textDocument, LspRange range, LspFormattingOptions options)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspRange Range { get; } = range;

    public LspFormattingOptions Options { get; } = options;
}

internal sealed class DocumentRangesFormattingParams(TextDocumentIdentifier textDocument, LspRange[] ranges, LspFormattingOptions options)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspRange[] Ranges { get; } = ranges;

    public LspFormattingOptions Options { get; } = options;
}

internal sealed class DocumentOnTypeFormattingParams(TextDocumentIdentifier textDocument, LspPosition position, string ch, LspFormattingOptions options)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition Position { get; } = position;

    public string Ch { get; } = ch;

    public LspFormattingOptions Options { get; } = options;
}

internal sealed class LspColor(double red, double green, double blue, double alpha)
{
    public double Red { get; } = red;

    public double Green { get; } = green;

    public double Blue { get; } = blue;

    public double Alpha { get; } = alpha;
}

internal sealed class LspColorInformation(LspRange range, LspColor color)
{
    public LspRange Range { get; } = range;

    public LspColor Color { get; } = color;
}

internal sealed class ColorPresentationParams(TextDocumentIdentifier textDocument, LspColor color, LspRange range)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspColor Color { get; } = color;

    public LspRange Range { get; } = range;
}

internal sealed class LspColorPresentation(string label)
{
    public string Label { get; } = label;

    public LspTextEdit? TextEdit { get; init; }

    public LspTextEdit[]? AdditionalTextEdits { get; init; }
}

// ---- code lens, semantic tokens, inlay hints ----

internal sealed class LspCodeLens(LspRange range)
{
    public LspRange Range { get; } = range;

    public LspCommand? Command { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed class SemanticTokensDeltaParams(TextDocumentIdentifier textDocument, string previousResultId)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public string PreviousResultId { get; } = previousResultId;
}

internal sealed class SemanticTokensRangeParams(TextDocumentIdentifier textDocument, LspRange range)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspRange Range { get; } = range;
}

internal sealed class LspSemanticTokens(uint[] data)
{
    public uint[] Data { get; } = data;

    public string? ResultId { get; init; }
}

internal sealed class LspSemanticTokensEdit(int start, int deleteCount)
{
    public int Start { get; } = start;

    public int DeleteCount { get; } = deleteCount;

    public uint[]? Data { get; init; }
}

internal sealed class LspSemanticTokensDelta(LspSemanticTokensEdit[] edits)
{
    public LspSemanticTokensEdit[] Edits { get; } = edits;

    public string? ResultId { get; init; }
}

internal sealed class InlayHintParams(TextDocumentIdentifier textDocument, LspRange range)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspRange Range { get; } = range;
}

internal sealed class LspInlayHintLabelPart(string value)
{
    public string Value { get; } = value;

    public LspStringOrMarkup? Tooltip { get; init; }

    public LspLocation? Location { get; init; }

    public LspCommand? Command { get; init; }
}

internal sealed class LspInlayHint(LspPosition position, LspInlayHintLabel label)
{
    public LspPosition Position { get; } = position;

    public LspInlayHintLabel Label { get; } = label;

    public int? Kind { get; init; }

    public LspTextEdit[]? TextEdits { get; init; }

    public LspStringOrMarkup? Tooltip { get; init; }

    public bool? PaddingLeft { get; init; }

    public bool? PaddingRight { get; init; }

    public JsonElement? Data { get; init; }
}

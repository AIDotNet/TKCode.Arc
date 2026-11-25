// The Language Server Protocol's language feature structures (specification 3.17) this client sends or reads. The
// conventions are in LspTypes.cs.

using System.Text.Json;

namespace TKCode.Arc.Editor.Lsp.Protocol;

// ---- completion ----

internal sealed record LspCompletionContext(int TriggerKind)
{
    public string? TriggerCharacter { get; init; }
}

internal sealed record CompletionParams(TextDocumentIdentifier TextDocument, LspPosition Position)
{
    public LspCompletionContext? Context { get; init; }
}

internal sealed record LspCompletionItemLabelDetails
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
    public JsonElement? Documentation { get; init; }

    public bool? Deprecated { get; init; }

    public bool? Preselect { get; init; }

    public string? SortText { get; init; }

    public string? FilterText { get; init; }

    public string? InsertText { get; init; }

    public int? InsertTextFormat { get; init; }

    /// <summary>TextEdit | InsertReplaceEdit.</summary>
    public JsonElement? TextEdit { get; init; }

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
    public JsonElement? EditRange { get; init; }

    public int? InsertTextFormat { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed record LspCompletionList(bool IsIncomplete, LspCompletionItem[] Items)
{
    public LspCompletionItemDefaults? ItemDefaults { get; init; }
}

// ---- hover, signature help ----

internal sealed record LspHover(JsonElement Contents)
{
    public LspRange? Range { get; init; }
}

internal sealed record LspParameterInformation(JsonElement Label)
{
    public JsonElement? Documentation { get; init; }
}

internal sealed record LspSignatureInformation(string Label)
{
    public JsonElement? Documentation { get; init; }

    public LspParameterInformation[]? Parameters { get; init; }

    public int? ActiveParameter { get; init; }
}

internal sealed record LspSignatureHelp(LspSignatureInformation[] Signatures)
{
    public int? ActiveSignature { get; init; }

    public int? ActiveParameter { get; init; }
}

internal sealed record LspSignatureHelpContext(int TriggerKind, bool IsRetrigger)
{
    public string? TriggerCharacter { get; init; }

    public LspSignatureHelp? ActiveSignatureHelp { get; init; }
}

internal sealed record SignatureHelpParams(TextDocumentIdentifier TextDocument, LspPosition Position)
{
    public LspSignatureHelpContext? Context { get; init; }
}

// ---- navigation ----

internal sealed record LspReferenceContext(bool IncludeDeclaration);

internal sealed record ReferenceParams(TextDocumentIdentifier TextDocument, LspPosition Position, LspReferenceContext Context);

internal sealed record LspDocumentHighlight(LspRange Range)
{
    public int? Kind { get; init; }
}

internal sealed record LspLinkedEditingRanges(LspRange[] Ranges)
{
    public string? WordPattern { get; init; }
}

internal sealed record LspDocumentSymbol(string Name, int Kind, LspRange Range, LspRange SelectionRange)
{
    public string? Detail { get; init; }

    public int[]? Tags { get; init; }

    public bool? Deprecated { get; init; }

    public LspDocumentSymbol[]? Children { get; init; }
}

internal sealed record LspSymbolInformation(string Name, int Kind, LspLocation Location)
{
    public int[]? Tags { get; init; }

    public bool? Deprecated { get; init; }

    public string? ContainerName { get; init; }
}

internal sealed record SelectionRangeParams(TextDocumentIdentifier TextDocument, LspPosition[] Positions);

internal sealed record LspSelectionRange(LspRange Range)
{
    public LspSelectionRange? Parent { get; init; }
}

internal sealed record LspFoldingRange(int StartLine, int EndLine)
{
    public int? StartCharacter { get; init; }

    public int? EndCharacter { get; init; }

    public string? Kind { get; init; }
}

// ---- editing ----

internal sealed record LspCodeActionContext(JsonElement[] Diagnostics)
{
    public string[]? Only { get; init; }

    public int? TriggerKind { get; init; }
}

internal sealed record CodeActionParams(TextDocumentIdentifier TextDocument, LspRange Range, LspCodeActionContext Context);

internal sealed record LspCodeActionDisabled(string Reason);

internal sealed record LspCodeAction(string Title)
{
    public string? Kind { get; init; }

    public LspDiagnostic[]? Diagnostics { get; init; }

    public bool? IsPreferred { get; init; }

    public LspCodeActionDisabled? Disabled { get; init; }

    public LspWorkspaceEdit? Edit { get; init; }

    public LspCommand? Command { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed record RenameParams(TextDocumentIdentifier TextDocument, LspPosition Position, string NewName);

internal sealed record LspFormattingOptions(int TabSize, bool InsertSpaces);

internal sealed record DocumentFormattingParams(TextDocumentIdentifier TextDocument, LspFormattingOptions Options);

internal sealed record DocumentRangeFormattingParams(TextDocumentIdentifier TextDocument, LspRange Range, LspFormattingOptions Options);

internal sealed record DocumentRangesFormattingParams(TextDocumentIdentifier TextDocument, LspRange[] Ranges, LspFormattingOptions Options);

internal sealed record DocumentOnTypeFormattingParams(TextDocumentIdentifier TextDocument, LspPosition Position, string Ch, LspFormattingOptions Options);

internal sealed record LspColor(double Red, double Green, double Blue, double Alpha);

internal sealed record LspColorInformation(LspRange Range, LspColor Color);

internal sealed record ColorPresentationParams(TextDocumentIdentifier TextDocument, LspColor Color, LspRange Range);

internal sealed record LspColorPresentation(string Label)
{
    public LspTextEdit? TextEdit { get; init; }

    public LspTextEdit[]? AdditionalTextEdits { get; init; }
}

// ---- code lens, semantic tokens, inlay hints ----

internal sealed record LspCodeLens(LspRange Range)
{
    public LspCommand? Command { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed record SemanticTokensDeltaParams(TextDocumentIdentifier TextDocument, string PreviousResultId);

internal sealed record SemanticTokensRangeParams(TextDocumentIdentifier TextDocument, LspRange Range);

internal sealed record LspSemanticTokens(uint[] Data)
{
    public string? ResultId { get; init; }
}

internal sealed record LspSemanticTokensEdit(int Start, int DeleteCount)
{
    public uint[]? Data { get; init; }
}

internal sealed record LspSemanticTokensDelta(LspSemanticTokensEdit[] Edits)
{
    public string? ResultId { get; init; }
}

internal sealed record LspSemanticTokensLegend(string[] TokenTypes, string[] TokenModifiers);

internal sealed record InlayHintParams(TextDocumentIdentifier TextDocument, LspRange Range);

internal sealed record LspInlayHintLabelPart(string Value)
{
    public JsonElement? Tooltip { get; init; }

    public LspLocation? Location { get; init; }

    public LspCommand? Command { get; init; }
}

internal sealed record LspInlayHint(LspPosition Position, JsonElement Label)
{
    public int? Kind { get; init; }

    public LspTextEdit[]? TextEdits { get; init; }

    public JsonElement? Tooltip { get; init; }

    public bool? PaddingLeft { get; init; }

    public bool? PaddingRight { get; init; }

    public JsonElement? Data { get; init; }
}

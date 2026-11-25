// The Language Server Protocol's structures (specification 3.17, https://microsoft.github.io/language-server-protocol/)
// that this client sends or reads: base types, lifecycle, document sync, diagnostics, window and workspace messages.
// Written for this package. Conventions:
//  - names carry an Lsp prefix where the engine has a type of the same name (LspRange next to TextRange);
//  - a union the source generator cannot express (string | MarkupContent, Location | Location[] | LocationLink[], ...)
//    is a JsonElement that LspConvert reads; values the client sends back untouched (data, a diagnostic, an item to
//    resolve) are kept as JsonElement too;
//  - properties are camelCase and omitted when null (LspJsonContext); the few the specification requires as null are
//    marked JsonIgnoreCondition.Never.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKCode.Arc.Editor.Lsp.Protocol;

internal sealed record LspPosition(int Line, int Character);

internal sealed record LspRange(LspPosition Start, LspPosition End);

internal sealed record LspLocation(string Uri, LspRange Range);

internal sealed record LspLocationLink(string TargetUri, LspRange TargetRange, LspRange TargetSelectionRange)
{
    public LspRange? OriginSelectionRange { get; init; }
}

internal sealed record LspTextEdit(LspRange Range, string NewText);

internal sealed record LspInsertReplaceEdit(string NewText, LspRange Insert, LspRange Replace);

internal sealed record LspCommand(string Title, string Command)
{
    public JsonElement[]? Arguments { get; init; }
}

internal sealed record LspMarkupContent(string Kind, string Value);

internal sealed record TextDocumentIdentifier(string Uri);

internal sealed record VersionedTextDocumentIdentifier(string Uri, int Version);

internal sealed record OptionalVersionedTextDocumentIdentifier(string Uri)
{
    public int? Version { get; init; }
}

internal sealed record TextDocumentItem(string Uri, string LanguageId, int Version, string Text);

internal sealed record TextDocumentPositionParams(TextDocumentIdentifier TextDocument, LspPosition Position);

internal sealed record TextDocumentParams(TextDocumentIdentifier TextDocument);

// ---- lifecycle ----

internal sealed record ClientInfo(string Name)
{
    public string? Version { get; init; }
}

internal sealed record WorkspaceFolder(string Uri, string Name);

internal sealed record InitializeParams
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? ProcessId { get; init; }

    public ClientInfo? ClientInfo { get; init; }

    public string? Locale { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? RootUri { get; init; }

    public string? RootPath { get; init; }

    public JsonElement Capabilities { get; init; }

    public JsonElement? InitializationOptions { get; init; }

    public string? Trace { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public WorkspaceFolder[]? WorkspaceFolders { get; init; }
}

internal sealed record ServerInfo(string Name)
{
    public string? Version { get; init; }
}

internal sealed record InitializeResult
{
    public JsonElement Capabilities { get; init; }

    public ServerInfo? ServerInfo { get; init; }
}

internal sealed record InitializedParams;

// ---- document sync ----

internal sealed record DidOpenTextDocumentParams(TextDocumentItem TextDocument);

internal sealed record TextDocumentContentChangeEvent(string Text)
{
    public LspRange? Range { get; init; }

    public int? RangeLength { get; init; }
}

internal sealed record DidChangeTextDocumentParams(VersionedTextDocumentIdentifier TextDocument, TextDocumentContentChangeEvent[] ContentChanges);

internal sealed record DidCloseTextDocumentParams(TextDocumentIdentifier TextDocument);

// ---- diagnostics ----

internal sealed record LspCodeDescription(string Href);

internal sealed record LspDiagnosticRelatedInformation(LspLocation Location, string Message);

internal sealed record LspDiagnostic(LspRange Range, string Message)
{
    public int? Severity { get; init; }

    public JsonElement? Code { get; init; }

    public LspCodeDescription? CodeDescription { get; init; }

    public string? Source { get; init; }

    public int[]? Tags { get; init; }

    public LspDiagnosticRelatedInformation[]? RelatedInformation { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed record PublishDiagnosticsParams(string Uri, LspDiagnostic[] Diagnostics)
{
    public int? Version { get; init; }
}

// ---- window ----

internal sealed record ShowMessageParams(int Type, string Message);

internal sealed record MessageActionItem(string Title);

internal sealed record ShowMessageRequestParams(int Type, string Message)
{
    public MessageActionItem[]? Actions { get; init; }
}

// ---- workspace ----

internal sealed record LspWorkspaceEdit
{
    public Dictionary<string, LspTextEdit[]>? Changes { get; init; }

    /// <summary>(TextDocumentEdit | CreateFile | RenameFile | DeleteFile)[], read by LspConvert.</summary>
    public JsonElement? DocumentChanges { get; init; }
}

internal sealed record LspTextDocumentEdit(OptionalVersionedTextDocumentIdentifier TextDocument, LspTextEdit[] Edits);

internal sealed record ApplyWorkspaceEditParams(LspWorkspaceEdit Edit)
{
    public string? Label { get; init; }
}

internal sealed record ApplyWorkspaceEditResult(bool Applied)
{
    public string? FailureReason { get; init; }

    public int? FailedChange { get; init; }
}

internal sealed record ConfigurationItem
{
    public string? ScopeUri { get; init; }

    public string? Section { get; init; }
}

internal sealed record ConfigurationParams(ConfigurationItem[] Items);

internal sealed record ExecuteCommandParams(string Command)
{
    public JsonElement[]? Arguments { get; init; }
}

internal sealed record CancelParams(int Id);

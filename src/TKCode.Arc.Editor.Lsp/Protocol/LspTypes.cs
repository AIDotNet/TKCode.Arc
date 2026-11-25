// The Language Server Protocol's structures (specification 3.17, https://microsoft.github.io/language-server-protocol/)
// that this client sends or reads: base types, lifecycle, document sync, diagnostics, window and workspace messages.
// Written for this package. Conventions:
//  - names carry an Lsp prefix where the engine has a type of the same name (LspRange next to TextRange);
//  - protocol unions (string | MarkupContent, Location | Location[] | LocationLink[], TextEdit | InsertReplaceEdit, ...)
//    are C# 15 union types in LspUnions.cs; values the client sends back untouched (data, a diagnostic, an item to
//    resolve) remain JsonElement;
//  - DTOs are plain sealed classes unless value equality or `with` cloning is needed, avoiding unused record-synthesized
//    methods in the compiled assembly;
//  - properties are camelCase and omitted when null (LspJsonContext); the few the specification requires as null are
//    marked JsonIgnoreCondition.Never.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKCode.Arc.Editor.Lsp.Protocol;

internal sealed record LspPosition(int Line, int Character);

internal sealed record LspRange(LspPosition Start, LspPosition End);

internal sealed class LspLocation(string uri, LspRange range)
{
    public string Uri { get; } = uri;

    public LspRange Range { get; } = range;
}

internal sealed class LspLocationLink(string targetUri, LspRange targetRange, LspRange targetSelectionRange)
{
    public string TargetUri { get; } = targetUri;

    public LspRange TargetRange { get; } = targetRange;

    public LspRange TargetSelectionRange { get; } = targetSelectionRange;

    public LspRange? OriginSelectionRange { get; init; }
}

internal sealed class LspTextEdit(LspRange range, string newText)
{
    public LspRange Range { get; } = range;

    public string NewText { get; } = newText;
}

internal sealed class LspInsertReplaceEdit(string newText, LspRange insert, LspRange replace)
{
    public string NewText { get; } = newText;

    public LspRange Insert { get; } = insert;

    public LspRange Replace { get; } = replace;
}

internal sealed class LspInsertReplaceRange(LspRange insert, LspRange replace)
{
    public LspRange Insert { get; } = insert;

    public LspRange Replace { get; } = replace;
}

internal sealed class LspCommand(string title, string command)
{
    public string Title { get; } = title;

    public string Command { get; } = command;

    public JsonElement[]? Arguments { get; init; }
}

internal sealed class LspMarkupContent(string kind, string value)
{
    public string Kind { get; } = kind;

    public string Value { get; } = value;
}

internal sealed class LspMarkedStringObject(string language, string value)
{
    public string Language { get; } = language;

    public string Value { get; } = value;
}

internal sealed class TextDocumentIdentifier(string uri)
{
    public string Uri { get; } = uri;
}

internal sealed class VersionedTextDocumentIdentifier(string uri, int version)
{
    public string Uri { get; } = uri;

    public int Version { get; } = version;
}

internal sealed class OptionalVersionedTextDocumentIdentifier(string uri)
{
    public string Uri { get; } = uri;

    public int? Version { get; init; }
}

internal sealed class TextDocumentItem(string uri, string languageId, int version, string text)
{
    public string Uri { get; } = uri;

    public string LanguageId { get; } = languageId;

    public int Version { get; } = version;

    public string Text { get; } = text;
}

internal sealed class TextDocumentPositionParams(TextDocumentIdentifier textDocument, LspPosition position)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspPosition Position { get; } = position;
}

internal sealed class TextDocumentParams(TextDocumentIdentifier textDocument)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;
}

// ---- lifecycle ----

internal sealed class ClientInfo(string name)
{
    public string Name { get; } = name;

    public string? Version { get; init; }
}

internal sealed class WorkspaceFolder(string uri, string name)
{
    public string Uri { get; } = uri;

    public string Name { get; } = name;
}

internal sealed class InitializeParams
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

internal sealed class ServerInfo(string name)
{
    public string Name { get; } = name;

    public string? Version { get; init; }
}

internal sealed class InitializeResult
{
    public JsonElement Capabilities { get; init; }

    public ServerInfo? ServerInfo { get; init; }
}

internal sealed class InitializedParams;

// ---- document sync ----

internal sealed class DidOpenTextDocumentParams(TextDocumentItem textDocument)
{
    public TextDocumentItem TextDocument { get; } = textDocument;
}

internal sealed class TextDocumentContentChangeEvent(string text)
{
    public string Text { get; } = text;

    public LspRange? Range { get; init; }

    public int? RangeLength { get; init; }
}

internal sealed class DidChangeTextDocumentParams(VersionedTextDocumentIdentifier textDocument, TextDocumentContentChangeEvent[] contentChanges)
{
    public VersionedTextDocumentIdentifier TextDocument { get; } = textDocument;

    public TextDocumentContentChangeEvent[] ContentChanges { get; } = contentChanges;
}

internal sealed class DidCloseTextDocumentParams(TextDocumentIdentifier textDocument)
{
    public TextDocumentIdentifier TextDocument { get; } = textDocument;
}

// ---- diagnostics ----

internal sealed class LspCodeDescription(string href)
{
    public string Href { get; } = href;
}

internal sealed class LspDiagnosticRelatedInformation(LspLocation location, string message)
{
    public LspLocation Location { get; } = location;

    public string Message { get; } = message;
}

internal sealed class LspDiagnostic(LspRange range, string message)
{
    public LspRange Range { get; } = range;

    public string Message { get; } = message;

    public int? Severity { get; init; }

    public LspDiagnosticCode? Code { get; init; }

    public LspCodeDescription? CodeDescription { get; init; }

    public string? Source { get; init; }

    public int[]? Tags { get; init; }

    public LspDiagnosticRelatedInformation[]? RelatedInformation { get; init; }

    public JsonElement? Data { get; init; }
}

internal sealed class PublishDiagnosticsParams(string uri, LspDiagnostic[] diagnostics)
{
    public string Uri { get; } = uri;

    public LspDiagnostic[] Diagnostics { get; } = diagnostics;

    public int? Version { get; init; }
}

// ---- window ----

internal sealed class ShowMessageParams(int type, string message)
{
    public int Type { get; } = type;

    public string Message { get; } = message;
}

internal sealed class MessageActionItem(string title)
{
    public string Title { get; } = title;
}

internal sealed class ShowMessageRequestParams(int type, string message)
{
    public int Type { get; } = type;

    public string Message { get; } = message;

    public MessageActionItem[]? Actions { get; init; }
}

// ---- workspace ----

internal sealed class LspWorkspaceEdit
{
    public Dictionary<string, LspTextEdit[]>? Changes { get; init; }

    /// <summary>(TextDocumentEdit | CreateFile | RenameFile | DeleteFile)[].</summary>
    public LspDocumentChange[]? DocumentChanges { get; init; }
}

internal sealed class LspTextDocumentEdit(OptionalVersionedTextDocumentIdentifier textDocument, LspTextEdit[] edits)
{
    public OptionalVersionedTextDocumentIdentifier TextDocument { get; } = textDocument;

    public LspTextEdit[] Edits { get; } = edits;
}

internal sealed class LspResourceOperationOptions
{
    public bool Overwrite { get; init; }

    public bool IgnoreIfExists { get; init; }

    public bool IgnoreIfNotExists { get; init; }

    public bool Recursive { get; init; }
}

internal sealed class LspResourceOperation(string kind)
{
    public string Kind { get; } = kind;

    public string? Uri { get; init; }

    public string? OldUri { get; init; }

    public string? NewUri { get; init; }

    public LspResourceOperationOptions? Options { get; init; }
}

internal sealed class ApplyWorkspaceEditParams(LspWorkspaceEdit edit)
{
    public LspWorkspaceEdit Edit { get; } = edit;

    public string? Label { get; init; }
}

internal sealed class ApplyWorkspaceEditResult(bool applied)
{
    public bool Applied { get; } = applied;

    public string? FailureReason { get; init; }

    public int? FailedChange { get; init; }
}

internal sealed class ExecuteCommandParams(string command)
{
    public string Command { get; } = command;

    public JsonElement[]? Arguments { get; init; }
}

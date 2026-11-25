// Ported from vscode (src/vs/editor/common/languages.ts: Command, TextEdit, Location, LocationLink, WorkspaceEdit and the
// types shared by the language feature providers, commit b57d5c9e), MIT license.
// Differences from upstream (they hold for every LanguageFeatures.*.cs file):
//  - the provider interfaces carry an I prefix (IHoverProvider for upstream's HoverProvider), as .NET interfaces do;
//  - ProviderResult<T> (T | undefined | null | Thenable) is Task<T?>: a provider without a result returns null, and every
//    provider takes a System.Threading.CancellationToken last;
//  - optional provider members (resolveCompletionItem?, onDidChange?, ...) are default interface members: a resolve
//    that is not implemented returns null ("nothing to add"), an event that is not implemented never fires; a provider
//    that implements a resolve says so with the Supports* property next to it where upstream tests for the function;
//  - IRange / IPosition are TextRange / TextPosition, URI is System.Uri, IMarkdownString is MarkdownString;
//    `string | IMarkdownString` is StringOrMarkdown (implicit from either);
//  - languages.Command is LanguageCommand (Command is the platform's registered command in TKCode.Arc.Editor.Platform);
//  - IColor is LanguageColor;
//  - data types are records with init properties; arrays are IReadOnlyList.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Plain text or Markdown (upstream's <c>string | IMarkdownString</c>).</summary>
internal union StringOrMarkdown(string, MarkdownString)
{
    /// <summary>The plain text, or null when this is Markdown.</summary>
    public string? Text => this is string text ? text : null;

    /// <summary>The Markdown, or null when this is plain text.</summary>
    public MarkdownString? Markdown => this is MarkdownString markdown ? markdown : null;

    /// <summary>Whether this is Markdown.</summary>
    public bool IsMarkdown => this is MarkdownString;

    /// <summary>The text whichever form it has (the Markdown source for Markdown).</summary>
    public string Content => this switch
    {
        MarkdownString markdown => markdown.Value,
        string text => text,
        _ => string.Empty,
    };
}

/// <summary>A command a provider attaches to an item (upstream's languages.Command).</summary>
internal sealed record LanguageCommand(string Id, string Title)
{
    public string? Tooltip { get; init; }

    public IReadOnlyList<object?>? Arguments { get; init; }
}

/// <summary>A text edit (upstream's languages.TextEdit).</summary>
internal sealed record TextEdit(TextRange Range, string Text)
{
    public EndOfLineSequence? Eol { get; init; }

    /// <summary>The edit as an edit operation: an insertion when the range is empty (it moves markers), else a replacement.</summary>
    public static SingleEditOperation AsEditOperation(TextEdit edit) =>
        edit.Range.IsEmpty
            ? EditOperation.Insert(edit.Range.Start, edit.Text)
            : EditOperation.Replace(edit.Range, edit.Text);
}

/// <summary>A range of a document (upstream's languages.Location).</summary>
internal sealed record Location(Uri Uri, TextRange Range);

/// <summary>A link from a source range to a target range (upstream's LocationLink).</summary>
internal sealed record LocationLink(Uri Uri, TextRange Range)
{
    /// <summary>The span of the origin of this link; defaults to the word range at the definition position.</summary>
    public TextRange? OriginSelectionRange { get; init; }

    /// <summary>The span that should be selected and revealed when following this link (inside <see cref="Range"/>).</summary>
    public TextRange? TargetSelectionRange { get; init; }

    public static implicit operator LocationLink(Location location) => new(location.Uri, location.Range);
}

/// <summary>A label and options of a workspace edit (upstream's WorkspaceEditMetadata).</summary>
internal sealed record WorkspaceEditMetadata(bool NeedsConfirmation, string Label)
{
    public string? Description { get; init; }
}

/// <summary>Options of a file operation (upstream's WorkspaceFileEditOptions, without the file contents).</summary>
internal sealed record WorkspaceFileEditOptions
{
    public bool Overwrite { get; init; }

    public bool IgnoreIfNotExists { get; init; }

    public bool IgnoreIfExists { get; init; }

    public bool Recursive { get; init; }

    public bool Copy { get; init; }

    public bool Folder { get; init; }

    public bool SkipTrashBin { get; init; }

    public int? MaxSize { get; init; }
}

/// <summary>One edit of a <see cref="WorkspaceEdit"/>.</summary>
internal abstract record WorkspaceEditEntry
{
    public WorkspaceEditMetadata? Metadata { get; init; }
}

/// <summary>A text edit of one document (upstream's IWorkspaceTextEdit).</summary>
internal sealed record WorkspaceTextEdit(Uri Resource, TextEdit TextEdit) : WorkspaceEditEntry
{
    /// <summary>The text is a snippet.</summary>
    public bool InsertAsSnippet { get; init; }

    public bool KeepWhitespace { get; init; }

    /// <summary>The model version the edit was computed for, or null for any.</summary>
    public int? VersionId { get; init; }
}

/// <summary>Creates, renames or deletes a file (upstream's IWorkspaceFileEdit).</summary>
internal sealed record WorkspaceFileEdit(Uri? OldResource, Uri? NewResource) : WorkspaceEditEntry
{
    public WorkspaceFileEditOptions? Options { get; init; }
}

/// <summary>Edits across documents (upstream's WorkspaceEdit; ICustomEdit is not ported).</summary>
internal sealed class WorkspaceEdit(IReadOnlyList<WorkspaceEditEntry> edits)
{
    public IReadOnlyList<WorkspaceEditEntry> Edits { get; } = edits;
}

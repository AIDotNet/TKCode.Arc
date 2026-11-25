// Adaptation of vscode (src/vs/editor/browser/services/bulkEditService.ts: ResourceEdit.convert, and the model checks of
// src/vs/workbench/contrib/bulkEdit/browser/bulkTextEdits.ts: the version check and the edit operations of one model,
// commit b57d5c9e), MIT license.
// Differences from upstream: there is no bulk edit service and no workspace. An editor applies the text edits of its
// own model (one undo step) and hands everything else (other documents, file operations) to its host; this splits a
// workspace edit that way and checks the model version. Snippet edits (insertAsSnippet) are inserted as plain text.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib;

/// <summary>A workspace edit split into the edits of one model and the rest.</summary>
/// <param name="modelEdits">The text edits of the model, in the order the workspace edit lists them.</param>
/// <param name="other">The edits of other documents and the file operations, or null when there are none.</param>
/// <param name="error">Why the model's edits cannot be applied (the model changed since they were computed), or null.</param>
internal sealed class SplitWorkspaceEdit(IReadOnlyList<TextEdit> modelEdits, WorkspaceEdit? other, string? error)
{
    public IReadOnlyList<TextEdit> ModelEdits { get; } = modelEdits;
    public WorkspaceEdit? Other { get; } = other;
    public string? Error { get; } = error;

    /// <summary>The model's edits as edit operations (insertions where the range is empty, as upstream).</summary>
    public IReadOnlyList<SingleEditOperation> ToEditOperations() => [.. ModelEdits.Select(TextEdit.AsEditOperation)];
}

/// <summary>Splits workspace edits between an editor's model and its host.</summary>
internal static class WorkspaceEdits
{
    /// <summary>Upstream's message when a document changed after the edit was computed.</summary>
    public const string ModelChangedMessage = "The file has changed in the meantime.";

    /// <summary>Splits <paramref name="edit"/> into the text edits of <paramref name="model"/> and the rest.</summary>
    public static SplitWorkspaceEdit Split(WorkspaceEdit edit, TextModel model)
    {
        var modelEdits = new List<TextEdit>();
        var other = new List<WorkspaceEditEntry>();
        string? error = null;
        foreach (var entry in edit.Edits)
        {
            if (entry is WorkspaceTextEdit textEdit && IsSameResource(textEdit.Resource, model.Uri))
            {
                if (textEdit.VersionId is { } versionId && versionId != model.GetVersionId())
                {
                    error = ModelChangedMessage;
                }

                modelEdits.Add(textEdit.TextEdit);
            }
            else
            {
                other.Add(entry);
            }
        }

        return new SplitWorkspaceEdit(modelEdits, other.Count > 0 ? new WorkspaceEdit(other) : null, error);
    }

    /// <summary>Whether two URIs name the same document (upstream's isEqual of resources: exact, fragment included).</summary>
    public static bool IsSameResource(Uri a, Uri b) => string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);

    /// <summary>The number of text edits and the documents they change (for a summary such as "Made 3 text edits in 2 files").</summary>
    public static (int TextEdits, int Resources) Count(WorkspaceEdit edit)
    {
        var resources = new HashSet<string>(StringComparer.Ordinal);
        var textEdits = 0;
        foreach (var entry in edit.Edits)
        {
            if (entry is WorkspaceTextEdit textEdit)
            {
                textEdits++;
                resources.Add(textEdit.Resource.ToString());
            }
        }

        return (textEdits, resources.Count);
    }
}

// Ported from vscode (src/vs/editor/contrib/comment/browser/comment.ts, commit b57d5c9e), MIT license: the run methods of
// CommentLineAction (toggle, add, remove) and BlockCommentAction.
// Differences from upstream: each run is a static method on an IEditorActionHost; the control registers the actions with
// their ids, labels and keybindings.

using TKCode.Arc.Editor.Commands;

namespace TKCode.Arc.Editor.Contrib.Comment;

/// <summary>The comment actions of comment.ts.</summary>
internal static class CommentOperations
{
    /// <summary>CommentLineAction.run: toggle (Ctrl+/), add (Ctrl+K Ctrl+C) or remove (Ctrl+K Ctrl+U) line comments.</summary>
    public static void CommentLine(IEditorActionHost editor, LineCommentType type, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var commands = new List<ICommand?>();
        var modelOptions = model.GetOptions();
        var commentsOptions = editor.CommentsOptions;

        var selections = editor.GetSelections().Select((selection, index) => new CommentSelection(selection, index)).ToList();
        if (selections.Count == 0)
        {
            return;
        }
        selections.Sort((a, b) => TextRange.CompareRangesUsingStarts(a.Selection, b.Selection));

        // Remove selections that would result in copying the same line
        var prev = selections[0];
        for (var i = 1; i < selections.Count; i++)
        {
            var curr = selections[i];
            if (prev.Selection.EndLineNumber == curr.Selection.StartLineNumber)
            {
                // these two selections would copy the same line
                if (prev.Index < curr.Index)
                {
                    // prev wins
                    curr.IgnoreFirstLine = true;
                }
                else
                {
                    // curr wins
                    prev.IgnoreFirstLine = true;
                    prev = curr;
                }
            }
        }

        foreach (var selection in selections)
        {
            commands.Add(new LineCommentCommand(
                editor.LanguageConfigurationService,
                selection.Selection,
                modelOptions.IndentSize,
                type,
                commentsOptions.InsertSpace,
                commentsOptions.IgnoreEmptyLines,
                selection.IgnoreFirstLine));
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    /// <summary>BlockCommentAction.run (Shift+Alt+A).</summary>
    public static void BlockComment(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is null)
        {
            return;
        }

        var commentsOptions = editor.CommentsOptions;
        var commands = new List<ICommand?>();
        var selections = editor.GetSelections();
        foreach (var selection in selections)
        {
            commands.Add(new BlockCommentCommand(selection, commentsOptions.InsertSpace, editor.LanguageConfigurationService));
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    private sealed class CommentSelection(TextSelection selection, int index)
    {
        public TextSelection Selection { get; } = selection;

        public int Index { get; } = index;

        public bool IgnoreFirstLine { get; set; }
    }
}

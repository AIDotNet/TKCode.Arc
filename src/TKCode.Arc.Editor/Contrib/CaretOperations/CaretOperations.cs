// Ported from vscode (src/vs/editor/contrib/caretOperations/browser/caretOperations.ts and transpose.ts, commit
// b57d5c9e), MIT license: the run methods of MoveCaretAction and TransposeLettersAction.
// Differences from upstream: each run is a static method on an IEditorActionHost; the control registers the actions.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc.Editor.Contrib.CaretOperations;

/// <summary>The caret actions: move the selected text left or right, transpose letters.</summary>
internal static class CaretOperations
{
    /// <summary>MoveCaretAction.run.</summary>
    public static void MoveCaret(IEditorActionHost editor, bool left, string id)
    {
        if (editor.GetModel() is null)
        {
            return;
        }

        var commands = new List<ICommand?>();
        var selections = editor.GetSelections();

        foreach (var selection in selections)
        {
            commands.Add(new MoveCaretCommand(selection, left));
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    /// <summary>TransposeLettersAction.run (Ctrl+T on macOS).</summary>
    public static void TransposeLetters(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var commands = new List<ICommand?>();
        var selections = editor.GetSelections();

        foreach (var selection in selections)
        {
            if (!selection.IsEmpty)
            {
                continue;
            }

            var lineNumber = selection.StartLineNumber;
            var column = selection.StartColumn;

            var lastColumn = model.GetLineMaxColumn(lineNumber);

            if (lineNumber == 1 && (column == 1 || (column == 2 && lastColumn == 2)))
            {
                // at beginning of file, nothing to do
                continue;
            }

            // handle special case: when at end of line, transpose left two chars
            // otherwise, transpose left and right chars
            var endPosition = column == lastColumn
                ? selection.Position
                : MoveOperations.RightPosition(model, selection.Position.LineNumber, selection.Position.Column);

            var middlePosition = MoveOperations.LeftPosition(model, endPosition);
            var beginPosition = MoveOperations.LeftPosition(model, middlePosition);

            var leftChar = model.GetValueInRange(new TextRange(beginPosition, middlePosition));
            var rightChar = model.GetValueInRange(new TextRange(middlePosition, endPosition));

            var replaceRange = new TextRange(beginPosition, endPosition);
            commands.Add(new ReplaceCommand(replaceRange, rightChar + leftChar));
        }

        if (commands.Count > 0)
        {
            editor.PushUndoStop();
            editor.ExecuteCommands(id, commands);
            editor.PushUndoStop();
        }
    }
}

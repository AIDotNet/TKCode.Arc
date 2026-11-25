// Ported from vscode (src/vs/editor/contrib/caretOperations/browser/moveCaretCommand.ts, commit b57d5c9e), MIT license.
// Differences: none beyond the type names (Selection is TextSelection).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.CaretOperations;

/// <summary>Moves the selected text one character to the left or right.</summary>
internal sealed class MoveCaretCommand : ICommand
{
    private readonly TextSelection _selection;
    private readonly bool _isMovingLeft;

    public MoveCaretCommand(TextSelection selection, bool isMovingLeft)
    {
        _selection = selection;
        _isMovingLeft = isMovingLeft;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        if (_selection.StartLineNumber != _selection.EndLineNumber || _selection.IsEmpty)
        {
            return;
        }
        var lineNumber = _selection.StartLineNumber;
        var startColumn = _selection.StartColumn;
        var endColumn = _selection.EndColumn;
        if (_isMovingLeft && startColumn == 1)
        {
            return;
        }
        if (!_isMovingLeft && endColumn == model.GetLineMaxColumn(lineNumber))
        {
            return;
        }

        if (_isMovingLeft)
        {
            var rangeBefore = new TextRange(lineNumber, startColumn - 1, lineNumber, startColumn);
            var charBefore = model.GetValueInRange(rangeBefore);
            builder.AddEditOperation(rangeBefore, null);
            builder.AddEditOperation(new TextRange(lineNumber, endColumn, lineNumber, endColumn), charBefore);
        }
        else
        {
            var rangeAfter = new TextRange(lineNumber, endColumn, lineNumber, endColumn + 1);
            var charAfter = model.GetValueInRange(rangeAfter);
            builder.AddEditOperation(rangeAfter, null);
            builder.AddEditOperation(new TextRange(lineNumber, startColumn, lineNumber, startColumn), charAfter);
        }
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        if (_isMovingLeft)
        {
            return new TextSelection(_selection.StartLineNumber, _selection.StartColumn - 1, _selection.EndLineNumber, _selection.EndColumn - 1);
        }
        return new TextSelection(_selection.StartLineNumber, _selection.StartColumn + 1, _selection.EndLineNumber, _selection.EndColumn + 1);
    }
}

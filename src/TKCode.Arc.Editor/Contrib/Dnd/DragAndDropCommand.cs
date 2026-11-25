// Ported from vscode (src/vs/editor/contrib/dnd/browser/dragAndDropCommand.ts, commit b57d5c9e), MIT license.
// Differences: none beyond the names (Selection is TextSelection, Position is TextPosition).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Dnd;

/// <summary>
/// Moves (or copies) the text of a selection to a position and selects it there (upstream's DragAndDropCommand).
/// </summary>
internal sealed class DragAndDropCommand : ICommand
{
    private readonly TextSelection _selection;
    private readonly TextPosition _targetPosition;
    private readonly bool _copy;
    private TextSelection? _targetSelection;

    public DragAndDropCommand(TextSelection selection, TextPosition targetPosition, bool copy)
    {
        _selection = selection;
        _targetPosition = targetPosition;
        _copy = copy;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var selection = _selection;
        var target = _targetPosition;
        var text = model.GetValueInRange(selection.Range);
        if (!_copy)
        {
            builder.AddEditOperation(selection.Range, null);
        }

        builder.AddEditOperation(new TextRange(target.LineNumber, target.Column, target.LineNumber, target.Column), text);

        if (selection.Range.ContainsPosition(target) && !(
            _copy && (selection.Range.End == target || selection.Range.Start == target) // we allow users to paste content beside the selection
        ))
        {
            _targetSelection = selection;
            return;
        }

        var singleLine = selection.StartLineNumber == selection.EndLineNumber;

        if (_copy)
        {
            _targetSelection = new TextSelection(
                target.LineNumber,
                target.Column,
                selection.EndLineNumber - selection.StartLineNumber + target.LineNumber,
                singleLine ? target.Column + selection.EndColumn - selection.StartColumn : selection.EndColumn);
            return;
        }

        if (target.LineNumber > selection.EndLineNumber)
        {
            // Drag the selection downwards
            _targetSelection = new TextSelection(
                target.LineNumber - selection.EndLineNumber + selection.StartLineNumber,
                target.Column,
                target.LineNumber,
                singleLine ? target.Column + selection.EndColumn - selection.StartColumn : selection.EndColumn);
            return;
        }

        if (target.LineNumber < selection.EndLineNumber)
        {
            // Drag the selection upwards
            _targetSelection = new TextSelection(
                target.LineNumber,
                target.Column,
                target.LineNumber + selection.EndLineNumber - selection.StartLineNumber,
                singleLine ? target.Column + selection.EndColumn - selection.StartColumn : selection.EndColumn);
            return;
        }

        // The target position is at the same line as the selection's end position.
        if (selection.EndColumn <= target.Column)
        {
            // The target position is after the selection's end position
            _targetSelection = new TextSelection(
                target.LineNumber - selection.EndLineNumber + selection.StartLineNumber,
                target.Column - selection.EndColumn + selection.StartColumn,
                target.LineNumber,
                singleLine ? target.Column : selection.EndColumn);
        }
        else
        {
            // The target position is before the selection's end position. Since the selection doesn't contain the target
            // position, the selection is one-line and target position is before this selection.
            _targetSelection = new TextSelection(
                target.LineNumber - selection.EndLineNumber + selection.StartLineNumber,
                target.Column,
                target.LineNumber,
                target.Column + selection.EndColumn - selection.StartColumn);
        }
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => _targetSelection!.Value;
}

// Ported from vscode (src/vs/editor/contrib/linesOperations/browser/copyLinesCommand.ts, commit b57d5c9e), MIT license.
// Differences: none beyond the type names (Selection is TextSelection).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.LinesOperations;

/// <summary>Copies the lines of a selection above or below it (Shift+Alt+Up / Down).</summary>
internal sealed class CopyLinesCommand : ICommand
{
    private readonly TextSelection _selection;
    private readonly bool _isCopyingDown;
    private readonly bool _noop;

    private SelectionDirection _selectionDirection;
    private string? _selectionId;
    private int _startLineNumberDelta;
    private int _endLineNumberDelta;

    public CopyLinesCommand(TextSelection selection, bool isCopyingDown, bool noop = false)
    {
        _selection = selection;
        _isCopyingDown = isCopyingDown;
        _noop = noop;
        _selectionDirection = SelectionDirection.LeftToRight;
        _selectionId = null;
        _startLineNumberDelta = 0;
        _endLineNumberDelta = 0;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var s = _selection;

        _startLineNumberDelta = 0;
        _endLineNumberDelta = 0;
        if (s.StartLineNumber < s.EndLineNumber && s.EndColumn == 1)
        {
            _endLineNumberDelta = 1;
            s = s.SetEndPosition(s.EndLineNumber - 1, model.GetLineMaxColumn(s.EndLineNumber - 1));
        }

        var sourceLines = new List<string>();
        for (var i = s.StartLineNumber; i <= s.EndLineNumber; i++)
        {
            sourceLines.Add(model.GetLineContent(i));
        }
        var sourceText = string.Join('\n', sourceLines);

        if (sourceText.Length == 0)
        {
            // Duplicating empty line
            if (_isCopyingDown)
            {
                _startLineNumberDelta++;
                _endLineNumberDelta++;
            }
        }

        if (_noop)
        {
            builder.AddEditOperation(new TextRange(s.EndLineNumber, model.GetLineMaxColumn(s.EndLineNumber), s.EndLineNumber + 1, 1), s.EndLineNumber == model.GetLineCount() ? "" : "\n");
        }
        else
        {
            if (!_isCopyingDown)
            {
                builder.AddEditOperation(new TextRange(s.EndLineNumber, model.GetLineMaxColumn(s.EndLineNumber), s.EndLineNumber, model.GetLineMaxColumn(s.EndLineNumber)), "\n" + sourceText);
            }
            else
            {
                builder.AddEditOperation(new TextRange(s.StartLineNumber, 1, s.StartLineNumber, 1), sourceText + "\n");
            }
        }

        _selectionId = builder.TrackSelection(s);
        _selectionDirection = _selection.Direction;
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var result = helper.GetTrackedSelection(_selectionId!);

        if (_startLineNumberDelta != 0 || _endLineNumberDelta != 0)
        {
            var startLineNumber = result.StartLineNumber;
            var startColumn = result.StartColumn;
            var endLineNumber = result.EndLineNumber;
            var endColumn = result.EndColumn;

            if (_startLineNumberDelta != 0)
            {
                startLineNumber += _startLineNumberDelta;
                startColumn = 1;
            }

            if (_endLineNumberDelta != 0)
            {
                endLineNumber += _endLineNumberDelta;
                endColumn = 1;
            }

            result = TextSelection.FromRange(new TextRange(startLineNumber, startColumn, endLineNumber, endColumn), _selectionDirection);
        }

        return result;
    }
}

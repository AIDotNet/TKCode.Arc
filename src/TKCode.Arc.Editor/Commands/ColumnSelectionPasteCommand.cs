// Ported from vscode (src/vs/editor/common/commands/columnSelectionPasteCommand.ts, commit b57d5c9e), MIT license.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Commands;

/// <summary>
/// Pastes lines as a block: line i goes to the same visible column on line i of the selection, padded with spaces to
/// the width of the widest line, and lines past the end of the document are appended.
/// </summary>
internal sealed class ColumnSelectionPasteCommand : ICommand
{
    private readonly TextSelection _selection;
    private readonly IReadOnlyList<string> _text;
    private readonly int _tabSize;
    private readonly bool _overtype;

    public ColumnSelectionPasteCommand(TextSelection selection, IReadOnlyList<string> text, int tabSize, bool overtype)
    {
        _selection = selection;
        _text = text;
        _tabSize = tabSize;
        _overtype = overtype;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var visibleColumn = CursorColumns.VisibleColumnFromColumn(
            model.GetLineContent(_selection.StartLineNumber), _selection.StartColumn, _tabSize);
        var text = PadTextToBlockWidth(visibleColumn);
        var lineCount = model.GetLineCount();

        for (var i = 0; i < text.Count; i++)
        {
            var lineNumber = _selection.EndLineNumber + i;
            if (lineNumber > lineCount)
            {
                var endColumn = model.GetLineMaxColumn(lineCount);
                var padding = new string(' ', visibleColumn);
                var appendedText = "\n" + padding + string.Join("\n" + padding, text.Skip(i));
                builder.AddTrackedEditOperation(new TextRange(lineCount, endColumn, lineCount, endColumn), appendedText);
                break;
            }

            TextRange range;
            var lineText = text[i];
            if (i == 0)
            {
                range = _selection.Range;
            }
            else
            {
                var lineContent = model.GetLineContent(lineNumber);
                var column = CursorColumns.ColumnFromVisibleColumn(lineContent, visibleColumn, _tabSize);
                range = new TextRange(lineNumber, column, lineNumber, column);
                if (column == model.GetLineMaxColumn(lineNumber))
                {
                    var endVisibleColumn = CursorColumns.VisibleColumnFromColumn(lineContent, column, _tabSize);
                    lineText = new string(' ', Math.Max(0, visibleColumn - endVisibleColumn)) + lineText;
                }
            }

            ICommand command = _overtype ? new ReplaceOvertypeCommand(range, lineText) : new ReplaceCommand(range, lineText);
            command.GetEditOperations(model, builder);
        }
    }

    private List<string> PadTextToBlockWidth(int visibleColumn)
    {
        var prefix = new string(' ', visibleColumn);
        var endVisibleColumns = new List<int>(_text.Count);
        var blockEndVisibleColumn = visibleColumn;
        foreach (var text in _text)
        {
            var textWithPrefix = prefix + text;
            var endVisibleColumn = CursorColumns.VisibleColumnFromColumn(textWithPrefix, textWithPrefix.Length + 1, _tabSize);
            endVisibleColumns.Add(endVisibleColumn);
            blockEndVisibleColumn = Math.Max(blockEndVisibleColumn, endVisibleColumn);
        }

        return _text.Select((text, index) => text + new string(' ', blockEndVisibleColumn - endVisibleColumns[index])).ToList();
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var operations = helper.GetInverseEditOperations();
        return new TextSelection(operations[^1].Range.End);
    }
}

// Ported from vscode (src/vs/editor/common/cursor/cursorColumnSelection.ts, commit b57d5c9e), MIT license.
// Differences from upstream: IColumnSelectResult is the record ColumnSelectResult; IColumnSelectData is the
// existing ColumnSelectData (CursorCommon.cs).

namespace TKCode.Arc.Editor.Cursor;

/// <summary>The cursors of a column (box) selection and the corners it was made from, in view coordinates.</summary>
internal sealed record ColumnSelectResult(IReadOnlyList<SingleCursorState> ViewStates, bool Reversed, int FromLineNumber, int FromVisualColumn, int ToLineNumber, int ToVisualColumn);

/// <summary>Column (box) selection: one cursor per line between two visible columns.</summary>
internal static class ColumnSelection
{
    public static ColumnSelectResult ColumnSelect(
        CursorConfiguration config,
        ICursorSimpleModel model,
        int fromLineNumber,
        int fromVisibleColumn,
        int toLineNumber,
        int toVisibleColumn)
    {
        var lineCount = Math.Abs(toLineNumber - fromLineNumber) + 1;
        var reversed = fromLineNumber > toLineNumber;
        var isRTL = fromVisibleColumn > toVisibleColumn;
        var isLTR = fromVisibleColumn < toVisibleColumn;

        var result = new List<SingleCursorState>();

        for (var i = 0; i < lineCount; i++)
        {
            var lineNumber = fromLineNumber + (reversed ? -i : i);

            var startColumn = config.ColumnFromVisibleColumn(model, lineNumber, fromVisibleColumn);
            var endColumn = config.ColumnFromVisibleColumn(model, lineNumber, toVisibleColumn);
            var visibleStartColumn = config.VisibleColumnFromColumn(model, new TextPosition(lineNumber, startColumn));
            var visibleEndColumn = config.VisibleColumnFromColumn(model, new TextPosition(lineNumber, endColumn));

            if (isLTR)
            {
                if (visibleStartColumn > toVisibleColumn)
                {
                    continue;
                }
                if (visibleEndColumn < fromVisibleColumn)
                {
                    continue;
                }
            }

            if (isRTL)
            {
                if (visibleEndColumn > fromVisibleColumn)
                {
                    continue;
                }
                if (visibleStartColumn < toVisibleColumn)
                {
                    continue;
                }
            }

            result.Add(new SingleCursorState(
                new TextRange(lineNumber, startColumn, lineNumber, startColumn), SelectionStartKind.Simple, 0,
                new TextPosition(lineNumber, endColumn), 0));
        }

        if (result.Count == 0)
        {
            // We are after all the lines, so add cursor at the end of each line
            for (var i = 0; i < lineCount; i++)
            {
                var lineNumber = fromLineNumber + (reversed ? -i : i);
                var maxColumn = model.GetLineMaxColumn(lineNumber);

                result.Add(new SingleCursorState(
                    new TextRange(lineNumber, maxColumn, lineNumber, maxColumn), SelectionStartKind.Simple, 0,
                    new TextPosition(lineNumber, maxColumn), 0));
            }
        }

        return new ColumnSelectResult(
            ViewStates: result,
            Reversed: reversed,
            FromLineNumber: fromLineNumber,
            FromVisualColumn: fromVisibleColumn,
            ToLineNumber: toLineNumber,
            ToVisualColumn: toVisibleColumn);
    }

    public static ColumnSelectResult ColumnSelectLeft(CursorConfiguration config, ICursorSimpleModel model, ColumnSelectData prevColumnSelectData)
    {
        var toViewVisualColumn = prevColumnSelectData.ToViewVisualColumn;
        if (toViewVisualColumn > 0)
        {
            toViewVisualColumn--;
        }

        return ColumnSelect(config, model, prevColumnSelectData.FromViewLineNumber, prevColumnSelectData.FromViewVisualColumn, prevColumnSelectData.ToViewLineNumber, toViewVisualColumn);
    }

    public static ColumnSelectResult ColumnSelectRight(CursorConfiguration config, ICursorSimpleModel model, ColumnSelectData prevColumnSelectData)
    {
        var maxVisualViewColumn = 0;
        var minViewLineNumber = Math.Min(prevColumnSelectData.FromViewLineNumber, prevColumnSelectData.ToViewLineNumber);
        var maxViewLineNumber = Math.Max(prevColumnSelectData.FromViewLineNumber, prevColumnSelectData.ToViewLineNumber);
        for (var lineNumber = minViewLineNumber; lineNumber <= maxViewLineNumber; lineNumber++)
        {
            var lineMaxViewColumn = model.GetLineMaxColumn(lineNumber);
            var lineMaxVisualViewColumn = config.VisibleColumnFromColumn(model, new TextPosition(lineNumber, lineMaxViewColumn));
            maxVisualViewColumn = Math.Max(maxVisualViewColumn, lineMaxVisualViewColumn);
        }

        var toViewVisualColumn = prevColumnSelectData.ToViewVisualColumn;
        if (toViewVisualColumn < maxVisualViewColumn)
        {
            toViewVisualColumn++;
        }

        return ColumnSelect(config, model, prevColumnSelectData.FromViewLineNumber, prevColumnSelectData.FromViewVisualColumn, prevColumnSelectData.ToViewLineNumber, toViewVisualColumn);
    }

    public static ColumnSelectResult ColumnSelectUp(CursorConfiguration config, ICursorSimpleModel model, ColumnSelectData prevColumnSelectData, bool isPaged)
    {
        var linesCount = isPaged ? config.PageSize : 1;
        var toViewLineNumber = Math.Max(1, prevColumnSelectData.ToViewLineNumber - linesCount);
        return ColumnSelect(config, model, prevColumnSelectData.FromViewLineNumber, prevColumnSelectData.FromViewVisualColumn, toViewLineNumber, prevColumnSelectData.ToViewVisualColumn);
    }

    public static ColumnSelectResult ColumnSelectDown(CursorConfiguration config, ICursorSimpleModel model, ColumnSelectData prevColumnSelectData, bool isPaged)
    {
        var linesCount = isPaged ? config.PageSize : 1;
        var toViewLineNumber = Math.Min(model.GetLineCount(), prevColumnSelectData.ToViewLineNumber + linesCount);
        return ColumnSelect(config, model, prevColumnSelectData.FromViewLineNumber, prevColumnSelectData.FromViewVisualColumn, toViewLineNumber, prevColumnSelectData.ToViewVisualColumn);
    }
}

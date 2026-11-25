// Ported from vscode (src/vs/editor/common/cursor/cursorMoveOperations.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - Constants.MAX_SAFE_SMALL_INTEGER (base/common/uint.ts) is the private constant MaxSafeSmallInteger;
//  - the optional normalizationAffinity of vertical is a nullable PositionAffinity;
//  - the private _isBlankLine is IsBlankLine.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>A position a cursor moves to, with the visible columns it keeps past the end of a short line.</summary>
internal readonly record struct CursorPosition(int LineNumber, int Column, int LeftoverVisibleColumns);

/// <summary>The cursor moves (left, right, up, down, line start and end, buffer start and end, blank lines).</summary>
internal static class MoveOperations
{
    // Constants.MAX_SAFE_SMALL_INTEGER: the largest value that fits in a V8 small integer.
    private const int MaxSafeSmallInteger = 1 << 30;

    public static TextPosition LeftPosition(ICursorSimpleModel model, TextPosition position)
    {
        if (position.Column > model.GetLineMinColumn(position.LineNumber))
        {
            return position.Delta(0, -Strings.PrevCharLength(model.GetLineContent(position.LineNumber), position.Column - 1));
        }
        else if (position.LineNumber > 1)
        {
            var newLineNumber = position.LineNumber - 1;
            return new TextPosition(newLineNumber, model.GetLineMaxColumn(newLineNumber));
        }
        else
        {
            return position;
        }
    }

    private static TextPosition LeftPositionAtomicSoftTabs(ICursorSimpleModel model, TextPosition position, int tabSize)
    {
        if (position.Column <= model.GetLineIndentColumn(position.LineNumber))
        {
            var minColumn = model.GetLineMinColumn(position.LineNumber);
            var lineContent = model.GetLineContent(position.LineNumber);
            var newPosition = AtomicTabMoveOperations.AtomicPosition(lineContent, position.Column - 1, tabSize, AtomicTabMoveDirection.Left);
            if (newPosition != -1 && newPosition + 1 >= minColumn)
            {
                return new TextPosition(position.LineNumber, newPosition + 1);
            }
        }
        return LeftPosition(model, position);
    }

    private static CursorPosition Left(CursorConfiguration config, ICursorSimpleModel model, TextPosition position)
    {
        var pos = config.StickyTabStops
            ? LeftPositionAtomicSoftTabs(model, position, config.TabSize)
            : LeftPosition(model, position);
        return new CursorPosition(pos.LineNumber, pos.Column, 0);
    }

    /// <summary>
    /// Moves the cursor left. <c>noOfColumns</c> must be either <c>1</c> or
    /// <c>Math.round(viewModel.getLineContent(viewLineNumber).length / 2)</c> (for half lines).
    /// </summary>
    public static SingleCursorState MoveLeft(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode, int noOfColumns)
    {
        int lineNumber;
        int column;

        if (cursor.HasSelection() && !inSelectionMode)
        {
            // If the user has a selection and does not want to extend it,
            // put the cursor at the beginning of the selection.
            lineNumber = cursor.Selection.StartLineNumber;
            column = cursor.Selection.StartColumn;
        }
        else
        {
            // This has no effect if noOfColumns === 1.
            // It is ok to do so in the half-line scenario.
            var pos = cursor.Position.Delta(0, -(noOfColumns - 1));
            // We clip the position before normalization, as normalization is not defined
            // for possibly negative columns.
            var normalizedPos = model.NormalizePosition(ClipPositionColumn(pos, model), PositionAffinity.Left);
            var p = Left(config, model, normalizedPos);

            lineNumber = p.LineNumber;
            column = p.Column;
        }

        return cursor.Move(inSelectionMode, lineNumber, column, 0);
    }

    /// <summary>Adjusts the column so that it is within min/max of the line.</summary>
    private static TextPosition ClipPositionColumn(TextPosition position, ICursorSimpleModel model) =>
        new(
            position.LineNumber,
            ClipRange(position.Column, model.GetLineMinColumn(position.LineNumber), model.GetLineMaxColumn(position.LineNumber)));

    private static int ClipRange(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }
        if (value > max)
        {
            return max;
        }
        return value;
    }

    public static TextPosition RightPosition(ICursorSimpleModel model, int lineNumber, int column)
    {
        if (column < model.GetLineMaxColumn(lineNumber))
        {
            column = column + Strings.NextCharLength(model.GetLineContent(lineNumber), column - 1);
        }
        else if (lineNumber < model.GetLineCount())
        {
            lineNumber = lineNumber + 1;
            column = model.GetLineMinColumn(lineNumber);
        }
        return model.NormalizePosition(new TextPosition(lineNumber, column), PositionAffinity.Right);
    }

    public static TextPosition RightPositionAtomicSoftTabs(ICursorSimpleModel model, int lineNumber, int column, int tabSize, int indentSize)
    {
        if (column < model.GetLineIndentColumn(lineNumber))
        {
            var lineContent = model.GetLineContent(lineNumber);
            var newPosition = AtomicTabMoveOperations.AtomicPosition(lineContent, column - 1, tabSize, AtomicTabMoveDirection.Right);
            if (newPosition != -1)
            {
                return new TextPosition(lineNumber, newPosition + 1);
            }
        }
        return RightPosition(model, lineNumber, column);
    }

    public static CursorPosition Right(CursorConfiguration config, ICursorSimpleModel model, TextPosition position)
    {
        var pos = config.StickyTabStops
            ? RightPositionAtomicSoftTabs(model, position.LineNumber, position.Column, config.TabSize, config.IndentSize)
            : RightPosition(model, position.LineNumber, position.Column);
        return new CursorPosition(pos.LineNumber, pos.Column, 0);
    }

    public static SingleCursorState MoveRight(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode, int noOfColumns)
    {
        int lineNumber;
        int column;

        if (cursor.HasSelection() && !inSelectionMode)
        {
            // If we are in selection mode, move right without selection cancels selection and puts cursor at the end of the selection
            lineNumber = cursor.Selection.EndLineNumber;
            column = cursor.Selection.EndColumn;
        }
        else
        {
            var pos = cursor.Position.Delta(0, noOfColumns - 1);
            var normalizedPos = model.NormalizePosition(ClipPositionColumn(pos, model), PositionAffinity.Right);
            var r = Right(config, model, normalizedPos);
            lineNumber = r.LineNumber;
            column = r.Column;
        }

        return cursor.Move(inSelectionMode, lineNumber, column, 0);
    }

    public static CursorPosition Vertical(
        CursorConfiguration config,
        ICursorSimpleModel model,
        int lineNumber,
        int column,
        int leftoverVisibleColumns,
        int newLineNumber,
        bool allowMoveOnEdgeLine,
        PositionAffinity? normalizationAffinity = null)
    {
        var currentVisibleColumn = CursorColumns.VisibleColumnFromColumn(model.GetLineContent(lineNumber), column, config.TabSize) + leftoverVisibleColumns;
        var lineCount = model.GetLineCount();
        var wasOnFirstPosition = lineNumber == 1 && column == 1;
        var wasOnLastPosition = lineNumber == lineCount && column == model.GetLineMaxColumn(lineNumber);
        var wasAtEdgePosition = newLineNumber < lineNumber ? wasOnFirstPosition : wasOnLastPosition;

        lineNumber = newLineNumber;
        if (lineNumber < 1)
        {
            lineNumber = 1;
            if (allowMoveOnEdgeLine)
            {
                column = model.GetLineMinColumn(lineNumber);
            }
            else
            {
                column = Math.Min(model.GetLineMaxColumn(lineNumber), column);
            }
        }
        else if (lineNumber > lineCount)
        {
            lineNumber = lineCount;
            if (allowMoveOnEdgeLine)
            {
                column = model.GetLineMaxColumn(lineNumber);
            }
            else
            {
                column = Math.Min(model.GetLineMaxColumn(lineNumber), column);
            }
        }
        else
        {
            column = config.ColumnFromVisibleColumn(model, lineNumber, currentVisibleColumn);
        }

        if (wasAtEdgePosition)
        {
            leftoverVisibleColumns = 0;
        }
        else
        {
            leftoverVisibleColumns = currentVisibleColumn - CursorColumns.VisibleColumnFromColumn(model.GetLineContent(lineNumber), column, config.TabSize);
        }

        if (normalizationAffinity is { } affinity)
        {
            var position = new TextPosition(lineNumber, column);
            var newPosition = model.NormalizePosition(position, affinity);
            leftoverVisibleColumns = leftoverVisibleColumns + (column - newPosition.Column);
            lineNumber = newPosition.LineNumber;
            column = newPosition.Column;
        }
        return new CursorPosition(lineNumber, column, leftoverVisibleColumns);
    }

    public static CursorPosition Down(
        CursorConfiguration config, ICursorSimpleModel model, int lineNumber, int column, int leftoverVisibleColumns, int count, bool allowMoveOnLastLine) =>
        Vertical(config, model, lineNumber, column, leftoverVisibleColumns, lineNumber + count, allowMoveOnLastLine, PositionAffinity.RightOfInjectedText);

    public static SingleCursorState MoveDown(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode, int linesCount)
    {
        int lineNumber;
        int column;

        if (cursor.HasSelection() && !inSelectionMode)
        {
            // If we are in selection mode, move down acts relative to the end of selection
            lineNumber = cursor.Selection.EndLineNumber;
            column = cursor.Selection.EndColumn;
        }
        else
        {
            lineNumber = cursor.Position.LineNumber;
            column = cursor.Position.Column;
        }

        var i = 0;
        CursorPosition r;
        do
        {
            r = Down(config, model, lineNumber + i, column, cursor.LeftoverVisibleColumns, linesCount, true);
            var np = model.NormalizePosition(new TextPosition(r.LineNumber, r.Column), PositionAffinity.None);
            if (np.LineNumber > lineNumber)
            {
                break;
            }
        }
        while (i++ < 10 && lineNumber + i < model.GetLineCount());

        return cursor.Move(inSelectionMode, r.LineNumber, r.Column, r.LeftoverVisibleColumns);
    }

    public static SingleCursorState TranslateDown(CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor)
    {
        var selection = cursor.Selection;

        var selectionStart = Down(config, model, selection.SelectionStartLineNumber, selection.SelectionStartColumn, cursor.SelectionStartLeftoverVisibleColumns, 1, false);
        var position = Down(config, model, selection.PositionLineNumber, selection.PositionColumn, cursor.LeftoverVisibleColumns, 1, false);

        return new SingleCursorState(
            new TextRange(selectionStart.LineNumber, selectionStart.Column, selectionStart.LineNumber, selectionStart.Column),
            SelectionStartKind.Simple,
            selectionStart.LeftoverVisibleColumns,
            new TextPosition(position.LineNumber, position.Column),
            position.LeftoverVisibleColumns);
    }

    public static CursorPosition Up(
        CursorConfiguration config, ICursorSimpleModel model, int lineNumber, int column, int leftoverVisibleColumns, int count, bool allowMoveOnFirstLine) =>
        Vertical(config, model, lineNumber, column, leftoverVisibleColumns, lineNumber - count, allowMoveOnFirstLine, PositionAffinity.LeftOfInjectedText);

    public static SingleCursorState MoveUp(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode, int linesCount)
    {
        int lineNumber;
        int column;

        if (cursor.HasSelection() && !inSelectionMode)
        {
            // If we are in selection mode, move up acts relative to the beginning of selection
            lineNumber = cursor.Selection.StartLineNumber;
            column = cursor.Selection.StartColumn;
        }
        else
        {
            lineNumber = cursor.Position.LineNumber;
            column = cursor.Position.Column;
        }

        var r = Up(config, model, lineNumber, column, cursor.LeftoverVisibleColumns, linesCount, true);

        return cursor.Move(inSelectionMode, r.LineNumber, r.Column, r.LeftoverVisibleColumns);
    }

    public static SingleCursorState TranslateUp(CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor)
    {
        var selection = cursor.Selection;

        var selectionStart = Up(config, model, selection.SelectionStartLineNumber, selection.SelectionStartColumn, cursor.SelectionStartLeftoverVisibleColumns, 1, false);
        var position = Up(config, model, selection.PositionLineNumber, selection.PositionColumn, cursor.LeftoverVisibleColumns, 1, false);

        return new SingleCursorState(
            new TextRange(selectionStart.LineNumber, selectionStart.Column, selectionStart.LineNumber, selectionStart.Column),
            SelectionStartKind.Simple,
            selectionStart.LeftoverVisibleColumns,
            new TextPosition(position.LineNumber, position.Column),
            position.LeftoverVisibleColumns);
    }

    private static bool IsBlankLine(ICursorSimpleModel model, int lineNumber)
    {
        if (model.GetLineFirstNonWhitespaceColumn(lineNumber) == 0)
        {
            // empty or contains only whitespace
            return true;
        }
        return false;
    }

    public static SingleCursorState MoveToPrevBlankLine(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode)
    {
        var lineNumber = cursor.Position.LineNumber;

        // If our current line is blank, move to the previous non-blank line
        while (lineNumber > 1 && IsBlankLine(model, lineNumber))
        {
            lineNumber--;
        }

        // Find the previous blank line
        while (lineNumber > 1 && !IsBlankLine(model, lineNumber))
        {
            lineNumber--;
        }

        return cursor.Move(inSelectionMode, lineNumber, model.GetLineMinColumn(lineNumber), 0);
    }

    public static SingleCursorState MoveToNextBlankLine(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode)
    {
        var lineCount = model.GetLineCount();
        var lineNumber = cursor.Position.LineNumber;

        // If our current line is blank, move to the next non-blank line
        while (lineNumber < lineCount && IsBlankLine(model, lineNumber))
        {
            lineNumber++;
        }

        // Find the next blank line
        while (lineNumber < lineCount && !IsBlankLine(model, lineNumber))
        {
            lineNumber++;
        }

        return cursor.Move(inSelectionMode, lineNumber, model.GetLineMinColumn(lineNumber), 0);
    }

    public static SingleCursorState MoveToBeginningOfLine(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode)
    {
        var lineNumber = cursor.Position.LineNumber;
        var minColumn = model.GetLineMinColumn(lineNumber);
        var firstNonBlankColumn = model.GetLineFirstNonWhitespaceColumn(lineNumber);
        if (firstNonBlankColumn == 0)
        {
            firstNonBlankColumn = minColumn;
        }

        int column;

        var relevantColumnNumber = cursor.Position.Column;
        if (relevantColumnNumber == firstNonBlankColumn)
        {
            column = minColumn;
        }
        else
        {
            column = firstNonBlankColumn;
        }

        return cursor.Move(inSelectionMode, lineNumber, column, 0);
    }

    public static SingleCursorState MoveToEndOfLine(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode, bool sticky)
    {
        var lineNumber = cursor.Position.LineNumber;
        var maxColumn = model.GetLineMaxColumn(lineNumber);
        return cursor.Move(inSelectionMode, lineNumber, maxColumn, sticky ? MaxSafeSmallInteger - maxColumn : 0);
    }

    public static SingleCursorState MoveToBeginningOfBuffer(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode) =>
        cursor.Move(inSelectionMode, 1, 1, 0);

    public static SingleCursorState MoveToEndOfBuffer(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode)
    {
        var lastLineNumber = model.GetLineCount();
        var lastColumn = model.GetLineMaxColumn(lastLineNumber);

        return cursor.Move(inSelectionMode, lastLineNumber, lastColumn, 0);
    }
}

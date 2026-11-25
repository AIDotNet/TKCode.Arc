// Ported from vscode (src/vs/editor/common/cursor/cursorMoveCommands.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the commands return arrays of PartialCursorState (upstream's PartialCursorState[]; an unchanged cursor is a
//    PartialCursorState with both states, upstream's new CursorState(modelState, viewState)); null is "no move";
//  - the optional view positions are nullable TextPositions; Math.round of a non-negative half is (n + 1) / 2;
//  - the CursorMove namespace of the same file is in CursorMove.cs; simpleMove and viewportMove take its Direction
//    enum (upstream's SimpleMoveDirection and ViewportDirection unions) and return null for the directions they do not
//    take.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>The cursor moves of the editor commands, computed on a view model for a set of cursors.</summary>
internal static class CursorMoveCommands
{
    public static PartialCursorState[] AddCursorDown(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool useLogicalLine)
    {
        var result = new PartialCursorState[cursors.Count * 2];
        var resultLen = 0;
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[resultLen++] = PartialCursorState.From(new CursorState(cursor.ModelState, cursor.ViewState));
            if (useLogicalLine)
            {
                result[resultLen++] = CursorState.FromModelState(MoveOperations.TranslateDown(viewModel.CursorConfig, viewModel.Model, cursor.ModelState));
            }
            else
            {
                result[resultLen++] = CursorState.FromViewState(MoveOperations.TranslateDown(viewModel.CursorConfig, viewModel, cursor.ViewState));
            }
        }
        return result;
    }

    public static PartialCursorState[] AddCursorUp(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool useLogicalLine)
    {
        var result = new PartialCursorState[cursors.Count * 2];
        var resultLen = 0;
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[resultLen++] = PartialCursorState.From(new CursorState(cursor.ModelState, cursor.ViewState));
            if (useLogicalLine)
            {
                result[resultLen++] = CursorState.FromModelState(MoveOperations.TranslateUp(viewModel.CursorConfig, viewModel.Model, cursor.ModelState));
            }
            else
            {
                result[resultLen++] = CursorState.FromViewState(MoveOperations.TranslateUp(viewModel.CursorConfig, viewModel, cursor.ViewState));
            }
        }
        return result;
    }

    public static PartialCursorState[] MoveToBeginningOfLine(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = MoveToLineStart(viewModel, cursor, inSelectionMode);
        }

        return result;
    }

    private static PartialCursorState MoveToLineStart(IViewModel viewModel, CursorState cursor, bool inSelectionMode)
    {
        var currentViewStateColumn = cursor.ViewState.Position.Column;
        var currentModelStateColumn = cursor.ModelState.Position.Column;
        var isFirstLineOfWrappedLine = currentViewStateColumn == currentModelStateColumn;

        var currentViewStatelineNumber = cursor.ViewState.Position.LineNumber;
        var firstNonBlankColumn = viewModel.GetLineFirstNonWhitespaceColumn(currentViewStatelineNumber);
        var isBeginningOfViewLine = currentViewStateColumn == firstNonBlankColumn;

        if (!isFirstLineOfWrappedLine && !isBeginningOfViewLine)
        {
            return MoveToLineStartByView(viewModel, cursor, inSelectionMode);
        }
        else
        {
            return MoveToLineStartByModel(viewModel, cursor, inSelectionMode);
        }
    }

    private static PartialCursorState MoveToLineStartByView(IViewModel viewModel, CursorState cursor, bool inSelectionMode) =>
        CursorState.FromViewState(
            MoveOperations.MoveToBeginningOfLine(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode));

    private static PartialCursorState MoveToLineStartByModel(IViewModel viewModel, CursorState cursor, bool inSelectionMode) =>
        CursorState.FromModelState(
            MoveOperations.MoveToBeginningOfLine(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode));

    public static PartialCursorState[] MoveToEndOfLine(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, bool sticky)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = MoveToLineEnd(viewModel, cursor, inSelectionMode, sticky);
        }

        return result;
    }

    private static PartialCursorState MoveToLineEnd(IViewModel viewModel, CursorState cursor, bool inSelectionMode, bool sticky)
    {
        var viewStatePosition = cursor.ViewState.Position;
        var viewModelMaxColumn = viewModel.GetLineMaxColumn(viewStatePosition.LineNumber);
        var isEndOfViewLine = viewStatePosition.Column == viewModelMaxColumn;

        var modelStatePosition = cursor.ModelState.Position;
        var modelMaxColumn = viewModel.Model.GetLineMaxColumn(modelStatePosition.LineNumber);
        var isEndLineOfWrappedLine = viewModelMaxColumn - viewStatePosition.Column == modelMaxColumn - modelStatePosition.Column;

        if (isEndOfViewLine || isEndLineOfWrappedLine)
        {
            return MoveToLineEndByModel(viewModel, cursor, inSelectionMode, sticky);
        }
        else
        {
            return MoveToLineEndByView(viewModel, cursor, inSelectionMode, sticky);
        }
    }

    private static PartialCursorState MoveToLineEndByView(IViewModel viewModel, CursorState cursor, bool inSelectionMode, bool sticky) =>
        CursorState.FromViewState(
            MoveOperations.MoveToEndOfLine(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, sticky));

    private static PartialCursorState MoveToLineEndByModel(IViewModel viewModel, CursorState cursor, bool inSelectionMode, bool sticky) =>
        CursorState.FromModelState(
            MoveOperations.MoveToEndOfLine(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode, sticky));

    public static PartialCursorState[] ExpandLineSelection(IViewModel viewModel, IReadOnlyList<CursorState> cursors)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];

            var startLineNumber = cursor.ModelState.Selection.StartLineNumber;
            var lineCount = viewModel.Model.GetLineCount();

            var endLineNumber = cursor.ModelState.Selection.EndLineNumber;
            int endColumn;
            if (endLineNumber == lineCount)
            {
                endColumn = viewModel.Model.GetLineMaxColumn(lineCount);
            }
            else
            {
                endLineNumber++;
                endColumn = 1;
            }

            result[i] = CursorState.FromModelState(new SingleCursorState(
                new TextRange(startLineNumber, 1, startLineNumber, 1), SelectionStartKind.Simple, 0,
                new TextPosition(endLineNumber, endColumn), 0));
        }
        return result;
    }

    public static PartialCursorState[] MoveToBeginningOfBuffer(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = CursorState.FromModelState(MoveOperations.MoveToBeginningOfBuffer(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode));
        }
        return result;
    }

    public static PartialCursorState[] MoveToEndOfBuffer(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = CursorState.FromModelState(MoveOperations.MoveToEndOfBuffer(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode));
        }
        return result;
    }

    public static PartialCursorState SelectAll(IViewModel viewModel, CursorState cursor)
    {
        var lineCount = viewModel.Model.GetLineCount();
        var maxColumn = viewModel.Model.GetLineMaxColumn(lineCount);

        return CursorState.FromModelState(new SingleCursorState(
            new TextRange(1, 1, 1, 1), SelectionStartKind.Simple, 0,
            new TextPosition(lineCount, maxColumn), 0));
    }

    public static PartialCursorState Line(IViewModel viewModel, CursorState cursor, bool inSelectionMode, TextPosition position, TextPosition? viewPosition)
    {
        var validPosition = viewModel.Model.ValidatePosition(position);
        var validViewPosition = viewPosition is { } vp
            ? viewModel.CoordinatesConverter.ValidateViewPosition(new TextPosition(vp.LineNumber, vp.Column), validPosition)
            : viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(validPosition);

        if (!inSelectionMode)
        {
            // Entering line selection for the first time
            var lineCount = viewModel.Model.GetLineCount();

            var selectToLineNumber = validPosition.LineNumber + 1;
            var selectToColumn = 1;
            if (selectToLineNumber > lineCount)
            {
                selectToLineNumber = lineCount;
                selectToColumn = viewModel.Model.GetLineMaxColumn(selectToLineNumber);
            }

            return CursorState.FromModelState(new SingleCursorState(
                new TextRange(validPosition.LineNumber, 1, selectToLineNumber, selectToColumn), SelectionStartKind.Line, 0,
                new TextPosition(selectToLineNumber, selectToColumn), 0));
        }

        // Continuing line selection
        var enteringLineNumber = cursor.ModelState.SelectionStart.Start.LineNumber;

        if (validPosition.LineNumber < enteringLineNumber)
        {
            return CursorState.FromViewState(cursor.ViewState.Move(
                true, validViewPosition.LineNumber, 1, 0));
        }
        else if (validPosition.LineNumber > enteringLineNumber)
        {
            var lineCount = viewModel.GetLineCount();

            var selectToViewLineNumber = validViewPosition.LineNumber + 1;
            var selectToViewColumn = 1;
            if (selectToViewLineNumber > lineCount)
            {
                selectToViewLineNumber = lineCount;
                selectToViewColumn = viewModel.GetLineMaxColumn(selectToViewLineNumber);
            }

            return CursorState.FromViewState(cursor.ViewState.Move(
                true, selectToViewLineNumber, selectToViewColumn, 0));
        }
        else
        {
            var endPositionOfSelectionStart = cursor.ModelState.SelectionStart.End;
            return CursorState.FromModelState(cursor.ModelState.Move(
                true, endPositionOfSelectionStart.LineNumber, endPositionOfSelectionStart.Column, 0));
        }
    }

    public static PartialCursorState Word(IViewModel viewModel, CursorState cursor, bool inSelectionMode, TextPosition position)
    {
        var validPosition = viewModel.Model.ValidatePosition(position);
        return CursorState.FromModelState(WordOperations.Word(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode, validPosition));
    }

    public static PartialCursorState CancelSelection(IViewModel viewModel, CursorState cursor)
    {
        if (!cursor.ModelState.HasSelection())
        {
            return PartialCursorState.From(new CursorState(cursor.ModelState, cursor.ViewState));
        }

        var lineNumber = cursor.ViewState.Position.LineNumber;
        var column = cursor.ViewState.Position.Column;

        return CursorState.FromViewState(new SingleCursorState(
            new TextRange(lineNumber, column, lineNumber, column), SelectionStartKind.Simple, 0,
            new TextPosition(lineNumber, column), 0));
    }

    public static PartialCursorState MoveTo(IViewModel viewModel, CursorState cursor, bool inSelectionMode, TextPosition position, TextPosition? viewPosition)
    {
        if (inSelectionMode)
        {
            if (cursor.ModelState.SelectionStartKind == SelectionStartKind.Word)
            {
                return Word(viewModel, cursor, inSelectionMode, position);
            }
            if (cursor.ModelState.SelectionStartKind == SelectionStartKind.Line)
            {
                return Line(viewModel, cursor, inSelectionMode, position, viewPosition);
            }
        }
        var validPosition = viewModel.Model.ValidatePosition(position);
        var validViewPosition = viewPosition is { } vp
            ? viewModel.CoordinatesConverter.ValidateViewPosition(new TextPosition(vp.LineNumber, vp.Column), validPosition)
            : viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(validPosition);
        return CursorState.FromViewState(cursor.ViewState.Move(inSelectionMode, validViewPosition.LineNumber, validViewPosition.Column, 0));
    }

    /// <summary>
    /// The moves of <see cref="CursorMove.Direction.Left"/> to
    /// <see cref="CursorMove.Direction.WrappedLineLastNonWhitespaceCharacter"/> (upstream's SimpleMoveDirection); null for
    /// any other direction.
    /// </summary>
    public static PartialCursorState[]? SimpleMove(
        IViewModel viewModel,
        IReadOnlyList<CursorState> cursors,
        CursorMove.Direction direction,
        bool inSelectionMode,
        int value,
        CursorMove.Unit unit)
    {
        switch (direction)
        {
            case CursorMove.Direction.Left:
            {
                if (unit == CursorMove.Unit.HalfLine)
                {
                    // Move left by half the current line length
                    return MoveHalfLineLeft(viewModel, cursors, inSelectionMode);
                }
                else
                {
                    // Move left by `moveParams.value` columns
                    return MoveLeft(viewModel, cursors, inSelectionMode, value);
                }
            }
            case CursorMove.Direction.Right:
            {
                if (unit == CursorMove.Unit.HalfLine)
                {
                    // Move right by half the current line length
                    return MoveHalfLineRight(viewModel, cursors, inSelectionMode);
                }
                else
                {
                    // Move right by `moveParams.value` columns
                    return MoveRight(viewModel, cursors, inSelectionMode, value);
                }
            }
            case CursorMove.Direction.Up:
            {
                if (unit == CursorMove.Unit.WrappedLine)
                {
                    // Move up by view lines
                    return MoveUpByViewLines(viewModel, cursors, inSelectionMode, value);
                }
                else if (unit == CursorMove.Unit.FoldedLine)
                {
                    // Move up by model lines, skipping over folded regions
                    return MoveUpByFoldedLines(viewModel, cursors, inSelectionMode, value);
                }
                else
                {
                    // Move up by model lines
                    return MoveUpByModelLines(viewModel, cursors, inSelectionMode, value);
                }
            }
            case CursorMove.Direction.Down:
            {
                if (unit == CursorMove.Unit.WrappedLine)
                {
                    // Move down by view lines
                    return MoveDownByViewLines(viewModel, cursors, inSelectionMode, value);
                }
                else if (unit == CursorMove.Unit.FoldedLine)
                {
                    // Move down by model lines, skipping over folded regions
                    return MoveDownByFoldedLines(viewModel, cursors, inSelectionMode, value);
                }
                else
                {
                    // Move down by model lines
                    return MoveDownByModelLines(viewModel, cursors, inSelectionMode, value);
                }
            }
            case CursorMove.Direction.PrevBlankLine:
            {
                if (unit == CursorMove.Unit.WrappedLine)
                {
                    return cursors.Select(cursor => CursorState.FromViewState(MoveOperations.MoveToPrevBlankLine(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode))).ToArray();
                }
                else
                {
                    return cursors.Select(cursor => CursorState.FromModelState(MoveOperations.MoveToPrevBlankLine(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode))).ToArray();
                }
            }
            case CursorMove.Direction.NextBlankLine:
            {
                if (unit == CursorMove.Unit.WrappedLine)
                {
                    return cursors.Select(cursor => CursorState.FromViewState(MoveOperations.MoveToNextBlankLine(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode))).ToArray();
                }
                else
                {
                    return cursors.Select(cursor => CursorState.FromModelState(MoveOperations.MoveToNextBlankLine(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode))).ToArray();
                }
            }
            case CursorMove.Direction.WrappedLineStart:
            {
                // Move to the beginning of the current view line
                return MoveToViewMinColumn(viewModel, cursors, inSelectionMode);
            }
            case CursorMove.Direction.WrappedLineFirstNonWhitespaceCharacter:
            {
                // Move to the first non-whitespace column of the current view line
                return MoveToViewFirstNonWhitespaceColumn(viewModel, cursors, inSelectionMode);
            }
            case CursorMove.Direction.WrappedLineColumnCenter:
            {
                // Move to the "center" of the current view line
                return MoveToViewCenterColumn(viewModel, cursors, inSelectionMode);
            }
            case CursorMove.Direction.WrappedLineEnd:
            {
                // Move to the end of the current view line
                return MoveToViewMaxColumn(viewModel, cursors, inSelectionMode);
            }
            case CursorMove.Direction.WrappedLineLastNonWhitespaceCharacter:
            {
                // Move to the last non-whitespace column of the current view line
                return MoveToViewLastNonWhitespaceColumn(viewModel, cursors, inSelectionMode);
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// The moves of <see cref="CursorMove.Direction.ViewPortTop"/> to <see cref="CursorMove.Direction.ViewPortIfOutside"/>
    /// (upstream's ViewportDirection); null for any other direction.
    /// </summary>
    public static PartialCursorState[]? ViewportMove(
        IViewModel viewModel,
        IReadOnlyList<CursorState> cursors,
        CursorMove.Direction direction,
        bool inSelectionMode,
        int value)
    {
        var visibleViewRange = viewModel.GetCompletelyVisibleViewRange();
        var visibleModelRange = viewModel.CoordinatesConverter.ConvertViewRangeToModelRange(visibleViewRange);
        switch (direction)
        {
            case CursorMove.Direction.ViewPortTop:
            {
                // Move to the nth line start in the viewport (from the top)
                var modelLineNumber = FirstLineNumberInRange(viewModel.Model, visibleModelRange, value);
                var modelColumn = viewModel.Model.GetLineFirstNonWhitespaceColumn(modelLineNumber);
                return [MoveToModelPosition(viewModel, cursors[0], inSelectionMode, modelLineNumber, modelColumn)];
            }
            case CursorMove.Direction.ViewPortBottom:
            {
                // Move to the nth line start in the viewport (from the bottom)
                var modelLineNumber = LastLineNumberInRange(viewModel.Model, visibleModelRange, value);
                var modelColumn = viewModel.Model.GetLineFirstNonWhitespaceColumn(modelLineNumber);
                return [MoveToModelPosition(viewModel, cursors[0], inSelectionMode, modelLineNumber, modelColumn)];
            }
            case CursorMove.Direction.ViewPortCenter:
            {
                // Move to the line start in the viewport center
                var modelLineNumber = RoundHalf(visibleModelRange.StartLineNumber + visibleModelRange.EndLineNumber);
                var modelColumn = viewModel.Model.GetLineFirstNonWhitespaceColumn(modelLineNumber);
                return [MoveToModelPosition(viewModel, cursors[0], inSelectionMode, modelLineNumber, modelColumn)];
            }
            case CursorMove.Direction.ViewPortIfOutside:
            {
                // Move to a position inside the viewport
                var result = new PartialCursorState[cursors.Count];
                for (int i = 0, len = cursors.Count; i < len; i++)
                {
                    var cursor = cursors[i];
                    result[i] = FindPositionInViewportIfOutside(viewModel, cursor, visibleViewRange, inSelectionMode);
                }
                return result;
            }
            default:
                return null;
        }
    }

    public static PartialCursorState FindPositionInViewportIfOutside(IViewModel viewModel, CursorState cursor, TextRange visibleViewRange, bool inSelectionMode)
    {
        var viewLineNumber = cursor.ViewState.Position.LineNumber;

        if (visibleViewRange.StartLineNumber <= viewLineNumber && viewLineNumber <= visibleViewRange.EndLineNumber - 1)
        {
            // Nothing to do, cursor is in viewport
            return PartialCursorState.From(new CursorState(cursor.ModelState, cursor.ViewState));
        }
        else
        {
            int newViewLineNumber;
            if (viewLineNumber > visibleViewRange.EndLineNumber - 1)
            {
                newViewLineNumber = visibleViewRange.EndLineNumber - 1;
            }
            else if (viewLineNumber < visibleViewRange.StartLineNumber)
            {
                newViewLineNumber = visibleViewRange.StartLineNumber;
            }
            else
            {
                newViewLineNumber = viewLineNumber;
            }
            var position = MoveOperations.Vertical(viewModel.CursorConfig, viewModel, viewLineNumber, cursor.ViewState.Position.Column, cursor.ViewState.LeftoverVisibleColumns, newViewLineNumber, false);
            return CursorState.FromViewState(cursor.ViewState.Move(inSelectionMode, position.LineNumber, position.Column, position.LeftoverVisibleColumns));
        }
    }

    /// <summary>Find the nth line start included in the range (from the start).</summary>
    private static int FirstLineNumberInRange(ICursorSimpleModel model, TextRange range, int count)
    {
        var startLineNumber = range.StartLineNumber;
        if (range.StartColumn != model.GetLineMinColumn(startLineNumber))
        {
            // Move on to the second line if the first line start is not included in the range
            startLineNumber++;
        }

        return Math.Min(range.EndLineNumber, startLineNumber + count - 1);
    }

    /// <summary>Find the nth line start included in the range (from the end).</summary>
    private static int LastLineNumberInRange(ICursorSimpleModel model, TextRange range, int count)
    {
        var startLineNumber = range.StartLineNumber;
        if (range.StartColumn != model.GetLineMinColumn(startLineNumber))
        {
            // Move on to the second line if the first line start is not included in the range
            startLineNumber++;
        }

        return Math.Max(startLineNumber, range.EndLineNumber - count + 1);
    }

    private static PartialCursorState[] MoveLeft(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int noOfColumns) =>
        cursors.Select(cursor =>
        {
            var direction = viewModel.GetTextDirection(cursor.ViewState.Position.LineNumber);
            var isRtl = direction == TextDirection.RTL;

            return CursorState.FromViewState(
                isRtl
                    ? MoveOperations.MoveRight(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, noOfColumns)
                    : MoveOperations.MoveLeft(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, noOfColumns));
        }).ToArray();

    private static PartialCursorState[] MoveHalfLineLeft(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var halfLine = RoundHalf(viewModel.GetLineLength(viewLineNumber));
            result[i] = CursorState.FromViewState(MoveOperations.MoveLeft(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, halfLine));
        }
        return result;
    }

    private static PartialCursorState[] MoveRight(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int noOfColumns) =>
        cursors.Select(cursor =>
        {
            var direction = viewModel.GetTextDirection(cursor.ViewState.Position.LineNumber);
            var isRtl = direction == TextDirection.RTL;

            return CursorState.FromViewState(
                isRtl
                    ? MoveOperations.MoveLeft(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, noOfColumns)
                    : MoveOperations.MoveRight(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, noOfColumns));
        }).ToArray();

    private static PartialCursorState[] MoveHalfLineRight(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var halfLine = RoundHalf(viewModel.GetLineLength(viewLineNumber));
            result[i] = CursorState.FromViewState(MoveOperations.MoveRight(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, halfLine));
        }
        return result;
    }

    private static PartialCursorState[] MoveDownByViewLines(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int linesCount)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = CursorState.FromViewState(MoveOperations.MoveDown(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, linesCount));
        }
        return result;
    }

    private static PartialCursorState[] MoveDownByModelLines(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int linesCount)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = CursorState.FromModelState(MoveOperations.MoveDown(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode, linesCount));
        }
        return result;
    }

    private static PartialCursorState[] MoveUpByViewLines(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int linesCount)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = CursorState.FromViewState(MoveOperations.MoveUp(viewModel.CursorConfig, viewModel, cursor.ViewState, inSelectionMode, linesCount));
        }
        return result;
    }

    private static PartialCursorState[] MoveUpByModelLines(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int linesCount)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            result[i] = CursorState.FromModelState(MoveOperations.MoveUp(viewModel.CursorConfig, viewModel.Model, cursor.ModelState, inSelectionMode, linesCount));
        }
        return result;
    }

    private static PartialCursorState[] MoveDownByFoldedLines(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int count)
    {
        var model = viewModel.Model;
        var lineCount = model.GetLineCount();
        var hiddenAreas = viewModel.GetHiddenAreas();

        return cursors.Select(cursor =>
        {
            var startLine = cursor.ModelState.HasSelection() && !inSelectionMode
                ? cursor.ModelState.Selection.EndLineNumber
                : cursor.ModelState.Position.LineNumber;

            var targetLine = TargetFoldedDown(startLine, count, hiddenAreas, lineCount);
            var delta = targetLine - startLine;
            if (delta == 0)
            {
                return CursorState.FromModelState(cursor.ModelState);
            }
            return CursorState.FromModelState(MoveOperations.MoveDown(viewModel.CursorConfig, model, cursor.ModelState, inSelectionMode, delta));
        }).ToArray();
    }

    private static PartialCursorState[] MoveUpByFoldedLines(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode, int count)
    {
        var model = viewModel.Model;
        var hiddenAreas = viewModel.GetHiddenAreas();

        return cursors.Select(cursor =>
        {
            var startLine = cursor.ModelState.HasSelection() && !inSelectionMode
                ? cursor.ModelState.Selection.StartLineNumber
                : cursor.ModelState.Position.LineNumber;

            var targetLine = TargetFoldedUp(startLine, count, hiddenAreas);
            var delta = startLine - targetLine;
            if (delta == 0)
            {
                return CursorState.FromModelState(cursor.ModelState);
            }
            return CursorState.FromModelState(MoveOperations.MoveUp(viewModel.CursorConfig, model, cursor.ModelState, inSelectionMode, delta));
        }).ToArray();
    }

    // Compute the target line after moving `count` steps downward from `startLine`,
    // treating each folded region as a single step.
    private static int TargetFoldedDown(int startLine, int count, IReadOnlyList<TextRange> hiddenAreas, int lineCount)
    {
        var line = startLine;
        var i = 0;

        while (i < hiddenAreas.Count && hiddenAreas[i].EndLineNumber < line + 1)
        {
            i++;
        }

        for (var step = 0; step < count; step++)
        {
            if (line >= lineCount)
            {
                return lineCount;
            }

            var candidate = line + 1;
            while (i < hiddenAreas.Count && hiddenAreas[i].EndLineNumber < candidate)
            {
                i++;
            }

            if (i < hiddenAreas.Count && hiddenAreas[i].StartLineNumber <= candidate)
            {
                candidate = hiddenAreas[i].EndLineNumber + 1;
            }

            if (candidate > lineCount)
            {
                // The next visible line does not exist (e.g. a fold reaches EOF).
                return line;
            }

            line = candidate;
        }

        return line;
    }

    // Compute the target line after moving `count` steps upward from `startLine`,
    // treating each folded region as a single step.
    private static int TargetFoldedUp(int startLine, int count, IReadOnlyList<TextRange> hiddenAreas)
    {
        var line = startLine;
        var i = hiddenAreas.Count - 1;

        while (i >= 0 && hiddenAreas[i].StartLineNumber > line - 1)
        {
            i--;
        }

        for (var step = 0; step < count; step++)
        {
            if (line <= 1)
            {
                return 1;
            }

            var candidate = line - 1;
            while (i >= 0 && hiddenAreas[i].StartLineNumber > candidate)
            {
                i--;
            }

            if (i >= 0 && hiddenAreas[i].EndLineNumber >= candidate)
            {
                candidate = hiddenAreas[i].StartLineNumber - 1;
            }

            if (candidate < 1)
            {
                // The previous visible line does not exist (e.g. a fold reaches BOF).
                return line;
            }

            line = candidate;
        }

        return line;
    }

    private static PartialCursorState MoveToViewPosition(IViewModel viewModel, CursorState cursor, bool inSelectionMode, int toViewLineNumber, int toViewColumn) =>
        CursorState.FromViewState(cursor.ViewState.Move(inSelectionMode, toViewLineNumber, toViewColumn, 0));

    private static PartialCursorState MoveToModelPosition(IViewModel viewModel, CursorState cursor, bool inSelectionMode, int toModelLineNumber, int toModelColumn) =>
        CursorState.FromModelState(cursor.ModelState.Move(inSelectionMode, toModelLineNumber, toModelColumn, 0));

    private static PartialCursorState[] MoveToViewMinColumn(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var viewColumn = viewModel.GetLineMinColumn(viewLineNumber);
            result[i] = MoveToViewPosition(viewModel, cursor, inSelectionMode, viewLineNumber, viewColumn);
        }
        return result;
    }

    private static PartialCursorState[] MoveToViewFirstNonWhitespaceColumn(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var viewColumn = viewModel.GetLineFirstNonWhitespaceColumn(viewLineNumber);
            result[i] = MoveToViewPosition(viewModel, cursor, inSelectionMode, viewLineNumber, viewColumn);
        }
        return result;
    }

    private static PartialCursorState[] MoveToViewCenterColumn(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var viewColumn = RoundHalf(viewModel.GetLineMaxColumn(viewLineNumber) + viewModel.GetLineMinColumn(viewLineNumber));
            result[i] = MoveToViewPosition(viewModel, cursor, inSelectionMode, viewLineNumber, viewColumn);
        }
        return result;
    }

    private static PartialCursorState[] MoveToViewMaxColumn(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var viewColumn = viewModel.GetLineMaxColumn(viewLineNumber);
            result[i] = MoveToViewPosition(viewModel, cursor, inSelectionMode, viewLineNumber, viewColumn);
        }
        return result;
    }

    private static PartialCursorState[] MoveToViewLastNonWhitespaceColumn(IViewModel viewModel, IReadOnlyList<CursorState> cursors, bool inSelectionMode)
    {
        var result = new PartialCursorState[cursors.Count];
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            var cursor = cursors[i];
            var viewLineNumber = cursor.ViewState.Position.LineNumber;
            var viewColumn = viewModel.GetLineLastNonWhitespaceColumn(viewLineNumber);
            result[i] = MoveToViewPosition(viewModel, cursor, inSelectionMode, viewLineNumber, viewColumn);
        }
        return result;
    }

    // Math.round(n / 2) for n >= 0.
    private static int RoundHalf(int n) => (n + 1) / 2;
}

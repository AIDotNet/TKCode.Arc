// Ported from vscode (src/vs/editor/common/commands/replaceCommand.ts, commit b57d5c9e), MIT license.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Commands;

/// <summary>Replaces a range; the cursor goes to the end of the new text.</summary>
internal sealed class ReplaceCommand : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;

    public ReplaceCommand(TextRange range, string text, bool insertsAutoWhitespace = false)
    {
        _range = range;
        _text = text;
        InsertsAutoWhitespace = insertsAutoWhitespace;
    }

    public bool InsertsAutoWhitespace { get; }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder) => builder.AddTrackedEditOperation(_range, _text);

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return new TextSelection(srcRange.End);
    }
}

/// <summary>Types over the text after the range (overtype mode); the cursor goes to the end of the new text.</summary>
internal sealed class ReplaceOvertypeCommand : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;

    public ReplaceOvertypeCommand(TextRange range, string text, bool insertsAutoWhitespace = false)
    {
        _range = range;
        _text = text;
        InsertsAutoWhitespace = insertsAutoWhitespace;
    }

    public bool InsertsAutoWhitespace { get; }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var initialStartPosition = _range.Start;
        var initialEndPosition = _range.End;
        var initialEndLineNumber = initialEndPosition.LineNumber;
        var offsetDelta = _text.Length + (_range.IsEmpty ? 0 : -1);
        var endPosition = ReplaceCommands.AddPositiveOffsetToModelPosition(model, initialEndPosition, offsetDelta);
        if (endPosition.LineNumber > initialEndLineNumber)
        {
            endPosition = new TextPosition(initialEndLineNumber, model.GetLineMaxColumn(initialEndLineNumber));
        }

        var replaceRange = new TextRange(initialStartPosition, endPosition);
        builder.AddTrackedEditOperation(replaceRange, _text);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return new TextSelection(srcRange.End);
    }
}

/// <summary>Replaces a range and selects the new text.</summary>
internal sealed class ReplaceCommandThatSelectsText : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;

    public ReplaceCommandThatSelectsText(TextRange range, string text)
    {
        _range = range;
        _text = text;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder) => builder.AddTrackedEditOperation(_range, _text);

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return TextSelection.FromRange(srcRange, SelectionDirection.LeftToRight);
    }
}

/// <summary>Replaces a range; the cursor stays at the start of the new text.</summary>
internal sealed class ReplaceCommandWithoutChangingPosition : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;

    public ReplaceCommandWithoutChangingPosition(TextRange range, string text, bool insertsAutoWhitespace = false)
    {
        _range = range;
        _text = text;
        InsertsAutoWhitespace = insertsAutoWhitespace;
    }

    public bool InsertsAutoWhitespace { get; }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder) => builder.AddTrackedEditOperation(_range, _text);

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return new TextSelection(srcRange.Start);
    }
}

/// <summary>Replaces a range; the cursor goes to the end of the new text moved by a line and column delta.</summary>
internal sealed class ReplaceCommandWithOffsetCursorState : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;
    private readonly int _columnDeltaOffset;
    private readonly int _lineNumberDeltaOffset;

    public ReplaceCommandWithOffsetCursorState(
        TextRange range,
        string text,
        int lineNumberDeltaOffset,
        int columnDeltaOffset,
        bool insertsAutoWhitespace = false)
    {
        _range = range;
        _text = text;
        _columnDeltaOffset = columnDeltaOffset;
        _lineNumberDeltaOffset = lineNumberDeltaOffset;
        InsertsAutoWhitespace = insertsAutoWhitespace;
    }

    public bool InsertsAutoWhitespace { get; }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder) => builder.AddTrackedEditOperation(_range, _text);

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return new TextSelection(srcRange.End.Delta(_lineNumberDeltaOffset, _columnDeltaOffset));
    }
}

/// <summary>At the end of a composition in overtype mode: deletes as many characters after it as it inserted.</summary>
internal sealed class ReplaceOvertypeCommandOnCompositionEnd : ICommand
{
    private readonly TextRange _range;

    public ReplaceOvertypeCommandOnCompositionEnd(TextRange range)
    {
        _range = range;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var text = model.GetValueInRange(_range);
        var initialEndPosition = _range.End;
        var initialEndLineNumber = initialEndPosition.LineNumber;
        var endPosition = ReplaceCommands.AddPositiveOffsetToModelPosition(model, initialEndPosition, text.Length);
        if (endPosition.LineNumber > initialEndLineNumber)
        {
            endPosition = new TextPosition(initialEndLineNumber, model.GetLineMaxColumn(initialEndLineNumber));
        }

        var replaceRange = new TextRange(initialEndPosition, endPosition);
        builder.AddTrackedEditOperation(replaceRange, string.Empty);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return new TextSelection(srcRange.End);
    }
}

/// <summary>Replaces a range and keeps a selection where it was, tracked through the edit.</summary>
internal sealed class ReplaceCommandThatPreservesSelection : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;
    private readonly TextSelection _initialSelection;
    private readonly bool _forceMoveMarkers;
    private string? _selectionId;

    public ReplaceCommandThatPreservesSelection(TextRange editRange, string text, TextSelection initialSelection, bool forceMoveMarkers = false)
    {
        _range = editRange;
        _text = text;
        _initialSelection = initialSelection;
        _forceMoveMarkers = forceMoveMarkers;
        _selectionId = null;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        builder.AddTrackedEditOperation(_range, _text, _forceMoveMarkers);
        _selectionId = builder.TrackSelection(_initialSelection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => helper.GetTrackedSelection(_selectionId!);
}

internal static class ReplaceCommands
{
    /// <summary>The position <paramref name="offset"/> characters after <paramref name="position"/>, counting line breaks as one.</summary>
    internal static TextPosition AddPositiveOffsetToModelPosition(TextModel model, TextPosition position, int offset)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Unexpected negative delta");
        }

        var lineCount = model.GetLineCount();
        var endPosition = new TextPosition(lineCount, model.GetLineMaxColumn(lineCount));
        for (var lineNumber = position.LineNumber; lineNumber <= lineCount; lineNumber++)
        {
            if (lineNumber == position.LineNumber)
            {
                var futureOffset = offset - model.GetLineMaxColumn(position.LineNumber) + position.Column;
                if (futureOffset <= 0)
                {
                    endPosition = new TextPosition(position.LineNumber, position.Column + offset);
                    break;
                }

                offset = futureOffset;
            }
            else
            {
                var futureOffset = offset - model.GetLineMaxColumn(lineNumber);
                if (futureOffset <= 0)
                {
                    endPosition = new TextPosition(lineNumber, offset);
                    break;
                }

                offset = futureOffset;
            }
        }

        return endPosition;
    }
}

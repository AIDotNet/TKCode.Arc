namespace TKCode.Arc.Editor;

/// <summary>
/// A selection in a text model (Monaco's <c>Selection</c>): a range with a direction. It runs from the anchor
/// (<see cref="SelectionStartLineNumber"/>, <see cref="SelectionStartColumn"/>) to the cursor
/// (<see cref="PositionLineNumber"/>, <see cref="PositionColumn"/>); the cursor may be before the anchor.
/// </summary>
public readonly struct TextSelection : IEquatable<TextSelection>
{
    /// <summary>Creates a selection from the anchor to the cursor position.</summary>
    public TextSelection(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn)
    {
        SelectionStartLineNumber = selectionStartLineNumber;
        SelectionStartColumn = selectionStartColumn;
        PositionLineNumber = positionLineNumber;
        PositionColumn = positionColumn;
    }

    /// <summary>Creates a selection from <paramref name="selectionStart"/> to <paramref name="position"/> (empty when one is given).</summary>
    public TextSelection(TextPosition selectionStart, TextPosition? position = null)
        : this(selectionStart.LineNumber, selectionStart.Column, (position ?? selectionStart).LineNumber, (position ?? selectionStart).Column)
    {
    }

    /// <summary>The line the selection was started on (the anchor).</summary>
    public int SelectionStartLineNumber { get; }

    /// <summary>The column the selection was started at (the anchor).</summary>
    public int SelectionStartColumn { get; }

    /// <summary>The line the cursor is on.</summary>
    public int PositionLineNumber { get; }

    /// <summary>The column the cursor is at.</summary>
    public int PositionColumn { get; }

    /// <summary>The anchor: where the selection was started.</summary>
    public TextPosition SelectionStart => new(SelectionStartLineNumber, SelectionStartColumn);

    /// <summary>The cursor position: where the selection ends.</summary>
    public TextPosition Position => new(PositionLineNumber, PositionColumn);

    /// <summary>The selected range, start before end whatever the direction.</summary>
    public TextRange Range => new(SelectionStartLineNumber, SelectionStartColumn, PositionLineNumber, PositionColumn);

    /// <summary>The first line of the selected range.</summary>
    public int StartLineNumber => Range.StartLineNumber;

    /// <summary>The first column of the selected range.</summary>
    public int StartColumn => Range.StartColumn;

    /// <summary>The last line of the selected range.</summary>
    public int EndLineNumber => Range.EndLineNumber;

    /// <summary>The last column of the selected range.</summary>
    public int EndColumn => Range.EndColumn;

    /// <summary>Whether nothing is selected (the anchor equals the cursor).</summary>
    public bool IsEmpty => SelectionStartLineNumber == PositionLineNumber && SelectionStartColumn == PositionColumn;

    /// <summary>Whether the cursor is after the anchor (or equal) or before it.</summary>
    public SelectionDirection Direction
    {
        get
        {
            var range = Range;
            return SelectionStartLineNumber == range.StartLineNumber && SelectionStartColumn == range.StartColumn
                ? SelectionDirection.LeftToRight
                : SelectionDirection.RightToLeft;
        }
    }

    /// <summary>Returns a selection over the same range with a new end, keeping the direction.</summary>
    public TextSelection SetEndPosition(int endLineNumber, int endColumn)
    {
        var range = Range;
        return Direction == SelectionDirection.LeftToRight
            ? new TextSelection(range.StartLineNumber, range.StartColumn, endLineNumber, endColumn)
            : new TextSelection(endLineNumber, endColumn, range.StartLineNumber, range.StartColumn);
    }

    /// <summary>Returns a selection over the same range with a new start, keeping the direction.</summary>
    public TextSelection SetStartPosition(int startLineNumber, int startColumn)
    {
        var range = Range;
        return Direction == SelectionDirection.LeftToRight
            ? new TextSelection(startLineNumber, startColumn, range.EndLineNumber, range.EndColumn)
            : new TextSelection(range.EndLineNumber, range.EndColumn, startLineNumber, startColumn);
    }

    /// <summary>Creates a selection over <paramref name="range"/> in the given direction.</summary>
    public static TextSelection FromRange(TextRange range, SelectionDirection direction = SelectionDirection.LeftToRight) =>
        direction == SelectionDirection.LeftToRight
            ? new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn)
            : new TextSelection(range.EndLineNumber, range.EndColumn, range.StartLineNumber, range.StartColumn);

    /// <summary>The selected range.</summary>
    public static implicit operator TextRange(TextSelection selection) => selection.Range;

    /// <inheritdoc />
    public bool Equals(TextSelection other) =>
        SelectionStartLineNumber == other.SelectionStartLineNumber && SelectionStartColumn == other.SelectionStartColumn
        && PositionLineNumber == other.PositionLineNumber && PositionColumn == other.PositionColumn;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TextSelection other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(SelectionStartLineNumber, SelectionStartColumn, PositionLineNumber, PositionColumn);

    /// <summary>Whether the two selections have the same anchor and cursor.</summary>
    public static bool operator ==(TextSelection left, TextSelection right) => left.Equals(right);

    /// <summary>Whether the two selections differ.</summary>
    public static bool operator !=(TextSelection left, TextSelection right) => !left.Equals(right);

    /// <summary>Formats the selection as <c>[anchorLine,anchorColumn -> cursorLine,cursorColumn]</c>.</summary>
    public override string ToString() =>
        $"[{SelectionStartLineNumber},{SelectionStartColumn} -> {PositionLineNumber},{PositionColumn}]";
}

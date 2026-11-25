namespace TKCode.Arc.Editor;

/// <summary>
/// A range in a text model (Monaco's <c>Range</c>): from (<see cref="StartLineNumber"/>, <see cref="StartColumn"/>) to
/// (<see cref="EndLineNumber"/>, <see cref="EndColumn"/>). The start is always before or equal to the end; the
/// constructor swaps the two when they are given the other way round.
/// </summary>
public readonly struct TextRange : IEquatable<TextRange>
{
    /// <summary>Creates a range; the ends are swapped when the start is after the end.</summary>
    public TextRange(int startLineNumber, int startColumn, int endLineNumber, int endColumn)
    {
        if (startLineNumber > endLineNumber || (startLineNumber == endLineNumber && startColumn > endColumn))
        {
            StartLineNumber = endLineNumber;
            StartColumn = endColumn;
            EndLineNumber = startLineNumber;
            EndColumn = startColumn;
        }
        else
        {
            StartLineNumber = startLineNumber;
            StartColumn = startColumn;
            EndLineNumber = endLineNumber;
            EndColumn = endColumn;
        }
    }

    /// <summary>Creates a range between two positions (an empty range at <paramref name="start"/> when one is given).</summary>
    public TextRange(TextPosition start, TextPosition? end = null)
        : this(start.LineNumber, start.Column, (end ?? start).LineNumber, (end ?? start).Column)
    {
    }

    /// <summary>The line the range starts on (starts at 1).</summary>
    public int StartLineNumber { get; }

    /// <summary>The column the range starts at on <see cref="StartLineNumber"/> (starts at 1).</summary>
    public int StartColumn { get; }

    /// <summary>The line the range ends on.</summary>
    public int EndLineNumber { get; }

    /// <summary>The column the range ends at on <see cref="EndLineNumber"/>.</summary>
    public int EndColumn { get; }

    /// <summary>The start position.</summary>
    public TextPosition Start => new(StartLineNumber, StartColumn);

    /// <summary>The end position.</summary>
    public TextPosition End => new(EndLineNumber, EndColumn);

    /// <summary>Whether the range covers no text (start equals end).</summary>
    public bool IsEmpty => StartLineNumber == EndLineNumber && StartColumn == EndColumn;

    /// <summary>Whether the range starts and ends on the same line.</summary>
    public bool IsSingleLine => StartLineNumber == EndLineNumber;

    /// <summary>Whether <paramref name="position"/> is in the range; a position at either edge counts.</summary>
    public bool ContainsPosition(TextPosition position)
    {
        if (position.LineNumber < StartLineNumber || position.LineNumber > EndLineNumber)
        {
            return false;
        }
        if (position.LineNumber == StartLineNumber && position.Column < StartColumn)
        {
            return false;
        }
        if (position.LineNumber == EndLineNumber && position.Column > EndColumn)
        {
            return false;
        }
        return true;
    }

    /// <summary>Whether <paramref name="position"/> is strictly inside the range; a position at either edge does not count.</summary>
    public bool StrictContainsPosition(TextPosition position)
    {
        if (position.LineNumber < StartLineNumber || position.LineNumber > EndLineNumber)
        {
            return false;
        }
        if (position.LineNumber == StartLineNumber && position.Column <= StartColumn)
        {
            return false;
        }
        if (position.LineNumber == EndLineNumber && position.Column >= EndColumn)
        {
            return false;
        }
        return true;
    }

    /// <summary>Whether <paramref name="other"/> is in this range; an equal range counts.</summary>
    public bool ContainsRange(TextRange other)
    {
        if (other.StartLineNumber < StartLineNumber || other.EndLineNumber < StartLineNumber)
        {
            return false;
        }
        if (other.StartLineNumber > EndLineNumber || other.EndLineNumber > EndLineNumber)
        {
            return false;
        }
        if (other.StartLineNumber == StartLineNumber && other.StartColumn < StartColumn)
        {
            return false;
        }
        if (other.EndLineNumber == EndLineNumber && other.EndColumn > EndColumn)
        {
            return false;
        }
        return true;
    }

    /// <summary>Whether <paramref name="other"/> is strictly inside this range; a shared edge does not count.</summary>
    public bool StrictContainsRange(TextRange other)
    {
        if (other.StartLineNumber < StartLineNumber || other.EndLineNumber < StartLineNumber)
        {
            return false;
        }
        if (other.StartLineNumber > EndLineNumber || other.EndLineNumber > EndLineNumber)
        {
            return false;
        }
        if (other.StartLineNumber == StartLineNumber && other.StartColumn <= StartColumn)
        {
            return false;
        }
        if (other.EndLineNumber == EndLineNumber && other.EndColumn >= EndColumn)
        {
            return false;
        }
        return true;
    }

    /// <summary>The smallest range that contains both this range and <paramref name="other"/>.</summary>
    public TextRange PlusRange(TextRange other)
    {
        int startLineNumber;
        int startColumn;
        int endLineNumber;
        int endColumn;

        if (other.StartLineNumber < StartLineNumber)
        {
            startLineNumber = other.StartLineNumber;
            startColumn = other.StartColumn;
        }
        else if (other.StartLineNumber == StartLineNumber)
        {
            startLineNumber = other.StartLineNumber;
            startColumn = Math.Min(other.StartColumn, StartColumn);
        }
        else
        {
            startLineNumber = StartLineNumber;
            startColumn = StartColumn;
        }

        if (other.EndLineNumber > EndLineNumber)
        {
            endLineNumber = other.EndLineNumber;
            endColumn = other.EndColumn;
        }
        else if (other.EndLineNumber == EndLineNumber)
        {
            endLineNumber = other.EndLineNumber;
            endColumn = Math.Max(other.EndColumn, EndColumn);
        }
        else
        {
            endLineNumber = EndLineNumber;
            endColumn = EndColumn;
        }

        return new TextRange(startLineNumber, startColumn, endLineNumber, endColumn);
    }

    /// <summary>The part both ranges cover, or null when they do not meet. Touching ranges give an empty range.</summary>
    public TextRange? IntersectRanges(TextRange other)
    {
        var resultStartLineNumber = StartLineNumber;
        var resultStartColumn = StartColumn;
        var resultEndLineNumber = EndLineNumber;
        var resultEndColumn = EndColumn;

        if (resultStartLineNumber < other.StartLineNumber)
        {
            resultStartLineNumber = other.StartLineNumber;
            resultStartColumn = other.StartColumn;
        }
        else if (resultStartLineNumber == other.StartLineNumber)
        {
            resultStartColumn = Math.Max(resultStartColumn, other.StartColumn);
        }

        if (resultEndLineNumber > other.EndLineNumber)
        {
            resultEndLineNumber = other.EndLineNumber;
            resultEndColumn = other.EndColumn;
        }
        else if (resultEndLineNumber == other.EndLineNumber)
        {
            resultEndColumn = Math.Min(resultEndColumn, other.EndColumn);
        }

        if (resultStartLineNumber > resultEndLineNumber)
        {
            return null;
        }
        if (resultStartLineNumber == resultEndLineNumber && resultStartColumn > resultEndColumn)
        {
            return null;
        }
        return new TextRange(resultStartLineNumber, resultStartColumn, resultEndLineNumber, resultEndColumn);
    }

    /// <summary>Returns a range with the same start and the given end.</summary>
    public TextRange SetEndPosition(int endLineNumber, int endColumn) =>
        new(StartLineNumber, StartColumn, endLineNumber, endColumn);

    /// <summary>Returns a range with the same end and the given start.</summary>
    public TextRange SetStartPosition(int startLineNumber, int startColumn) =>
        new(startLineNumber, startColumn, EndLineNumber, EndColumn);

    /// <summary>An empty range at this range's start.</summary>
    public TextRange CollapseToStart() => new(StartLineNumber, StartColumn, StartLineNumber, StartColumn);

    /// <summary>An empty range at this range's end.</summary>
    public TextRange CollapseToEnd() => new(EndLineNumber, EndColumn, EndLineNumber, EndColumn);

    /// <summary>Returns this range moved down by <paramref name="lineCount"/> lines (up when negative).</summary>
    public TextRange Delta(int lineCount) =>
        new(StartLineNumber + lineCount, StartColumn, EndLineNumber + lineCount, EndColumn);

    /// <summary>Whether the two ranges overlap or touch at an edge.</summary>
    public static bool AreIntersectingOrTouching(TextRange a, TextRange b)
    {
        if (a.EndLineNumber < b.StartLineNumber || (a.EndLineNumber == b.StartLineNumber && a.EndColumn < b.StartColumn))
        {
            return false;
        }
        if (b.EndLineNumber < a.StartLineNumber || (b.EndLineNumber == a.StartLineNumber && b.EndColumn < a.StartColumn))
        {
            return false;
        }
        return true;
    }

    /// <summary>Whether the two ranges overlap; ranges that only touch at an edge do not.</summary>
    public static bool AreIntersecting(TextRange a, TextRange b)
    {
        if (a.EndLineNumber < b.StartLineNumber || (a.EndLineNumber == b.StartLineNumber && a.EndColumn <= b.StartColumn))
        {
            return false;
        }
        if (b.EndLineNumber < a.StartLineNumber || (b.EndLineNumber == a.StartLineNumber && b.EndColumn <= a.StartColumn))
        {
            return false;
        }
        return true;
    }

    /// <summary>Orders ranges by start, then by end. Useful for sorting.</summary>
    public static int CompareRangesUsingStarts(TextRange a, TextRange b)
    {
        if (a.StartLineNumber != b.StartLineNumber)
        {
            return a.StartLineNumber.CompareTo(b.StartLineNumber);
        }
        if (a.StartColumn != b.StartColumn)
        {
            return a.StartColumn.CompareTo(b.StartColumn);
        }
        if (a.EndLineNumber != b.EndLineNumber)
        {
            return a.EndLineNumber.CompareTo(b.EndLineNumber);
        }
        return a.EndColumn.CompareTo(b.EndColumn);
    }

    /// <summary>Orders ranges by end, then by start. Useful for sorting.</summary>
    public static int CompareRangesUsingEnds(TextRange a, TextRange b)
    {
        if (a.EndLineNumber != b.EndLineNumber)
        {
            return a.EndLineNumber.CompareTo(b.EndLineNumber);
        }
        if (a.EndColumn != b.EndColumn)
        {
            return a.EndColumn.CompareTo(b.EndColumn);
        }
        if (a.StartLineNumber != b.StartLineNumber)
        {
            return a.StartLineNumber.CompareTo(b.StartLineNumber);
        }
        return a.StartColumn.CompareTo(b.StartColumn);
    }

    /// <inheritdoc />
    public bool Equals(TextRange other) =>
        StartLineNumber == other.StartLineNumber && StartColumn == other.StartColumn
        && EndLineNumber == other.EndLineNumber && EndColumn == other.EndColumn;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TextRange other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StartLineNumber, StartColumn, EndLineNumber, EndColumn);

    /// <summary>Whether the two ranges have the same start and end.</summary>
    public static bool operator ==(TextRange left, TextRange right) => left.Equals(right);

    /// <summary>Whether the two ranges differ.</summary>
    public static bool operator !=(TextRange left, TextRange right) => !left.Equals(right);

    /// <summary>Formats the range as <c>[startLine,startColumn -> endLine,endColumn]</c>.</summary>
    public override string ToString() => $"[{StartLineNumber},{StartColumn} -> {EndLineNumber},{EndColumn}]";
}

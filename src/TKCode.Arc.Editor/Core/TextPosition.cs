namespace TKCode.Arc.Editor;

/// <summary>
/// A position in a text model (Monaco's <c>Position</c>). Line numbers start at 1; the first character of a line is
/// between column 1 and column 2. Columns count UTF-16 code units.
/// </summary>
/// <param name="LineNumber">The line number, starting at 1.</param>
/// <param name="Column">The column, starting at 1.</param>
public readonly record struct TextPosition(int LineNumber, int Column) : IComparable<TextPosition>
{
    /// <summary>Returns this position with the given parts replaced.</summary>
    public TextPosition With(int? lineNumber = null, int? column = null) =>
        new(lineNumber ?? LineNumber, column ?? Column);

    /// <summary>Returns this position moved by the given amounts; neither part goes below 1.</summary>
    public TextPosition Delta(int deltaLineNumber = 0, int deltaColumn = 0) =>
        new(Math.Max(1, LineNumber + deltaLineNumber), Math.Max(1, Column + deltaColumn));

    /// <summary>Whether this position is before <paramref name="other"/>; false when they are equal.</summary>
    public bool IsBefore(TextPosition other) => CompareTo(other) < 0;

    /// <summary>Whether this position is before or equal to <paramref name="other"/>.</summary>
    public bool IsBeforeOrEqual(TextPosition other) => CompareTo(other) <= 0;

    /// <inheritdoc />
    public int CompareTo(TextPosition other) =>
        LineNumber == other.LineNumber ? Column.CompareTo(other.Column) : LineNumber.CompareTo(other.LineNumber);

    /// <summary>Whether <paramref name="left"/> is before <paramref name="right"/>.</summary>
    public static bool operator <(TextPosition left, TextPosition right) => left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is after <paramref name="right"/>.</summary>
    public static bool operator >(TextPosition left, TextPosition right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is before or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(TextPosition left, TextPosition right) => left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is after or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(TextPosition left, TextPosition right) => left.CompareTo(right) >= 0;

    /// <summary>Formats the position as <c>(line,column)</c>.</summary>
    public override string ToString() => $"({LineNumber},{Column})";
}

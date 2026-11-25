// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/length.ts and the parts of
// src/vs/editor/common/core/text/textLength.ts it uses, commit b57d5c9e), MIT license.
//
// Differences: Length is a value type over a long (upstream: a branded JS number; the packing is the same, line count
// times 2^26 plus column count, which a long holds exactly); the free functions are static members of LengthHelpers.

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>A non-negative length in lines and columns, packed into one number. Does not allocate.</summary>
internal readonly record struct Length(long Value) : IComparable<Length>
{
    public static readonly Length Zero = new(0);

    public bool IsZero => Value == 0;

    public int CompareTo(Length other) => Value.CompareTo(other.Value);

    public static bool operator <(Length a, Length b) => a.Value < b.Value;

    public static bool operator >(Length a, Length b) => a.Value > b.Value;

    public static bool operator <=(Length a, Length b) => a.Value <= b.Value;

    public static bool operator >=(Length a, Length b) => a.Value >= b.Value;

    /// <inheritdoc />
    public override string ToString() => LengthHelpers.LengthToObj(this).ToString();
}

/// <summary>A line count and a column count (upstream's TextLength).</summary>
internal readonly record struct TextLength(int LineCount, int ColumnCount)
{
    public static readonly TextLength Zero = new(0, 0);

    public bool IsZero => LineCount == 0 && ColumnCount == 0;

    public bool IsLessThan(TextLength other) =>
        LineCount != other.LineCount ? LineCount < other.LineCount : ColumnCount < other.ColumnCount;

    public bool IsGreaterThan(TextLength other) =>
        LineCount != other.LineCount ? LineCount > other.LineCount : ColumnCount > other.ColumnCount;

    public static TextLength LengthDiffNonNegative(TextLength start, TextLength end)
    {
        if (end.IsLessThan(start))
        {
            return Zero;
        }

        return start.LineCount == end.LineCount
            ? new TextLength(0, end.ColumnCount - start.ColumnCount)
            : new TextLength(end.LineCount - start.LineCount, end.ColumnCount);
    }

    public TextLength Add(TextLength other) =>
        other.LineCount == 0
            ? new TextLength(LineCount, ColumnCount + other.ColumnCount)
            : new TextLength(LineCount + other.LineCount, other.ColumnCount);

    public TextPosition AddToPosition(TextPosition position) =>
        LineCount == 0
            ? new TextPosition(position.LineNumber, position.Column + ColumnCount)
            : new TextPosition(position.LineNumber + LineCount, ColumnCount + 1);

    /// <inheritdoc />
    public override string ToString() => $"{LineCount},{ColumnCount}";
}

/// <summary>Arithmetic on <see cref="Length"/> (upstream's length.ts functions).</summary>
internal static class LengthHelpers
{
    // We use the upper 26 bits to store the line and the lower 26 bits to store the column.
    private const long Factor = 1L << 26;

    /// <summary>The end must be greater than or equal to the start.</summary>
    public static Length LengthDiff(int startLineCount, int startColumnCount, int endLineCount, int endColumnCount) =>
        startLineCount != endLineCount
            ? ToLength(endLineCount - startLineCount, endColumnCount)
            : ToLength(0, endColumnCount - startColumnCount);

    public static bool LengthIsZero(Length length) => length.Value == 0;

    public static Length ToLength(int lineCount, int columnCount) =>
        // If there is no overflow (all values/sums below 2^26 = 67108864),
        // we have `toLength(lns1, cols1) + toLength(lns2, cols2) = toLength(lns1 + lns2, cols1 + cols2)`.
        new((lineCount * Factor) + columnCount);

    public static TextLength LengthToObj(Length length)
    {
        var l = length.Value;
        var lineCount = (int)(l / Factor);
        var columnCount = (int)(l - (lineCount * Factor));
        return new TextLength(lineCount, columnCount);
    }

    public static int LengthGetLineCount(Length length) => (int)(length.Value / Factor);

    /// <summary>Returns the amount of columns of the given length, assuming that it does not span any line.</summary>
    public static int LengthGetColumnCountIfZeroLineCount(Length length) => (int)length.Value;

    // [10 lines, 5 cols] + [ 0 lines, 3 cols] = [10 lines, 8 cols]
    // [10 lines, 5 cols] + [20 lines, 3 cols] = [30 lines, 3 cols]
    public static Length LengthAdd(Length length1, Length length2)
    {
        var r = length1.Value + length2.Value;
        if (length2.Value >= Factor)
        {
            r -= length1.Value % Factor;
        }

        return new Length(r);
    }

    public static Length SumLengths<T>(IEnumerable<T> items, Func<T, Length> lengthFn)
    {
        var result = Length.Zero;
        foreach (var item in items)
        {
            result = LengthAdd(result, lengthFn(item));
        }

        return result;
    }

    public static bool LengthEquals(Length length1, Length length2) => length1.Value == length2.Value;

    /// <summary>Returns a non negative length <c>result</c> such that <c>lengthAdd(length1, result) = length2</c>, or zero if such length does not exist.</summary>
    public static Length LengthDiffNonNegative(Length length1, Length length2)
    {
        var l1 = length1.Value;
        var l2 = length2.Value;

        var diff = l2 - l1;
        if (diff <= 0)
        {
            // line-count of length1 is higher than line-count of length2
            // or they are equal and column-count of length1 is higher than column-count of length2
            return Length.Zero;
        }

        var lineCount1 = l1 / Factor;
        var lineCount2 = l2 / Factor;

        var colCount2 = l2 - (lineCount2 * Factor);

        if (lineCount1 == lineCount2)
        {
            var colCount1 = l1 - (lineCount1 * Factor);
            return ToLength(0, (int)(colCount2 - colCount1));
        }

        return ToLength((int)(lineCount2 - lineCount1), (int)colCount2);
    }

    public static bool LengthLessThan(Length length1, Length length2) => length1.Value < length2.Value;

    public static bool LengthLessThanEqual(Length length1, Length length2) => length1.Value <= length2.Value;

    public static bool LengthGreaterThanEqual(Length length1, Length length2) => length1.Value >= length2.Value;

    public static TextPosition LengthToPosition(Length length)
    {
        var l = length.Value;
        var lineCount = l / Factor;
        var colCount = l - (lineCount * Factor);
        return new TextPosition((int)lineCount + 1, (int)colCount + 1);
    }

    public static Length PositionToLength(TextPosition position) => ToLength(position.LineNumber - 1, position.Column - 1);

    public static TextRange LengthsToRange(Length lengthStart, Length lengthEnd)
    {
        var start = LengthToPosition(lengthStart);
        var end = LengthToPosition(lengthEnd);
        return new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column);
    }

    public static TextLength LengthOfRange(TextRange range) =>
        range.StartLineNumber == range.EndLineNumber
            ? new TextLength(0, range.EndColumn - range.StartColumn)
            : new TextLength(range.EndLineNumber - range.StartLineNumber, range.EndColumn - 1);

    public static long LengthCompare(Length length1, Length length2) => length1.Value - length2.Value;

    public static Length LengthOfString(string str)
    {
        var lines = PieceTreeStrings.SplitLines(str);
        return ToLength(lines.Length - 1, lines[^1].Length);
    }

    public static TextLength LengthOfStringObj(string str)
    {
        var lines = PieceTreeStrings.SplitLines(str);
        return new TextLength(lines.Length - 1, lines[^1].Length);
    }

    /// <summary>Computes a numeric hash of the given length.</summary>
    public static long LengthHash(Length length) => length.Value;

    public static Length LengthMax(Length length1, Length length2) => length1.Value > length2.Value ? length1 : length2;
}

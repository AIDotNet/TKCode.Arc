// Ported from vscode (src/vs/editor/common/diff/rangeMapping.ts, commit b57d5c9e), MIT license.
// Differences:
// - Text is a list of lines (upstream's AbstractText / ArrayText): DiffTextLines gives line lengths and range text.
// - toTextEdit / fromEdit (TextEdit, TextReplacement) are not ported; ApplyToLines applies inner changes for the
//   verification upstream does with TextEdit.
// - Number.MAX_SAFE_INTEGER columns are int.MaxValue; BugIndicatingError is InvalidOperationException.
// - The assertFn checks of lineRangeMappingFromRangeMappings run only in Debug builds.

using System.Diagnostics;
using System.Text;

namespace TKCode.Arc.Editor.Diff;

/// <summary>Maps a line range in the original text model to a line range in the modified text model.</summary>
internal class LineRangeMapping
{
    public static List<LineRangeMapping> Inverse(IReadOnlyList<LineRangeMapping> mapping, int originalLineCount, int modifiedLineCount)
    {
        var result = new List<LineRangeMapping>();
        var lastOriginalEndLineNumber = 1;
        var lastModifiedEndLineNumber = 1;

        foreach (var m in mapping)
        {
            var r = new LineRangeMapping(
                new LineRange(lastOriginalEndLineNumber, m.Original.StartLineNumber),
                new LineRange(lastModifiedEndLineNumber, m.Modified.StartLineNumber));
            if (!r.Modified.IsEmpty)
            {
                result.Add(r);
            }

            lastOriginalEndLineNumber = m.Original.EndLineNumberExclusive;
            lastModifiedEndLineNumber = m.Modified.EndLineNumberExclusive;
        }

        var last = new LineRangeMapping(
            new LineRange(lastOriginalEndLineNumber, originalLineCount + 1),
            new LineRange(lastModifiedEndLineNumber, modifiedLineCount + 1));
        if (!last.Modified.IsEmpty)
        {
            result.Add(last);
        }

        return result;
    }

    public static List<LineRangeMapping> Clip(IReadOnlyList<LineRangeMapping> mapping, LineRange originalRange, LineRange modifiedRange)
    {
        var result = new List<LineRangeMapping>();
        foreach (var m in mapping)
        {
            var original = m.Original.Intersect(originalRange);
            var modified = m.Modified.Intersect(modifiedRange);
            if (original is { IsEmpty: false } o && modified is { IsEmpty: false } mo)
            {
                result.Add(new LineRangeMapping(o, mo));
            }
        }

        return result;
    }

    public LineRangeMapping(LineRange originalRange, LineRange modifiedRange)
    {
        Original = originalRange;
        Modified = modifiedRange;
    }

    /// <summary>The line range in the original text model.</summary>
    public LineRange Original { get; }

    /// <summary>The line range in the modified text model.</summary>
    public LineRange Modified { get; }

    public override string ToString() => $"{{{Original}->{Modified}}}";

    public virtual LineRangeMapping Flip() => new(Modified, Original);

    public LineRangeMapping Join(LineRangeMapping other) => new(Original.Join(other.Original), Modified.Join(other.Modified));

    public int ChangedLineCount => Math.Max(Original.Length, Modified.Length);

    /// <summary>
    /// This method assumes that the LineRangeMapping describes a valid diff: if one range is empty, the other range
    /// cannot be the entire document. It avoids various problems when the line range points to non-existing lines.
    /// </summary>
    public RangeMapping ToRangeMapping()
    {
        var origInclusiveRange = Original.ToInclusiveRange();
        var modInclusiveRange = Modified.ToInclusiveRange();
        if (origInclusiveRange is { } o && modInclusiveRange is { } m)
        {
            return new RangeMapping(o, m);
        }

        if (Original.StartLineNumber == 1 || Modified.StartLineNumber == 1)
        {
            if (!(Modified.StartLineNumber == 1 && Original.StartLineNumber == 1))
            {
                // If one line range starts at 1, the other one must start at 1 as well.
                throw new InvalidOperationException("not a valid diff");
            }

            // Because one range is empty and both ranges start at line 1, none of the ranges can cover all lines.
            // Thus, `endLineNumberExclusive` is a valid line number.
            return new RangeMapping(
                new TextRange(Original.StartLineNumber, 1, Original.EndLineNumberExclusive, 1),
                new TextRange(Modified.StartLineNumber, 1, Modified.EndLineNumberExclusive, 1));
        }

        // We can assume here that both startLineNumbers are greater than 1.
        return new RangeMapping(
            new TextRange(Original.StartLineNumber - 1, int.MaxValue, Original.EndLineNumberExclusive - 1, int.MaxValue),
            new TextRange(Modified.StartLineNumber - 1, int.MaxValue, Modified.EndLineNumberExclusive - 1, int.MaxValue));
    }

    /// <summary>
    /// Like <see cref="ToRangeMapping"/>, with the positions made valid for the given lines (assumes a valid diff).
    /// </summary>
    public RangeMapping ToRangeMapping2(IReadOnlyList<string> original, IReadOnlyList<string> modified)
    {
        if (IsValidLineNumber(Original.EndLineNumberExclusive, original) && IsValidLineNumber(Modified.EndLineNumberExclusive, modified))
        {
            return new RangeMapping(
                new TextRange(Original.StartLineNumber, 1, Original.EndLineNumberExclusive, 1),
                new TextRange(Modified.StartLineNumber, 1, Modified.EndLineNumberExclusive, 1));
        }

        if (!Original.IsEmpty && !Modified.IsEmpty)
        {
            return new RangeMapping(
                new TextRange(new TextPosition(Original.StartLineNumber, 1), NormalizePosition(new TextPosition(Original.EndLineNumberExclusive - 1, int.MaxValue), original)),
                new TextRange(new TextPosition(Modified.StartLineNumber, 1), NormalizePosition(new TextPosition(Modified.EndLineNumberExclusive - 1, int.MaxValue), modified)));
        }

        if (Original.StartLineNumber > 1 && Modified.StartLineNumber > 1)
        {
            return new RangeMapping(
                new TextRange(
                    NormalizePosition(new TextPosition(Original.StartLineNumber - 1, int.MaxValue), original),
                    NormalizePosition(new TextPosition(Original.EndLineNumberExclusive - 1, int.MaxValue), original)),
                new TextRange(
                    NormalizePosition(new TextPosition(Modified.StartLineNumber - 1, int.MaxValue), modified),
                    NormalizePosition(new TextPosition(Modified.EndLineNumberExclusive - 1, int.MaxValue), modified)));
        }

        // Situation now: one range is empty and one range touches the last line and one range starts at line 1.
        throw new InvalidOperationException("not a valid diff");
    }

    private static TextPosition NormalizePosition(TextPosition position, IReadOnlyList<string> content)
    {
        if (position.LineNumber < 1)
        {
            return new TextPosition(1, 1);
        }

        if (position.LineNumber > content.Count)
        {
            return new TextPosition(content.Count, content[^1].Length + 1);
        }

        var line = content[position.LineNumber - 1];
        if (position.Column > line.Length + 1)
        {
            return new TextPosition(position.LineNumber, line.Length + 1);
        }

        return position;
    }

    private static bool IsValidLineNumber(int lineNumber, IReadOnlyList<string> lines) => lineNumber >= 1 && lineNumber <= lines.Count;
}

/// <summary>Maps a line range in the original text model to one in the modified text model, with inner range mappings.</summary>
internal sealed class DetailedLineRangeMapping : LineRangeMapping
{
    public static DetailedLineRangeMapping FromRangeMappings(IReadOnlyList<RangeMapping> rangeMappings)
    {
        var originalRange = LineRange.Join(rangeMappings.Select(r => LineRange.FromRangeInclusive(r.OriginalRange)).ToList());
        var modifiedRange = LineRange.Join(rangeMappings.Select(r => LineRange.FromRangeInclusive(r.ModifiedRange)).ToList());
        return new DetailedLineRangeMapping(originalRange, modifiedRange, [.. rangeMappings]);
    }

    public DetailedLineRangeMapping(LineRange originalRange, LineRange modifiedRange, IReadOnlyList<RangeMapping>? innerChanges)
        : base(originalRange, modifiedRange)
    {
        InnerChanges = innerChanges;
    }

    /// <summary>
    /// If inner changes have not been computed, this is null. Otherwise, it represents the character-level diff in this
    /// line range. The original range of each range mapping should be contained in the original line range (same for
    /// modified), exceptions are new-lines. Must not be an empty list.
    /// </summary>
    public IReadOnlyList<RangeMapping>? InnerChanges { get; }

    public override DetailedLineRangeMapping Flip() =>
        new(Modified, Original, InnerChanges?.Select(c => c.Flip()).ToList());

    public DetailedLineRangeMapping WithInnerChangesFromLineRanges() => new(Original, Modified, [ToRangeMapping()]);
}

/// <summary>Maps a range in the original text model to a range in the modified text model.</summary>
internal sealed class RangeMapping(TextRange originalRange, TextRange modifiedRange)
{
    public static RangeMapping Join(IReadOnlyList<RangeMapping> rangeMappings)
    {
        if (rangeMappings.Count == 0)
        {
            throw new InvalidOperationException("Cannot join an empty list of range mappings");
        }

        var result = rangeMappings[0];
        for (var i = 1; i < rangeMappings.Count; i++)
        {
            result = result.Join(rangeMappings[i]);
        }

        return result;
    }

    public static void AssertSorted(IReadOnlyList<RangeMapping> rangeMappings)
    {
        for (var i = 1; i < rangeMappings.Count; i++)
        {
            var previous = rangeMappings[i - 1];
            var current = rangeMappings[i];
            if (!(previous.OriginalRange.End.IsBeforeOrEqual(current.OriginalRange.Start)
                  && previous.ModifiedRange.End.IsBeforeOrEqual(current.ModifiedRange.Start)))
            {
                throw new InvalidOperationException("Range mappings must be sorted");
            }
        }
    }

    /// <summary>The original range.</summary>
    public TextRange OriginalRange { get; } = originalRange;

    /// <summary>The modified range.</summary>
    public TextRange ModifiedRange { get; } = modifiedRange;

    public override string ToString() => $"{{{OriginalRange}->{ModifiedRange}}}";

    public RangeMapping Flip() => new(ModifiedRange, OriginalRange);

    public RangeMapping Join(RangeMapping other) => new(OriginalRange.PlusRange(other.OriginalRange), ModifiedRange.PlusRange(other.ModifiedRange));
}

/// <summary>Text as a list of lines (upstream's ArrayText): line lengths and the text of a range.</summary>
internal sealed class DiffTextLines(IReadOnlyList<string> lines)
{
    public IReadOnlyList<string> Lines { get; } = lines;

    public int LineCount => Lines.Count;

    public int GetLineLength(int lineNumber) => Lines[lineNumber - 1].Length;

    /// <summary>The text of a range, lines joined by \n.</summary>
    public string GetValueOfRange(TextRange range)
    {
        // columns past the end of a line (int.MaxValue) mean its end, as JavaScript's substring clamps
        if (range.StartLineNumber == range.EndLineNumber)
        {
            var line = Lines[range.StartLineNumber - 1];
            var start = Math.Min(range.StartColumn, line.Length + 1) - 1;
            var end = Math.Min(range.EndColumn, line.Length + 1) - 1;
            return end > start ? line.Substring(start, end - start) : string.Empty;
        }

        var builder = new StringBuilder();
        var first = Lines[range.StartLineNumber - 1];
        var firstStart = Math.Min(range.StartColumn, first.Length + 1) - 1;
        builder.Append(first, firstStart, first.Length - firstStart);
        for (var lineNumber = range.StartLineNumber + 1; lineNumber < range.EndLineNumber; lineNumber++)
        {
            builder.Append('\n').Append(Lines[lineNumber - 1]);
        }

        var last = Lines[range.EndLineNumber - 1];
        builder.Append('\n').Append(last, 0, Math.Min(range.EndColumn - 1, last.Length));
        return builder.ToString();
    }
}

/// <summary>The line-level helpers of rangeMapping.ts.</summary>
internal static class RangeMappings
{
    /// <summary>Groups character-level range mappings into line range mappings (upstream's lineRangeMappingFromRangeMappings).</summary>
    public static List<DetailedLineRangeMapping> LineRangeMappingFromRangeMappings(
        IReadOnlyList<RangeMapping> alignments, DiffTextLines originalLines, DiffTextLines modifiedLines, bool dontAssertStartLine = false)
    {
        var changes = new List<DetailedLineRangeMapping>();
        foreach (var g in DiffArrays.GroupAdjacentBy(
                     alignments.Select(a => GetLineRangeMapping(a, originalLines, modifiedLines)),
                     (a1, a2) => a1.Original.IntersectsOrTouches(a2.Original) || a1.Modified.IntersectsOrTouches(a2.Modified)))
        {
            var first = g[0];
            var last = g[^1];
            changes.Add(new DetailedLineRangeMapping(
                first.Original.Join(last.Original),
                first.Modified.Join(last.Modified),
                g.Select(a => a.InnerChanges![0]).ToList()));
        }

        Debug.Assert(CheckChanges(changes, originalLines, modifiedLines, dontAssertStartLine));
        return changes;
    }

    private static bool CheckChanges(List<DetailedLineRangeMapping> changes, DiffTextLines originalLines, DiffTextLines modifiedLines, bool dontAssertStartLine)
    {
        if (!dontAssertStartLine && changes.Count > 0)
        {
            if (changes[0].Modified.StartLineNumber != changes[0].Original.StartLineNumber)
            {
                return false;
            }

            if (modifiedLines.LineCount - changes[^1].Modified.EndLineNumberExclusive != originalLines.LineCount - changes[^1].Original.EndLineNumberExclusive)
            {
                return false;
            }
        }

        for (var i = 1; i < changes.Count; i++)
        {
            var m1 = changes[i - 1];
            var m2 = changes[i];
            if (!(m2.Original.StartLineNumber - m1.Original.EndLineNumberExclusive == m2.Modified.StartLineNumber - m1.Modified.EndLineNumberExclusive
                  // There has to be an unchanged line in between (otherwise both diffs should have been joined)
                  && m1.Original.EndLineNumberExclusive < m2.Original.StartLineNumber
                  && m1.Modified.EndLineNumberExclusive < m2.Modified.StartLineNumber))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The lines a range mapping changes (upstream's getLineRangeMapping).</summary>
    public static DetailedLineRangeMapping GetLineRangeMapping(RangeMapping rangeMapping, DiffTextLines originalLines, DiffTextLines modifiedLines)
    {
        var lineStartDelta = 0;
        var lineEndDelta = 0;

        // original: ]xxx \n <- this line is not modified
        // modified: ]xx  \n
        if (rangeMapping.ModifiedRange.EndColumn == 1 && rangeMapping.OriginalRange.EndColumn == 1
            && rangeMapping.OriginalRange.StartLineNumber + lineStartDelta <= rangeMapping.OriginalRange.EndLineNumber
            && rangeMapping.ModifiedRange.StartLineNumber + lineStartDelta <= rangeMapping.ModifiedRange.EndLineNumber)
        {
            // We can only do this if the range is not empty yet
            lineEndDelta = -1;
        }

        // original: xxx[ \n <- this line is not modified
        // modified: xxx[ \n
        if (rangeMapping.ModifiedRange.StartColumn - 1 >= modifiedLines.GetLineLength(rangeMapping.ModifiedRange.StartLineNumber)
            && rangeMapping.OriginalRange.StartColumn - 1 >= originalLines.GetLineLength(rangeMapping.OriginalRange.StartLineNumber)
            && rangeMapping.OriginalRange.StartLineNumber <= rangeMapping.OriginalRange.EndLineNumber + lineEndDelta
            && rangeMapping.ModifiedRange.StartLineNumber <= rangeMapping.ModifiedRange.EndLineNumber + lineEndDelta)
        {
            // We can only do this if the range is not empty yet
            lineStartDelta = 1;
        }

        var originalLineRange = new LineRange(
            rangeMapping.OriginalRange.StartLineNumber + lineStartDelta,
            rangeMapping.OriginalRange.EndLineNumber + 1 + lineEndDelta);
        var modifiedLineRange = new LineRange(
            rangeMapping.ModifiedRange.StartLineNumber + lineStartDelta,
            rangeMapping.ModifiedRange.EndLineNumber + 1 + lineEndDelta);

        return new DetailedLineRangeMapping(originalLineRange, modifiedLineRange, [rangeMapping]);
    }

    /// <summary>
    /// Applies the inner changes of <paramref name="changes"/> to the original lines and returns the text (lines joined
    /// by \n); with a correct diff it equals the modified text (upstream's verifyLinesDiff through TextEdit).
    /// </summary>
    public static string ApplyToLines(IReadOnlyList<DetailedLineRangeMapping> changes, IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines)
    {
        var original = string.Join('\n', originalLines);
        var modified = new DiffTextLines(modifiedLines);
        var lineStarts = new int[originalLines.Count];
        for (int i = 1, offset = 0; i < originalLines.Count; i++)
        {
            offset += originalLines[i - 1].Length + 1;
            lineStarts[i] = offset;
        }

        int OffsetOf(TextPosition position)
        {
            var line = Math.Clamp(position.LineNumber, 1, originalLines.Count);
            var column = Math.Clamp(position.Column, 1, originalLines[line - 1].Length + 1);
            return lineStarts[line - 1] + column - 1;
        }

        var builder = new StringBuilder();
        var last = 0;
        foreach (var change in changes)
        {
            foreach (var inner in change.InnerChanges ?? throw new InvalidOperationException("The diff has no inner changes"))
            {
                var start = OffsetOf(inner.OriginalRange.Start);
                var end = OffsetOf(inner.OriginalRange.End);
                if (start < last)
                {
                    throw new InvalidOperationException("Inner changes overlap or are not sorted");
                }

                builder.Append(original, last, start - last);
                builder.Append(modified.GetValueOfRange(inner.ModifiedRange));
                last = end;
            }
        }

        builder.Append(original, last, original.Length - last);
        return builder.ToString();
    }
}

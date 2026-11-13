namespace TKCode.Arc;

/// <summary>Where a reader is in an article, worked out from the scroll position and the tops of its headings.</summary>
/// <param name="ActiveIndex">The section being read: the last heading that has passed the reading line, or -1 before the first.</param>
/// <param name="SectionProgress">How far through that section the reading line is (0 to 1).</param>
/// <param name="Progress">How far through the whole article the reader has scrolled (0 to 1).</param>
public readonly record struct TocProgressPosition(int ActiveIndex, double SectionProgress, double Progress);

/// <summary>The pure rules of <see cref="TocProgress"/>.</summary>
internal static class TocProgressMath
{
    /// <summary>Words a minute assumed when the reading time is estimated from the article's text.</summary>
    public const double WordsPerMinute = 230;

    /// <summary>
    /// Where the reading line is, in content coordinates. It sits <paramref name="lineOffset"/> below the viewport's top
    /// edge; over the last stretch of the article (the last <c>viewport − lineOffset</c> pixels of scrolling) it eases
    /// down to the viewport's bottom edge on a quadratic curve, so the last sections are reached when the scroll bottoms
    /// out even when their headings never get to the line.
    /// </summary>
    public static double ReadingLine(double offset, double viewport, double extent, double lineOffset)
    {
        var line = offset + lineOffset;
        var maxOffset = Math.Max(0, extent - viewport);
        var zone = viewport - lineOffset;
        if (maxOffset <= 0.5 || zone <= 0.5)
        {
            return line;
        }
        var start = maxOffset - zone;
        if (offset > start)
        {
            var t = Math.Clamp((offset - start) / zone, 0, 1);
            line += zone * t * t;
        }
        return line;
    }

    /// <summary>
    /// Locates the reader with the reading line <paramref name="activationRatio"/> of the viewport below its top edge (see
    /// <see cref="LocateAt"/>).
    /// </summary>
    public static TocProgressPosition Locate(ReadOnlySpan<double> tops, double offset, double viewport, double extent, double activationRatio) =>
        LocateAt(tops, offset, viewport, extent, viewport * Math.Clamp(activationRatio, 0, 1));

    /// <summary>
    /// Locates the reader. The reading line sits <paramref name="lineOffset"/> pixels below the viewport's top edge (eased
    /// to the bottom edge near the end, <see cref="ReadingLine"/>); a heading at or above it has been reached, and the
    /// section runs until the next heading reaches it. Once the article cannot scroll further the last section counts as
    /// read.
    /// </summary>
    public static TocProgressPosition LocateAt(ReadOnlySpan<double> tops, double offset, double viewport, double extent, double lineOffset)
    {
        var maxOffset = Math.Max(0, extent - viewport);
        var overall = maxOffset <= 0.5 ? 1 : Math.Clamp(offset / maxOffset, 0, 1);
        if (tops.Length == 0)
        {
            return new TocProgressPosition(-1, 0, overall);
        }
        if (maxOffset > 0.5 && offset >= maxOffset - 1)
        {
            return new TocProgressPosition(tops.Length - 1, 1, 1);
        }
        var line = ReadingLine(offset, viewport, extent, lineOffset);
        var active = -1;
        for (var i = 0; i < tops.Length; i++)
        {
            if (tops[i] <= line + 0.5)
            {
                active = i;
            }
            else
            {
                break;
            }
        }
        if (active < 0)
        {
            return new TocProgressPosition(-1, 0, overall);
        }
        var start = tops[active];
        var end = active + 1 < tops.Length ? tops[active + 1] : Math.Max(start + 1, extent);
        var section = end - start <= 0.5 ? 1 : Math.Clamp((line - start) / (end - start), 0, 1);
        return new TocProgressPosition(active, section, overall);
    }

    /// <summary>The reading position as one number: the section index plus how far through it (0 before the first heading).</summary>
    public static double ReadingPosition(TocProgressPosition position) =>
        position.ActiveIndex < 0 ? 0 : position.ActiveIndex + Math.Clamp(position.SectionProgress, 0, 1);

    /// <summary>
    /// The whole minutes left at <paramref name="reading"/> (0 to <paramref name="sections"/>) of an article that takes
    /// <paramref name="totalMinutes"/>: rounded up, at least 1 until the end, and 0 once everything is read.
    /// </summary>
    public static int MinutesLeft(double reading, int sections, double totalMinutes)
    {
        if (sections <= 0 || reading >= sections - 1e-3)
        {
            return 0;
        }
        var left = Math.Max(0, totalMinutes) * (1 - Math.Clamp(reading, 0, sections) / sections);
        return Math.Max(1, (int)Math.Ceiling(left - 1e-9));
    }

    /// <summary>Counts the words (runs of non-space characters) in <paramref name="text"/>.</summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// The scroll offset that brings a heading at <paramref name="top"/> to <paramref name="margin"/> below the viewport's
    /// top edge, inside the range the article can scroll.
    /// </summary>
    public static double OffsetFor(double top, double margin, double viewport, double extent) =>
        Math.Clamp(top - margin, 0, Math.Max(0, extent - viewport));

    /// <summary>
    /// Where the fill along the rail ends: inside the active row, as far down as the section has been read. Before the
    /// first section it is 0; when everything is read it reaches the bottom of the last row.
    /// </summary>
    public static double FillEnd(ReadOnlySpan<double> rowTops, ReadOnlySpan<double> rowHeights, int active, double section)
    {
        if (active < 0 || active >= rowTops.Length)
        {
            return 0;
        }
        return rowTops[active] + rowHeights[active] * Math.Clamp(section, 0, 1);
    }
}

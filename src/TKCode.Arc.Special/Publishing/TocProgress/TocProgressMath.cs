namespace TKCode.Arc;

/// <summary>Where a reader is in an article, worked out from the scroll position and the tops of its headings.</summary>
/// <param name="ActiveIndex">The section being read: the last heading that has passed the reading line, or -1 before the first.</param>
/// <param name="SectionProgress">How far through that section the reading line is (0 to 1).</param>
/// <param name="Progress">How far through the whole article the reader has scrolled (0 to 1).</param>
public readonly record struct TocProgressPosition(int ActiveIndex, double SectionProgress, double Progress);

/// <summary>The pure rules of <see cref="TocProgress"/>.</summary>
internal static class TocProgressMath
{
    /// <summary>
    /// Locates the reader. The reading line sits <paramref name="activationRatio"/> of the viewport below its top edge; a
    /// heading at or above it has been reached. Once the article cannot scroll further the last section counts as read,
    /// even when its heading never reaches the line (short last sections).
    /// </summary>
    public static TocProgressPosition Locate(ReadOnlySpan<double> tops, double offset, double viewport, double extent, double activationRatio)
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
        var line = offset + viewport * Math.Clamp(activationRatio, 0, 1);
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

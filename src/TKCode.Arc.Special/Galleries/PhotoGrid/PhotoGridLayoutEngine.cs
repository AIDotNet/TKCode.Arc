using Avalonia;

namespace TKCode.Arc;

/// <summary>The two arrangements of a <see cref="PhotoGrid"/>.</summary>
public enum PhotoGridLayout
{
    /// <summary>
    /// Rows of even height that fill the width exactly: each row takes photos until they would be wider than the grid at
    /// the target height, then all of them are scaled together to fit. Every photo keeps its proportions; the last row
    /// keeps the target height.
    /// </summary>
    Justified,

    /// <summary>Columns of equal width; each photo goes to the shortest column at its own height.</summary>
    Masonry
}

/// <summary>
/// The arithmetic of <see cref="PhotoGrid"/>'s two layouts: from the photos' proportions, the width and the settings to
/// one rectangle per photo. Pure and allocation free (the caller owns the output), so it is unit tested directly.
/// </summary>
internal static class PhotoGridLayoutEngine
{
    /// <summary>Proportions assumed for a photo whose own are not known yet (3:2).</summary>
    public const double DefaultRatio = 1.5;

    /// <summary>
    /// Lays out <paramref name="ratios"/> (width / height of each photo) into <paramref name="rects"/> and returns the
    /// total height. The photo at <paramref name="selected"/> (or none, -1) gets a row of its own, as large as fits the
    /// width up to <paramref name="selectedMaxHeight"/>, centred.
    /// </summary>
    public static double Arrange(ReadOnlySpan<double> ratios, Span<Rect> rects, double width, double spacing, PhotoGridLayout layout,
                                 double rowHeight, double columnWidth, int selected, double selectedMaxHeight, Span<double> columns)
    {
        if (ratios.Length == 0 || !(width > 0))
        {
            for (var i = 0; i < rects.Length; i++)
            {
                rects[i] = default;
            }
            return 0;
        }
        spacing = Math.Max(0, spacing);
        return layout == PhotoGridLayout.Masonry
            ? Masonry(ratios, rects, width, spacing, columnWidth, selected, selectedMaxHeight, columns)
            : Justified(ratios, rects, width, spacing, rowHeight, selected, selectedMaxHeight);
    }

    /// <summary>The number of masonry columns for <paramref name="width"/>.</summary>
    public static int ColumnCount(double width, double spacing, double columnWidth) =>
        width > 0 ? Math.Max(1, (int)Math.Floor((width + spacing) / (Math.Max(1, columnWidth) + spacing))) : 1;

    private static double Ratio(ReadOnlySpan<double> ratios, int index) =>
        ratios[index] > 0 && double.IsFinite(ratios[index]) ? Math.Clamp(ratios[index], 0.1, 10) : DefaultRatio;

    private static double Justified(ReadOnlySpan<double> ratios, Span<Rect> rects, double width, double spacing, double rowHeight,
                                    int selected, double selectedMaxHeight)
    {
        rowHeight = rowHeight > 0 && double.IsFinite(rowHeight) ? rowHeight : 200;
        var y = 0.0;
        var start = 0;
        var sum = 0.0;
        for (var i = 0; i < ratios.Length; i++)
        {
            if (i == selected)
            {
                // The row so far closes early (stretched only if it is nearly full), and the selected photo gets its own.
                if (i > start)
                {
                    y = PlaceRow(ratios, rects, start, i, sum, width, spacing, rowHeight, y, stretch: NearlyFull(sum, i - start, width, spacing, rowHeight));
                }
                y = PlaceAlone(rects, i, Ratio(ratios, i), width, selectedMaxHeight, y) + spacing;
                start = i + 1;
                sum = 0;
                continue;
            }
            sum += Ratio(ratios, i);
            var count = i - start + 1;
            if (sum * rowHeight + spacing * (count - 1) >= width)
            {
                y = PlaceRow(ratios, rects, start, i + 1, sum, width, spacing, rowHeight, y, stretch: true);
                start = i + 1;
                sum = 0;
            }
        }
        if (start < ratios.Length)
        {
            y = PlaceRow(ratios, rects, start, ratios.Length, sum, width, spacing, rowHeight, y, stretch: false);
        }
        return Math.Max(0, y - spacing);
    }

    private static bool NearlyFull(double sum, int count, double width, double spacing, double rowHeight) =>
        sum * rowHeight + spacing * (count - 1) >= width * 0.7;

    /// <summary>Places photos [start, end) in one row and returns the top of the next row.</summary>
    private static double PlaceRow(ReadOnlySpan<double> ratios, Span<Rect> rects, int start, int end, double sum, double width,
                                   double spacing, double rowHeight, double y, bool stretch)
    {
        var count = end - start;
        var height = rowHeight;
        if (stretch || sum * rowHeight + spacing * (count - 1) > width)
        {
            height = Math.Max(1, (width - spacing * (count - 1)) / sum);
        }
        var x = 0.0;
        for (var i = start; i < end; i++)
        {
            var w = Ratio(ratios, i) * height;
            if (stretch && i == end - 1)
            {
                // The last photo takes up the rounding, so the row ends exactly at the edge.
                w = Math.Max(1, width - x);
            }
            rects[i] = new Rect(x, y, w, height);
            x += w + spacing;
        }
        return y + height + spacing;
    }

    /// <summary>Places one photo on a row of its own, centred; returns the bottom of the photo.</summary>
    private static double PlaceAlone(Span<Rect> rects, int index, double ratio, double width, double maxHeight, double y)
    {
        maxHeight = maxHeight > 0 && double.IsFinite(maxHeight) ? maxHeight : double.PositiveInfinity;
        var height = Math.Min(width / ratio, maxHeight);
        var w = height * ratio;
        rects[index] = new Rect((width - w) / 2, y, w, height);
        return y + height;
    }

    private static double Masonry(ReadOnlySpan<double> ratios, Span<Rect> rects, double width, double spacing, double columnWidth,
                                  int selected, double selectedMaxHeight, Span<double> columns)
    {
        var count = Math.Min(ColumnCount(width, spacing, columnWidth), columns.Length);
        var column = Math.Max(1, (width - spacing * (count - 1)) / count);
        var heights = columns[..count];
        heights.Clear();
        for (var i = 0; i < ratios.Length; i++)
        {
            if (i == selected)
            {
                // The selected photo spans every column below the tallest one.
                var top = 0.0;
                foreach (var h in heights)
                {
                    top = Math.Max(top, h);
                }
                var bottom = PlaceAlone(rects, i, Ratio(ratios, i), width, selectedMaxHeight, top) + spacing;
                heights.Fill(bottom);
                continue;
            }
            var shortest = 0;
            for (var c = 1; c < count; c++)
            {
                if (heights[c] < heights[shortest] - 0.5)
                {
                    shortest = c;
                }
            }
            var height = column / Ratio(ratios, i);
            rects[i] = new Rect(shortest * (column + spacing), heights[shortest], column, height);
            heights[shortest] += height + spacing;
        }
        var total = 0.0;
        foreach (var h in heights)
        {
            total = Math.Max(total, h);
        }
        return Math.Max(0, total - spacing);
    }
}

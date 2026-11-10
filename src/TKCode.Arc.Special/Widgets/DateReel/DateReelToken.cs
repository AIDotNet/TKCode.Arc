namespace TKCode.Arc;

/// <summary>
/// Geometry and release physics of <see cref="DateReel"/>. Text, the selection band and the focus ring take their colors,
/// radii and fonts from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DateReelToken : AbstractControlDesignToken
{
    /// <summary>Height of one row on the wheel, and of the selection band.</summary>
    public double ItemHeight { get; set; }

    /// <summary>Space at the sides of each column.</summary>
    public double ColumnPadding { get; set; }

    /// <summary>Space between the columns.</summary>
    public double ColumnSpacing { get; set; }

    /// <summary>How strongly the rows away from the middle fade (0 none, 1 to nothing at the edge).</summary>
    public double EdgeFade { get; set; }

    /// <summary>Per-millisecond decay of a flick: how far a released wheel coasts (0.99 short, 0.998 long).</summary>
    public double FlickDecay { get; set; }

    /// <summary>Rows the wheel moves for Page Up and Page Down.</summary>
    public int PageRows { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemHeight = 36;
        ColumnPadding = 10;
        ColumnSpacing = 4;
        EdgeFade = 0.85;
        FlickDecay = 0.995;
        PageRows = 5;
    }
}

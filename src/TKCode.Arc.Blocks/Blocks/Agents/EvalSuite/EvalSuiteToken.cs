using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry, timing and the accent-independent tints of <see cref="EvalSuite"/>. Colors that follow the accent, radii and
/// fonts come from ArcDesign tokens in the theme. The compact values apply while the suite is at most
/// <see cref="CompactWidth"/> wide.
/// </summary>
[ControlDesignToken]
internal sealed class EvalSuiteToken : AbstractControlDesignToken
{
    /// <summary>The card is at most this wide (960) and centered.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>At or below this width of the suite the paddings tighten, the tiles stack and the check column hides (620).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the header (20 20 16).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Padding of the header in a compact suite (16 16 12).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Padding of the summary, filter and detail sections (20 at the sides).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>Section padding in a compact suite (16 at the sides).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>Space between the summary tiles (12).</summary>
    public double TileGap { get; set; }

    /// <summary>Padding inside a summary tile (14 12).</summary>
    public Thickness TilePadding { get; set; }

    /// <summary>Size of a pass rate figure (28).</summary>
    public double RateFontSize { get; set; }

    /// <summary>Height of a case row (56).</summary>
    public double RowHeight { get; set; }

    /// <summary>Height of the column titles (32).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Space at the start and end of a row (20; 12 compact).</summary>
    public double RowInset { get; set; }

    /// <summary>Row inset in a compact suite (12).</summary>
    public double CompactRowInset { get; set; }

    /// <summary>Width of the status column (124).</summary>
    public double StatusColumnWidth { get; set; }

    /// <summary>Width of the check column (96; hidden when compact).</summary>
    public double CheckColumnWidth { get; set; }

    /// <summary>Width of each verdict column (92).</summary>
    public double VerdictColumnWidth { get; set; }

    /// <summary>Width of each verdict column in a compact suite (60, mark only).</summary>
    public double CompactVerdictColumnWidth { get; set; }

    /// <summary>Height of a status chip (24).</summary>
    public double ChipHeight { get; set; }

    /// <summary>The list scrolls inside the card above this height (448).</summary>
    public double ListMaxHeight { get; set; }

    /// <summary>The tint behind a hovered row: the muted surface at 70%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The tint behind the open case's row: the foreground at 6%.</summary>
    public Color RowSelectedBackground { get; set; }

    /// <summary>The line between rows: the subtle border.</summary>
    public Color RowDivider { get; set; }

    /// <summary>A result chip fades and rises in over this long when its case finishes (240ms).</summary>
    public TimeSpan RevealDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 960;
        CompactWidth = 620;
        HeaderPadding = new Thickness(20, 20, 20, 16);
        CompactHeaderPadding = new Thickness(16, 16, 16, 12);
        SectionPadding = new Thickness(20, 0, 20, 0);
        CompactSectionPadding = new Thickness(16, 0, 16, 0);
        TileGap = 12;
        TilePadding = new Thickness(14, 12, 14, 12);
        RateFontSize = 28;
        RowHeight = 56;
        HeaderHeight = 32;
        RowInset = 20;
        CompactRowInset = 12;
        StatusColumnWidth = 124;
        CheckColumnWidth = 96;
        VerdictColumnWidth = 92;
        CompactVerdictColumnWidth = 60;
        ChipHeight = 24;
        ListMaxHeight = 448;
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.7);
        RowSelectedBackground = Oklch.WithAlpha(n.Foreground, 0.06);
        RowDivider = n.BorderSubtle;
        RevealDuration = TimeSpan.FromMilliseconds(240);
    }
}

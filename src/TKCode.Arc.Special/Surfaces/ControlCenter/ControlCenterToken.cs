using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="ControlCenter"/> (Arc Control center, design reference), as
/// measured: a 360 × 396 panel with radius 30 and 10px padding, tiles with radius 22, a detail inset 8 with radius 22,
/// the dial's track, ticks and the meters' segment track as fractions of the text color, and the scrim as 55% of the
/// raised surface. Accent, surfaces, text, shadows and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ControlCenterToken : AbstractControlDesignToken
{
    /// <summary>The panel's corner radius (30).</summary>
    public CornerRadius PanelCornerRadius { get; set; }

    /// <summary>A tile's and the detail's corner radius (22).</summary>
    public CornerRadius TileCornerRadius { get; set; }

    /// <summary>A tile's keyboard ring: 2px outside a 2px gap, so its radius is 26.</summary>
    public CornerRadius TileFocusRingRadius { get; set; }

    /// <summary>How much accent an "on" tile mixes into its muted surface (9%).</summary>
    public double TileOnTint { get; set; }

    /// <summary>The meters' unfilled segments and the mode switch's well: the text color at 11% and 6%.</summary>
    public Color MeterTrackColor { get; set; }

    public Color MutedFillColor { get; set; }

    /// <summary>The dial's track (text at 8%), its eight long ticks (36%) and its sixteen short ticks (16%, dark 36%).</summary>
    public Color DialTrackColor { get; set; }

    public Color DialMajorTickColor { get; set; }

    public Color DialMinorTickColor { get; set; }

    /// <summary>The scrim over the tiles while a detail is open: the raised surface at 55%.</summary>
    public Color ScrimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelCornerRadius = new CornerRadius(30);
        TileCornerRadius = new CornerRadius(22);
        TileFocusRingRadius = new CornerRadius(26);
        TileOnTint = 0.09;
        var n = ArcPalette.Neutrals(isDarkMode);
        MeterTrackColor = Oklch.WithAlpha(n.Foreground, 0.11);
        MutedFillColor = Oklch.WithAlpha(n.Foreground, 0.06);
        DialTrackColor = Oklch.WithAlpha(n.Foreground, 0.08);
        DialMajorTickColor = Oklch.WithAlpha(n.Foreground, 0.36);
        DialMinorTickColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.36 : 0.16);
        ScrimColor = Oklch.WithAlpha(n.SurfaceRaised, 0.55);
    }
}

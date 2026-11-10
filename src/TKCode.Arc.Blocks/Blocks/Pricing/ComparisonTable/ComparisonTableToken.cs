// Ported from Arc's free comparison-table block (MIT, Copyright (c) 2026 Elia Kuratli): comparison-table.module.css.
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ComparisonTable"/> and the colors it derives from the neutrals; accent colors come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class ComparisonTableToken : AbstractControlDesignToken
{
    /// <summary>The block's content is at most this wide (1080).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The intro is at most this wide (560).</summary>
    public double IntroMaxWidth { get; set; }

    /// <summary>The padding around the content (64 24).</summary>
    public Thickness ContentInset { get; set; }

    /// <summary>The padding around the content in the stacked layout (48 16).</summary>
    public Thickness ContentInsetStacked { get; set; }

    /// <summary>A feature row's minimum height (56).</summary>
    public double RowMinHeight { get; set; }

    /// <summary>The header row's minimum height (84).</summary>
    public double HeadMinHeight { get; set; }

    /// <summary>The header row's minimum height in the stacked layout (72).</summary>
    public double HeadMinHeightStacked { get; set; }

    /// <summary>The share of the accent in your column's band (.06).</summary>
    public double BandOpacity { get; set; }

    /// <summary>The share of the accent in your column's band on a hovered row (.09).</summary>
    public double BandHoverOpacity { get; set; }

    /// <summary>The corner radius at the band's ends (20).</summary>
    public CornerRadius BandRadius { get; set; }

    /// <summary>A hovered row's fill: the foreground at 2.5%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The fill behind the pinned header: the background at 94%.</summary>
    public Color HeadBackground { get; set; }

    /// <summary>The circle of a check outside your column: the foreground at 8%.</summary>
    public Color CheckCircle { get; set; }

    /// <summary>The button's fill once it is done: the success color at 14%.</summary>
    public Color CtaDoneBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ContentMaxWidth = 1080;
        IntroMaxWidth = 560;
        ContentInset = new Thickness(24, 64);
        ContentInsetStacked = new Thickness(16, 48);
        RowMinHeight = 56;
        HeadMinHeight = 84;
        HeadMinHeightStacked = 72;
        BandOpacity = 0.06;
        BandHoverOpacity = 0.09;
        BandRadius = new CornerRadius(20);
        RowHoverBackground = Oklch.WithAlpha(n.Foreground, 0.025);
        HeadBackground = Oklch.WithAlpha(n.Background, 0.94);
        CheckCircle = Oklch.WithAlpha(n.Foreground, 0.08);
        CtaDoneBackground = Oklch.WithAlpha(n.Success, 0.14);
    }
}

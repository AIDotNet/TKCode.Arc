using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="ActivityRings"/>. Ring colors come from the theme's series colors and the legend's
/// text from the ArcDesign text tokens.
/// </summary>
[ControlDesignToken]
internal sealed class ActivityRingsToken : AbstractControlDesignToken
{
    /// <summary>Outer diameter of the rings when there is room for it.</summary>
    public double Diameter { get; set; }

    /// <summary>Stroke width of each ring.</summary>
    public double RingThickness { get; set; }

    /// <summary>Space between two rings.</summary>
    public double RingGap { get; set; }

    /// <summary>How strongly the empty track under a ring shows its color (0 to 1).</summary>
    public double TrackOpacity { get; set; }

    /// <summary>Shade under the head of a ring that has gone past its goal, where it overlaps its own start.</summary>
    public Color OverlapShadowColor { get; set; }

    /// <summary>Space between the rings and the legend.</summary>
    public double LegendSpacing { get; set; }

    /// <summary>Space between legend rows.</summary>
    public double LegendRowSpacing { get; set; }

    /// <summary>Below this width the legend moves under the rings.</summary>
    public double SideLegendMinWidth { get; set; }

    /// <summary>Size of the small ring in front of each legend row.</summary>
    public double LegendRingSize { get; set; }

    /// <summary>Stroke of the small ring in front of each legend row.</summary>
    public double LegendRingThickness { get; set; }

    /// <summary>How long a ring takes to fill, before its spring settles.</summary>
    public TimeSpan FillDuration { get; set; }

    /// <summary>Bounce of the fill spring (0 none, 1 most).</summary>
    public double FillBounce { get; set; }

    /// <summary>Delay between rings when they fill on first view, outer ring first.</summary>
    public TimeSpan RingStagger { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Diameter = 168;
        RingThickness = 15;
        RingGap = 3;
        TrackOpacity = isDarkMode ? 0.24 : 0.16;
        OverlapShadowColor = Color.FromArgb(isDarkMode ? (byte)0x8C : (byte)0x59, 0, 0, 0);
        LegendSpacing = 24;
        LegendRowSpacing = 12;
        SideLegendMinWidth = 340;
        LegendRingSize = 16;
        LegendRingThickness = 3;
        FillDuration = TimeSpan.FromMilliseconds(900);
        FillBounce = 0.18;
        RingStagger = TimeSpan.FromMilliseconds(90);
    }
}

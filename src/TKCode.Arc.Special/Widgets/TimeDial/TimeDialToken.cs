using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TimeDial"/>. The face, track, fill, handle, text and focus ring take their colors and fonts from
/// the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TimeDialToken : AbstractControlDesignToken
{
    /// <summary>Diameter of the dial when there is room for it.</summary>
    public double DialSize { get; set; }

    /// <summary>Stroke width of the track the handle runs on.</summary>
    public double TrackThickness { get; set; }

    /// <summary>Distance from the dial's edge to the middle of the track.</summary>
    public double TrackInset { get; set; }

    /// <summary>Radius of the handle.</summary>
    public double HandleRadius { get; set; }

    /// <summary>How much larger the handle grows while it is held (1 = not at all).</summary>
    public double HandlePressScale { get; set; }

    /// <summary>Length of a minor tick.</summary>
    public double MinorTickLength { get; set; }

    /// <summary>Length of a major tick.</summary>
    public double MajorTickLength { get; set; }

    /// <summary>Gap between the inside of the track and the ticks.</summary>
    public double TickGap { get; set; }

    /// <summary>Space between the focus ring and the handle.</summary>
    public double FocusRingGap { get; set; }

    /// <summary>A press this far outside or inside the track still grabs it.</summary>
    public double GrabSlack { get; set; }

    /// <summary>Soft shade under the handle.</summary>
    public Color HandleShadowColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DialSize = 240;
        TrackThickness = 10;
        TrackInset = 16;
        HandleRadius = 13;
        HandlePressScale = 1.12;
        MinorTickLength = 4;
        MajorTickLength = 9;
        TickGap = 12;
        FocusRingGap = 3;
        GrabSlack = 18;
        HandleShadowColor = Color.FromArgb(isDarkMode ? (byte)0x66 : (byte)0x2E, 0, 0, 0);
    }
}

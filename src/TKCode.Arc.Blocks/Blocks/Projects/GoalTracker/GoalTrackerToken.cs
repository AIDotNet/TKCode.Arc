using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="GoalTracker"/>. Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class GoalTrackerToken : AbstractControlDesignToken
{
    /// <summary>At or below this width key result rows stack their parts (640).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's padding (24) and its compact form (16).</summary>
    public Thickness Padding { get; set; }

    public Thickness CompactPadding { get; set; }

    /// <summary>A goal card's padding (16).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>A progress bar's height (6).</summary>
    public double BarHeight { get; set; }

    /// <summary>One timeline row's height (30) and the timeline's label gutter (200).</summary>
    public double TimelineRowHeight { get; set; }

    public double TimelineGutter { get; set; }

    /// <summary>An owner avatar's diameter (24).</summary>
    public double AvatarDiameter { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 640;
        Padding = new Thickness(24);
        CompactPadding = new Thickness(16);
        CardPadding = new Thickness(16);
        BarHeight = 6;
        TimelineRowHeight = 30;
        TimelineGutter = 200;
        AvatarDiameter = 24;
    }
}

using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ActivityFeed"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class ActivityFeedToken : AbstractControlDesignToken
{
    /// <summary>The widest the feed grows (640).</summary>
    public double FeedMaxWidth { get; set; }

    /// <summary>Padding inside the feed (20).</summary>
    public Thickness FeedPadding { get; set; }

    /// <summary>The tallest the list grows before it scrolls (560).</summary>
    public double FeedMaxHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FeedMaxWidth = 640;
        FeedPadding = new Thickness(20);
        FeedMaxHeight = 560;
    }
}

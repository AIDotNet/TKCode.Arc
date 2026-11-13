using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="FeedbackBoard"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class FeedbackBoardToken : AbstractControlDesignToken
{
    /// <summary>Padding inside the board (24).</summary>
    public Thickness BoardPadding { get; set; }

    /// <summary>Below this width the open request replaces the list (760).</summary>
    public double StackedWidth { get; set; }

    /// <summary>Width of the open request beside the list (380).</summary>
    public double DetailWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BoardPadding = new Thickness(24);
        StackedWidth = 760;
        DetailWidth = 380;
    }
}

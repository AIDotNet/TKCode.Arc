using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PageCurl"/> (Arc Page curl, design reference). Paper, shade and focus colors come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class PageCurlToken : AbstractControlDesignToken
{
    /// <summary>Room under the book for the hint, so dismissing the hint never moves the book.</summary>
    public double HintAreaHeight { get; set; }

    /// <summary>Space between the book and the page count row.</summary>
    public double ControlsSpacing { get; set; }

    /// <summary>The corner radius of a sheet of paper.</summary>
    public CornerRadius PageRadius { get; set; }

    /// <summary>The keyboard focus ring sits 4px outside the book.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HintAreaHeight = 34;
        ControlsSpacing = 6;
        PageRadius = new CornerRadius(3);
        FocusRingMargin = new Thickness(-4);
    }
}

using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ExpandableTextToken : AbstractControlDesignToken
{
    /// <summary>Gap between the text and the toggle (Arc: space-2).</summary>
    public double ToggleGap { get; set; }

    /// <summary>text-sm × leading-body: the unit of the clamp.</summary>
    public double TextLineHeight { get; set; }

    /// <summary>Arc toggle: min-height 28px, padding 0 2px, margin-left −2px, radius 8px.</summary>
    public double ToggleMinHeight { get; set; }

    public Thickness TogglePadding { get; set; }

    public Thickness ToggleMargin { get; set; }

    public CornerRadius ToggleRadius { get; set; }

    public double ChevronSize { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ToggleGap = 8;
        TextLineHeight = 14 * 1.4;
        ToggleMinHeight = 28;
        TogglePadding = new Thickness(2, 0);
        ToggleMargin = new Thickness(-2, 0, 0, 0);
        ToggleRadius = new CornerRadius(8);
        ChevronSize = 16;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(12);
    }
}

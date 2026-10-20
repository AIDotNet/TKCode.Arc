using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ProgressToken : AbstractControlDesignToken
{
    /// <summary>Track height (Arc: 7px including the 1px border).</summary>
    public double TrackHeight { get; set; }

    /// <summary>Space between the label row and the track.</summary>
    public Thickness MetaMargin { get; set; }

    /// <summary>Line box of the label and count (text-sm × leading-body).</summary>
    public double MetaLineHeight { get; set; }

    /// <summary>Minimum gap between the label and the count.</summary>
    public Thickness ReadoutMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackHeight = 7;
        MetaMargin = new Thickness(0, 0, 0, 8);
        MetaLineHeight = 19.6;
        ReadoutMargin = new Thickness(12, 0, 0, 0);
    }
}

using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of a <see cref="Ribbon"/> (self-designed): a 36px tab row with 13px medium headers (28px high, 12px padding, 2px
/// apart) and a 2px accent underline under the selected one, then the command area with 4px above and below the groups.
/// </summary>
[ControlDesignToken]
internal sealed class RibbonToken : AbstractControlDesignToken
{
    /// <summary>Height of the tab row: 36.</summary>
    public double TabRowHeight { get; set; }

    /// <summary>Padding of the tab row: 8 at the sides.</summary>
    public Thickness TabRowPadding { get; set; }

    /// <summary>Height of a tab header: 28.</summary>
    public double TabHeight { get; set; }

    /// <summary>Padding of a tab header: 12 at the sides.</summary>
    public Thickness TabPadding { get; set; }

    /// <summary>Space between tab headers: 2.</summary>
    public double TabGap { get; set; }

    /// <summary>Size of a tab header's text: 13.</summary>
    public double TabFontSize { get; set; }

    /// <summary>Thickness of the underline under the selected tab: 2.</summary>
    public double IndicatorThickness { get; set; }

    /// <summary>The keyboard focus ring of a tab header sits on its edge: 0.</summary>
    public Thickness TabFocusRingMargin { get; set; }

    /// <summary>Padding of the command area: 4 above and below the groups.</summary>
    public Thickness ContentPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TabRowHeight = 36;
        TabRowPadding = new Thickness(8, 0);
        TabHeight = 28;
        TabPadding = new Thickness(12, 0);
        TabGap = 2;
        TabFontSize = 13;
        IndicatorThickness = 2;
        TabFocusRingMargin = new Thickness(0);
        ContentPadding = new Thickness(0, 4);
    }

    public override void ApplyCompact(bool isDarkMode)
    {
        base.ApplyCompact(isDarkMode);
        TabRowHeight = 28;
        TabRowPadding = new Thickness(6, 0);
        TabHeight = 24;
        TabPadding = new Thickness(8, 0);
        TabFontSize = 12;
        ContentPadding = new Thickness(0, 2);
    }
}

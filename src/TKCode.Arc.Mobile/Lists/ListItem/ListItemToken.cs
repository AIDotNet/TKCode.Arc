using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="ListItem"/>: a 48px row, a 22px icon, a 16px chevron and a hairline separator.</summary>
[ControlDesignToken]
internal sealed class ListItemToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of a row: 48.</summary>
    public double RowHeight { get; set; }

    /// <summary>Padding inside a row: 16 by 10.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>The icon's size: 22.</summary>
    public double IconExtent { get; set; }

    /// <summary>Gap between the icon and the text: 12.</summary>
    public double IconGap { get; set; }

    /// <summary>Margin before the trailing content and the chevron: 8 on the left.</summary>
    public Thickness TrailingMargin { get; set; }

    /// <summary>The chevron's size: 16.</summary>
    public double ChevronExtent { get; set; }

    /// <summary>How far the separator is inset from the row's start: 16.</summary>
    public Thickness SeparatorInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeight = 48;
        ContentPadding = new Thickness(16, 10);
        IconExtent = 22;
        IconGap = 12;
        TrailingMargin = new Thickness(8, 0, 0, 0);
        ChevronExtent = 16;
        SeparatorInset = new Thickness(16, 0, 0, 0);
    }
}

using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Sparkline geometry (Arc: 2.5px line, 12px between the caption and the plot).</summary>
[ControlDesignToken]
internal sealed class SparklineToken : AbstractControlDesignToken
{
    /// <summary>Line stroke width.</summary>
    public double StrokeWidth { get; set; }

    /// <summary>Space between the caption and the plot.</summary>
    public double CaptionGap { get; set; }

    /// <summary>Settles the 14px label on the 18px headline's baseline.</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>Settles the 12px aside on the headline's baseline.</summary>
    public Thickness AsideMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StrokeWidth = 2.5;
        CaptionGap = 12;
        LabelMargin = new Thickness(0, 0, 0, 1);
        AsideMargin = new Thickness(0, 0, 0, 2);
    }
}

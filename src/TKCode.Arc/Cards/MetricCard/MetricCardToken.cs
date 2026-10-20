using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MetricCardToken : AbstractControlDesignToken
{
    /// <summary>Card padding (space-6; space-4 when narrow).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness NarrowCardPadding { get; set; }

    /// <summary>Space under the label row (space-8; space-6 when narrow).</summary>
    public Thickness TopRowMargin { get; set; }

    public Thickness NarrowTopRowMargin { get; set; }

    /// <summary>Space above the context line (space-4).</summary>
    public Thickness ContextMargin { get; set; }

    /// <summary>Change chip padding (3px 8px).</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>A narrow card wraps the chip under the label (space-3 apart).</summary>
    public Thickness NarrowChipMargin { get; set; }

    public double CopyLineHeight { get; set; }

    /// <summary>Sparkline box beside the number.</summary>
    public double SparklineWidth { get; set; }

    public double SparklineHeight { get; set; }

    /// <summary>Signed chips: a faint tint of success / danger behind the colored text.</summary>
    public Color UpFill { get; set; }

    public Color DownFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(24);
        NarrowCardPadding = new Thickness(16);
        TopRowMargin = new Thickness(0, 0, 0, 32);
        NarrowTopRowMargin = new Thickness(0, 0, 0, 24);
        ContextMargin = new Thickness(0, 16, 0, 0);
        ChipPadding = new Thickness(8, 3);
        NarrowChipMargin = new Thickness(0, 12, 0, 0);
        CopyLineHeight = 14 * 1.4;
        SparklineWidth = 96;
        SparklineHeight = 36;
        var n = ArcPalette.Neutrals(isDarkMode);
        UpFill = Oklch.WithAlpha(n.Success, 0.11);
        DownFill = Oklch.WithAlpha(n.Danger, 0.11);
    }
}

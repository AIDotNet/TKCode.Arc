using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SignaturePadToken : AbstractControlDesignToken
{
    /// <summary>Screen blue ink: oklch(46% .17 262) in light, oklch(76% .12 258) in dark.</summary>
    public Color InkBlueColor { get; set; }

    /// <summary>Screen violet ink: oklch(49% .2 292) in light, oklch(77% .13 295) in dark.</summary>
    public Color InkVioletColor { get; set; }

    /// <summary>The faint trail behind the clear wipe's edge (the foreground; the wipe applies its own alpha).</summary>
    public Color SweepColor { get; set; }

    /// <summary>Choice options and toolbar buttons (36px pills).</summary>
    public double OptionSize { get; set; }

    public double ExportMinWidth { get; set; }

    public double GroupDividerHeight { get; set; }

    /// <summary>Gap between the pad, the toolbar and the export row (Arc's grid gap).</summary>
    public double RowGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        InkBlueColor = isDarkMode ? Oklch.ToColor(0.76, 0.12, 258) : Oklch.ToColor(0.46, 0.17, 262);
        InkVioletColor = isDarkMode ? Oklch.ToColor(0.77, 0.13, 295) : Oklch.ToColor(0.49, 0.2, 292);
        SweepColor = neutrals.Foreground;
        OptionSize = 36;
        ExportMinWidth = 76;
        GroupDividerHeight = 20;
        RowGap = 10;
    }
}

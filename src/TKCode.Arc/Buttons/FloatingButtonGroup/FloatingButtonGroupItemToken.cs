using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FloatingButtonGroupItemToken : AbstractControlDesignToken
{
    /// <summary>Button height and minimum width (Arc <c>--fbg-h</c>: 36 / 30px).</summary>
    public double ItemExtentMD { get; set; }
    public double ItemExtentSM { get; set; }

    /// <summary>Inline padding (Arc <c>--fbg-pad-x</c>: 12 / 10px).</summary>
    public Thickness ItemInsetMD { get; set; }
    public Thickness ItemInsetSM { get; set; }

    /// <summary>Inline padding with a leading icon (2px less on the left).</summary>
    public Thickness IconItemInsetMD { get; set; }
    public Thickness IconItemInsetSM { get; set; }

    public CornerRadius ItemRadiusMD { get; set; }
    public CornerRadius ItemRadiusSM { get; set; }

    /// <summary>Icon (16) plus gap (6) before the reserved label text.</summary>
    public double SizerLeading { get; set; }

    /// <summary>Opacity of the control-on tint behind a pressed (checked) item: 12% (16% dark).</summary>
    public double OnTintOpacity { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemExtentMD = 36;
        ItemExtentSM = 30;
        ItemInsetMD = new Thickness(12, 0);
        ItemInsetSM = new Thickness(10, 0);
        IconItemInsetMD = new Thickness(10, 0, 12, 0);
        IconItemInsetSM = new Thickness(8, 0, 10, 0);
        ItemRadiusMD = new CornerRadius(13);
        ItemRadiusSM = new CornerRadius(11);
        SizerLeading = 22;
        OnTintOpacity = isDarkMode ? 0.16 : 0.12;
        DisabledOpacity = 0.5;
    }
}

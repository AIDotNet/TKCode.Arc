using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ToggleGroupToken : AbstractControlDesignToken
{
    /// <summary>Space between the group's edge and its options.</summary>
    public Thickness GroupPadding { get; set; }

    /// <summary>Space between two options; a joined fill reaches across it.</summary>
    public double ItemGap { get; set; }

    /// <summary>An option's height and its smallest width.</summary>
    public double ItemSizeSM { get; set; }
    public double ItemSizeMD { get; set; }
    public Thickness ItemInsetSM { get; set; }
    public Thickness ItemInsetMD { get; set; }
    public double IconSizeSM { get; set; }
    public double IconSizeMD { get; set; }
    public double IconGap { get; set; }

    /// <summary>How much of ControlOn tints the pressed fill over the surface (Arc: 11%, 14% in dark mode).</summary>
    public double FillMix { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public CornerRadius FocusRingRadius { get; set; }
    public double DisabledOpacity { get; set; }
    public double ItemDisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GroupPadding = new Thickness(3);
        ItemGap = 2;
        ItemSizeSM = 28;
        ItemSizeMD = 36;
        ItemInsetSM = new Thickness(8, 0);
        ItemInsetMD = new Thickness(10, 0);
        IconSizeSM = 14;
        IconSizeMD = 16;
        IconGap = 6;
        FillMix = isDarkMode ? 0.14 : 0.11;
        FocusRingMargin = new Thickness(-2);
        FocusRingRadius = new CornerRadius(17);
        DisabledOpacity = 0.52;
        ItemDisabledOpacity = 0.45;
    }
}

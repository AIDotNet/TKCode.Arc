using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MorphSelectToken : AbstractControlDesignToken
{
    /// <summary>Trigger padding: 14px before the value, 36px kept for the chevron.</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>The chevron sits 12px from the trigger's right edge.</summary>
    public Thickness ChevronMargin { get; set; }

    /// <summary>Closed trigger corner (pill at 44px) and the open surface corner.</summary>
    public CornerRadius ShapeCornerRadius { get; set; }

    /// <summary>Closed width cap: min(20rem, viewport − 2rem).</summary>
    public double TriggerMaxWidth { get; set; }

    /// <summary>The highlight: foreground at 6.5% (accent independent).</summary>
    public Color HighlightColor { get; set; }

    /// <summary>Search clear button and close-hit hover: foreground at 8%.</summary>
    public Color ControlFillColor { get; set; }

    public double DisabledOpacity { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        TriggerPadding = new Thickness(14, 0, 36, 0);
        ChevronMargin = new Thickness(0, 0, 12, 0);
        ShapeCornerRadius = new CornerRadius(22);
        TriggerMaxWidth = 320;
        HighlightColor = Oklch.WithAlpha(neutrals.Foreground, 0.065);
        ControlFillColor = Oklch.WithAlpha(neutrals.Foreground, 0.08);
        DisabledOpacity = 0.55;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(26);
    }
}

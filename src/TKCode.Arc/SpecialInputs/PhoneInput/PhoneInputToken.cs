using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PhoneInputToken : AbstractControlDesignToken
{
    /// <summary>The closed trigger sits in a 3px inset inside the anchor (<c>inset: 3px auto 3px 3px</c>).</summary>
    public Thickness TriggerMargin { get; set; }

    /// <summary>Trigger padding: 9px before the flag, 33px kept for the chevron (<c>padding: 0 33px 0 9px</c>).</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Trigger corners: <c>calc(var(--radius-control) - 4px)</c>.</summary>
    public CornerRadius TriggerCornerRadius { get; set; }

    /// <summary>The chevron sits 12px from the anchor's right edge.</summary>
    public Thickness ChevronMargin { get; set; }

    /// <summary>The divider on the anchor's right edge is inset 10px top and bottom.</summary>
    public Thickness DividerMargin { get; set; }

    /// <summary>The number and its guide: <c>padding: 0 36px 0 12px</c>.</summary>
    public Thickness NumberPadding { get; set; }

    /// <summary>The valid check: an 18px slot 12px from the right edge.</summary>
    public Thickness ValidMargin { get; set; }

    public double ValidSlotSize { get; set; }

    /// <summary>The typing guide: <c>color-mix(in oklab, var(--text-muted) 72%, transparent)</c>.</summary>
    public Color GuideColor { get; set; }

    /// <summary>The gliding row highlight: foreground at 6.5%.</summary>
    public Color HighlightColor { get; set; }

    /// <summary>Clear button fill and close-target hover: foreground at 8%.</summary>
    public Color ControlFillColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        TriggerMargin = new Thickness(3);
        TriggerPadding = new Thickness(9, 0, 33, 0);
        TriggerCornerRadius = new CornerRadius(14);
        ChevronMargin = new Thickness(0, 0, 12, 0);
        DividerMargin = new Thickness(0, 10);
        NumberPadding = new Thickness(12, 0, 36, 0);
        ValidMargin = new Thickness(0, 0, 12, 0);
        ValidSlotSize = 18;
        GuideColor = Oklch.WithAlpha(neutrals.TextMuted, 0.72);
        HighlightColor = Oklch.WithAlpha(neutrals.Foreground, 0.065);
        ControlFillColor = Oklch.WithAlpha(neutrals.Foreground, 0.08);
    }
}

using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DateRangePickerToken : AbstractControlDesignToken
{
    /// <summary>Trigger: 44px tall, radius 18, <c>padding: 0 12px 0 14px</c> inside the 1px border, 10px gaps.</summary>
    public CornerRadius TriggerRadius { get; set; }
    public Thickness TriggerInset { get; set; }
    public double TriggerGap { get; set; }
    public Thickness TriggerFocusRingMargin { get; set; }
    public CornerRadius TriggerFocusRingRadius { get; set; }
    public double ValueLineHeight { get; set; }

    /// <summary>Panel: 10px inset (8 compact), 10px between rail and months (8 compact).</summary>
    public Thickness PanelInset { get; set; }
    public Thickness PanelCompactInset { get; set; }
    public double BodyGap { get; set; }
    public double BodyCompactGap { get; set; }

    /// <summary>Preset rail: 9.25rem wide with a 10px inset before its rule; compact it scrolls in a row under a rule.</summary>
    public double RailWidth { get; set; }
    public Thickness RailInset { get; set; }
    public Thickness RailCompactInset { get; set; }
    public Thickness RailCompactMargin { get; set; }
    public double PresetHeight { get; set; }
    public Thickness PresetInset { get; set; }
    public Thickness PresetCompactInset { get; set; }
    public CornerRadius PresetRadius { get; set; }

    /// <summary>Months: 2px side inset, 32px round month buttons (disabled 0.35).</summary>
    public Thickness CalendarsInset { get; set; }
    public double NavButtonSize { get; set; }
    public double NavDisabledOpacity { get; set; }

    /// <summary>Footer: rule 10px under the months (6 compact), <c>padding: 10px 2px 0 12px</c>; 36px actions, Apply at least 4.75rem.</summary>
    public Thickness FooterMargin { get; set; }
    public Thickness FooterCompactMargin { get; set; }
    public Thickness FooterInset { get; set; }
    public Thickness FooterCompactInset { get; set; }
    public double ActionHeight { get; set; }
    public Thickness ActionInset { get; set; }
    public double ApplyMinWidth { get; set; }
    public double ApplyDisabledOpacity { get; set; }
    public double ActionsGap { get; set; }
    public double CountLineHeight { get; set; }

    /// <summary>Accent-independent tints: trigger hover (foreground 3% into the raised surface), preset highlight 6.5%, preset hover 4%, focus 5%, day hover 7%.</summary>
    public Color TriggerHoverFill { get; set; }
    public Color RailHighlightFill { get; set; }
    public Color PresetHoverFill { get; set; }
    public Color PresetFocusFill { get; set; }
    public Color DayHoverFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerRadius = new CornerRadius(18);
        TriggerInset = new Thickness(13, 0, 11, 0);
        TriggerGap = 10;
        TriggerFocusRingMargin = new Thickness(-4);
        TriggerFocusRingRadius = new CornerRadius(22);
        ValueLineHeight = 14 * 1.4;
        PanelInset = new Thickness(10);
        PanelCompactInset = new Thickness(8);
        BodyGap = 10;
        BodyCompactGap = 8;
        RailWidth = 148;
        RailInset = new Thickness(0, 0, 10, 0);
        RailCompactInset = new Thickness(8, 0, 8, 8);
        RailCompactMargin = new Thickness(-8, 0);
        PresetHeight = 32;
        PresetInset = new Thickness(12, 0);
        PresetCompactInset = new Thickness(11, 0);
        PresetRadius = new CornerRadius(12);
        CalendarsInset = new Thickness(2, 0);
        NavButtonSize = 32;
        NavDisabledOpacity = 0.35;
        FooterMargin = new Thickness(0, 10, 0, 0);
        FooterCompactMargin = new Thickness(0, 6, 0, 0);
        FooterInset = new Thickness(12, 10, 2, 0);
        FooterCompactInset = new Thickness(11, 10, 2, 0);
        ActionHeight = 36;
        ActionInset = new Thickness(14, 0);
        ApplyMinWidth = 76;
        ApplyDisabledOpacity = 0.45;
        ActionsGap = 6;
        CountLineHeight = 12 * 1.4;

        var n = ArcPalette.Neutrals(isDarkMode);
        TriggerHoverFill = Oklch.MixOklab(n.SurfaceRaised, n.Foreground, 0.03);
        RailHighlightFill = Oklch.WithAlpha(n.Foreground, 0.065);
        PresetHoverFill = Oklch.WithAlpha(n.Foreground, 0.04);
        PresetFocusFill = Oklch.WithAlpha(n.Foreground, 0.05);
        DayHoverFill = Oklch.WithAlpha(n.Foreground, 0.07);
    }
}

using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SelectToken : AbstractControlDesignToken
{
    /// <summary>Trigger inline padding (space-3).</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Gap between the value and the chevron (space-3).</summary>
    public double ValueGap { get; set; }

    /// <summary>Menu corner: radius-control + 2.</summary>
    public CornerRadius MenuCornerRadius { get; set; }

    public Thickness MenuPadding { get; set; }

    /// <summary>Radix viewport inset (2px top and bottom).</summary>
    public Thickness ViewportPadding { get; set; }

    public double OptionHeight { get; set; }

    /// <summary>Option corner: radius-control − 4.</summary>
    public CornerRadius OptionCornerRadius { get; set; }

    /// <summary>11px before the label, 34px kept for the indicator.</summary>
    public Thickness OptionPadding { get; set; }

    /// <summary>The item indicator sits 10px from the right edge.</summary>
    public double IndicatorInset { get; set; }

    public double OptionDisabledOpacity { get; set; }

    public double ScrollButtonHeight { get; set; }

    public double DisabledOpacity { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerPadding = new Thickness(12, 0);
        ValueGap = 12;
        MenuCornerRadius = new CornerRadius(20);
        MenuPadding = new Thickness(6);
        ViewportPadding = new Thickness(0, 2);
        OptionHeight = 36;
        OptionCornerRadius = new CornerRadius(14);
        OptionPadding = new Thickness(11, 0, 34, 0);
        IndicatorInset = 10;
        OptionDisabledOpacity = 0.45;
        ScrollButtonHeight = 28;
        DisabledOpacity = 0.5;
        FocusRingRadius = new CornerRadius(22);
        FocusRingMargin = new Thickness(-4);
    }
}

using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CascaderToken : AbstractControlDesignToken
{
    /// <summary>Trigger inline padding (space-3).</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Menu corner: radius-control + 2.</summary>
    public CornerRadius MenuCornerRadius { get; set; }

    public Thickness MenuPadding { get; set; }

    public double OptionHeight { get; set; }

    /// <summary>Option corner: radius-control − 4.</summary>
    public CornerRadius OptionCornerRadius { get; set; }

    /// <summary>11px before the label, 34px kept for the chevron or the check.</summary>
    public Thickness OptionPadding { get; set; }

    /// <summary>The chevron and the check sit 10px from the right edge of a column.</summary>
    public double ChevronInset { get; set; }

    public double OptionDisabledOpacity { get; set; }

    public double MinColumnWidth { get; set; }

    public double MaxColumnWidth { get; set; }

    /// <summary>Space between two columns; the divider is drawn in its middle.</summary>
    public double ColumnGap { get; set; }

    public double ClearButtonSize { get; set; }

    public CornerRadius ClearRingRadius { get; set; }

    public CornerRadius ClearGlyphRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerPadding = new Thickness(12, 0);
        MenuCornerRadius = new CornerRadius(20);
        MenuPadding = new Thickness(6);
        OptionHeight = 36;
        OptionCornerRadius = new CornerRadius(14);
        OptionPadding = new Thickness(11, 0, 34, 0);
        ChevronInset = 10;
        OptionDisabledOpacity = 0.45;
        MinColumnWidth = 160;
        MaxColumnWidth = 280;
        ColumnGap = 6;
        ClearButtonSize = 22;
        ClearRingRadius = new CornerRadius(14);
        ClearGlyphRadius = new CornerRadius(11);
        DisabledOpacity = 0.5;
        FocusRingRadius = new CornerRadius(22);
        FocusRingMargin = new Thickness(-4);
    }
}

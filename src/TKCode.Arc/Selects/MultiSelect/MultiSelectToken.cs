using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MultiSelectToken : AbstractControlDesignToken
{
    /// <summary>Trigger padding: 5px block, 12px before the chips, 11px after the chevron.</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Minimum height of the chip row inside the trigger.</summary>
    public double ValueMinHeight { get; set; }

    /// <summary>Chevron gap; a selection reserves 26px more for the clear button beside it.</summary>
    public Thickness ChevronMargin { get; set; }

    public Thickness ChevronMarginWithClear { get; set; }

    /// <summary>The clear button sits 36px from the trigger's right edge.</summary>
    public Thickness ClearButtonMargin { get; set; }

    public double ClearButtonSize { get; set; }

    public double ClearGlyphSize { get; set; }

    /// <summary>Chip: 9rem wide at most, 4px × 8px padding (1px of it is the inset hairline), 5px apart.</summary>
    public double ChipMaxWidth { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipSpacing { get; set; }

    public double MenuOffset { get; set; }

    public Thickness MenuPadding { get; set; }

    public double OptionHeight { get; set; }

    /// <summary>Option corner: radius-panel − 5.</summary>
    public CornerRadius OptionCornerRadius { get; set; }

    public Thickness OptionPadding { get; set; }

    public double OptionDisabledOpacity { get; set; }

    public double DisabledOpacity { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public double SearchRowHeight { get; set; }

    public CornerRadius ClearRingRadius { get; set; }
    public CornerRadius ClearGlyphRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ClearRingRadius = new CornerRadius(14);
        ClearGlyphRadius = new CornerRadius(11);
        TriggerPadding = new Thickness(12, 5, 11, 5);
        ValueMinHeight = 26;
        ChevronMargin = new Thickness(8, 0, 0, 0);
        ChevronMarginWithClear = new Thickness(34, 0, 0, 0);
        ClearButtonMargin = new Thickness(0, 0, 36, 0);
        ClearButtonSize = 22;
        ClearGlyphSize = 14;
        ChipMaxWidth = 144;
        ChipPadding = new Thickness(7, 3);
        ChipSpacing = 5;
        MenuOffset = 8;
        MenuPadding = new Thickness(5);
        OptionHeight = 38;
        OptionCornerRadius = new CornerRadius(21);
        OptionPadding = new Thickness(11, 0);
        OptionDisabledOpacity = 0.42;
        DisabledOpacity = 0.5;
        FocusRingRadius = new CornerRadius(22);
        FocusRingMargin = new Thickness(-4);
        SearchRowHeight = 40;
    }
}

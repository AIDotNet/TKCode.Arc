using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ComboboxToken : AbstractControlDesignToken
{
    /// <summary>Field inline padding (space-3).</summary>
    public Thickness FieldPadding { get; set; }

    /// <summary>Gap between the search glyph, input, clear button and chevron (space-2).</summary>
    public double FieldGap { get; set; }

    public double ClearButtonSize { get; set; }

    /// <summary>The 3px focus ring around the field (box-shadow 0 0 0 3px).</summary>
    public Thickness FocusRingMargin { get; set; }

    public Thickness FocusRingThickness { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>The popover sits 8px under the field.</summary>
    public double PopoverOffset { get; set; }

    public Thickness PopoverPadding { get; set; }

    public double OptionHeight { get; set; }

    /// <summary>Option corner: radius-panel − 5.</summary>
    public CornerRadius OptionCornerRadius { get; set; }

    public Thickness OptionPadding { get; set; }

    public double OptionDisabledOpacity { get; set; }

    public Thickness EmptyPadding { get; set; }

    public double DisabledOpacity { get; set; }

    public CornerRadius ClearRingRadius { get; set; }
    public CornerRadius ClearGlyphRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ClearRingRadius = new CornerRadius(14);
        ClearGlyphRadius = new CornerRadius(11);
        FieldPadding = new Thickness(12, 0);
        FieldGap = 8;
        ClearButtonSize = 22;
        FocusRingMargin = new Thickness(-3);
        FocusRingThickness = new Thickness(3);
        FocusRingRadius = new CornerRadius(21);
        PopoverOffset = 8;
        PopoverPadding = new Thickness(5);
        OptionHeight = 36;
        OptionCornerRadius = new CornerRadius(21);
        OptionPadding = new Thickness(12, 0);
        OptionDisabledOpacity = 0.42;
        EmptyPadding = new Thickness(12);
        DisabledOpacity = 0.5;
    }
}

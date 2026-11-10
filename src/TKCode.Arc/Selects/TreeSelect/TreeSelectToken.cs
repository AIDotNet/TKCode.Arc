using Avalonia;

namespace TKCode.Arc;

/// <summary>TreeSelect geometry: the field (as Cascader's), the chips (as MultiSelect's) and the tree rows of the menu.</summary>
[ControlDesignToken]
internal sealed class TreeSelectToken : AbstractControlDesignToken
{
    /// <summary>Trigger padding: 12px inline, 4px block so a row of chips fits the 44px field.</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Minimum height of the value area (one row of chips).</summary>
    public double ValueMinHeight { get; set; }

    /// <summary>Menu corner: radius-control + 2 (Cascader).</summary>
    public CornerRadius MenuCornerRadius { get; set; }

    public Thickness MenuPadding { get; set; }

    /// <summary>The menu is at least this wide, and never narrower than the field.</summary>
    public double PopupMinWidth { get; set; }

    /// <summary>A row is 34px plus the 2px gap above it; both collapse together while a branch opens or closes.</summary>
    public double RowHeight { get; set; }

    public double RowGap { get; set; }

    public double RowRadius { get; set; }

    /// <summary>Horizontal step of one tree level.</summary>
    public double IndentWidth { get; set; }

    public double RowInlinePadding { get; set; }

    /// <summary>Space between the chevron, the checkbox, the icon and the label.</summary>
    public double ContentGap { get; set; }

    /// <summary>Edge of the drawn checkbox square (Checkbox draws 18px; rows use a denser 16px).</summary>
    public double CheckboxSize { get; set; }

    public double CheckboxRadius { get; set; }

    public double CheckStrokeThickness { get; set; }

    public double IconSize { get; set; }

    public double RowDisabledOpacity { get; set; }

    /// <summary>Corner of the rounded background behind the matched text.</summary>
    public double MatchRadius { get; set; }

    /// <summary>Width the search input keeps after the chips while the menu is open.</summary>
    public double SearchMinWidth { get; set; }

    public double ChipMaxWidth { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipSpacing { get; set; }

    public Thickness EmptyPadding { get; set; }

    public double ClearButtonSize { get; set; }

    public CornerRadius ClearRingRadius { get; set; }

    public CornerRadius ClearGlyphRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerPadding = new Thickness(12, 4);
        ValueMinHeight = 26;
        MenuCornerRadius = new CornerRadius(20);
        MenuPadding = new Thickness(6);
        PopupMinWidth = 240;
        RowHeight = 36;
        RowGap = 2;
        RowRadius = 12;
        IndentWidth = 18;
        RowInlinePadding = 8;
        ContentGap = 8;
        CheckboxSize = 16;
        CheckboxRadius = 4.5;
        CheckStrokeThickness = 1.75;
        IconSize = 16;
        RowDisabledOpacity = 0.45;
        MatchRadius = 3;
        SearchMinWidth = 48;
        ChipMaxWidth = 144;
        ChipPadding = new Thickness(7, 3);
        ChipSpacing = 5;
        EmptyPadding = new Thickness(12, 10);
        ClearButtonSize = 22;
        ClearRingRadius = new CornerRadius(14);
        ClearGlyphRadius = new CornerRadius(11);
        DisabledOpacity = 0.5;
        FocusRingRadius = new CornerRadius(22);
        FocusRingMargin = new Thickness(-4);
    }
}

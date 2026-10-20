using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MenuEntryToken : AbstractControlDesignToken
{
    /// <summary>Row heights: dropdown 36, context 34, account 32.</summary>
    public double RowHeight { get; set; }
    public double ContextRowHeight { get; set; }
    public double AccountRowHeight { get; set; }
    public double SheetRowHeight { get; set; }

    /// <summary>Row inline padding: 11px (account: 8px).</summary>
    public Thickness RowInset { get; set; }
    public Thickness AccountRowInset { get; set; }

    /// <summary>Row radius: RadiusPanel minus the 6px inset (account: 6px).</summary>
    public CornerRadius RowRadius { get; set; }
    public CornerRadius AccountRowRadius { get; set; }

    /// <summary>Icon slot width (dropdown 17, context and account 16) and the gap after it (10, context 9).</summary>
    public double IconSlotWidth { get; set; }
    public double CompactIconSlotWidth { get; set; }
    public Thickness IconSpacing { get; set; }
    public Thickness ContextIconSpacing { get; set; }

    public double GlyphSize { get; set; }
    public double RadioDotSize { get; set; }
    public double AccountFontSize { get; set; }
    public Thickness GestureSpacing { get; set; }
    public double GestureOpacity { get; set; }
    public double DisabledOpacity { get; set; }

    /// <summary>Context rows press with a tint, not a scale: foreground at 5%.</summary>
    public Color PressedTint { get; set; }

    /// <summary>Separator drawn by <see cref="MenuEntry.HasSeparatorBefore"/>: 1px (account 0.5px), bleeding 4px into the inset.</summary>
    public double SeparatorThickness { get; set; }
    public double AccountSeparatorThickness { get; set; }
    public Thickness SeparatorMargin { get; set; }
    public Thickness SheetSeparatorMargin { get; set; }

    /// <summary>Account separator: foreground at 9%.</summary>
    public Color AccountSeparatorColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        RowHeight = 36;
        ContextRowHeight = 34;
        AccountRowHeight = 32;
        SheetRowHeight = 46;
        RowInset = new Thickness(11, 0);
        AccountRowInset = new Thickness(8, 0);
        RowRadius = new CornerRadius(20);
        AccountRowRadius = new CornerRadius(6);
        IconSlotWidth = 17;
        CompactIconSlotWidth = 16;
        IconSpacing = new Thickness(0, 0, 10, 0);
        ContextIconSpacing = new Thickness(0, 0, 9, 0);
        GlyphSize = 16;
        RadioDotSize = 6;
        AccountFontSize = 13;
        GestureSpacing = new Thickness(16, 0, 0, 0);
        GestureOpacity = 0.8;
        DisabledOpacity = 0.45;
        PressedTint = Oklch.WithAlpha(n.Foreground, 0.05);
        SeparatorThickness = 1;
        AccountSeparatorThickness = 0.5;
        SeparatorMargin = new Thickness(-4, 4);
        SheetSeparatorMargin = new Thickness(-10, 10);
        AccountSeparatorColor = Oklch.WithAlpha(n.Foreground, 0.09);
    }
}

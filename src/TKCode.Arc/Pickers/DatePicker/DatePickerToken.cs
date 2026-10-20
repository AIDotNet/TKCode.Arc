using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DatePickerToken : AbstractControlDesignToken
{
    /// <summary>Gap between the calendar icon, the value and the chevron (Arc: <c>gap: 9px</c>).</summary>
    public double AffixGap { get; set; }

    /// <summary>Square of the chevron toggle; its negative right margin keeps the glyph 12px from the border.</summary>
    public double DropDownButtonSize { get; set; }
    public Thickness DropDownButtonMargin { get; set; }

    /// <summary>Popup: width min(320px, viewport − 32px), 16px inset (12px on phone widths), 8px below the field.</summary>
    public double PopupWidth { get; set; }
    public Thickness PopupInset { get; set; }
    public Thickness PopupCompactInset { get; set; }
    public double PopupGap { get; set; }

    /// <summary>Footer: 14px above, 11px top inset under a subtle rule; buttons 4px × 8px.</summary>
    public Thickness FooterMargin { get; set; }
    public Thickness FooterInset { get; set; }
    public Thickness FooterButtonInset { get; set; }
    public double FooterButtonDisabledOpacity { get; set; }

    /// <summary>Line height of the value copy (14px × 1.4).</summary>
    public double ValueLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        AffixGap = 9;
        DropDownButtonSize = 28;
        DropDownButtonMargin = new Thickness(0, 0, -6, 0);
        PopupWidth = 320;
        PopupInset = new Thickness(16);
        PopupCompactInset = new Thickness(12);
        PopupGap = 8;
        FooterMargin = new Thickness(0, 14, 0, 0);
        FooterInset = new Thickness(0, 11, 0, 0);
        FooterButtonInset = new Thickness(8, 4);
        FooterButtonDisabledOpacity = 0.4;
        ValueLineHeight = 14 * 1.4;
    }
}

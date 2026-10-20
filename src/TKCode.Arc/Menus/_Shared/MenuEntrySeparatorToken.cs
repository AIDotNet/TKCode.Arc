using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MenuEntrySeparatorToken : AbstractControlDesignToken
{
    /// <summary>1px line (account 0.5px) that bleeds 4px into the surface inset, 4px above and below.</summary>
    public double LineThickness { get; set; }
    public double AccountLineThickness { get; set; }
    public Thickness LineMargin { get; set; }

    /// <summary>Sheet separators follow the 10px sheet inset.</summary>
    public Thickness SheetLineMargin { get; set; }

    /// <summary>Account separator: foreground at 9%.</summary>
    public Color AccountLineColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LineThickness = 1;
        AccountLineThickness = 0.5;
        LineMargin = new Thickness(-4, 4);
        SheetLineMargin = new Thickness(-10, 10);
        AccountLineColor = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Foreground, 0.09);
    }
}

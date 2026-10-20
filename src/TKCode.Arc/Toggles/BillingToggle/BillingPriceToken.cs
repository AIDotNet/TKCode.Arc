using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BillingPriceToken : AbstractControlDesignToken
{
    public double PartGap { get; set; }

    /// <summary>The currency sign at 0.55em of the 36px amount, raised 0.12em, 0.04em before the digits.</summary>
    public double CurrencyFontSize { get; set; }
    public Thickness CurrencyMargin { get; set; }

    /// <summary>The aside (old price and period) sits 0.3em above the amount's baseline box.</summary>
    public Thickness AsideMargin { get; set; }
    public Thickness WasInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PartGap = 10;
        CurrencyFontSize = 19.8;
        CurrencyMargin = new Thickness(0, 4.32, 1.44, 0);
        AsideMargin = new Thickness(0, 0, 0, 4.2);
        WasInset = new Thickness(0, 0, 6, 0);
    }
}

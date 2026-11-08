using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry, breakpoints and the accent-independent colors of <see cref="PricingCalculator"/> (Arc pricing-calculator,
/// design reference, measured on its preview).
/// </summary>
[ControlDesignToken]
internal sealed class PricingCalculatorToken : AbstractControlDesignToken
{
    /// <summary>Block width at or below which the cards stack in one column (860).</summary>
    public double StackedWidth { get; set; }

    /// <summary>Block width at or below which the compact layout applies (560).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Block width at or below which savings text may wrap (360).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Widest the header grows.</summary>
    public double HeaderMaxWidth { get; set; }

    /// <summary>Space under the header.</summary>
    public Thickness HeaderMargin { get; set; }

    /// <summary>Padding of the controls panel.</summary>
    public Thickness ControlsPadding { get; set; }

    /// <summary>Padding of the controls panel in the compact layout.</summary>
    public Thickness ControlsPaddingCompact { get; set; }

    /// <summary>Space under the controls panel.</summary>
    public Thickness ControlsMargin { get; set; }

    /// <summary>Gap between the seat block and the billing switch on one row.</summary>
    public double ControlsColumnGap { get; set; }

    /// <summary>Gap between the seat block and the billing switch once it wraps below.</summary>
    public double ControlsRowGap { get; set; }

    /// <summary>The narrowest the seat block gets before the billing switch wraps below it.</summary>
    public double SeatsMinWidth { get; set; }

    /// <summary>Diameter of the seat step buttons.</summary>
    public double StepSize { get; set; }

    /// <summary>Width of the seat readout between the step buttons.</summary>
    public double SeatValueWidth { get; set; }

    /// <summary>Height of the slider (thumb diameter).</summary>
    public double SliderHeight { get; set; }

    /// <summary>Thickness of the slider track.</summary>
    public double TrackThickness { get; set; }

    /// <summary>The filled part of the track (dark ink in light mode, light gray in dark mode).</summary>
    public Color TrackFillColor { get; set; }

    /// <summary>The unfilled part of the track.</summary>
    public Color TrackRestColor { get; set; }

    /// <summary>Height of the billing switch's options.</summary>
    public double BillingOptionHeight { get; set; }

    /// <summary>Inset of the billing switch around its options.</summary>
    public Thickness BillingInset { get; set; }

    /// <summary>The billing switch's groove: foreground at 6%.</summary>
    public Color BillingGrooveColor { get; set; }

    /// <summary>Gap between the cards.</summary>
    public double CardGap { get; set; }

    /// <summary>Padding inside a card.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Padding inside a card in the compact layout.</summary>
    public Thickness CardPaddingCompact { get; set; }

    /// <summary>Height of the price area (price block or cap note).</summary>
    public double PriceAreaHeight { get; set; }

    /// <summary>Height of the recommended badge.</summary>
    public double BadgeHeight { get; set; }

    /// <summary>Padding of the totals panel.</summary>
    public Thickness TotalsPadding { get; set; }

    /// <summary>Padding of the totals panel in the compact layout.</summary>
    public Thickness TotalsPaddingCompact { get; set; }

    /// <summary>The savings green (checks of the selected card, savings line, yearly note).</summary>
    public Color SavingsColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StackedWidth = 860;
        CompactWidth = 560;
        NarrowWidth = 360;
        HeaderMaxWidth = 544;
        HeaderMargin = new Thickness(0, 0, 0, 28);
        ControlsPadding = new Thickness(24, 20);
        ControlsPaddingCompact = new Thickness(16, 16, 16, 18);
        ControlsMargin = new Thickness(0, 0, 0, 20);
        ControlsColumnGap = 32;
        ControlsRowGap = 20;
        SeatsMinWidth = 300;
        StepSize = 30;
        SeatValueWidth = 87;
        SliderHeight = 22;
        TrackThickness = 6;
        TrackFillColor = isDarkMode ? Oklch.MixOklab(n.Foreground, n.Background, 0.3) : Oklch.MixOklab(n.Foreground, n.Background, 0.1);
        TrackRestColor = isDarkMode ? n.Border : Oklch.MixOklab(n.Background, n.Foreground, 0.09);
        BillingOptionHeight = 32;
        BillingInset = new Thickness(3);
        BillingGrooveColor = Oklch.WithAlpha(n.Foreground, 0.06);
        CardGap = 12;
        CardPadding = new Thickness(20);
        CardPaddingCompact = new Thickness(18, 18, 18, 16);
        PriceAreaHeight = 76;
        BadgeHeight = 22;
        TotalsPadding = new Thickness(24, 18, 20, 18);
        TotalsPaddingCompact = new Thickness(16);
        SavingsColor = isDarkMode ? Oklch.ToColor(0.79, 0.14, 162) : Oklch.ToColor(0.5, 0.12, 162);
    }
}

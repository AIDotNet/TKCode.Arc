using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the accent-independent derived colors of <see cref="UsagePricing"/>.</summary>
[ControlDesignToken]
internal sealed class UsagePricingToken : AbstractControlDesignToken
{
    /// <summary>Block width from which the plan card sits in its own column beside the controls.</summary>
    public double ColumnsThreshold { get; set; }

    /// <summary>Gap between the controls column and the card column.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Width of the card column.</summary>
    public double CardWidth { get; set; }

    /// <summary>Gap above the card once it sits under the controls.</summary>
    public double StackedCardGap { get; set; }

    /// <summary>Space inside the card (two columns).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Space inside the card once stacked.</summary>
    public Thickness StackedCardPadding { get; set; }

    /// <summary>Space above the controls (two columns).</summary>
    public Thickness ControlsMargin { get; set; }

    /// <summary>Space above the controls once stacked.</summary>
    public Thickness StackedControlsMargin { get; set; }

    /// <summary>Gap between the sliders and the billing row.</summary>
    public double ControlsGap { get; set; }

    /// <summary>Space around the breakdown (two columns).</summary>
    public Thickness BreakdownMargin { get; set; }

    /// <summary>Space around the breakdown once stacked.</summary>
    public Thickness StackedBreakdownMargin { get; set; }

    /// <summary>Heading size (two columns).</summary>
    public double TitleFontSize { get; set; }

    /// <summary>Heading size once stacked.</summary>
    public double StackedTitleFontSize { get; set; }

    /// <summary>Widest the paragraph under the heading grows.</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>Size of the plan name on the card.</summary>
    public double PlanNameFontSize { get; set; }

    /// <summary>Size of the card's price.</summary>
    public double PriceFontSize { get; set; }

    /// <summary>Height of a feature line.</summary>
    public double FeatureRowHeight { get; set; }

    /// <summary>Thickness of a plan meter bar.</summary>
    public double MeterBarHeight { get; set; }

    /// <summary>Gap between the plan meter's segments.</summary>
    public double MeterGap { get; set; }

    /// <summary>Space inside a breakdown row, above and below its text.</summary>
    public Thickness BreakdownRowPadding { get; set; }

    /// <summary>The meter's unfilled bars: 10% foreground (accent independent).</summary>
    public Color MeterTrackColor { get; set; }

    /// <summary>The blur, in px, of a price digit a whole step away from the reading line.</summary>
    public double DigitBlur { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ColumnsThreshold = 780;
        ColumnGap = 40;
        CardWidth = 392;
        StackedCardGap = 28;
        CardPadding = new Thickness(26, 26, 26, 22);
        StackedCardPadding = new Thickness(20, 22, 20, 18);
        ControlsMargin = new Thickness(0, 32, 0, 0);
        StackedControlsMargin = new Thickness(0, 28, 0, 0);
        ControlsGap = 28;
        BreakdownMargin = new Thickness(0, 36, 0, 4);
        StackedBreakdownMargin = new Thickness(0, 28, 0, 4);
        TitleFontSize = 36;
        StackedTitleFontSize = 28;
        DescriptionMaxWidth = 392;
        PlanNameFontSize = 36;
        PriceFontSize = 52;
        FeatureRowHeight = 36;
        MeterBarHeight = 4;
        MeterGap = 4;
        BreakdownRowPadding = new Thickness(0, 12);
        MeterTrackColor = Oklch.WithAlpha(n.Foreground, 0.10);
        DigitBlur = 2;
    }
}

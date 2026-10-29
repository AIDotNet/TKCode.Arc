using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RadioCardsToken : AbstractControlDesignToken
{
    public double GridGap { get; set; }
    public double ListGap { get; set; }
    public CornerRadius CardRadius { get; set; }
    public Thickness CardInsetGrid { get; set; }
    public Thickness CardInsetList { get; set; }
    public double SelectedTintOpacity { get; set; }
    public double IndicatorDiameter { get; set; }
    public CornerRadius IndicatorRadius { get; set; }
    public Thickness IndicatorBorder { get; set; }
    public double IndicatorDotDiameter { get; set; }
    public CornerRadius IndicatorDotRadius { get; set; }
    public Thickness IndicatorMarginGrid { get; set; }
    public Thickness IndicatorMarginList { get; set; }
    public double IconDiameter { get; set; }
    public Thickness IconMargin { get; set; }
    public double BodyGap { get; set; }
    public Thickness MetaMarginGrid { get; set; }
    public Thickness MetaMarginList { get; set; }
    public double DescriptionLineHeight { get; set; }

    /// <summary>The ring's stroke is centered 0.75px inside the card, so its radius is the card's minus half a stroke.</summary>
    public CornerRadius RingRadius { get; set; }
    public double RingStroke { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GridGap = 10;
        ListGap = 8;
        CardRadius = new CornerRadius(20);
        CardInsetGrid = new Thickness(16, 14);
        CardInsetList = new Thickness(14, 13, 16, 13);
        SelectedTintOpacity = 0.04;
        IndicatorDiameter = 18;
        IndicatorRadius = new CornerRadius(9);
        IndicatorBorder = new Thickness(1.5);
        IndicatorDotDiameter = 6;
        IndicatorDotRadius = new CornerRadius(3);
        IndicatorMarginGrid = new Thickness(12, 1, 0, 0);
        IndicatorMarginList = new Thickness(0, 0, 12, 0);
        IconDiameter = 18;
        IconMargin = new Thickness(0, 0, 8, 0);
        BodyGap = 2;
        // Row gap 4 + meta margin 10.
        MetaMarginGrid = new Thickness(0, 14, 0, 0);
        MetaMarginList = new Thickness(12, 0, 0, 0);
        DescriptionLineHeight = 16.8;
        RingRadius = new CornerRadius(19.25);
        RingStroke = 1.5;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(24);
    }
}

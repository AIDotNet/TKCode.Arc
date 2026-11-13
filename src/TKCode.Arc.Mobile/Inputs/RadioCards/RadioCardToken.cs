using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of one mobile <see cref="RadioCard"/>: padding, icon, the radio indicator, press and focus ring.</summary>
// The desktop TKCode.Arc package declares a RadioCardToken too, and identities are unique per application.
[ControlDesignToken(Identity = "MobileRadioCard")]
internal sealed class RadioCardToken : AbstractControlDesignToken
{
    /// <summary>Padding inside a card: 12.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Size of an icon given as a geometry: 22.</summary>
    public double IconExtent { get; set; }

    /// <summary>Space above the label when an icon is above it: 10 at the top.</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>Space above the description: 4 at the top.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Width and height of the radio indicator: 22, as in a mobile <see cref="Radio"/>.</summary>
    public double IndicatorSize { get; set; }

    /// <summary>Border width of the radio indicator: 1.5.</summary>
    public Thickness IndicatorBorderWidth { get; set; }

    /// <summary>Width and height of the indicator's dot: 10.</summary>
    public double DotSize { get; set; }

    /// <summary>Margin between the card's content and the indicator: 12 on the left of the indicator.</summary>
    public Thickness IndicatorMargin { get; set; }

    /// <summary>Opacity of a disabled card: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Margin of the keyboard focus ring: 3 outside the card.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Corner radius of the keyboard focus ring: the card's 16 plus 3.</summary>
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(12);
        IconExtent = 22;
        LabelMargin = new Thickness(0, 10, 0, 0);
        DescriptionMargin = new Thickness(0, 4, 0, 0);
        IndicatorSize = 22;
        IndicatorBorderWidth = new Thickness(1.5);
        DotSize = 10;
        IndicatorMargin = new Thickness(12, 0, 0, 0);
        DisabledOpacity = 0.5;
        FocusRingMargin = new Thickness(-3);
        FocusRingRadius = new CornerRadius(19);
    }
}

using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="PrizeWheel"/>: the metal rim, pegs and hub, the shadows, and the
/// reward panel's measures. Slice, text, border, pointer and highlight colors, the fonts and the focus ring come from the
/// ArcDesign tokens in the theme (Arc Prize wheel, design reference).
/// </summary>
[ControlDesignToken]
internal sealed class PrizeWheelToken : AbstractControlDesignToken
{
    /// <summary>Width of the metal rim that carries the pegs, in the wheel's 400-unit drawing box.</summary>
    public double RimWidth { get; set; }

    /// <summary>
    /// Length of the pointer from the top of its head to its tip, in the wheel's 400-unit drawing box. The pegs' collision
    /// with the pointer is measured on the default length; other values only scale its drawing.
    /// </summary>
    public double PointerLength { get; set; }

    /// <summary>The wheel's largest size; it scales down with the width it gets.</summary>
    public double StageMaxSize { get; set; }

    /// <summary>The panel's width beside the wheel.</summary>
    public double PanelWidth { get; set; }

    /// <summary>The panel's largest width under the wheel.</summary>
    public double StackedPanelMaxWidth { get; set; }

    /// <summary>The control's width from which the panel sits beside the wheel instead of under it.</summary>
    public double SideBySideMinWidth { get; set; }

    /// <summary>The gap between the wheel and the panel beside it.</summary>
    public double ColumnGap { get; set; }

    /// <summary>The gap between the wheel and the panel under it.</summary>
    public double RowGap { get; set; }

    /// <summary>The gap between the reward slot and the odds.</summary>
    public double PanelSpacing { get; set; }

    /// <summary>The gap between lines of the idle text and of the card.</summary>
    public double TextSpacing { get; set; }

    /// <summary>The hint's space above it.</summary>
    public Thickness HintMargin { get; set; }

    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double PrizeLineHeight { get; set; }

    public double PrizeLetterSpacing { get; set; }

    /// <summary>The size of the panel's small text (description, hint, detail, code, odds rows).</summary>
    public double TextFontSize { get; set; }

    public double TextLineHeight { get; set; }

    public Thickness CardPadding { get; set; }

    public Thickness CodeBoxMargin { get; set; }

    public Thickness CodeBoxPadding { get; set; }

    public double CodeBoxGap { get; set; }

    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public double ActionIconSize { get; set; }

    public double ActionIconGap { get; set; }

    /// <summary>"Spin again" sits 12px into the card's padding so its icon lines up with the text above.</summary>
    public Thickness SpinAgainMargin { get; set; }

    public Thickness OddsRowPadding { get; set; }

    public double OddsColumnGap { get; set; }

    /// <summary>The odds list's width from which it folds into two columns.</summary>
    public double OddsTwoColumnMinWidth { get; set; }

    public double FeaturedDotSize { get; set; }

    public Thickness FeaturedDotMargin { get; set; }

    /// <summary>The hub's label size on wheels of 420px and more; smaller wheels use <see cref="HubCompactFontSize"/>.</summary>
    public double HubFontSize { get; set; }

    public double HubCompactFontSize { get; set; }

    /// <summary>The keyboard focus ring sits 2px outside the hub.</summary>
    public Thickness FocusRingMargin { get; set; }

    public Color RimColor { get; set; }

    public Color RimSheenStart { get; set; }

    public Color RimSheenEnd { get; set; }

    public Color OuterHairline { get; set; }

    public Color PegFill { get; set; }

    public Color PegStroke { get; set; }

    public Color PegHighlight { get; set; }

    public Color PointerStroke { get; set; }

    public Color SliceAlternate { get; set; }

    public Color HubTop { get; set; }

    public Color HubMiddle { get; set; }

    public Color HubBottom { get; set; }

    public Color HubForeground { get; set; }

    public BoxShadows HubShadow { get; set; }

    public BoxShadows WheelShadow { get; set; }

    public BoxShadows CardShadow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        RimWidth = 16;
        PointerLength = 29;
        StageMaxSize = 480;
        PanelWidth = 320;
        StackedPanelMaxWidth = 480;
        SideBySideMinWidth = 736;
        ColumnGap = 40;
        RowGap = 24;
        PanelSpacing = 20;
        TextSpacing = 8;
        HintMargin = new Thickness(0, 8, 0, 0);
        TitleFontSize = 20;
        TitleLineHeight = 22;
        TitleLetterSpacing = -0.6;
        PrizeLineHeight = 30.8;
        PrizeLetterSpacing = -0.84;
        TextFontSize = 13;
        TextLineHeight = 18.2;
        CardPadding = new Thickness(20);
        CodeBoxMargin = new Thickness(0, 12, 0, 0);
        CodeBoxPadding = new Thickness(16, 6, 6, 6);
        CodeBoxGap = 8;
        ActionHeight = 32;
        ActionPadding = new Thickness(12, 0);
        ActionIconSize = 16;
        ActionIconGap = 6;
        SpinAgainMargin = new Thickness(-12, 8, 0, 0);
        OddsRowPadding = new Thickness(0, 7);
        OddsColumnGap = 20;
        OddsTwoColumnMinWidth = 420;
        FeaturedDotSize = 6;
        FeaturedDotMargin = new Thickness(8, 0, 0, 0);
        HubFontSize = 15;
        HubCompactFontSize = 13;
        FocusRingMargin = new Thickness(-4);

        var black = Colors.Black;
        var white = Colors.White;
        if (isDarkMode)
        {
            RimColor = Oklch.ToColor(0.33, 0.005, 260);
            RimSheenStart = Oklch.WithAlpha(white, 0.22);
            RimSheenEnd = Oklch.WithAlpha(white, 0.08);
            OuterHairline = Oklch.WithAlpha(white, 0.10);
            PegFill = Oklch.ToColor(0.78, 0.004, 260);
            PegStroke = Oklch.WithAlpha(black, 0.5);
            PegHighlight = Oklch.WithAlpha(white, 0.45);
            PointerStroke = Oklch.WithAlpha(black, 0.45);
            SliceAlternate = Oklch.MixOklab(n.SurfaceRaised, n.Background, 0.55);
            HubTop = Oklch.ToColor(0.34, 0.005, 260);
            HubMiddle = Oklch.ToColor(0.25, 0.004, 260);
            HubBottom = Oklch.ToColor(0.30, 0.005, 260);
            HubForeground = Oklch.ToColor(0.955, 0.004, 260);
            HubShadow = Shadows(
                Shadow(0, 0, 0, 1, Oklch.WithAlpha(white, 0.10), inset: true),
                Shadow(0, 1, 0, 0, Oklch.WithAlpha(white, 0.08), inset: true),
                Shadow(0, 1, 2, 0, Oklch.WithAlpha(black, 0.12)),
                Shadow(0, 10, 20, -10, Oklch.WithAlpha(black, 0.35)));
            WheelShadow = Shadows(
                Shadow(0, 1, 2, 0, Oklch.WithAlpha(black, 0.40)),
                Shadow(0, 30, 60, -28, Oklch.WithAlpha(black, 0.70)));
            CardShadow = Shadows(Shadow(0, 22, 55, 0, Oklch.WithAlpha(black, 0.34)));
        }
        else
        {
            RimColor = Oklch.ToColor(0.885, 0.004, 260);
            RimSheenStart = Oklch.WithAlpha(white, 0.85);
            RimSheenEnd = Oklch.WithAlpha(white, 0.35);
            OuterHairline = Oklch.WithAlpha(black, 0.14);
            PegFill = Oklch.ToColor(0.97, 0.002, 260);
            PegStroke = Oklch.WithAlpha(black, 0.28);
            PegHighlight = Oklch.WithAlpha(white, 0.80);
            PointerStroke = Oklch.WithAlpha(black, 0.14);
            SliceAlternate = n.SurfaceMuted;
            HubTop = Oklch.ToColor(0.975, 0.002, 260);
            HubMiddle = Oklch.ToColor(0.905, 0.004, 260);
            HubBottom = Oklch.ToColor(0.95, 0.003, 260);
            HubForeground = Oklch.ToColor(0.26, 0.012, 260);
            HubShadow = Shadows(
                Shadow(0, 0, 0, 1, Oklch.WithAlpha(black, 0.08), inset: true),
                Shadow(0, 1, 0, 0, Oklch.WithAlpha(white, 0.95), inset: true),
                Shadow(0, 1, 2, 0, Oklch.WithAlpha(black, 0.12)),
                Shadow(0, 10, 20, -10, Oklch.WithAlpha(black, 0.35)));
            WheelShadow = Shadows(
                Shadow(0, 1, 2, 0, Oklch.WithAlpha(black, 0.06)),
                Shadow(0, 30, 60, -28, Oklch.WithAlpha(black, 0.32)),
                Shadow(0, 12, 24, -16, Oklch.WithAlpha(black, 0.16)));
            CardShadow = Shadows(
                Shadow(0, 20, 48, 0, Oklch.WithAlpha(black, 0.105)),
                Shadow(0, 3, 10, 0, Oklch.WithAlpha(black, 0.045)));
        }
    }

    private static BoxShadow Shadow(double x, double y, double blur, double spread, Color color, bool inset = false) => new()
    {
        OffsetX = x,
        OffsetY = y,
        Blur = blur,
        Spread = spread,
        Color = color,
        IsInset = inset
    };

    private static BoxShadows Shadows(BoxShadow first, params BoxShadow[] rest) => new(first, rest);
}

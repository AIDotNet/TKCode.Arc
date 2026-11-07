using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ChargeSession"/> (Arc Charge session, design reference) and the scene's accent-independent
/// materials measured in light and dark (brushed metal, screen, cable, pack shell, well and tints). Surface, borders, text,
/// the accent, the card radius and the type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ChargeSessionToken : AbstractControlDesignToken
{
    /// <summary>Card padding: 32 wide, 24 stacked, 16 compact.</summary>
    public Thickness CardPadding { get; set; }

    public Thickness StackedCardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>Gap between the card's rows: 24, 16 when stacked or compact.</summary>
    public double RowGap { get; set; }

    public double StackedRowGap { get; set; }

    /// <summary>The card is at most 880 wide.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The title: 16 on a 17.6 line with −0.48 letter spacing; 2 above the location line (14/19.6).</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>The big percentage: 72, 52 stacked, 36 compact; line height equals size; −0.03em letter spacing.</summary>
    public double ChargeSize { get; set; }

    public double StackedChargeSize { get; set; }

    public double CompactChargeSize { get; set; }

    /// <summary>The status icon: 16 with a 1.75 stroke, 6 before the text.</summary>
    public double StatusIconSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>Space above the scene for the limit pill: 36, 32 compact.</summary>
    public double HeroTopSpace { get; set; }

    public double CompactHeroTopSpace { get; set; }

    /// <summary>The limit marker's touch target (64 × 44) and its pill (48 × 26, 12/16.8 text, 8 side padding).</summary>
    public double HandleWidth { get; set; }

    public double HandleHeight { get; set; }

    public double PillWidth { get; set; }

    public double PillHeight { get; set; }

    public double PillLineHeight { get; set; }

    /// <summary>The pill grows to 1.08 while held.</summary>
    public double PillHeldScale { get; set; }

    /// <summary>Opacity of the pill's stem and the limit line: 0.55 at rest, 0.9 while held.</summary>
    public double LimitLineOpacity { get; set; }

    public double LimitLineHeldOpacity { get; set; }

    /// <summary>The curve and the stats: columns 1.55 : 1 with 24 between; plot height 0.40 of its width beside, 0.50 stacked.</summary>
    public double CurveColumnWeight { get; set; }

    public double LowerGap { get; set; }

    public double StackedLowerGap { get; set; }

    public double PlotAspect { get; set; }

    public double StackedPlotAspect { get; set; }

    /// <summary>Gap between the curve heading and the plot.</summary>
    public double CurveHeadGap { get; set; }

    /// <summary>X labels sit 6 under the plot.</summary>
    public double AxisLabelGap { get; set; }

    /// <summary>The stats grid cells are padded 16 (12/16 stacked, 12 compact), 2 between name and value; the grid's radius is RadiusStep22.</summary>
    public Thickness StatPadding { get; set; }

    public Thickness StackedStatPadding { get; set; }

    public Thickness CompactStatPadding { get; set; }

    public double StatGap { get; set; }

    /// <summary>The stat values: 22/30.8, 18/25.2 stacked, 16/22.4 compact.</summary>
    public double StatValueSize { get; set; }

    public double StackedStatValueSize { get; set; }

    public double CompactStatValueSize { get; set; }

    /// <summary>The footer: 16 under its divider, 16 between hint and button (12 between rows); the hint wants 220.</summary>
    public double FooterTopSpace { get; set; }

    public double FooterColumnGap { get; set; }

    public double FooterRowGap { get; set; }

    public double HintMinWidth { get; set; }

    /// <summary>The action button: 44 high, 20 side padding, 8 between icon and label, a 14px icon in a 16 slot.</summary>
    public double ButtonHeight { get; set; }

    public Thickness ButtonPadding { get; set; }

    public double ButtonGap { get; set; }

    public double ButtonIconSize { get; set; }

    /// <summary>The button shrinks to 0.97 while pressed.</summary>
    public double ButtonPressedScale { get; set; }

    /// <summary>The solid button's hover color: the foreground eased a little toward the background.</summary>
    public Color SolidHoverFill { get; set; }

    /// <summary>Brushed metal of the charger: five diagonal gray stops (light .915/.9745/.898/.9575/.881, dark .319/.379/.304/.357/.312).</summary>
    public Color MetalStop0 { get; set; }

    public Color MetalStop1 { get; set; }

    public Color MetalStop2 { get; set; }

    public Color MetalStop3 { get; set; }

    public Color MetalStop4 { get; set; }

    /// <summary>The charger's outline: black 16% in light, white 12% in dark.</summary>
    public Color MetalEdge { get; set; }

    /// <summary>The charger's screen and the plug holster (oklab .252 in light, the background in dark).</summary>
    public Color ScreenFill { get; set; }

    /// <summary>The cable jacket (oklab .8725 in light, .364 in dark).</summary>
    public Color CableFill { get; set; }

    /// <summary>The pack shell and the plug nub: the raised surface with a black 22% / white 20% outline.</summary>
    public Color ShellFill { get; set; }

    public Color ShellEdge { get; set; }

    /// <summary>The empty well inside the pack (oklab .9366 in light, .288 in dark).</summary>
    public Color WellFill { get; set; }

    /// <summary>Beyond the limit and the cell dividers: black 7% in light, white 6% in dark.</summary>
    public Color BeyondTint { get; set; }

    /// <summary>The highlight along the shell's top inner edge: none in light, white 12% in dark.</summary>
    public Color SheenStroke { get; set; }

    /// <summary>The curve's area beyond the limit: black 3% in light, white 3% in dark.</summary>
    public Color CurveBeyondTint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardPadding = new Thickness(32);
        StackedCardPadding = new Thickness(24);
        CompactCardPadding = new Thickness(16);
        RowGap = 24;
        StackedRowGap = 16;
        CardMaxWidth = 880;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        BodyLineHeight = 19.6;
        ChargeSize = 72;
        StackedChargeSize = 52;
        CompactChargeSize = 36;
        StatusIconSize = 16;
        StatusGap = 6;
        HeroTopSpace = 36;
        CompactHeroTopSpace = 32;
        HandleWidth = 64;
        HandleHeight = 44;
        PillWidth = 48;
        PillHeight = 26;
        PillLineHeight = 16.8;
        PillHeldScale = 1.08;
        LimitLineOpacity = 0.55;
        LimitLineHeldOpacity = 0.9;
        CurveColumnWeight = 1.55;
        LowerGap = 24;
        StackedLowerGap = 16;
        PlotAspect = 0.40;
        StackedPlotAspect = 0.50;
        CurveHeadGap = 8;
        AxisLabelGap = 6;
        StatPadding = new Thickness(16);
        StackedStatPadding = new Thickness(16, 12);
        CompactStatPadding = new Thickness(12);
        StatGap = 2;
        StatValueSize = 22;
        StackedStatValueSize = 18;
        CompactStatValueSize = 16;
        FooterTopSpace = 16;
        FooterColumnGap = 16;
        FooterRowGap = 12;
        HintMinWidth = 220;
        ButtonHeight = 44;
        ButtonPadding = new Thickness(20, 0);
        ButtonGap = 8;
        ButtonIconSize = 14;
        ButtonPressedScale = 0.97;
        SolidHoverFill = Oklch.MixOklab(n.Foreground, n.Background, 0.12);
        if (isDarkMode)
        {
            MetalStop0 = Gray(0.319);
            MetalStop1 = Gray(0.379);
            MetalStop2 = Gray(0.304);
            MetalStop3 = Gray(0.357);
            MetalStop4 = Gray(0.312);
            MetalEdge = Oklch.WithAlpha(Gray(0.96), 0.12);
            ScreenFill = n.Background;
            CableFill = Gray(0.364);
            ShellEdge = Oklch.WithAlpha(Colors.White, 0.20);
            WellFill = Gray(0.288);
            BeyondTint = Oklch.WithAlpha(Colors.White, 0.06);
            SheenStroke = Oklch.WithAlpha(Colors.White, 0.12);
            CurveBeyondTint = Oklch.WithAlpha(Colors.White, 0.03);
        }
        else
        {
            MetalStop0 = Gray(0.915);
            MetalStop1 = Gray(0.9745);
            MetalStop2 = Gray(0.898);
            MetalStop3 = Gray(0.9575);
            MetalStop4 = Gray(0.881);
            MetalEdge = Oklch.WithAlpha(Colors.Black, 0.16);
            ScreenFill = Gray(0.252);
            CableFill = Gray(0.8725);
            ShellEdge = Oklch.WithAlpha(Colors.Black, 0.22);
            WellFill = Gray(0.9366);
            BeyondTint = Oklch.WithAlpha(Colors.Black, 0.07);
            SheenStroke = Colors.Transparent;
            CurveBeyondTint = Oklch.WithAlpha(Colors.Black, 0.03);
        }
        ShellFill = isDarkMode ? Gray(0.24) : n.SurfaceRaised;
    }

    private static Color Gray(double lightness) => Oklch.ToColor(lightness, 0, 0);
}

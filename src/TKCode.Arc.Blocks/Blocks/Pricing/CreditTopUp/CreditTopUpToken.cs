using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry, timings and the accent-independent colors of <see cref="CreditTopUp"/> (Arc Credit top-up, design
/// reference: a card fluid up to 480 wide in five sections; values measured on the reference preview).
/// </summary>
[ControlDesignToken]
internal sealed class CreditTopUpToken : AbstractControlDesignToken
{
    /// <summary>The card's widest width.</summary>
    public double MaxCardWidth { get; set; }

    /// <summary>Card width at or below which the chart moves under the balance.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Card width at or below which the side paddings tighten from 24 to 16.</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the balance header.</summary>
    public Thickness HeadPadding { get; set; }

    /// <summary>Padding of the balance header in a compact card.</summary>
    public Thickness HeadPaddingCompact { get; set; }

    /// <summary>Padding of the pack section.</summary>
    public Thickness AddPadding { get; set; }

    /// <summary>Padding of the pack section in a compact card.</summary>
    public Thickness AddPaddingCompact { get; set; }

    /// <summary>Padding of the auto top-up and card sections.</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>Padding of the auto top-up and card sections in a compact card.</summary>
    public Thickness SectionPaddingCompact { get; set; }

    /// <summary>Padding of the checkout.</summary>
    public Thickness CheckoutPadding { get; set; }

    /// <summary>Padding of the checkout in a compact card.</summary>
    public Thickness CheckoutPaddingCompact { get; set; }

    /// <summary>Gap between the parts of a section.</summary>
    public double SectionGap { get; set; }

    /// <summary>Gap between the balance and the chart (side by side or stacked).</summary>
    public double HeadGap { get; set; }

    /// <summary>Gap between the balance's three lines.</summary>
    public double BalanceLineGap { get; set; }

    /// <summary>Size of the balance figure.</summary>
    public double BalanceSize { get; set; }

    /// <summary>Line height of the balance figure.</summary>
    public double BalanceLineHeight { get; set; }

    /// <summary>Letter spacing of the balance figure (−0.02em).</summary>
    public double BalanceLetterSpacing { get; set; }

    /// <summary>Size of secondary text.</summary>
    public double SmallSize { get; set; }

    /// <summary>Line height of body text (1.4 × 14).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Line height of secondary text (1.4 × 13).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Letter spacing of the card's text.</summary>
    public double CopyLetterSpacing { get; set; }

    /// <summary>Width of the chart beside the balance.</summary>
    public double ChartWidth { get; set; }

    /// <summary>Height of the bars beside the balance.</summary>
    public double ChartHeight { get; set; }

    /// <summary>Height of the bars under the balance.</summary>
    public double ChartHeightNarrow { get; set; }

    /// <summary>Gap between the bars and their label.</summary>
    public double ChartLabelGap { get; set; }

    /// <summary>Width of a bar.</summary>
    public double BarWidth { get; set; }

    /// <summary>Width of a bar's hover column beside the balance.</summary>
    public double BarSlot { get; set; }

    /// <summary>Corner radius of a bar.</summary>
    public double BarRadius { get; set; }

    /// <summary>Opacity of the other bars while one is pointed at.</summary>
    public double BarDimOpacity { get; set; }

    /// <summary>Gap between the packs.</summary>
    public double PackGap { get; set; }

    /// <summary>Padding inside a pack.</summary>
    public Thickness PackPadding { get; set; }


    /// <summary>Stroke of the selection ring (drawn inside the pack).</summary>
    public double PackRingThickness { get; set; }

    /// <summary>Height of the bonus badge.</summary>
    public double BadgeHeight { get; set; }

    /// <summary>Inline padding of the bonus badge.</summary>
    public Thickness BadgePadding { get; set; }

    /// <summary>Height of the slider row.</summary>
    public double SliderHeight { get; set; }

    /// <summary>Height of the auto top-up row.</summary>
    public double AutoRowHeight { get; set; }

    /// <summary>Height of the threshold row.</summary>
    public double ThresholdRowHeight { get; set; }

    /// <summary>Width of a threshold choice.</summary>
    public double ThresholdOptionWidth { get; set; }

    /// <summary>Height of a threshold choice.</summary>
    public double ThresholdOptionHeight { get; set; }

    /// <summary>Padding of the threshold track around its choices.</summary>
    public Thickness ThresholdTrackPadding { get; set; }

    /// <summary>Opacity of the threshold label while auto top-up is off.</summary>
    public double ThresholdLabelDisabledOpacity { get; set; }

    /// <summary>Opacity of the threshold choices while auto top-up is off.</summary>
    public double ThresholdOptionsDisabledOpacity { get; set; }

    /// <summary>Width of the card mark.</summary>
    public double MarkWidth { get; set; }

    /// <summary>Height of the card mark.</summary>
    public double MarkHeight { get; set; }

    /// <summary>Gap between the card mark and its text.</summary>
    public double MarkGap { get; set; }

    /// <summary>Pulls the Change button out so its text lines up with the content edge.</summary>
    public Thickness ChangeMargin { get; set; }

    /// <summary>Height of the status line.</summary>
    public double StatusHeight { get; set; }

    /// <summary>Gap between the status icon and its text.</summary>
    public double StatusIconGap { get; set; }

    /// <summary>Size of the small icons (health, status).</summary>
    public double IconSize { get; set; }

    /// <summary>How long a settings notice stays in the status line.</summary>
    public TimeSpan NoticeDwell { get; set; }

    /// <summary>How long the button and status line show a finished purchase.</summary>
    public TimeSpan DoneDwell { get; set; }

    /// <summary>Bars: muted text at 38%.</summary>
    public Color BarColor { get; set; }

    /// <summary>A pack's border on hover.</summary>
    public Color PackHoverBorder { get; set; }

    /// <summary>Bonus badge fill (a green at 13%).</summary>
    public Color BadgeBackground { get; set; }

    /// <summary>Bonus badge text.</summary>
    public Color BadgeForeground { get; set; }

    /// <summary>The low-balance icon (a warm amber, quieter than Warning).</summary>
    public Color LowIconColor { get; set; }

    /// <summary>Card mark plate (white in both themes).</summary>
    public Color MarkBackground { get; set; }

    /// <summary>Card mark outline (warm grey).</summary>
    public Color MarkBorder { get; set; }

    /// <summary>The Nimbus mark's stripes and capsule.</summary>
    public Color NimbusInk { get; set; }

    /// <summary>The Orbit mark's disc.</summary>
    public Color OrbitDisc { get; set; }

    /// <summary>The Orbit mark's ring.</summary>
    public Color OrbitRing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        MaxCardWidth = 480;
        NarrowWidth = 448;
        CompactWidth = 416;
        HeadPadding = new Thickness(24, 24, 24, 16);
        HeadPaddingCompact = new Thickness(16);
        AddPadding = new Thickness(24, 16, 24, 8);
        AddPaddingCompact = new Thickness(16, 16, 16, 8);
        SectionPadding = new Thickness(24, 16);
        SectionPaddingCompact = new Thickness(16);
        CheckoutPadding = new Thickness(24, 16, 24, 12);
        CheckoutPaddingCompact = new Thickness(16, 16, 16, 12);
        SectionGap = 12;
        HeadGap = 16;
        BalanceLineGap = 4;
        BalanceSize = 32;
        BalanceLineHeight = 38.4;
        BalanceLetterSpacing = -0.64;
        SmallSize = 13;
        BodyLineHeight = 19.6;
        SmallLineHeight = 18.2;
        CopyLetterSpacing = -0.16;
        ChartWidth = 140;
        ChartHeight = 44;
        ChartHeightNarrow = 32;
        ChartLabelGap = 8;
        BarWidth = 6;
        BarSlot = 10;
        BarRadius = 2;
        BarDimOpacity = 0.55;
        PackGap = 8;
        PackPadding = new Thickness(12);
        PackRingThickness = 1.5;
        BadgeHeight = 20;
        BadgePadding = new Thickness(6, 0);
        SliderHeight = 44;
        AutoRowHeight = 44;
        ThresholdRowHeight = 36;
        ThresholdOptionWidth = 56;
        ThresholdOptionHeight = 28;
        ThresholdTrackPadding = new Thickness(2);
        ThresholdLabelDisabledOpacity = 0.6;
        ThresholdOptionsDisabledOpacity = 0.5;
        MarkWidth = 34;
        MarkHeight = 22;
        MarkGap = 12;
        ChangeMargin = new Thickness(0, 0, -12, 0);
        StatusHeight = 20;
        StatusIconGap = 6;
        IconSize = 14;
        NoticeDwell = TimeSpan.FromMilliseconds(2600);
        DoneDwell = TimeSpan.FromMilliseconds(2400);
        BarColor = Oklch.WithAlpha(n.TextMuted, 0.38);
        PackHoverBorder = Oklch.MixOklab(n.Border, n.Foreground, 0.2);
        BadgeBackground = isDarkMode ? Oklch.ToColor(0.79, 0.14, 162, 0.13) : Oklch.ToColor(0.50, 0.12, 162, 0.13);
        BadgeForeground = isDarkMode ? Oklch.ToColor(0.84, 0.10, 162) : Oklch.ToColor(0.40, 0.086, 162);
        LowIconColor = isDarkMode ? Oklch.ToColor(0.83, 0.12, 78) : Oklch.ToColor(0.55, 0.12, 68);
        MarkBackground = Colors.White;
        MarkBorder = Color.Parse("#D9D3CA");
        NimbusInk = Color.Parse("#0F5E6E");
        OrbitDisc = Color.Parse("#3B3FB6");
        OrbitRing = Color.Parse("#E0A21B");
    }
}

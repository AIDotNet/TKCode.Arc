using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CheckoutSummary"/> and its accent-independent colors (Arc CheckoutSummary（设计参考）, notes
/// section 3): a 440 px card, padding clamp(20, 5% of the viewport, 28), a 44 px promo row, a 52 px pay button.
/// </summary>
[ControlDesignToken]
internal sealed class CheckoutSummaryToken : AbstractControlDesignToken
{
    /// <summary>Widest the card grows (narrower containers make it fill).</summary>
    public double PreferredWidth { get; set; }

    /// <summary>Smallest padding inside the card (narrow viewports).</summary>
    public double PaddingMin { get; set; }

    /// <summary>Largest padding inside the card.</summary>
    public double PaddingMax { get; set; }

    /// <summary>Padding as a fraction of the viewport width, between <see cref="PaddingMin"/> and <see cref="PaddingMax"/>.</summary>
    public double PaddingViewportFactor { get; set; }

    /// <summary>Space between the card's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>Line height of the title (16 × 1.1).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Letter spacing of the title (−0.03 em).</summary>
    public double TitleLetterSpacing { get; set; }

    /// <summary>Letter spacing of body text (−0.01 em at 14 px).</summary>
    public double BodyLetterSpacing { get; set; }

    /// <summary>Line height of 14 px text.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Line height of 12 px text.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Space after the item's media cell.</summary>
    public Thickness MediaMargin { get; set; }

    /// <summary>Space before the item's price column.</summary>
    public Thickness PriceMargin { get; set; }

    /// <summary>Side of the item's media cell.</summary>
    public double MediaSize { get; set; }

    /// <summary>Height of the promo row (form and chip).</summary>
    public double CouponHeight { get; set; }

    /// <summary>Padding inside the promo form.</summary>
    public Thickness CouponPadding { get; set; }

    /// <summary>Gap between the code input and Apply.</summary>
    public double CouponGap { get; set; }

    /// <summary>Height of Apply.</summary>
    public double ApplyHeight { get; set; }

    /// <summary>Narrowest Apply gets (keeps its width while it spins).</summary>
    public double ApplyMinWidth { get; set; }

    /// <summary>Padding inside Apply.</summary>
    public Thickness ApplyPadding { get; set; }

    /// <summary>Padding inside the applied chip.</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>Gap between the chip's glyph, code and "applied".</summary>
    public double ChipGap { get; set; }

    /// <summary>Letter spacing of the code on the chip (+0.02 em).</summary>
    public double ChipCodeLetterSpacing { get; set; }

    /// <summary>Side of the chip's remove button.</summary>
    public double RemoveSize { get; set; }

    /// <summary>Space above an error line.</summary>
    public Thickness ErrorPadding { get; set; }

    /// <summary>Padding above the first breakdown line (under the divider).</summary>
    public Thickness LinesPadding { get; set; }

    /// <summary>Padding of each breakdown line.</summary>
    public Thickness LinePadding { get; set; }

    /// <summary>Space above the total row's divider.</summary>
    public Thickness TotalMargin { get; set; }

    /// <summary>Padding of the total row.</summary>
    public Thickness TotalPadding { get; set; }

    /// <summary>Size of the rolling total.</summary>
    public double TotalFontSize { get; set; }

    /// <summary>Room above and below the rolling total inside its clip (0.16 em).</summary>
    public Thickness TotalClipPadding { get; set; }

    /// <summary>Negative margin that cancels <see cref="TotalClipPadding"/> in the layout.</summary>
    public Thickness TotalClipMargin { get; set; }

    /// <summary>Height of the pay button.</summary>
    public double PayHeight { get; set; }

    /// <summary>Line height of the pay button's label.</summary>
    public double PayLineHeight { get; set; }

    /// <summary>Gap between a glyph and its label (pay button).</summary>
    public double PayGap { get; set; }

    /// <summary>Space between the pay button and the lines under it.</summary>
    public double PayFooterGap { get; set; }

    /// <summary>Gap between the secure note's lock and its text.</summary>
    public double SecureGap { get; set; }

    /// <summary>Horizontal travel of the refusal shake.</summary>
    public double ShakeAmplitude { get; set; }

    /// <summary>Scale the applied chip grows from.</summary>
    public double ChipEnterScale { get; set; }

    /// <summary>Scale the promo form grows from when the chip is removed.</summary>
    public double FormEnterScale { get; set; }

    /// <summary>Discount amounts and the paid button (green, hue 162).</summary>
    public Color Positive { get; set; }

    /// <summary>Text and glyph on the paid button.</summary>
    public Color PositiveForeground { get; set; }

    /// <summary>Error text (rose, hue 10).</summary>
    public Color ErrorText { get; set; }

    /// <summary>Border of the promo form after a refused code (error mixed into the border).</summary>
    public Color ErrorBorder { get; set; }

    /// <summary>Apply's background under the pointer (muted surface with a touch of foreground).</summary>
    public Color ApplyHover { get; set; }

    /// <summary>Shade laid over the pay button under the pointer (brightness 0.97).</summary>
    public Color PayHoverShade { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        PreferredWidth = 440;
        PaddingMin = 20;
        PaddingMax = 28;
        PaddingViewportFactor = 0.05;
        SectionGap = 20;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        BodyLetterSpacing = -0.14;
        BodyLineHeight = 19.6;
        SmallLineHeight = 16.8;
        MediaMargin = new Thickness(0, 0, 12, 0);
        PriceMargin = new Thickness(12, 0, 0, 0);
        MediaSize = 44;
        CouponHeight = 44;
        CouponPadding = new Thickness(14, 4, 4, 4);
        CouponGap = 4;
        ApplyHeight = 34;
        ApplyMinWidth = 64;
        ApplyPadding = new Thickness(12, 0);
        ChipPadding = new Thickness(14, 0, 6, 0);
        ChipGap = 8;
        ChipCodeLetterSpacing = 0.28;
        RemoveSize = 32;
        ErrorPadding = new Thickness(0, 8, 0, 0);
        LinesPadding = new Thickness(0, 16, 0, 0);
        LinePadding = new Thickness(0, 5);
        TotalMargin = new Thickness(0, 8, 0, 0);
        TotalPadding = new Thickness(0, 14, 0, 5);
        TotalFontSize = 28;
        TotalClipPadding = new Thickness(0, 4.48);
        TotalClipMargin = new Thickness(0, -4.48);
        PayHeight = 52;
        PayLineHeight = 22.4;
        PayGap = 8;
        PayFooterGap = 12;
        SecureGap = 6;
        ShakeAmplitude = 6;
        ChipEnterScale = 0.97;
        FormEnterScale = 0.98;
        Positive = isDarkMode ? Oklch.ToColor(0.79, 0.14, 162) : Oklch.ToColor(0.50, 0.12, 162);
        PositiveForeground = isDarkMode ? Oklch.ToColor(0.19, 0, 0) : Colors.White;
        ErrorText = isDarkMode ? Oklch.ToColor(0.71, 0.155, 8) : Oklch.ToColor(0.53, 0.18, 10);
        ErrorBorder = isDarkMode ? Oklch.ToColor(0.542, 0.093, 8) : Oklch.ToColor(0.692, 0.108, 10);
        ApplyHover = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, isDarkMode ? 0.04 : 0.025);
        PayHoverShade = Color.FromArgb(8, 0, 0, 0);
    }
}

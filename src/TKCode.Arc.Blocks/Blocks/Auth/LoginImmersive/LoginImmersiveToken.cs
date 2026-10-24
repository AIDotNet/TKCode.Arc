using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LoginImmersive"/> and the colours it derives from the neutrals: the layers over the picture
/// (base, scrim, the near-white header and footer text), the glass card's fill and rules. Accent-following colours,
/// radii, shadows and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class LoginImmersiveToken : AbstractControlDesignToken
{
    /// <summary>The block's height when it is not full screen (640).</summary>
    public double FramedMinHeight { get; set; }

    /// <summary>The header row's height (64).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>The footer row's height (56).</summary>
    public double FooterHeight { get; set; }

    /// <summary>Header and footer padding (12 24).</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Header and footer padding at most 520 wide (12 16).</summary>
    public Thickness BarPaddingCompact { get; set; }

    /// <summary>Padding around the card (16 16 40).</summary>
    public Thickness StagePadding { get; set; }

    /// <summary>Padding around the card at most 520 wide (16 16 24).</summary>
    public Thickness StagePaddingCompact { get; set; }

    /// <summary>The card's widest width (420).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>A step's padding (32).</summary>
    public Thickness StepPadding { get; set; }

    /// <summary>A step's padding when the card's inside is narrower than 380 (24 20).</summary>
    public Thickness StepPaddingCompact { get; set; }

    /// <summary>The heading's size (36), line height (39.6) and tracking (−0.03em).</summary>
    public double HeadingFontSize { get; set; }

    /// <summary>The heading's line height.</summary>
    public double HeadingLineHeight { get; set; }

    /// <summary>The heading's letter spacing in px.</summary>
    public double HeadingLetterSpacing { get; set; }

    /// <summary>The heading's size in a compact card (28).</summary>
    public double HeadingFontSizeCompact { get; set; }

    /// <summary>The heading's line height in a compact card.</summary>
    public double HeadingLineHeightCompact { get; set; }

    /// <summary>The heading's letter spacing in a compact card.</summary>
    public double HeadingLetterSpacingCompact { get; set; }

    /// <summary>Body text line height (14 × 1.4).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Small text line height (12 × 1.4).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>The header's brand mark size (20).</summary>
    public double BrandMarkSize { get; set; }

    /// <summary>The header switch's inline padding (0 12).</summary>
    public Thickness SwitchPadding { get; set; }

    /// <summary>The header switch's inline padding at most 520 wide (0 8, pulled 8 to the right).</summary>
    public Thickness SwitchPaddingCompact { get; set; }

    /// <summary>A footer link's height (32).</summary>
    public double LegalHeight { get; set; }

    /// <summary>The section's colour under the picture (oklch .30, both themes).</summary>
    public Color BackdropBase { get; set; }

    /// <summary>The flat scrim over the picture (light oklch .14 / .30, dark oklch .10 / .50).</summary>
    public Color Scrim { get; set; }

    /// <summary>Text on the picture (oklch .985).</summary>
    public Color OnPhotoForeground { get; set; }

    /// <summary>Quieter text on the picture (oklch .985 at 76%).</summary>
    public Color OnPhotoMuted { get; set; }

    /// <summary>The keyboard focus ring on the picture (near white; Arc's dark ring is hard to see there).</summary>
    public Color OnPhotoFocusRing { get; set; }

    /// <summary>A pressed control on the picture.</summary>
    public Color OnPhotoPressed { get; set; }

    /// <summary>The glass card's fill over the blurred picture (surface at 80%, dark 74%).</summary>
    public Color CardFill { get; set; }

    /// <summary>The card's border (border-strong at 70%).</summary>
    public Color CardBorder { get; set; }

    /// <summary>The address box's border (border-strong at 70%).</summary>
    public Color AddressBorder { get; set; }

    /// <summary>The rules of the Done step's details (border-strong at 60%).</summary>
    public Color DetailsRule { get; set; }

    /// <summary>Opacity of the envelope's ink lines (.42).</summary>
    public double InkOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        FramedMinHeight = 640;
        HeaderHeight = 64;
        FooterHeight = 56;
        BarPadding = new Thickness(24, 12);
        BarPaddingCompact = new Thickness(16, 12);
        StagePadding = new Thickness(16, 16, 16, 40);
        StagePaddingCompact = new Thickness(16, 16, 16, 24);
        CardMaxWidth = 420;
        StepPadding = new Thickness(32);
        StepPaddingCompact = new Thickness(20, 24);
        HeadingFontSize = 36;
        HeadingLineHeight = 36 * 1.1;
        HeadingLetterSpacing = 36 * -0.03;
        HeadingFontSizeCompact = 28;
        HeadingLineHeightCompact = 28 * 1.1;
        HeadingLetterSpacingCompact = 28 * -0.03;
        BodyLineHeight = 14 * 1.4;
        SmallLineHeight = 12 * 1.4;
        BrandMarkSize = 20;
        SwitchPadding = new Thickness(12, 0);
        SwitchPaddingCompact = new Thickness(8, 0);
        LegalHeight = 32;
        BackdropBase = Oklch.ToColor(0.30, 0, 0);
        Scrim = isDarkMode ? Oklch.ToColor(0.10, 0, 0, 0.50) : Oklch.ToColor(0.14, 0, 0, 0.30);
        OnPhotoForeground = Oklch.ToColor(0.985, 0, 0);
        OnPhotoMuted = Oklch.ToColor(0.985, 0, 0, 0.76);
        OnPhotoFocusRing = Oklch.ToColor(0.985, 0, 0, 0.9);
        OnPhotoPressed = Oklch.ToColor(0.985, 0, 0, 0.12);
        CardFill = Oklch.WithAlpha(n.Surface, isDarkMode ? 0.74 : 0.80);
        CardBorder = Oklch.WithAlpha(n.BorderStrong, 0.70);
        AddressBorder = Oklch.WithAlpha(n.BorderStrong, 0.70);
        DetailsRule = Oklch.WithAlpha(n.BorderStrong, 0.60);
        InkOpacity = 0.42;
    }
}

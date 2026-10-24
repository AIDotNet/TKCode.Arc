using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LoginSplit"/>, measured on the reference preview (colors, radii and type sizes come from the
/// design tokens).
/// </summary>
[ControlDesignToken]
internal sealed class LoginSplitToken : AbstractControlDesignToken
{
    /// <summary>A framed block (not full screen) is at least 640 high.</summary>
    public double FramedMinHeight { get; set; }

    /// <summary>Header padding 24 32 0 (compact 20 20 0); the header row is 46 high (24 + the 22px brand).</summary>
    public Thickness HeaderInset { get; set; }

    public Thickness CompactHeaderInset { get; set; }

    /// <summary>Footer padding 0 32 24 (compact 0 20 20).</summary>
    public Thickness FooterInset { get; set; }

    public Thickness CompactFooterInset { get; set; }

    /// <summary>The middle of the form column: 40 above and below, 20 at the sides.</summary>
    public Thickness MainInset { get; set; }

    /// <summary>The step viewport is at most 384 wide (the 360 form plus 12 each side for blur and focus rings).</summary>
    public double ViewportMaxWidth { get; set; }

    /// <summary>The viewport hangs 12 outside the form on both sides.</summary>
    public Thickness ViewportOutset { get; set; }

    /// <summary>Each step has 8 above and below, 12 at the sides.</summary>
    public Thickness StepInset { get; set; }

    /// <summary>Brand mark 22 and 8 to the name.</summary>
    public double BrandMarkSize { get; set; }

    public double BrandGap { get; set; }

    /// <summary>Step heading: 36 / 39.6, tracking −0.03em; 8 to the line under it, 24 to the form.</summary>
    public double HeadingLineHeight { get; set; }

    public double HeadingLetterSpacing { get; set; }

    public double HeadingGap { get; set; }

    public double HeadingMargin { get; set; }

    /// <summary>Body text tracking −0.01em at 14 and 12 px and at 16 px (brand).</summary>
    public double BodyLetterSpacing { get; set; }

    public double SmallLetterSpacing { get; set; }

    public double BrandLetterSpacing { get; set; }

    /// <summary>16 between the field and the button; 8 above Sign in.</summary>
    public double FormGap { get; set; }

    public double SubmitGap { get; set; }

    /// <summary>"or": 20 above and below, 12 between the lines and the word.</summary>
    public double DividerMargin { get; set; }

    public double DividerGap { get; set; }

    /// <summary>8 between single sign-on buttons, 8 between mark and name, 8 inline padding.</summary>
    public double ProviderGap { get; set; }

    public Thickness ProviderInset { get; set; }

    /// <summary>Account chip: 36 high, padding 3 12 3 3, 8 between parts; chevron 16.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipInset { get; set; }

    public double ChipGap { get; set; }

    /// <summary>Account row: 12 between avatar and text, 24 below.</summary>
    public double AccountGap { get; set; }

    /// <summary>Progress line 2 high.</summary>
    public double ProgressHeight { get; set; }

    /// <summary>Sign out sits 8 below the heading's margin.</summary>
    public double SignOutGap { get; set; }

    /// <summary>"Forgot password?" padding 6 4 (it hangs out of the label row by the same).</summary>
    public Thickness ForgotInset { get; set; }

    /// <summary>Reset notice: 8 above, about 8 before the resend button.</summary>
    public double NoticeGap { get; set; }

    /// <summary>Footer: 8 between rows when it wraps, 20 between status and links, 16 between the links.</summary>
    public double FooterRowGap { get; set; }

    public double FooterColumnGap { get; set; }

    public double LegalGap { get; set; }

    /// <summary>Container widths: two columns 5:6 from 1100, 1:1 from 760, one column below; compact padding below 480.</summary>
    public double WideBreakpoint { get; set; }

    public double SplitBreakpoint { get; set; }

    public double CompactBreakpoint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FramedMinHeight = LoginSplitMetrics.FramedMinHeight;
        HeaderInset = LoginSplitMetrics.HeaderInset;
        CompactHeaderInset = LoginSplitMetrics.CompactHeaderInset;
        FooterInset = LoginSplitMetrics.FooterInset;
        CompactFooterInset = LoginSplitMetrics.CompactFooterInset;
        MainInset = LoginSplitMetrics.MainInset;
        ViewportMaxWidth = LoginSplitMetrics.ViewportMaxWidth;
        ViewportOutset = LoginSplitMetrics.ViewportOutset;
        StepInset = LoginSplitMetrics.StepInset;
        BrandMarkSize = LoginSplitMetrics.BrandMarkSize;
        BrandGap = LoginSplitMetrics.BrandGap;
        HeadingLineHeight = LoginSplitMetrics.HeadingLineHeight;
        HeadingLetterSpacing = LoginSplitMetrics.HeadingLetterSpacing;
        HeadingGap = LoginSplitMetrics.HeadingGap;
        HeadingMargin = LoginSplitMetrics.HeadingMargin;
        BodyLetterSpacing = LoginSplitMetrics.BodyLetterSpacing;
        SmallLetterSpacing = LoginSplitMetrics.SmallLetterSpacing;
        BrandLetterSpacing = LoginSplitMetrics.BrandLetterSpacing;
        FormGap = LoginSplitMetrics.FormGap;
        SubmitGap = LoginSplitMetrics.SubmitGap;
        DividerMargin = LoginSplitMetrics.DividerMargin;
        DividerGap = LoginSplitMetrics.DividerGap;
        ProviderGap = LoginSplitMetrics.ProviderGap;
        ProviderInset = LoginSplitMetrics.ProviderInset;
        ChipHeight = LoginSplitMetrics.ChipHeight;
        ChipInset = LoginSplitMetrics.ChipInset;
        ChipGap = LoginSplitMetrics.ChipGap;
        AccountGap = LoginSplitMetrics.AccountGap;
        ProgressHeight = LoginSplitMetrics.ProgressHeight;
        SignOutGap = LoginSplitMetrics.SignOutGap;
        ForgotInset = LoginSplitMetrics.ForgotInset;
        NoticeGap = LoginSplitMetrics.NoticeGap;
        FooterRowGap = LoginSplitMetrics.FooterRowGap;
        FooterColumnGap = LoginSplitMetrics.FooterColumnGap;
        LegalGap = LoginSplitMetrics.LegalGap;
        WideBreakpoint = LoginSplitMetrics.WideBreakpoint;
        SplitBreakpoint = LoginSplitMetrics.SplitBreakpoint;
        CompactBreakpoint = LoginSplitMetrics.CompactBreakpoint;
    }
}

/// <summary>
/// The measured geometry of <see cref="LoginSplit"/> in one place: the theme reads it through <see cref="LoginSplitToken"/>,
/// the parts built in code read it here.
/// </summary>
internal static class LoginSplitMetrics
{
    public const double FramedMinHeight = 640;
    public static readonly Thickness HeaderInset = new(32, 24, 32, 0);
    public static readonly Thickness CompactHeaderInset = new(20, 20, 20, 0);
    public static readonly Thickness FooterInset = new(32, 0, 32, 24);
    public static readonly Thickness CompactFooterInset = new(20, 0, 20, 20);
    public static readonly Thickness MainInset = new(20, 40);
    public const double ViewportMaxWidth = 384;
    public static readonly Thickness ViewportOutset = new(-12, 0);
    public static readonly Thickness StepInset = new(12, 8);
    public const double BrandMarkSize = 22;
    public const double BrandGap = 8;
    public const double HeadingLineHeight = 39.6;
    public const double HeadingLetterSpacing = -1.08;
    public const double HeadingGap = 8;
    public const double HeadingMargin = 24;
    public const double BodyLetterSpacing = -0.14;
    public const double SmallLetterSpacing = -0.12;
    public const double BrandLetterSpacing = -0.16;
    public const double BodyLineHeight = 19.6;
    public const double SmallLineHeight = 16.8;
    public const double FormGap = 16;
    public const double SubmitGap = 8;
    public const double DividerMargin = 20;
    public const double DividerGap = 12;
    public const double ProviderGap = 8;
    public static readonly Thickness ProviderInset = new(8, 0);
    public const double ChipHeight = 36;
    public static readonly Thickness ChipInset = new(3, 3, 12, 3);
    public const double ChipGap = 8;
    public const double AccountGap = 12;
    public const double ProgressHeight = 2;
    public const double SignOutGap = 8;
    public static readonly Thickness ForgotInset = new(4, 6);
    public const double NoticeGap = 8;
    public const double SubmitDiameter = 44;
    public const double FooterRowGap = 8;
    public const double FooterColumnGap = 20;
    public const double LegalGap = 16;
    public const double WideBreakpoint = 1100;
    public const double SplitBreakpoint = 760;
    public const double CompactBreakpoint = 480;
}

// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): newsletter-signup.module.css.
using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="NewsletterSignup"/> and the accent-independent colors it derives from the neutrals: the paper
/// of the issues (the dark theme uses fixed grays), the pill's hover and done borders and the button's hover and done
/// fills. Everything that follows the accent, and radii and fonts, come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class NewsletterSignupToken : AbstractControlDesignToken
{
    /// <summary>The front issue's paper (<c>--paper</c>): the raised surface, fixed 24.5% gray in the dark theme.</summary>
    public Color PaperBackground { get; set; }

    /// <summary>The second issue's paper (<c>--paper-back</c>): the raised surface mixed 55% toward the muted one.</summary>
    public Color PaperBackBackground { get; set; }

    /// <summary>The third issue's paper (<c>--paper-far</c>): the muted surface.</summary>
    public Color PaperFarBackground { get; set; }

    /// <summary>The front issue's shadow (<c>--paper-shadow</c>).</summary>
    public BoxShadows PaperShadow { get; set; }

    /// <summary>Issue corner radius (22; 20 in a narrow block).</summary>
    public CornerRadius IssueRadius { get; set; }

    /// <summary>Issue corner radius in a narrow block.</summary>
    public CornerRadius IssueRadiusNarrow { get; set; }

    /// <summary>Issue padding (22 22 22 18).</summary>
    public Thickness IssueInset { get; set; }

    /// <summary>Issue padding in a narrow block (18 18 18 16).</summary>
    public Thickness IssueInsetNarrow { get; set; }

    /// <summary>The stack is at most this wide (440).</summary>
    public double StackMaxWidth { get; set; }

    /// <summary>The room above the front issue for the ones behind it (24 = two steps).</summary>
    public double StackTopInset { get; set; }

    /// <summary>How far each issue behind the front one peeks out above it (12).</summary>
    public double StackStep { get; set; }

    /// <summary>How much each issue behind the front one shrinks (.045).</summary>
    public double StackShrink { get; set; }

    /// <summary>The story thumbnail's edge (44).</summary>
    public double ThumbEdge { get; set; }

    /// <summary>The story thumbnail's corner radius (10).</summary>
    public CornerRadius ThumbRadius { get; set; }

    /// <summary>The pill's height (58).</summary>
    public double PillHeight { get; set; }

    /// <summary>The pill's padding (22 5 5 5).</summary>
    public Thickness PillInset { get; set; }

    /// <summary>The input's height once the form stacks (52).</summary>
    public double StackedInputHeight { get; set; }

    /// <summary>The button's height once the form stacks (48).</summary>
    public double StackedButtonHeight { get; set; }

    /// <summary>The hovered pill's border: the foreground at 32% over the strong border.</summary>
    public Color PillHoverBorder { get; set; }

    /// <summary>The pill's border once subscribed: the success color at 45% over the border color.</summary>
    public Color PillDoneBorder { get; set; }

    /// <summary>The hovered button's fill: the foreground at 84% over the background.</summary>
    public Color SubmitHoverBackground { get; set; }

    /// <summary>The button's fill once subscribed: the success color at 13% over the surface.</summary>
    public Color SubmitDoneBackground { get; set; }

    /// <summary>The card tray's background: the muted surface, the page background in the dark theme.</summary>
    public Color TrayBackground { get; set; }

    /// <summary>The card tray's height (248; 232 in a narrow block).</summary>
    public double TrayHeight { get; set; }

    /// <summary>The card tray's height in a narrow block.</summary>
    public double TrayHeightNarrow { get; set; }

    /// <summary>The card tray's padding (20 32 0).</summary>
    public Thickness TrayInset { get; set; }

    /// <summary>The card tray's padding in a narrow block (16 20 0).</summary>
    public Thickness TrayInsetNarrow { get; set; }

    /// <summary>The card is at most this wide (460).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The card's body padding (32; 24 in a narrow block).</summary>
    public Thickness CardBodyInset { get; set; }

    /// <summary>The card's body padding in a narrow block.</summary>
    public Thickness CardBodyInsetNarrow { get; set; }

    /// <summary>The inline section is at most this wide (1120; 720 without the stack and the readers).</summary>
    public double InlineMaxWidth { get; set; }

    /// <summary>The inline section's width without the stack and the readers.</summary>
    public double InlineMaxWidthSingle { get; set; }

    /// <summary>The copy column is at most this wide (540; 600 in one column).</summary>
    public double IntroMaxWidth { get; set; }

    /// <summary>The copy column's width in one column.</summary>
    public double IntroMaxWidthCompact { get; set; }

    /// <summary>The note under the form starts 22 in (4 in a narrow block or a stacked form).</summary>
    public Thickness MessageInset { get; set; }

    /// <summary>The note's start inset in a narrow block or a stacked form.</summary>
    public Thickness MessageInsetNarrow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        if (isDarkMode)
        {
            PaperBackground = Oklch.ToColor(0.245, 0, 0);
            PaperBackBackground = Oklch.ToColor(0.225, 0, 0);
            PaperFarBackground = Oklch.ToColor(0.21, 0, 0);
            PaperShadow = BoxShadows.Parse("0 1 1 0 #33000000, 0 14 32 -6 #73000000");
        }
        else
        {
            PaperBackground = n.SurfaceRaised;
            PaperBackBackground = Oklch.MixOklab(n.SurfaceRaised, n.SurfaceMuted, 0.55);
            PaperFarBackground = n.SurfaceMuted;
            PaperShadow = BoxShadows.Parse("0 1 1 0 #08000000, 0 10 28 -6 #17000000");
        }
        IssueRadius = new CornerRadius(22);
        IssueRadiusNarrow = new CornerRadius(20);
        IssueInset = new Thickness(22, 22, 22, 18);
        IssueInsetNarrow = new Thickness(18, 18, 18, 16);
        StackMaxWidth = 440;
        StackTopInset = 24;
        StackStep = 12;
        StackShrink = 0.045;
        ThumbEdge = 44;
        ThumbRadius = new CornerRadius(10);
        PillHeight = 58;
        PillInset = new Thickness(22, 5, 5, 5);
        StackedInputHeight = 52;
        StackedButtonHeight = 48;
        PillHoverBorder = Oklch.MixOklab(n.Foreground, n.BorderStrong, 0.68);
        PillDoneBorder = Oklch.MixOklab(n.Success, n.Border, 0.55);
        SubmitHoverBackground = Oklch.MixOklab(n.Foreground, n.Background, 0.16);
        SubmitDoneBackground = Oklch.MixOklab(n.Success, n.Surface, 0.87);
        TrayBackground = isDarkMode ? n.Background : n.SurfaceMuted;
        TrayHeight = 248;
        TrayHeightNarrow = 232;
        TrayInset = new Thickness(32, 20, 32, 0);
        TrayInsetNarrow = new Thickness(20, 16, 20, 0);
        CardMaxWidth = 460;
        CardBodyInset = new Thickness(32);
        CardBodyInsetNarrow = new Thickness(24);
        InlineMaxWidth = 1120;
        InlineMaxWidthSingle = 720;
        IntroMaxWidth = 540;
        IntroMaxWidthCompact = 600;
        MessageInset = new Thickness(22, 0, 0, 0);
        MessageInsetNarrow = new Thickness(4, 0, 0, 0);
    }
}

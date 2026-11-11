using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc global semantic tokens, carried by <see cref="ArcDesign"/>. The single source of every value is
/// design/architecture/theming/token-catalog.md. Values are computed from the accent seed
/// (<see cref="ArcSeedToken.ColorPrimary"/>, the theme state's accent) and the appearance. It is the global identity: theme
/// overrides that name no identity (<c>Token("RadiusControl", "12")</c>) land here.
/// </summary>
[ControlDesignToken(IsGlobal = true)]
internal sealed class ArcDesignToken : AbstractControlDesignToken
{
    #region Neutral ramp

    public Color Neutral0 { get; set; }
    public Color Neutral1 { get; set; }
    public Color Neutral2 { get; set; }
    public Color Neutral3 { get; set; }
    public Color Neutral4 { get; set; }
    public Color Neutral5 { get; set; }
    public Color Neutral6 { get; set; }
    public Color Neutral7 { get; set; }
    public Color Neutral8 { get; set; }
    public Color Neutral9 { get; set; }
    public Color Neutral10 { get; set; }
    public Color Neutral11 { get; set; }

    #endregion

    #region Surfaces, text, borders

    public Color Background { get; set; }
    public Color Surface { get; set; }
    public Color SurfaceRaised { get; set; }
    public Color SurfaceMuted { get; set; }
    public Color Scrim { get; set; }
    public Color Foreground { get; set; }
    public Color TextSecondary { get; set; }
    public Color TextMuted { get; set; }
    public Color Border { get; set; }
    public Color BorderSubtle { get; set; }
    public Color BorderStrong { get; set; }

    #endregion

    #region Accent

    public Color Accent { get; set; }
    public Color AccentStrong { get; set; }
    public Color AccentSubtle { get; set; }
    public Color AccentForeground { get; set; }
    public string AccentPreset { get; set; } = "neutral";

    #endregion

    #region Selection controls

    public Color ControlOn { get; set; }
    public Color ControlOnSubtle { get; set; }
    public Color ControlGlyph { get; set; }
    public Color ControlTrack { get; set; }
    public Color ControlTrackHover { get; set; }
    public Color ControlThumb { get; set; }
    public Color ControlThumbOn { get; set; }
    public Color ControlFill { get; set; }
    public BoxShadows ControlThumbShadow { get; set; }

    #endregion

    #region Status and series

    public Color Success { get; set; }
    public Color Warning { get; set; }
    public Color Danger { get; set; }
    public Color SuccessSubtle { get; set; }
    public Color WarningSubtle { get; set; }
    public Color DangerSubtle { get; set; }
    public Color Series1 { get; set; }
    public Color Series2 { get; set; }
    public Color Series3 { get; set; }
    public Color Series4 { get; set; }

    #endregion

    #region Shadows and focus

    public BoxShadows ShadowResting { get; set; }
    public BoxShadows ShadowRaised { get; set; }
    public BoxShadows ShadowFloating { get; set; }
    public Color FocusRing { get; set; }
    public double FocusRingWidth { get; set; }
    public double FocusRingOffset { get; set; }

    /// <summary>Border thickness of controls and surfaces (1). Pixel-style themes raise it.</summary>
    public Thickness EdgeThickness { get; set; }

    /// <summary>Border thickness of the focus ring (2).</summary>
    public Thickness FocusEdgeThickness { get; set; }

    /// <summary>One-sided and three-sided border thicknesses for dividers and joined edges (1 on the named side). They follow <see cref="EdgeThickness"/> in a theme that thickens edges.</summary>
    public Thickness EdgeThicknessTop { get; set; }
    public Thickness EdgeThicknessBottom { get; set; }
    public Thickness EdgeThicknessLeft { get; set; }
    public Thickness EdgeThicknessRight { get; set; }
    public Thickness EdgeThicknessRightBottom { get; set; }
    public Thickness EdgeThicknessNoBottom { get; set; }
    public Thickness EdgeThicknessNoRight { get; set; }
    public Thickness EdgeThicknessNoLeft { get; set; }

    #endregion

    #region Geometry

    public CornerRadius RadiusControl { get; set; }
    public CornerRadius RadiusPanel { get; set; }
    public CornerRadius RadiusSurface { get; set; }
    public CornerRadius RadiusPill { get; set; }
    public double ControlSizeSM { get; set; }
    public double ControlSizeMD { get; set; }
    public double ControlSizeLG { get; set; }
    public double GlyphSizeSM { get; set; }
    public double GlyphSizeMD { get; set; }
    public double GlyphSizeLG { get; set; }
    public double IconStrokeWidth { get; set; }
    public double Space1 { get; set; }
    public double Space2 { get; set; }
    public double Space3 { get; set; }
    public double Space4 { get; set; }
    public double Space5 { get; set; }
    public double Space6 { get; set; }
    public double Space8 { get; set; }
    public double Space10 { get; set; }
    public double Space12 { get; set; }
    public double Space16 { get; set; }
    public double Space20 { get; set; }
    public double Space24 { get; set; }

    /// <summary>Radius steps that block templates use as literals (the number is the default). Pixel-style themes zero them.</summary>
    public CornerRadius RadiusStep1 { get; set; }
    public CornerRadius RadiusStep1p25 { get; set; }
    public CornerRadius RadiusStep1p5 { get; set; }
    public CornerRadius RadiusStep2 { get; set; }
    public CornerRadius RadiusStep3 { get; set; }
    public CornerRadius RadiusStep4 { get; set; }
    public CornerRadius RadiusStep5 { get; set; }
    public CornerRadius RadiusStep6 { get; set; }
    public CornerRadius RadiusStep7 { get; set; }
    public CornerRadius RadiusStep8 { get; set; }
    public CornerRadius RadiusStep9 { get; set; }
    public CornerRadius RadiusStep10 { get; set; }
    public CornerRadius RadiusStep11 { get; set; }
    public CornerRadius RadiusStep12 { get; set; }
    public CornerRadius RadiusStep13 { get; set; }
    public CornerRadius RadiusStep14 { get; set; }
    public CornerRadius RadiusStep14p5 { get; set; }
    public CornerRadius RadiusStep15 { get; set; }
    public CornerRadius RadiusStep15Bottom { get; set; }
    public CornerRadius RadiusStep16 { get; set; }
    public CornerRadius RadiusStep17 { get; set; }
    public CornerRadius RadiusStep18 { get; set; }
    public CornerRadius RadiusStep19 { get; set; }
    public CornerRadius RadiusStep19Top { get; set; }
    public CornerRadius RadiusStep20 { get; set; }
    public CornerRadius RadiusStep20Top { get; set; }
    public CornerRadius RadiusStep21 { get; set; }
    public CornerRadius RadiusStep22 { get; set; }
    public CornerRadius RadiusStep24 { get; set; }
    public CornerRadius RadiusStep25 { get; set; }
    public CornerRadius RadiusStep25Bottom { get; set; }
    public CornerRadius RadiusStep28 { get; set; }
    public CornerRadius RadiusStep33 { get; set; }
    public CornerRadius RadiusStep33Bottom { get; set; }

    #endregion

    #region Typography

    public FontFamily DisplayFontFamily { get; set; } = FontFamily.Default;
    public FontFamily BodyFontFamily { get; set; } = FontFamily.Default;
    public FontFamily MonoFontFamily { get; set; } = FontFamily.Default;
    public double TypeXS { get; set; }
    public double TypeSM { get; set; }
    public double TypeBase { get; set; }
    public double TypeLG { get; set; }
    public double TypeXL { get; set; }
    public double Type2XL { get; set; }
    public double Type3XL { get; set; }
    public double Type4XL { get; set; }
    public double Type5XL { get; set; }
    public FontWeight WeightRegular { get; set; }
    public FontWeight WeightMedium { get; set; }
    public double TrackingDisplay { get; set; }
    public double TrackingBody { get; set; }
    public double LeadingDisplay { get; set; }
    public double LeadingBody { get; set; }

    #endregion

    #region Motion

    public TimeSpan DurationInstant { get; set; }
    public TimeSpan DurationFast { get; set; }
    public TimeSpan DurationExit { get; set; }
    public TimeSpan DurationStandard { get; set; }
    public TimeSpan DurationConsidered { get; set; }
    public TimeSpan DurationSpring { get; set; }
    public Easing EaseEnter { get; set; } = new LinearEasing();
    public Easing EaseExit { get; set; } = new LinearEasing();
    public Easing EaseStandard { get; set; } = new LinearEasing();
    public Easing EaseInOut { get; set; } = new LinearEasing();
    public TimeSpan SpringSmoothDuration { get; set; }
    public double SpringSmoothBounce { get; set; }
    public TimeSpan SpringSnappyDuration { get; set; }
    public double SpringSnappyBounce { get; set; }
    public TimeSpan SpringMorphDuration { get; set; }
    public double SpringMorphBounce { get; set; }
    public double SpringResponsiveStiffness { get; set; }
    public double SpringResponsiveDamping { get; set; }
    public double SpringGentleStiffness { get; set; }
    public double SpringGentleDamping { get; set; }
    public TimeSpan StaggerChar { get; set; }
    public TimeSpan StaggerWord { get; set; }
    public TimeSpan StaggerLine { get; set; }
    public TimeSpan StaggerItem { get; set; }

    #endregion

    #region Effects and policy

    public double BlurSubtle { get; set; }
    public double BlurSoft { get; set; }
    public double BlurText { get; set; }
    public double BackdropBlurDialog { get; set; }
    public double BackdropBlurDrawer { get; set; }
    public string MotionMode { get; set; } = "full";
    public bool ReduceTransparency { get; set; }

    #endregion

    // Appended after the earlier groups: the generated ArcDesignTokenKind numbers tokens in declaration order, and code
    // compiled against an earlier version keeps the numbers it was built with.
    #region Content widths and media

    /// <summary>
    /// Max widths of a page's content column, set as <c>MaxWidth</c> so a narrower window still uses its whole width:
    /// SM 640 (a reading column or a single form), MD 760 (a settings page), LG 880 (a document page with tables or two
    /// columns of fields), XL 1120 (full-width page sections, the width of the Blocks).
    /// </summary>
    public double ContentMaxWidthSM { get; set; }
    public double ContentMaxWidthMD { get; set; }
    public double ContentMaxWidthLG { get; set; }
    public double ContentMaxWidthXL { get; set; }

    /// <summary>
    /// Sides of square media, in steps of 64: SM 64 (a thumbnail in a row), MD 128 (a preview on a card), LG 192 (a QR
    /// code or cover art), XL 256 (a large image preview).
    /// </summary>
    public double MediaSizeSM { get; set; }
    public double MediaSizeMD { get; set; }
    public double MediaSizeLG { get; set; }
    public double MediaSizeXL { get; set; }

    #endregion

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ApplyNeutralRamp();
        ApplySemantic(isDarkMode);
        ApplyGeometry();
        ApplyLayout();
        ApplyTypography();
        ApplyMotion();
        BlurSubtle = 2;
        BlurSoft = 4;
        BlurText = 8;
        BackdropBlurDialog = 7;
        BackdropBlurDrawer = 4;
        MotionMode = EffectiveGlobalToken.MotionLevel switch
        {
            ArcMotionLevel.Off => "off",
            ArcMotionLevel.Reduced => "reduced",
            _ => "full"
        };
        ReduceTransparency = false;
    }

    // Literals parsed once: Compute runs on every theme change and these never vary.
    private static readonly Color[] s_neutralRamp =
    [
        Color.Parse("#FFFFFF"), Color.Parse("#FCFCFC"), Color.Parse("#F8F8F8"), Color.Parse("#F1F1F1"),
        Color.Parse("#E3E3E3"), Color.Parse("#CACACA"), Color.Parse("#A4A4A4"), Color.Parse("#7D7D7D"),
        Color.Parse("#585858"), Color.Parse("#383838"), Color.Parse("#1D1D1D"), Color.Parse("#0B0B0B")
    ];

    private static readonly BoxShadows s_controlThumbShadowDark = BoxShadows.Parse("0 0 0 0.5 #4C000000, 0 1 2 0 #59000000, 0 2 6 0 #33000000");
    private static readonly BoxShadows s_shadowRestingDark = BoxShadows.Parse("0 1 2 0 #2E000000");
    private static readonly BoxShadows s_shadowRaisedDark = BoxShadows.Parse("0 8 22 0 #38000000");
    private static readonly BoxShadows s_shadowFloatingDark = BoxShadows.Parse("0 22 55 0 #57000000");
    private static readonly BoxShadows s_controlThumbShadowLight = BoxShadows.Parse("0 0 0 0.5 #12000000, 0 1 2 0 #24000000, 0 2 6 0 #0F000000");
    private static readonly BoxShadows s_shadowRestingLight = BoxShadows.Parse("0 1 2 0 #09000000");
    private static readonly BoxShadows s_shadowRaisedLight = BoxShadows.Parse("0 6 18 0 #11000000, 0 1 3 0 #09000000");
    private static readonly BoxShadows s_shadowFloatingLight = BoxShadows.Parse("0 20 48 0 #1B000000, 0 3 10 0 #0B000000");

    private void ApplyNeutralRamp()
    {
        Neutral0 = s_neutralRamp[0];
        Neutral1 = s_neutralRamp[1];
        Neutral2 = s_neutralRamp[2];
        Neutral3 = s_neutralRamp[3];
        Neutral4 = s_neutralRamp[4];
        Neutral5 = s_neutralRamp[5];
        Neutral6 = s_neutralRamp[6];
        Neutral7 = s_neutralRamp[7];
        Neutral8 = s_neutralRamp[8];
        Neutral9 = s_neutralRamp[9];
        Neutral10 = s_neutralRamp[10];
        Neutral11 = s_neutralRamp[11];
    }

    private void ApplySemantic(bool isDark)
    {
        var n = ArcPalette.Neutrals(isDark);
        Background = n.Background;
        Surface = n.Surface;
        SurfaceRaised = n.SurfaceRaised;
        SurfaceMuted = n.SurfaceMuted;
        Scrim = n.Scrim;
        Foreground = n.Foreground;
        TextSecondary = n.TextSecondary;
        TextMuted = n.TextMuted;
        Border = n.Border;
        BorderSubtle = n.BorderSubtle;
        BorderStrong = n.BorderStrong;
        ControlTrack = n.ControlTrack;
        ControlTrackHover = n.ControlTrackHover;
        ControlThumb = n.ControlThumb;
        Success = n.Success;
        Warning = n.Warning;
        Danger = n.Danger;
        SuccessSubtle = Oklch.WithAlpha(n.Success, isDark ? 0.16 : 0.12);
        WarningSubtle = Oklch.WithAlpha(n.Warning, isDark ? 0.16 : 0.12);
        DangerSubtle = Oklch.WithAlpha(n.Danger, isDark ? 0.16 : 0.12);

        var accent = ArcPalette.Resolve(EffectiveGlobalToken.ColorPrimary, isDark);
        Accent = accent.Accent;
        AccentStrong = accent.AccentStrong;
        AccentSubtle = accent.AccentSubtle;
        AccentForeground = accent.AccentForeground;
        AccentPreset = accent.Preset;
        ControlOn = accent.ControlOn;
        ControlOnSubtle = Oklch.WithAlpha(accent.ControlOn, 0.10);
        ControlGlyph = accent.ControlGlyph;
        ControlThumbOn = accent.ControlThumbOn;
        ControlFill = accent.ControlFill;
        Series1 = accent.Series1;
        Series2 = accent.Series2;
        Series3 = accent.Series3;
        Series4 = accent.Series4;
        FocusRing = Oklch.WithAlpha(accent.AccentStrong, 0.72);
        FocusRingWidth = 2;
        FocusRingOffset = 2;

        if (isDark)
        {
            ControlThumbShadow = s_controlThumbShadowDark;
            ShadowResting = s_shadowRestingDark;
            ShadowRaised = s_shadowRaisedDark;
            ShadowFloating = s_shadowFloatingDark;
        }
        else
        {
            ControlThumbShadow = s_controlThumbShadowLight;
            ShadowResting = s_shadowRestingLight;
            ShadowRaised = s_shadowRaisedLight;
            ShadowFloating = s_shadowFloatingLight;
        }
    }

    private void ApplyGeometry()
    {
        EdgeThickness = new Thickness(1);
        FocusEdgeThickness = new Thickness(2);
        EdgeThicknessTop = new Thickness(0, 1, 0, 0);
        EdgeThicknessBottom = new Thickness(0, 0, 0, 1);
        EdgeThicknessLeft = new Thickness(1, 0, 0, 0);
        EdgeThicknessRight = new Thickness(0, 0, 1, 0);
        EdgeThicknessRightBottom = new Thickness(0, 0, 1, 1);
        EdgeThicknessNoBottom = new Thickness(1, 1, 1, 0);
        EdgeThicknessNoRight = new Thickness(1, 1, 0, 1);
        EdgeThicknessNoLeft = new Thickness(0, 1, 1, 1);
        RadiusControl = new CornerRadius(18);
        RadiusPanel = new CornerRadius(26);
        RadiusSurface = new CornerRadius(34);
        RadiusPill = new CornerRadius(9999);
        RadiusStep1 = new CornerRadius(1);
        RadiusStep1p25 = new CornerRadius(1.25);
        RadiusStep1p5 = new CornerRadius(1.5);
        RadiusStep2 = new CornerRadius(2);
        RadiusStep3 = new CornerRadius(3);
        RadiusStep4 = new CornerRadius(4);
        RadiusStep5 = new CornerRadius(5);
        RadiusStep6 = new CornerRadius(6);
        RadiusStep7 = new CornerRadius(7);
        RadiusStep8 = new CornerRadius(8);
        RadiusStep9 = new CornerRadius(9);
        RadiusStep10 = new CornerRadius(10);
        RadiusStep11 = new CornerRadius(11);
        RadiusStep12 = new CornerRadius(12);
        RadiusStep13 = new CornerRadius(13);
        RadiusStep14 = new CornerRadius(14);
        RadiusStep14p5 = new CornerRadius(14.5);
        RadiusStep15 = new CornerRadius(15);
        RadiusStep15Bottom = new CornerRadius(0, 0, 15, 15);
        RadiusStep16 = new CornerRadius(16);
        RadiusStep17 = new CornerRadius(17);
        RadiusStep18 = new CornerRadius(18);
        RadiusStep19 = new CornerRadius(19);
        RadiusStep19Top = new CornerRadius(19, 19, 0, 0);
        RadiusStep20 = new CornerRadius(20);
        RadiusStep20Top = new CornerRadius(20, 20, 0, 0);
        RadiusStep21 = new CornerRadius(21);
        RadiusStep22 = new CornerRadius(22);
        RadiusStep24 = new CornerRadius(24);
        RadiusStep25 = new CornerRadius(25);
        RadiusStep25Bottom = new CornerRadius(0, 0, 25, 25);
        RadiusStep28 = new CornerRadius(28);
        RadiusStep33 = new CornerRadius(33);
        RadiusStep33Bottom = new CornerRadius(0, 0, 33, 33);
        ControlSizeSM = 36;
        ControlSizeMD = 44;
        ControlSizeLG = 50;
        GlyphSizeSM = 16;
        GlyphSizeMD = 20;
        GlyphSizeLG = 24;
        IconStrokeWidth = 1.75;
        Space1 = 4;
        Space2 = 8;
        Space3 = 12;
        Space4 = 16;
        Space5 = 20;
        Space6 = 24;
        Space8 = 32;
        Space10 = 40;
        Space12 = 48;
        Space16 = 64;
        Space20 = 80;
        Space24 = 96;
    }

    private void ApplyLayout()
    {
        ContentMaxWidthSM = 640;
        ContentMaxWidthMD = 760;
        ContentMaxWidthLG = 880;
        ContentMaxWidthXL = 1120;
        MediaSizeSM = 64;
        MediaSizeMD = 128;
        MediaSizeLG = 192;
        MediaSizeXL = 256;
    }

    private void ApplyTypography()
    {
        DisplayFontFamily = ArcFonts.Display;
        BodyFontFamily = ArcFonts.Body;
        MonoFontFamily = ArcFonts.Mono;
        TypeXS = 12;
        TypeSM = 14;
        TypeBase = 16;
        TypeLG = 18;
        TypeXL = 22;
        Type2XL = 28;
        Type3XL = 36;
        Type4XL = 52;
        Type5XL = 72;
        WeightRegular = FontWeight.Normal;
        WeightMedium = FontWeight.Medium;
        TrackingDisplay = -0.03;
        TrackingBody = -0.01;
        LeadingDisplay = 1.1;
        LeadingBody = 1.4;
    }

    private void ApplyMotion()
    {
        DurationInstant = TimeSpan.FromMilliseconds(120);
        DurationFast = TimeSpan.FromMilliseconds(160);
        DurationExit = TimeSpan.FromMilliseconds(180);
        DurationStandard = TimeSpan.FromMilliseconds(240);
        DurationConsidered = TimeSpan.FromMilliseconds(480);
        DurationSpring = TimeSpan.FromMilliseconds(580);
        EaseEnter = ArcEasings.Enter;
        EaseExit = ArcEasings.Exit;
        EaseStandard = ArcEasings.Standard;
        EaseInOut = ArcEasings.InOut;
        SpringSmoothDuration = TimeSpan.FromSeconds(0.4);
        SpringSmoothBounce = 0;
        SpringSnappyDuration = TimeSpan.FromSeconds(0.26);
        SpringSnappyBounce = 0.12;
        SpringMorphDuration = TimeSpan.FromSeconds(0.42);
        SpringMorphBounce = 0.16;
        SpringResponsiveStiffness = 520;
        SpringResponsiveDamping = 38;
        SpringGentleStiffness = 340;
        SpringGentleDamping = 34;
        StaggerChar = TimeSpan.FromMilliseconds(16);
        StaggerWord = TimeSpan.FromMilliseconds(40);
        StaggerLine = TimeSpan.FromMilliseconds(80);
        StaggerItem = TimeSpan.FromMilliseconds(35);
    }
}

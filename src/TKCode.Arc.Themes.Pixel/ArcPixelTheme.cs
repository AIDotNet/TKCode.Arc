using AtomUI.Theme;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using AtomUI.Theme.Schema;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Themes.Pixel;

/// <summary>
/// The Arc pixel theme: token overrides only (design/architecture/theming/pixel-theme.md). Square corners, hard
/// zero-blur shadows, a 16-colour hand-tuned palette, a pixel font stack (Silkscreen, VT323). Motion is left as the app configures it (the stock springs still run; stepped motion needs Core support). It writes final
/// values on the ArcDesign identity (and a few control identities) and never an Algorithm on them; the dark variant only
/// adds the global Dark algorithm, like every Arc dark config.
/// </summary>
public static class ArcPixelTheme
{
    /// <summary>Theme id used for <see cref="ThemeRequest"/>. Starts with <c>Arc</c>; the default theme stays DaybreakBlue.</summary>
    public const string LightThemeId = "ArcPixel";

    /// <summary>Id of the compiled dark variant.</summary>
    public const string DarkThemeId = "ArcPixelDark";

    /// <summary>The catalog of Arc control token identities.</summary>
    private const string Catalog = "TKCode.Arc";

    /// <summary>
    /// Pixel body and mono font stack: VT323 from <c>TKCode.Arc.Fonts</c> (register it with <c>WithArcPixelFonts()</c>), then
    /// monospaced system fonts so the look degrades to blocky text when the collection is not registered. The stack ends
    /// in the default font, which also covers CJK text the pixel fonts do not have.
    /// </summary>
    public const string PixelFontStack = "fonts:ArcPixel#VT323, Menlo, Cascadia Mono, Consolas, DejaVu Sans Mono, Courier New, $Default";

    /// <summary>Pixel display font stack: Silkscreen (caps-style bitmap face) for headings, then <see cref="PixelFontStack"/>.</summary>
    public const string PixelDisplayFontStack = "fonts:ArcPixel#Silkscreen, " + PixelFontStack;

    /// <summary>Creates a theme config. <paramref name="dark"/> selects the hand-tuned dark palette.</summary>
    /// <param name="dark">True for the dark palette.</param>
    /// <param name="scope">The control packages the app registers; naming a package that is not registered fails the whole theme change.</param>
    public static ThemeConfig CreateConfig(bool dark, ArcPixelScope scope = ArcPixelScope.Controls)
    {
        var builder = new ThemeConfigBuilder();
        if (dark)
        {
            builder = builder.WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark);
        }
        return builder.WithArcPixel(dark, scope).Build();
    }

    /// <summary>Creates the request to hand to <c>IThemeManager.ApplyThemeAsync</c>.</summary>
    public static ThemeRequest CreateRequest(bool dark, ThemeTransitionReason reason = ThemeTransitionReason.UserRequest,
        ArcPixelScope scope = ArcPixelScope.Controls)
        => new(dark ? DarkThemeId : LightThemeId, CreateConfig(dark, scope), reason);

    /// <summary>Adds the pixel overrides to an existing builder (the caller sets the algorithms).</summary>
    public static ThemeConfigBuilder WithArcPixel(this ThemeConfigBuilder builder, bool dark, ArcPixelScope scope = ArcPixelScope.Controls)
    {
        var p = dark ? Dark : Light;
        builder = builder.WithArc(arc =>
        {
            foreach (var (name, value) in Shared)
            {
                arc.Token(name, value);
            }
            foreach (var (name, value) in p)
            {
                arc.Token(name, value);
            }
        });

        // WithControl replaces the whole config of an identity, so the tokens of each control are merged first.
        var controls = new Dictionary<(string Catalog, string Id), Dictionary<string, string>>();
        void Add(string catalog, string id, string name, string value)
        {
            if (!controls.TryGetValue((catalog, id), out var tokens))
            {
                tokens = new Dictionary<string, string>(StringComparer.Ordinal);
                controls[(catalog, id)] = tokens;
            }
            tokens[name] = value;
        }

        void AddRadii(ArcPixelScope flag, string catalog, (string Control, string[] Tokens)[] table)
        {
            if (!scope.HasFlag(flag))
            {
                return;
            }
            foreach (var (control, names) in table)
            {
                foreach (var name in names)
                {
                    Add(catalog, control, name, "0");
                }
            }
        }

        AddRadii(ArcPixelScope.Controls, Catalog, PixelRadiusTable.Controls);
        AddRadii(ArcPixelScope.Blocks, "TKCode.Arc.Blocks", PixelRadiusTable.Blocks);
        AddRadii(ArcPixelScope.Special, "TKCode.Arc.Special", PixelRadiusTable.Special);
        AddRadii(ArcPixelScope.AdvancedCharts, "TKCode.Arc.AdvancedCharts", PixelRadiusTable.AdvancedCharts);
        AddRadii(ArcPixelScope.Workflow, "TKCode.Arc.Workflow", PixelRadiusTable.Workflow);
        if (scope.HasFlag(ArcPixelScope.Controls))
        {
            foreach (var (id, tokens) in DerivedColors(dark ? DarkRoles : LightRoles))
            {
                foreach (var (name, value) in tokens)
                {
                    Add(Catalog, id, name, value);
                }
            }
        }

        foreach (var ((catalog, id), tokens) in controls)
        {
            var control = new ControlThemeConfigBuilder();
            foreach (var (name, value) in tokens)
            {
                control.WithToken(name, value);
            }
            builder = builder.WithControl(new ControlTokenIdentity(catalog, id), control.Build());
        }
        return builder;
    }

    // Palette: Sweetie-16 style, ink #1A1C2C, paper #F4F4F4, grays #94B0C2 #566C86 #333C57.
    private static readonly (string, string)[] Shared =
    [
        (nameof(ArcDesignTokenKind.RadiusControl), "0"),
        (nameof(ArcDesignTokenKind.RadiusPanel), "0"),
        (nameof(ArcDesignTokenKind.RadiusSurface), "0"),
        (nameof(ArcDesignTokenKind.RadiusPill), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep1), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep1p25), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep1p5), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep2), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep3), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep4), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep5), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep6), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep7), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep8), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep9), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep10), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep12), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep13), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep14), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep14p5), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep15), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep16), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep17), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep18), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep19), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep19Top), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep20), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep20Top), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep21), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep22), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep24), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep25), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep28), "0"),
        (nameof(ArcDesignTokenKind.RadiusStep33), "0"),
        (nameof(ArcDesignTokenKind.DisplayFontFamily), PixelDisplayFontStack),
        (nameof(ArcDesignTokenKind.BodyFontFamily), PixelFontStack),
        (nameof(ArcDesignTokenKind.MonoFontFamily), PixelFontStack),
        (nameof(ArcDesignTokenKind.WeightMedium), "Normal"),
        (nameof(ArcDesignTokenKind.TrackingDisplay), "0"),
        (nameof(ArcDesignTokenKind.TrackingBody), "0"),
        (nameof(ArcDesignTokenKind.EdgeThickness), "2"),
        (nameof(ArcDesignTokenKind.FocusEdgeThickness), "3"),
        (nameof(ArcDesignTokenKind.EdgeThicknessTop), "0,2,0,0"),
        (nameof(ArcDesignTokenKind.EdgeThicknessBottom), "0,0,0,2"),
        (nameof(ArcDesignTokenKind.EdgeThicknessLeft), "2,0,0,0"),
        (nameof(ArcDesignTokenKind.EdgeThicknessRight), "0,0,2,0"),
        (nameof(ArcDesignTokenKind.EdgeThicknessRightBottom), "0,0,2,2"),
        (nameof(ArcDesignTokenKind.EdgeThicknessNoBottom), "2,2,2,0"),
        (nameof(ArcDesignTokenKind.EdgeThicknessNoRight), "2,2,0,2"),
        (nameof(ArcDesignTokenKind.EdgeThicknessNoLeft), "0,2,2,2"),
        (nameof(ArcDesignTokenKind.FocusRingWidth), "2"),
        (nameof(ArcDesignTokenKind.FocusRingOffset), "2"),
        (nameof(ArcDesignTokenKind.IconStrokeWidth), "2"),
        (nameof(ArcDesignTokenKind.BlurSubtle), "0"),
        (nameof(ArcDesignTokenKind.BlurSoft), "0"),
        (nameof(ArcDesignTokenKind.BlurText), "0"),
        (nameof(ArcDesignTokenKind.BackdropBlurDialog), "0"),
        (nameof(ArcDesignTokenKind.BackdropBlurDrawer), "0"),
        (nameof(ArcDesignTokenKind.ReduceTransparency), "True"),
    ];

    private static readonly (string, string)[] Light =
    [
        (nameof(ArcDesignTokenKind.Background), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.Surface), "#FFFFFF"),
        (nameof(ArcDesignTokenKind.SurfaceRaised), "#FFFFFF"),
        (nameof(ArcDesignTokenKind.SurfaceMuted), "#DCE3E8"),
        (nameof(ArcDesignTokenKind.Scrim), "#991A1C2C"),
        (nameof(ArcDesignTokenKind.Foreground), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.TextSecondary), "#333C57"),
        (nameof(ArcDesignTokenKind.TextMuted), "#566C86"),
        (nameof(ArcDesignTokenKind.Border), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.BorderSubtle), "#94B0C2"),
        (nameof(ArcDesignTokenKind.BorderStrong), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.Accent), "#3B5DC9"),
        (nameof(ArcDesignTokenKind.AccentStrong), "#29366F"),
        (nameof(ArcDesignTokenKind.AccentSubtle), "#D3DEF8"),
        (nameof(ArcDesignTokenKind.AccentForeground), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.ControlOn), "#3B5DC9"),
        (nameof(ArcDesignTokenKind.ControlOnSubtle), "#1F3B5DC9"),
        (nameof(ArcDesignTokenKind.ControlGlyph), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.ControlTrack), "#94B0C2"),
        (nameof(ArcDesignTokenKind.ControlTrackHover), "#566C86"),
        (nameof(ArcDesignTokenKind.ControlThumb), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.ControlThumbOn), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.ControlFill), "#3B5DC9"),
        (nameof(ArcDesignTokenKind.Success), "#257179"),
        (nameof(ArcDesignTokenKind.Warning), "#B8601F"),
        (nameof(ArcDesignTokenKind.Danger), "#B13E53"),
        (nameof(ArcDesignTokenKind.SuccessSubtle), "#CDEBDC"),
        (nameof(ArcDesignTokenKind.WarningSubtle), "#FFE6B8"),
        (nameof(ArcDesignTokenKind.DangerSubtle), "#F3CFD6"),
        (nameof(ArcDesignTokenKind.Series1), "#3B5DC9"),
        (nameof(ArcDesignTokenKind.Series2), "#38B764"),
        (nameof(ArcDesignTokenKind.Series3), "#EF7D57"),
        (nameof(ArcDesignTokenKind.Series4), "#B13E53"),
        (nameof(ArcDesignTokenKind.FocusRing), "#EF7D57"),
        (nameof(ArcDesignTokenKind.ControlThumbShadow), "0 0 0 1 #1A1C2C"),
        (nameof(ArcDesignTokenKind.ShadowResting), "2 2 0 0 #1A1C2C"),
        (nameof(ArcDesignTokenKind.ShadowRaised), "4 4 0 0 #1A1C2C"),
        (nameof(ArcDesignTokenKind.ShadowFloating), "6 6 0 0 #1A1C2C"),
    ];

    private static readonly (string, string)[] Dark =
    [
        (nameof(ArcDesignTokenKind.Background), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.Surface), "#262B44"),
        (nameof(ArcDesignTokenKind.SurfaceRaised), "#333C57"),
        (nameof(ArcDesignTokenKind.SurfaceMuted), "#29315A"),
        (nameof(ArcDesignTokenKind.Scrim), "#CC000000"),
        (nameof(ArcDesignTokenKind.Foreground), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.TextSecondary), "#C5D3DD"),
        (nameof(ArcDesignTokenKind.TextMuted), "#94B0C2"),
        (nameof(ArcDesignTokenKind.Border), "#94B0C2"),
        (nameof(ArcDesignTokenKind.BorderSubtle), "#566C86"),
        (nameof(ArcDesignTokenKind.BorderStrong), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.Accent), "#73EFF7"),
        (nameof(ArcDesignTokenKind.AccentStrong), "#41A6F6"),
        (nameof(ArcDesignTokenKind.AccentSubtle), "#1F4A6B"),
        (nameof(ArcDesignTokenKind.AccentForeground), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.ControlOn), "#73EFF7"),
        (nameof(ArcDesignTokenKind.ControlOnSubtle), "#2673EFF7"),
        (nameof(ArcDesignTokenKind.ControlGlyph), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.ControlTrack), "#566C86"),
        (nameof(ArcDesignTokenKind.ControlTrackHover), "#94B0C2"),
        (nameof(ArcDesignTokenKind.ControlThumb), "#F4F4F4"),
        (nameof(ArcDesignTokenKind.ControlThumbOn), "#1A1C2C"),
        (nameof(ArcDesignTokenKind.ControlFill), "#73EFF7"),
        (nameof(ArcDesignTokenKind.Success), "#A7F070"),
        (nameof(ArcDesignTokenKind.Warning), "#FFCD75"),
        (nameof(ArcDesignTokenKind.Danger), "#EF7D57"),
        (nameof(ArcDesignTokenKind.SuccessSubtle), "#27503A"),
        (nameof(ArcDesignTokenKind.WarningSubtle), "#5A4A2A"),
        (nameof(ArcDesignTokenKind.DangerSubtle), "#5A2E3A"),
        (nameof(ArcDesignTokenKind.Series1), "#41A6F6"),
        (nameof(ArcDesignTokenKind.Series2), "#A7F070"),
        (nameof(ArcDesignTokenKind.Series3), "#FFCD75"),
        (nameof(ArcDesignTokenKind.Series4), "#EF7D57"),
        (nameof(ArcDesignTokenKind.FocusRing), "#FFCD75"),
        (nameof(ArcDesignTokenKind.ControlThumbShadow), "0 0 0 1 #000000"),
        (nameof(ArcDesignTokenKind.ShadowResting), "2 2 0 0 #000000"),
        (nameof(ArcDesignTokenKind.ShadowRaised), "4 4 0 0 #000000"),
        (nameof(ArcDesignTokenKind.ShadowFloating), "6 6 0 0 #000000"),
    ];

    // Derived colors of control tokens do not follow the ArcDesign overrides (they mix the fixed Arc neutrals), so the most
    // visible controls get hand-mixed solid values here. The mixes use the pixel palette; nothing is translucent but scrims.
    private sealed record Roles(Color Fg, Color Bg, Color Surface, Color Raised, Color Muted, Color Border, Color BorderSubtle,
        Color TextMuted, Color Accent, Color Danger, Color Success, Color Warning, Color Scrim);

    private static readonly Roles LightRoles = new(
        C("#1A1C2C"), C("#F4F4F4"), C("#FFFFFF"), C("#FFFFFF"), C("#DCE3E8"), C("#1A1C2C"), C("#94B0C2"),
        C("#566C86"), C("#3B5DC9"), C("#B13E53"), C("#257179"), C("#B8601F"), C("#991A1C2C"));

    private static readonly Roles DarkRoles = new(
        C("#F4F4F4"), C("#1A1C2C"), C("#262B44"), C("#333C57"), C("#29315A"), C("#94B0C2"), C("#566C86"),
        C("#94B0C2"), C("#73EFF7"), C("#EF7D57"), C("#A7F070"), C("#FFCD75"), C("#CC000000"));

    private static Color C(string value) => Color.Parse(value);

    /// <summary>Solid mix: <paramref name="amount"/> of <paramref name="b"/> over <paramref name="a"/> (sRGB, opaque).</summary>
    private static Color Mix(Color a, Color b, double amount)
        => Color.FromRgb(Lerp(a.R, b.R, amount), Lerp(a.G, b.G, amount), Lerp(a.B, b.B, amount));

    private static byte Lerp(byte a, byte b, double amount) => (byte)Math.Round(a + (b - a) * amount);

    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static (string, string)[] T(params (string, Color)[] tokens) => tokens.Select(t => (t.Item1, Hex(t.Item2))).ToArray();

    private static (string, (string, string)[])[] DerivedColors(Roles r)
    {
        var danger = Mix(r.Surface, r.Danger, 0.18);
        var success = Mix(r.Surface, r.Success, 0.18);
        var warning = Mix(r.Surface, r.Warning, 0.18);
        var press = Mix(r.Surface, r.Fg, 0.16);
        var solidHighlight = Mix(r.Fg, r.Bg, 0.20);
        var solidPressed = Mix(r.Fg, r.Bg, 0.35);
        var solidDivider = Mix(r.Fg, r.Bg, 0.30);
        return
        [
            ("Button", T(("DangerDisabledForeground", Mix(r.Muted, r.Danger, 0.55)), ("GhostRaisedHoverBackground", Mix(r.Raised, r.Fg, 0.10)))),
            ("SplitButton", T(("PrimaryHoverColor", solidHighlight), ("PrimaryPressedColor", solidPressed), ("PrimaryDividerColor", solidDivider),
                ("SecondaryPressedColor", Mix(r.Muted, r.Border, 0.30)))),
            ("Badge", T(("SuccessBackground", success), ("SuccessBorder", r.Success), ("WarningBackground", warning), ("WarningBorder", r.Warning),
                ("DangerBackground", danger), ("DangerBorder", r.Danger), ("NeutralHoverBackground", r.Muted), ("NeutralHoverBorder", r.Border),
                ("SuccessHoverBackground", Mix(r.Surface, r.Success, 0.30)), ("SuccessHoverBorder", r.Success),
                ("WarningHoverBackground", Mix(r.Surface, r.Warning, 0.30)), ("WarningHoverBorder", r.Warning),
                ("DangerHoverBackground", Mix(r.Surface, r.Danger, 0.30)), ("DangerHoverBorder", r.Danger))),
            ("BillingToggle", T(("BadgeBackground", Mix(r.Surface, r.Accent, 0.25)))),
            ("BottomSheet", T(("ScrimColor", r.Scrim))),
            ("Card", T(("ScrimColor", r.Scrim), ("CloseButtonBackground", r.Raised), ("CloseButtonBorder", r.Border))),
            ("Dialog", T(("ScrimColor", r.Scrim))),
            ("Drawer", T(("ScrimColor", r.Scrim))),
            ("DialogSurface", T(("CloseButtonBackground", r.Raised))),
            ("DrawerSurface", T(("CloseButtonBackground", r.Raised))),
            ("ButtonGroup", T(("OutlinePressedHighlightColor", press), ("SolidDividerColor", solidDivider), ("SolidHighlightColor", solidHighlight),
                ("SolidMutedInk", Mix(r.Fg, r.Bg, 0.70)), ("SolidPressedHighlightColor", solidPressed))),
            ("ButtonGroupItem", T(("SolidDividerColor", solidDivider))),
            ("ConfirmMorph", T(("TriggerHoverColor", Mix(r.Surface, r.Fg, 0.10)), ("TriggerPressColor", press), ("TriggerFocusColor", Mix(r.Surface, r.Fg, 0.12)),
                ("AnswerHoverColor", Mix(r.Surface, r.Fg, 0.12)), ("AnswerPressColor", Mix(r.Surface, r.Fg, 0.20)), ("AnswerFocusColor", Mix(r.Surface, r.Fg, 0.14)),
                ("ConfirmRestColor", danger), ("ConfirmHoverColor", Mix(r.Surface, r.Danger, 0.28)), ("ConfirmPressColor", Mix(r.Surface, r.Danger, 0.38)),
                ("ConfirmFocusColor", Mix(r.Surface, r.Danger, 0.32)))),
            ("Countdown", T(("CompleteBackground", success), ("CompleteBorder", r.Success))),
            ("ExpandingButtonGroupItem", T(("DangerExpandedColor", danger), ("DoneColor", success))),
            ("FloatingButtonGroup", T(("TrayColor", r.Muted), ("TrayEdgeColor", r.Border), ("HighlightColor", press), ("HighlightPressColor", Mix(r.Surface, r.Fg, 0.26)))),
            ("FloatingButtonGroupSeparator", T(("RuleColor", r.Border))),
            ("MetricCard", T(("UpFill", success), ("DownFill", danger))),
            ("Skeleton", T(("BlockColor", Mix(r.Surface, r.Fg, 0.12)))),
            ("Timeline", T(("MarkerRingColor", r.Border))),
            ("Tooltip", T(("BubbleBorderColor", r.Border))),
            ("Slider", T(("BubbleBorderColor", r.Border), ("TickColor", r.TextMuted))),
            ("Toast", T(("SurfaceColor", r.Raised), ("IconFill", r.Muted), ("IconRing", r.Border))),
            ("UsageMeter", T(("TrackColor", r.Muted), ("NearBadgeBackground", warning), ("OverBadgeBackground", danger))),
            ("Stepper", T(("ErrorDisc", r.Danger), ("ErrorDiscHover", Mix(r.Danger, r.Fg, 0.20)), ("ErrorHalo", danger))),
            ("FieldTextBox", T(("SelectionFill", Color.FromArgb(0x66, r.Accent.R, r.Accent.G, r.Accent.B)))),
            ("MenuSurface", T(("AccountHighlight", r.Muted), ("DangerHighlight", danger))),
            ("MenuEntry", T(("AccountSeparatorColor", r.BorderSubtle), ("PressedTint", press))),
            ("MenuEntrySeparator", T(("AccountLineColor", r.BorderSubtle))),
            ("DropdownMenu", T(("TriggerPressedBackground", press))),
            ("UserMenu", T(("TriggerPressedBackground", press))),
        ];
    }
}

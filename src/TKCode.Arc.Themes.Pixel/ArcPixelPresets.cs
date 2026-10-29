using Avalonia.Media;
using TKCode.Arc.Themes.Corners;

namespace TKCode.Arc.Themes.Pixel;

/// <summary>
/// The Arc pixel style as a theme preset (design/architecture/theming/pixel-theme.md): square corners, hard zero-blur
/// shadows, a 16-colour palette for light and one for dark, pixel fonts, 2px edges. Token overrides only; motion is left as
/// the app configures it.
/// </summary>
public static class ArcPixelPresets
{
    /// <summary>The preset name.</summary>
    public const string Name = "ArcPixel";

    /// <summary>
    /// The slot of style presets: a state holds one style. Corner styles have their own slot
    /// (<see cref="ArcCornerPresets.Slot"/>), so a corner style added after Pixel changes Pixel's (square) corners.
    /// </summary>
    public const string Slot = "ArcStyle";

    /// <summary>Pixel body and mono font stack (register the fonts with <c>WithArcPixelFonts()</c>).</summary>
    public const string PixelFontStack = "fonts:ArcPixel#VT323, Menlo, Cascadia Mono, Consolas, DejaVu Sans Mono, Courier New, $Default";

    /// <summary>Pixel display font stack: Silkscreen for headings, then <see cref="PixelFontStack"/>.</summary>
    public const string PixelDisplayFontStack = "fonts:ArcPixel#Silkscreen, " + PixelFontStack;

    /// <summary>
    /// The pixel style, light and dark in one preset (the appearance of the state picks the palette). Overrides of packages
    /// the app did not enable are skipped, so there is no scope parameter.
    /// </summary>
    public static ArcThemePreset Pixel => s_pixel.Value;

    // Lazy: the tables below are static fields initialized after this one.
    private static readonly Lazy<ArcThemePreset> s_pixel = new(() => ArcThemePreset.Create(Name, Define, Slot));

    /// <summary>Makes the preset discoverable through <c>ArcRuntime.Presets</c> (called by the package's <c>UseArcPixelTheme()</c>).</summary>
    /// <param name="package">The package being defined.</param>
    internal static void Register(ArcPackageBuilder package) => package.AddPresets([Pixel]);

    private static void Define(ArcThemePresetBuilder preset)
    {
        // Order: shared, appearance palette, square corners, derived control colors (as the 0.3 pixel theme applied them).
        Add(preset, Shared);
        preset.ForLight(light => Add(light, Light));
        preset.ForDark(dark => Add(dark, Dark));
        preset.Include(ArcCornerPresets.Square);
        preset.ForLight(light => AddDerived(light, LightRoles));
        preset.ForDark(dark => AddDerived(dark, DarkRoles));
    }

    private static void Add(ArcThemePresetBuilder preset, (string, string)[] tokens)
    {
        foreach (var (name, value) in tokens)
        {
            preset.Token(name, value);
        }
    }

    private static void AddDerived(ArcThemePresetBuilder preset, Roles roles)
    {
        foreach (var (identity, tokens) in DerivedColors(roles))
        {
            foreach (var (name, value) in tokens)
            {
                preset.Token(identity, name, value);
            }
        }
    }

    // Palette: Sweetie-16 style, ink #1A1C2C, paper #F4F4F4, grays #94B0C2 #566C86 #333C57.
    private static readonly (string, string)[] Shared =
    [
        ("DisplayFontFamily", PixelDisplayFontStack),
        ("BodyFontFamily", PixelFontStack),
        ("MonoFontFamily", PixelFontStack),
        ("WeightMedium", "Normal"),
        ("TrackingDisplay", "0"),
        ("TrackingBody", "0"),
        ("EdgeThickness", "2"),
        ("FocusEdgeThickness", "3"),
        ("EdgeThicknessTop", "0,2,0,0"),
        ("EdgeThicknessBottom", "0,0,0,2"),
        ("EdgeThicknessLeft", "2,0,0,0"),
        ("EdgeThicknessRight", "0,0,2,0"),
        ("EdgeThicknessRightBottom", "0,0,2,2"),
        ("EdgeThicknessNoBottom", "2,2,2,0"),
        ("EdgeThicknessNoRight", "2,2,0,2"),
        ("EdgeThicknessNoLeft", "0,2,2,2"),
        ("FocusRingWidth", "2"),
        ("FocusRingOffset", "2"),
        ("IconStrokeWidth", "2"),
        ("BlurSubtle", "0"),
        ("BlurSoft", "0"),
        ("BlurText", "0"),
        ("BackdropBlurDialog", "0"),
        ("BackdropBlurDrawer", "0"),
        ("ReduceTransparency", "True"),
    ];
    private static readonly (string, string)[] Light =
    [
        ("Background", "#F4F4F4"),
        ("Surface", "#FFFFFF"),
        ("SurfaceRaised", "#FFFFFF"),
        ("SurfaceMuted", "#DCE3E8"),
        ("Scrim", "#991A1C2C"),
        ("Foreground", "#1A1C2C"),
        ("TextSecondary", "#333C57"),
        ("TextMuted", "#566C86"),
        ("Border", "#1A1C2C"),
        ("BorderSubtle", "#94B0C2"),
        ("BorderStrong", "#1A1C2C"),
        ("Accent", "#3B5DC9"),
        ("AccentStrong", "#29366F"),
        ("AccentSubtle", "#D3DEF8"),
        ("AccentForeground", "#F4F4F4"),
        ("ControlOn", "#3B5DC9"),
        ("ControlOnSubtle", "#1F3B5DC9"),
        ("ControlGlyph", "#F4F4F4"),
        ("ControlTrack", "#94B0C2"),
        ("ControlTrackHover", "#566C86"),
        ("ControlThumb", "#F4F4F4"),
        ("ControlThumbOn", "#F4F4F4"),
        ("ControlFill", "#3B5DC9"),
        ("Success", "#257179"),
        ("Warning", "#B8601F"),
        ("Danger", "#B13E53"),
        ("SuccessSubtle", "#CDEBDC"),
        ("WarningSubtle", "#FFE6B8"),
        ("DangerSubtle", "#F3CFD6"),
        ("Series1", "#3B5DC9"),
        ("Series2", "#38B764"),
        ("Series3", "#EF7D57"),
        ("Series4", "#B13E53"),
        ("FocusRing", "#EF7D57"),
        ("ControlThumbShadow", "0 0 0 1 #1A1C2C"),
        ("ShadowResting", "2 2 0 0 #1A1C2C"),
        ("ShadowRaised", "4 4 0 0 #1A1C2C"),
        ("ShadowFloating", "6 6 0 0 #1A1C2C"),
    ];
    private static readonly (string, string)[] Dark =
    [
        ("Background", "#1A1C2C"),
        ("Surface", "#262B44"),
        ("SurfaceRaised", "#333C57"),
        ("SurfaceMuted", "#29315A"),
        ("Scrim", "#CC000000"),
        ("Foreground", "#F4F4F4"),
        ("TextSecondary", "#C5D3DD"),
        ("TextMuted", "#94B0C2"),
        ("Border", "#94B0C2"),
        ("BorderSubtle", "#566C86"),
        ("BorderStrong", "#F4F4F4"),
        ("Accent", "#73EFF7"),
        ("AccentStrong", "#41A6F6"),
        ("AccentSubtle", "#1F4A6B"),
        ("AccentForeground", "#1A1C2C"),
        ("ControlOn", "#73EFF7"),
        ("ControlOnSubtle", "#2673EFF7"),
        ("ControlGlyph", "#1A1C2C"),
        ("ControlTrack", "#566C86"),
        ("ControlTrackHover", "#94B0C2"),
        ("ControlThumb", "#F4F4F4"),
        ("ControlThumbOn", "#1A1C2C"),
        ("ControlFill", "#73EFF7"),
        ("Success", "#A7F070"),
        ("Warning", "#FFCD75"),
        ("Danger", "#EF7D57"),
        ("SuccessSubtle", "#27503A"),
        ("WarningSubtle", "#5A4A2A"),
        ("DangerSubtle", "#5A2E3A"),
        ("Series1", "#41A6F6"),
        ("Series2", "#A7F070"),
        ("Series3", "#FFCD75"),
        ("Series4", "#EF7D57"),
        ("FocusRing", "#FFCD75"),
        ("ControlThumbShadow", "0 0 0 1 #000000"),
        ("ShadowResting", "2 2 0 0 #000000"),
        ("ShadowRaised", "4 4 0 0 #000000"),
        ("ShadowFloating", "6 6 0 0 #000000"),
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

using AtomUI.Theme.Algorithms;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>The eight accent presets shipped by Arc.</summary>
public enum ArcAccentPreset
{
    Neutral,
    Violet,
    Blue,
    Green,
    Amber,
    Orange,
    Coral,
    Rose
}

/// <summary>Accent-dependent colors resolved for one appearance.</summary>
public readonly record struct ArcAccentColors(
    Color Accent,
    Color AccentStrong,
    Color AccentSubtle,
    Color AccentForeground,
    Color ControlOn,
    Color ControlThumbOn,
    Color ControlGlyph,
    Color ControlFill,
    Color Series1,
    Color Series2,
    Color Series3,
    Color Series4,
    string Preset);

/// <summary>
/// Resolves Arc accent colors from an accent seed (AtomUI ColorPrimary). Exact preset seeds use Arc's
/// hand-tuned tables; seeds produced by AtomUI's dark algorithm map back to their preset; AtomUI's default
/// primary maps to Arc's default (neutral); anything else is derived in OKLCH.
/// </summary>
public static class ArcPalette
{
    private readonly record struct PresetRow(
        string Accent, string Strong, string Subtle, string Foreground,
        string ControlOn, string ThumbOn, string Glyph, string Fill);

    private sealed record Preset(ArcAccentPreset Kind, string Seed, PresetRow Light, PresetRow Dark);

    private static readonly Preset[] s_presets =
    [
        new(ArcAccentPreset.Neutral, "#353535",
            new("#353535", "#1F1F1F", "#1A353535", "#FFFFFF", "#1D1D1D", "#FFFFFF", "#FFFFFF", "#1D1D1D"),
            new("#BEBEBE", "#E1E1E1", "#21BEBEBE", "#141414", "#E8E8E8", "#141414", "#141414", "#868686")),
        new(ArcAccentPreset.Violet, "#7747FF",
            new("#7747FF", "#5528CE", "#217747FF", "#FFFFFF", "#7747FF", "#FFFFFF", "#FFFFFF", "#7747FF"),
            new("#A78BFF", "#C7B7FF", "#387747FF", "#141414", "#8A66FF", "#FFFFFF", "#FFFFFF", "#8A66FF")),
        new(ArcAccentPreset.Blue, "#0562EF",
            new("#0562EF", "#074DB7", "#1F0562EF", "#FFFFFF", "#0562EF", "#FFFFFF", "#FFFFFF", "#0562EF"),
            new("#6BA3FF", "#A4C7FF", "#380562EF", "#141414", "#3B82F6", "#FFFFFF", "#FFFFFF", "#3B82F6")),
        new(ArcAccentPreset.Green, "#0DB879",
            new("#0DB879", "#087C54", "#210DB879", "#102016", "#10A870", "#FFFFFF", "#FFFFFF", "#10A870"),
            new("#53DCA6", "#93EFD0", "#330DB879", "#102016", "#1FAE78", "#FFFFFF", "#FFFFFF", "#1FAE78")),
        new(ArcAccentPreset.Amber, "#F3AD20",
            new("#F3AD20", "#9C6300", "#29F3AD20", "#20190D", "#EDA11A", "#FFFFFF", "#2A1C03", "#EDA11A"),
            new("#FFCB62", "#FFE0A1", "#33F3AD20", "#20190D", "#E8A52A", "#FFFFFF", "#241803", "#E8A52A")),
        new(ArcAccentPreset.Orange, "#F48120",
            new("#F48120", "#AA4700", "#21F48120", "#17130F", "#F07818", "#FFFFFF", "#FFFFFF", "#F07818"),
            new("#FFAD5E", "#FFD0A0", "#33F48120", "#17130F", "#EF873A", "#FFFFFF", "#FFFFFF", "#EF873A")),
        new(ArcAccentPreset.Coral, "#F15F55",
            new("#F15F55", "#B92C27", "#24F15F55", "#21110F", "#EC564B", "#FFFFFF", "#FFFFFF", "#EC564B"),
            new("#FF9386", "#FFC1BA", "#33F15F55", "#21110F", "#EC6A60", "#FFFFFF", "#FFFFFF", "#EC6A60")),
        new(ArcAccentPreset.Rose, "#ED4E9D",
            new("#ED4E9D", "#AE2570", "#24ED4E9D", "#250F1B", "#E64494", "#FFFFFF", "#FFFFFF", "#E64494"),
            new("#FF8BC5", "#FFC0DF", "#33ED4E9D", "#250F1B", "#E457A0", "#FFFFFF", "#FFFFFF", "#E457A0"))
    ];

    // Series colors for the neutral accent come from Arc's explicit CSS fallback values.
    private static readonly Color[] s_neutralSeriesLight = [Color.Parse("#1F7DCF"), Color.Parse("#7128A5"), Color.Parse("#E58F00"), Color.Parse("#009E84")];
    private static readonly Color[] s_neutralSeriesDark = [Color.Parse("#55ADFF"), Color.Parse("#9C37BE"), Color.Parse("#E66E00"), Color.Parse("#00A861")];

    private static readonly Color AtomUIDefaultPrimary = Color.Parse("#1677FF");

    private static readonly Lazy<Dictionary<uint, Preset>> s_darkDerived = new(BuildDarkDerivedLookup);

    /// <summary>Seed color of a preset (equal to its light accent).</summary>
    public static Color GetSeed(ArcAccentPreset preset) => Color.Parse(s_presets[(int)preset].Seed);

    /// <summary>Resolves the accent colors for <paramref name="seed"/> in the given appearance.</summary>
    public static ArcAccentColors Resolve(Color seed, bool isDark)
    {
        var preset = MatchPreset(seed);
        if (preset is not null)
        {
            return FromPreset(preset, isDark);
        }
        return Derive(Color.FromRgb(seed.R, seed.G, seed.B), isDark);
    }

    private static Preset? MatchPreset(Color seed)
    {
        var key = Key(seed);
        foreach (var preset in s_presets)
        {
            if (Key(Color.Parse(preset.Seed)) == key)
            {
                return preset;
            }
        }
        if (key == Key(AtomUIDefaultPrimary))
        {
            return s_presets[0];
        }
        return s_darkDerived.Value.TryGetValue(key, out var derived) ? derived : null;
    }

    private static Dictionary<uint, Preset> BuildDarkDerivedLookup()
    {
        var map = new Dictionary<uint, Preset>();
        var option = new PaletteGenerateOption { ThemeVariant = ThemeVariant.Dark };
        foreach (var preset in s_presets)
        {
            var derived = PaletteGenerator.GeneratePalette(Color.Parse(preset.Seed), option)[5];
            map.TryAdd(Key(derived), preset);
        }
        var defaultDerived = PaletteGenerator.GeneratePalette(AtomUIDefaultPrimary, option)[5];
        map.TryAdd(Key(defaultDerived), s_presets[0]);
        return map;
    }

    private static uint Key(Color c) => (uint)((c.R << 16) | (c.G << 8) | c.B);

    private static ArcAccentColors FromPreset(Preset preset, bool isDark)
    {
        var row = isDark ? preset.Dark : preset.Light;
        var accent = Color.Parse(row.Accent);
        var series = preset.Kind == ArcAccentPreset.Neutral
            ? (isDark ? s_neutralSeriesDark : s_neutralSeriesLight)
            : ComputeSeries(accent, isDark);
        return new ArcAccentColors(
            accent,
            Color.Parse(row.Strong),
            Color.Parse(row.Subtle),
            Color.Parse(row.Foreground),
            Color.Parse(row.ControlOn),
            Color.Parse(row.ThumbOn),
            Color.Parse(row.Glyph),
            Color.Parse(row.Fill),
            series[0], series[1], series[2], series[3],
            preset.Kind.ToString().ToLowerInvariant());
    }

    private static ArcAccentColors Derive(Color seed, bool isDark)
    {
        var (l, c, h) = Oklch.FromColor(seed);
        if (c <= 0.02)
        {
            // Low chroma seeds behave like neutral: lightness only, no hue.
            c = 0;
        }
        Color accent;
        Color strong;
        Color subtle;
        Color controlOn;
        if (!isDark)
        {
            accent = seed;
            strong = Oklch.ToColor(0.243 + 0.385 * l, 0.85 * c, h);
            subtle = Oklch.WithAlpha(seed, 0.13);
            controlOn = l > 0.6 ? Oklch.ToColor(l - 0.025, c, h) : seed;
        }
        else
        {
            var ld = 0.39 + 0.6 * l;
            var cd = 0.75 * c;
            accent = Oklch.ToColor(ld, cd, h);
            strong = Oklch.ToColor(Math.Min(ld + 0.09, 0.93), 0.6 * cd, h);
            subtle = Oklch.WithAlpha(seed, 0.21);
            controlOn = Oklch.ToColor(Math.Clamp(l, 0.62, 0.77), 0.88 * c, h);
        }
        var white = Colors.White;
        var foreground = Oklch.Contrast(white, accent) >= 4.5
            ? white
            : isDark ? Color.Parse("#141414") : Oklch.ToColor(0.2, Math.Min(c, 0.02), h);
        var controlOnL = Oklch.FromColor(controlOn).L;
        var glyph = controlOnL >= 0.75 ? Oklch.ToColor(0.2, Math.Min(c, 0.04), h) : white;
        var series = ComputeSeries(accent, isDark);
        return new ArcAccentColors(accent, strong, subtle, foreground, controlOn, white, glyph, controlOn,
            series[0], series[1], series[2], series[3], "custom");
    }

    /// <summary>Arc's chart series formula (CSS relative color syntax), evaluated from the accent of the appearance.</summary>
    public static Color[] ComputeSeries(Color accent, bool isDark)
    {
        var (l, c, h) = Oklch.FromColor(accent);
        var s = c - 0.02 > 0 ? 1 : 0;
        var bl = s == 1 ? l : (isDark ? 0.73 : 0.58);
        var bc = s == 1 ? c : 0.15;
        var bh = s == 1 ? h : 250;
        if (!isDark)
        {
            return
            [
                Oklch.ToColor(bl, bc, bh),
                Oklch.ToColor(Math.Clamp(bl - 0.25, 0.45, 0.76), 0.19, (bh + 57) % 360),
                Oklch.ToColor(Math.Clamp(bl + 0.14, 0.45, 0.76), 0.18, (bh + 184) % 360),
                Oklch.ToColor(Math.Clamp(bl, 0.45, 0.76), 0.21, (bh + 292) % 360)
            ];
        }
        return
        [
            Oklch.ToColor(bl, bc, bh),
            Oklch.ToColor(Math.Clamp(bl - 0.19, 0.485, 0.665), 0.21, (bh + 66) % 360),
            Oklch.ToColor(Math.Clamp(bl + 0.02, 0.485, 0.665), 0.21, (bh + 175) % 360),
            Oklch.ToColor(Math.Clamp(bl - 0.1, 0.485, 0.665), 0.18, (bh + 270) % 360)
        ];
    }

    /// <summary>Accent-independent neutral semantic colors for one appearance (shared with control tokens).</summary>
    public static ArcNeutralColors Neutrals(bool isDark) => isDark ? ArcNeutralColors.Dark : ArcNeutralColors.Light;
}

/// <summary>Accent-independent semantic colors. Control tokens may derive colors from these (never from accent).</summary>
public sealed record ArcNeutralColors(
    Color Background,
    Color Surface,
    Color SurfaceRaised,
    Color SurfaceMuted,
    Color Foreground,
    Color TextSecondary,
    Color TextMuted,
    Color Border,
    Color BorderSubtle,
    Color BorderStrong,
    Color ControlTrack,
    Color ControlTrackHover,
    Color ControlThumb,
    Color Success,
    Color Warning,
    Color Danger,
    Color Scrim)
{
    public static readonly ArcNeutralColors Light = new(
        Color.Parse("#FFFFFF"), Color.Parse("#FFFFFF"), Color.Parse("#FFFFFF"), Color.Parse("#F8F8F8"),
        Color.Parse("#0B0B0B"), Color.Parse("#585858"), Color.Parse("#7D7D7D"),
        Color.Parse("#E9E9E9"), Color.Parse("#F3F3F3"), Color.Parse("#C4C4C4"),
        Color.Parse("#DCDCDC"), Color.Parse("#D1D1D1"), Color.Parse("#FFFFFF"),
        Color.Parse("#007824"), Color.Parse("#AA6B00"), Color.Parse("#CD1526"),
        Color.Parse("#52000000"));

    public static readonly ArcNeutralColors Dark = new(
        Color.Parse("#141414"), Color.Parse("#191919"), Color.Parse("#1F1F1F"), Color.Parse("#242424"),
        Color.Parse("#F2F2F2"), Color.Parse("#B4B4B4"), Color.Parse("#8C8C8C"),
        Color.Parse("#2B2B2B"), Color.Parse("#222222"), Color.Parse("#484848"),
        Color.Parse("#353535"), Color.Parse("#404040"), Color.Parse("#F5F5F5"),
        Color.Parse("#4CD676"), Color.Parse("#FFB90F"), Color.Parse("#FF817A"),
        Color.Parse("#8C000000"));
}

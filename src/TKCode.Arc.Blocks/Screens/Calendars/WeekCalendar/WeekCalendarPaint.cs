using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>The brushes of one calendar in the current theme (from <see cref="WeekCalendarPalette"/>).</summary>
internal sealed class WeekCalendarBrushes
{
    public WeekCalendarBrushes(WeekCalendarColors colors, Color surface, Color border, bool dark)
    {
        Colors = colors;
        SelectedFill = new ImmutableSolidColorBrush(colors.SelectedFill);
        SelectedText = new ImmutableSolidColorBrush(colors.SelectedText);
        SelectedSubText = new ImmutableSolidColorBrush(Oklch.WithAlpha(colors.SelectedText, 0.78));
        // The draft's 1px outline (calendar at 60%), the origin of a drag (6% fill, 55% dashed outline) and the calendar
        // choice in the popovers (70% of the calendar into the border, 12% (dark 20%) into the surface).
        DraftOutline = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(colors.Bar, 0.60)), 1);
        OriginFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(colors.Bar, 0.06));
        OriginOutline = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(colors.Bar, 0.55)), 1,
            new ImmutableDashStyle(new double[] { 3, 3 }, 0));
        ChipFill = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, colors.Bar, dark ? 0.20 : 0.12));
        ChipBorder = new ImmutableSolidColorBrush(Oklch.MixOklab(border, colors.Bar, 0.70));
        Bar = new ImmutableSolidColorBrush(colors.Bar);
        Fill = new ImmutableSolidColorBrush(colors.Fill);
        HoverFill = new ImmutableSolidColorBrush(colors.HoverFill);
        Text = new ImmutableSolidColorBrush(colors.Text);
        SubText = new ImmutableSolidColorBrush(colors.SubText);
        PastFill = new ImmutableSolidColorBrush(colors.PastFill);
        PastBar = new ImmutableSolidColorBrush(colors.PastBar);
        PastText = new ImmutableSolidColorBrush(colors.PastText);
        PastSubText = new ImmutableSolidColorBrush(colors.PastSubText);
    }

    public WeekCalendarColors Colors { get; }

    public IBrush Bar { get; }

    public IBrush Fill { get; }

    public IBrush HoverFill { get; }

    public IBrush Text { get; }

    public IBrush SubText { get; }

    public IBrush PastFill { get; }

    public IBrush PastBar { get; }

    public IBrush PastText { get; }

    public IBrush PastSubText { get; }

    public IBrush SelectedFill { get; }

    public IBrush SelectedText { get; }

    public IBrush SelectedSubText { get; }

    public IPen DraftOutline { get; }

    public IBrush OriginFill { get; }

    public IPen OriginOutline { get; }

    public IBrush ChipFill { get; }

    public IBrush ChipBorder { get; }
}

/// <summary>
/// The theme as the week calendar's self-drawn parts need it: neutral brushes, typefaces and every calendar's brushes,
/// resolved lazily from the owner's ArcDesign tokens (only while it is attached; a detached owner gets the light neutrals for
/// one frame) and dropped when the theme or the calendars change. <see cref="Version"/> tells parts to drop their own caches.
/// </summary>
internal sealed class WeekCalendarPaint
{
    private readonly WeekCalendar _owner;
    private readonly Dictionary<string, WeekCalendarBrushes> _calendars = new(StringComparer.Ordinal);
    private WeekCalendarBrushes? _neutral;
    private bool _ready;

    public WeekCalendarPaint(WeekCalendar owner)
    {
        _owner = owner;
    }

    /// <summary>Changes every time the cached values are dropped.</summary>
    public int Version { get; private set; }

    public bool IsDark { get; private set; }

    public Color SurfaceColor { get; private set; }

    public IBrush Surface { get; private set; } = Brushes.White;

    public IBrush SurfaceMuted { get; private set; } = Brushes.WhiteSmoke;

    /// <summary>The 14 and 6 radius steps and the pill radius.</summary>
    public double Radius14 { get; private set; } = 14;
    public double Radius6 { get; private set; } = 6;
    public double RadiusPill { get; private set; } = 9999;

    public IBrush Foreground { get; private set; } = Brushes.Black;

    public IBrush TextSecondary { get; private set; } = Brushes.Gray;

    public IBrush TextMuted { get; private set; } = Brushes.Gray;

    public IBrush BorderSubtle { get; private set; } = Brushes.LightGray;

    public IBrush Border { get; private set; } = Brushes.LightGray;

    public IBrush HalfHourLine { get; private set; } = Brushes.LightGray;

    public IBrush WeekendShade { get; private set; } = Brushes.WhiteSmoke;

    public IBrush CellFocus { get; private set; } = Brushes.WhiteSmoke;

    public IBrush Ghost { get; private set; } = Brushes.WhiteSmoke;

    public IBrush Accent { get; private set; } = Brushes.Black;

    public IBrush AccentForeground { get; private set; } = Brushes.White;

    public IBrush NowRow { get; private set; } = Brushes.Gray;

    public IBrush HoverCircle { get; private set; } = Brushes.WhiteSmoke;

    public IBrush SurfaceRaised { get; private set; } = Brushes.White;

    public IPen FocusRing { get; private set; } = new ImmutablePen(Brushes.Black, 2);

    public IBrush Background { get; private set; } = Brushes.White;

    public IBrush Danger { get; private set; } = Brushes.Red;

    /// <summary>The raised and floating shadows (selected and dragged blocks, the draft).</summary>
    public BoxShadows ShadowRaised { get; private set; }

    public BoxShadows ShadowFloating { get; private set; }

    public Color SurfaceMutedColor { get; private set; }

    public FontFamily BodyFamily { get; private set; } = FontFamily.Default;

    public FontFamily DisplayFamily { get; private set; } = FontFamily.Default;

    public Typeface Body { get; private set; }

    public Typeface BodyMedium { get; private set; }

    public Typeface DisplayMedium { get; private set; }

    /// <summary>The generated avatars of attendees.</summary>
    public ProjectsAvatarPainter Avatars { get; } = new();

    /// <summary>Drops every cached value (theme, accent or calendars changed).</summary>
    public void Invalidate()
    {
        _ready = false;
        _calendars.Clear();
        _neutral = null;
        Avatars.Invalidate();
        Version++;
    }

    /// <summary>Resolves the theme values if they are not cached.</summary>
    public void EnsureReady()
    {
        if (_ready)
        {
            return;
        }
        _ready = true;
        var owner = _owner;
        var surface = Get(ArcDesignTokenKind.Surface, Colors.White);
        SurfaceColor = surface;
        IsDark = Oklch.FromColor(surface).L < 0.5;
        var neutrals = ArcPalette.Neutrals(IsDark);
        var muted = Get(ArcDesignTokenKind.SurfaceMuted, neutrals.SurfaceMuted);
        SurfaceMutedColor = muted;
        var foreground = Get(ArcDesignTokenKind.Foreground, neutrals.Foreground);
        var accent = Get(ArcDesignTokenKind.Accent, foreground);
        var subtle = Get(ArcDesignTokenKind.BorderSubtle, neutrals.BorderSubtle);
        Surface = new ImmutableSolidColorBrush(surface);
        SurfaceMuted = new ImmutableSolidColorBrush(muted);
        SurfaceRaised = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.SurfaceRaised, neutrals.SurfaceRaised));
        Foreground = new ImmutableSolidColorBrush(foreground);
        TextSecondary = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.TextSecondary, neutrals.TextSecondary));
        TextMuted = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.TextMuted, neutrals.TextMuted));
        BorderSubtle = new ImmutableSolidColorBrush(subtle);
        Border = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.Border, neutrals.Border));
        HalfHourLine = new ImmutableSolidColorBrush(Oklch.WithAlpha(subtle, IsDark ? 0.60 : 0.45));
        WeekendShade = new ImmutableSolidColorBrush(Oklch.WithAlpha(muted, 0.55));
        CellFocus = new ImmutableSolidColorBrush(Oklch.WithAlpha(muted, 0.80));
        Ghost = new ImmutableSolidColorBrush(Oklch.WithAlpha(muted, 0.80));
        Accent = new ImmutableSolidColorBrush(accent);
        AccentForeground = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.AccentForeground, Colors.White));
        NowRow = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, 0.35));
        HoverCircle = new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, 0.08));
        FocusRing = new ImmutablePen(new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.FocusRing, foreground)), 2);
        Background = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.Background, neutrals.Background));
        Danger = new ImmutableSolidColorBrush(Get(ArcDesignTokenKind.Danger, neutrals.Danger));
        try
        {
            BodyFamily = ArcTokens.Get<FontFamily>(owner, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
            DisplayFamily = ArcTokens.Get<FontFamily>(owner, ArcDesignTokenKind.DisplayFontFamily) ?? BodyFamily;
            ShadowRaised = ArcTokens.Get<BoxShadows>(owner, ArcDesignTokenKind.ShadowRaised);
            ShadowFloating = ArcTokens.Get<BoxShadows>(owner, ArcDesignTokenKind.ShadowFloating);
        }
        catch (Exception)
        {
            // ArcTokens.Get<T> throws without a theme context (a detached owner); the default font is fine for one frame.
            BodyFamily = FontFamily.Default;
            DisplayFamily = FontFamily.Default;
            _ready = false;
        }
        // The radii the calendar draws itself (range band, agenda rows, small chips, header pill) follow the radius steps, so a
        // corner style reaches them. The fallbacks are the values Arc ships.
        Radius14 = ArcTokens.GetCornerRadius(owner, ArcDesignTokenKind.RadiusStep14, new CornerRadius(14)).TopLeft;
        Radius6 = ArcTokens.GetCornerRadius(owner, ArcDesignTokenKind.RadiusStep6, new CornerRadius(6)).TopLeft;
        RadiusPill = ArcTokens.GetCornerRadius(owner, ArcDesignTokenKind.RadiusPill, new CornerRadius(9999)).TopLeft;
        Body = new Typeface(BodyFamily);
        BodyMedium = new Typeface(BodyFamily, FontStyle.Normal, FontWeight.Medium);
        DisplayMedium = new Typeface(DisplayFamily, FontStyle.Normal, FontWeight.Medium);
    }

    private Color Get(ArcDesignTokenKind kind, Color fallback)
    {
        var color = ArcTokens.GetColor(_owner, kind);
        return color.A == 0 && kind != ArcDesignTokenKind.FocusRing ? fallback : color;
    }

    /// <summary>The brushes of the calendar with <paramref name="calendarId"/> (gray for an unknown id).</summary>
    public WeekCalendarBrushes For(string? calendarId)
    {
        EnsureReady();
        if (calendarId is not null && _calendars.TryGetValue(calendarId, out var cached))
        {
            return cached;
        }
        var source = calendarId is null ? null : _owner.FindCalendar(calendarId);
        if (source is null)
        {
            return _neutral ??= Make(WeekCalendarPalette.Base(WeekCalendarColor.Neutral, IsDark));
        }
        var brushes = Make(WeekCalendarPalette.BaseOf(source, IsDark));
        _calendars[calendarId!] = brushes;
        return brushes;
    }

    /// <summary>The base color of a calendar in the current theme (swatches, toggles, agenda bars).</summary>
    public IBrush BarOf(WeekCalendarSource source) => For(source.Id).Bar;

    private WeekCalendarBrushes Make(Color calendar)
    {
        var n = ArcPalette.Neutrals(IsDark);
        var surface = SurfaceColor.A == 0 ? n.Surface : SurfaceColor;
        var foreground = Foreground is ISolidColorBrush f ? f.Color : n.Foreground;
        var secondary = TextSecondary is ISolidColorBrush s ? s.Color : n.TextSecondary;
        var border = Border is ISolidColorBrush b ? b.Color : n.Border;
        return new WeekCalendarBrushes(WeekCalendarPalette.Derive(calendar, IsDark, surface, foreground, secondary), surface, border, IsDark);
    }
}

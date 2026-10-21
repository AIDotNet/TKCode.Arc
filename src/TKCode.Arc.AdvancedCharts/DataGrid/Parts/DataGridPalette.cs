using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Resolved colors, pens and fonts shared by the self-drawn parts of one <see cref="DataGrid"/>.</summary>
internal sealed class DataGridPalette
{
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    public int Version { get; private set; }

    public Color Surface { get; private set; }
    public Color SurfaceRaised { get; private set; }
    public Color Foreground { get; private set; }
    public Color TextMuted { get; private set; }
    public Color Accent { get; private set; }

    public IBrush SurfaceBrush { get; private set; } = Brushes.Transparent;
    public IBrush CellBrush { get; private set; } = Brushes.Transparent;
    public IBrush CheckedBrush { get; private set; } = Brushes.Transparent;
    public IBrush GroupBrush { get; private set; } = Brushes.Transparent;
    public IBrush ForegroundBrush { get; private set; } = Brushes.Black;
    public IBrush SecondaryBrush { get; private set; } = Brushes.Gray;
    public IBrush MutedBrush { get; private set; } = Brushes.Gray;
    public IBrush AccentBrush { get; private set; } = Brushes.Black;
    public IBrush SelectionFill { get; private set; } = Brushes.Transparent;
    public IBrush PreviewFill { get; private set; } = Brushes.Transparent;
    public IBrush WashFill { get; private set; } = Brushes.Transparent;
    public IBrush SkeletonBrush { get; private set; } = Brushes.Transparent;
    public IBrush ControlOnBrush { get; private set; } = Brushes.Black;
    public IBrush BadgeBrush { get; private set; } = Brushes.Transparent;
    public IPen GridPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen BorderPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen SelectionPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen ActivePen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen LinkLine { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen ErrorPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen PreviewPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen HandleOutline { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen ChevronPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen CheckboxPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen GlyphPen { get; private set; } = new ImmutablePen(Brushes.Transparent);
    public IPen? FocusPen { get; private set; }
    public Color EdgeShadow { get; private set; }

    public Typeface Regular { get; private set; }
    public Typeface Medium { get; private set; }
    public double FontSize { get; private set; } = 14;
    public double SmallFontSize { get; private set; } = 12;

    private static readonly DataGridToneBrushes s_blankTone =
        new(Brushes.Transparent, Brushes.Black, new ImmutablePen(Brushes.Transparent), new ImmutablePen(Brushes.Black, 2));

    private readonly DataGridToneBrushes[] _tones = [s_blankTone, s_blankTone, s_blankTone, s_blankTone, s_blankTone];

    /// <summary>The fill, text, outline and icon stroke of a tone (renderers).</summary>
    public DataGridToneBrushes Tone(DataGridTone tone) => _tones[(int)tone];

    /// <summary>Builds the tone brushes from the semantic tokens; call after <see cref="Update"/>.</summary>
    public void UpdateTones(Color accent, Color accentSubtle, Color success, Color successSubtle, Color warning, Color warningSubtle,
                            Color danger, Color dangerSubtle)
    {
        static IPen Stroke(IBrush brush) => new ImmutablePen(brush.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        DataGridToneBrushes Make(Color fill, Color strong)
        {
            var foreground = new ImmutableSolidColorBrush(strong);
            return new DataGridToneBrushes(new ImmutableSolidColorBrush(fill), foreground,
                new ImmutablePen(new ImmutableSolidColorBrush(Oklch.MixOklab(fill, strong, 0.22)), 1), Stroke(foreground));
        }
        _tones[(int)DataGridTone.Neutral] = new DataGridToneBrushes(BadgeBrush, SecondaryBrush, GridPen, Stroke(SecondaryBrush));
        _tones[(int)DataGridTone.Accent] = Make(accentSubtle, accent);
        _tones[(int)DataGridTone.Success] = Make(successSubtle, success);
        _tones[(int)DataGridTone.Warning] = Make(warningSubtle, warning);
        _tones[(int)DataGridTone.Danger] = Make(dangerSubtle, danger);
        ErrorPen = new ImmutablePen(new ImmutableSolidColorBrush(danger), 1.5);
        Version++;
    }

    public void Update(Color surface, Color surfaceRaised, Color surfaceMuted, Color border, Color borderSubtle, Color borderStrong,
                       Color foreground, Color textSecondary, Color textMuted, Color accent, Color controlOn, Color controlGlyph,
                       Color? focusRing, double selectionOpacity, double checkedOpacity, double washOpacity, bool isDark,
                       FontFamily fontFamily, double fontSize, double smallFontSize, FontWeight medium)
    {
        Surface = surface;
        SurfaceRaised = surfaceRaised;
        Foreground = foreground;
        TextMuted = textMuted;
        Accent = accent;
        SurfaceBrush = new ImmutableSolidColorBrush(surface);
        CellBrush = new ImmutableSolidColorBrush(surfaceRaised);
        CheckedBrush = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, accent, checkedOpacity));
        GroupBrush = new ImmutableSolidColorBrush(Oklch.MixOklab(surfaceRaised, surfaceMuted, 0.55));
        ForegroundBrush = new ImmutableSolidColorBrush(foreground);
        SecondaryBrush = new ImmutableSolidColorBrush(textSecondary);
        MutedBrush = new ImmutableSolidColorBrush(textMuted);
        AccentBrush = new ImmutableSolidColorBrush(accent);
        SelectionFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, selectionOpacity));
        PreviewFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, selectionOpacity * 0.55));
        WashFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, washOpacity));
        SkeletonBrush = new ImmutableSolidColorBrush(surfaceMuted);
        ControlOnBrush = new ImmutableSolidColorBrush(controlOn);
        BadgeBrush = new ImmutableSolidColorBrush(surfaceMuted);
        GridPen = new ImmutablePen(new ImmutableSolidColorBrush(borderSubtle), 1);
        BorderPen = new ImmutablePen(new ImmutableSolidColorBrush(border), 1);
        SelectionPen = new ImmutablePen(new ImmutableSolidColorBrush(accent), 1);
        ActivePen = new ImmutablePen(new ImmutableSolidColorBrush(accent), 2);
        LinkLine = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, 0.45)), 1);
        PreviewPen = new ImmutablePen(new ImmutableSolidColorBrush(accent), 1.5, new ImmutableDashStyle([3, 2], 0));
        HandleOutline = new ImmutablePen(new ImmutableSolidColorBrush(surface), 2);
        ChevronPen = new ImmutablePen(new ImmutableSolidColorBrush(textSecondary), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        CheckboxPen = new ImmutablePen(new ImmutableSolidColorBrush(borderStrong), 1);
        GlyphPen = new ImmutablePen(new ImmutableSolidColorBrush(controlGlyph), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        FocusPen = focusRing is { } ring ? new ImmutablePen(new ImmutableSolidColorBrush(ring), 2) : null;
        EdgeShadow = Color.FromArgb((byte)(isDark ? 64 : 16), 0, 0, 0);
        Regular = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal);
        Medium = new Typeface(fontFamily, FontStyle.Normal, medium);
        FontSize = fontSize;
        SmallFontSize = smallFontSize;
        Version++;
    }
}

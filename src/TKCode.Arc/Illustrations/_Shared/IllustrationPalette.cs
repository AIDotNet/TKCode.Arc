using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Named colors of an illustration (ArcDesign tokens captured from the control's theme).</summary>
internal enum IllustrationInk
{
    Foreground,
    Secondary,
    Muted,
    Line,
    SubtleLine,
    StrongLine,
    Surface,
    SurfaceMuted,
    Raised,
    Accent,
    AccentSubtle,
    AccentForeground,
    Success,
    Warning,
    Danger
}

/// <summary>
/// Colors, pens, brushes and text of one illustration, captured from its ArcDesign token properties. Rebuilt only when
/// a token changes (theme or accent switch); everything a frame draws is cached here, so frames allocate nothing.
/// </summary>
internal sealed class IllustrationPalette
{
    private const int TextCacheLimit = 512;

    private readonly Color[] _colors = new Color[Enum.GetValues<IllustrationInk>().Length];
    private readonly IImmutableSolidColorBrush[] _brushes = new IImmutableSolidColorBrush[Enum.GetValues<IllustrationInk>().Length];
    private readonly Dictionary<(IllustrationInk Ink, int Alpha), IImmutableSolidColorBrush> _tints = new();
    private readonly Dictionary<(IllustrationInk Ink, double Thickness, bool Dashed), ImmutablePen> _pens = new();
    private readonly Dictionary<(IllustrationInk Ink, int Alpha, double Thickness), ImmutablePen> _tintPens = new();
    private readonly Dictionary<(string Text, double Size, bool Medium, bool Mono, IllustrationInk Ink, int MaxWidth), TextLayout> _texts = new();

    public int Version { get; private set; }

    public BoxShadows Shadow { get; private set; }

    public Typeface Regular { get; private set; }

    public Typeface Medium { get; private set; }

    public Typeface Mono { get; private set; }

    public double SmallSize { get; private set; } = 12;

    public double TextSize { get; private set; } = 13;

    public void Capture(Illustration owner)
    {
        Set(IllustrationInk.Foreground, owner.Foreground, Colors.Black);
        Set(IllustrationInk.Secondary, owner.SecondaryBrush, Color.FromRgb(0x52, 0x52, 0x52));
        Set(IllustrationInk.Muted, owner.MutedBrush, Color.FromRgb(0x73, 0x73, 0x73));
        Set(IllustrationInk.Line, owner.LineBrush, Color.FromRgb(0xE5, 0xE5, 0xE5));
        Set(IllustrationInk.SubtleLine, owner.SubtleLineBrush, Color.FromRgb(0xF0, 0xF0, 0xF0));
        Set(IllustrationInk.StrongLine, owner.StrongLineBrush, Color.FromRgb(0xC4, 0xC4, 0xC4));
        Set(IllustrationInk.Surface, owner.SurfaceBrush, Colors.White);
        Set(IllustrationInk.SurfaceMuted, owner.SurfaceMutedBrush, Color.FromRgb(0xF5, 0xF5, 0xF5));
        Set(IllustrationInk.Raised, owner.RaisedBrush, Colors.White);
        Set(IllustrationInk.Accent, owner.AccentBrush, Color.FromRgb(0x17, 0x17, 0x17));
        Set(IllustrationInk.AccentSubtle, owner.AccentSubtleBrush, Color.FromRgb(0xF5, 0xF5, 0xF5));
        Set(IllustrationInk.AccentForeground, owner.AccentForegroundBrush, Colors.White);
        Set(IllustrationInk.Success, owner.SuccessBrush, Color.FromRgb(0x16, 0xA3, 0x4A));
        Set(IllustrationInk.Warning, owner.WarningBrush, Color.FromRgb(0xD9, 0x77, 0x06));
        Set(IllustrationInk.Danger, owner.DangerBrush, Color.FromRgb(0xDC, 0x26, 0x26));
        Shadow = owner.RestingShadow;
        var family = owner.FontFamily;
        Regular = new Typeface(family, FontStyle.Normal, FontWeight.Normal);
        Medium = new Typeface(family, FontStyle.Normal, owner.MediumWeight);
        Mono = new Typeface(owner.MonoFontFamily ?? family, FontStyle.Normal, FontWeight.Normal);
        SmallSize = owner.TextSizeXS;
        TextSize = owner.TextSizeSM;
        _tints.Clear();
        _pens.Clear();
        _tintPens.Clear();
        ClearTexts();
        Version++;
    }

    private void ClearTexts()
    {
        foreach (var layout in _texts.Values)
        {
            layout.Dispose();
        }
        _texts.Clear();
    }

    private void Set(IllustrationInk ink, IBrush? brush, Color fallback)
    {
        var color = brush is ISolidColorBrush solid ? solid.Color : fallback;
        _colors[(int)ink] = color;
        _brushes[(int)ink] = new ImmutableSolidColorBrush(color);
    }

    public Color ColorOf(IllustrationInk ink) => _colors[(int)ink];

    public IImmutableSolidColorBrush Brush(IllustrationInk ink) => _brushes[(int)ink];

    /// <summary><paramref name="ink"/> at <paramref name="alpha"/> (quantized to 1%).</summary>
    public IImmutableSolidColorBrush Tint(IllustrationInk ink, double alpha)
    {
        var key = (ink, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 100));
        if (!_tints.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(_colors[(int)ink], key.Item2 / 100.0);
            _tints[key] = brush;
        }
        return brush;
    }

    public ImmutablePen Pen(IllustrationInk ink, double thickness, bool dashed = false)
    {
        var key = (ink, thickness, dashed);
        if (!_pens.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(_brushes[(int)ink], thickness,
                dashed ? new ImmutableDashStyle([3, 3], 0) : null, PenLineCap.Round, PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    /// <summary>A solid pen in <paramref name="ink"/> at <paramref name="alpha"/> (quantized to 1%).</summary>
    public ImmutablePen TintPen(IllustrationInk ink, double alpha, double thickness)
    {
        var key = (ink, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 100), thickness);
        if (!_tintPens.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(Tint(ink, key.Item2 / 100.0), thickness);
            _tintPens[key] = pen;
        }
        return pen;
    }

    /// <summary>
    /// A cached text run (the cache resets on a theme change or when it grows past its limit). A <see cref="TextLayout"/>
    /// rather than a <c>FormattedText</c>: the static and motion layers redraw these strings on every animated frame, and
    /// a FormattedText re-shapes its text on every Draw while a TextLayout keeps its lines and only emits glyphs.
    /// </summary>
    public TextLayout Text(string text, IllustrationInk ink, bool small = true, bool medium = false, bool mono = false,
                           double maxWidth = double.PositiveInfinity)
    {
        var size = small ? SmallSize : TextSize;
        var width = double.IsFinite(maxWidth) ? (int)Math.Floor(maxWidth) : 0;
        var key = (text, size, medium, mono, ink, width);
        if (!_texts.TryGetValue(key, out var layout))
        {
            if (_texts.Count > TextCacheLimit)
            {
                ClearTexts();
            }
            var typeface = mono ? Mono : medium ? Medium : Regular;
            layout = width > 0
                // Too long for its card: one line ending in an ellipsis.
                ? new TextLayout(text, typeface, size, _brushes[(int)ink], textWrapping: TextWrapping.NoWrap,
                    textTrimming: TextTrimming.CharacterEllipsis, maxWidth: width, maxLines: 1)
                : new TextLayout(text, typeface, size, _brushes[(int)ink]);
            _texts[key] = layout;
        }
        return layout;
    }

    public static IllustrationInk InkOf(IllustrationTone tone) => tone switch
    {
        IllustrationTone.Accent => IllustrationInk.Accent,
        IllustrationTone.Success => IllustrationInk.Success,
        IllustrationTone.Warning => IllustrationInk.Warning,
        IllustrationTone.Danger => IllustrationInk.Danger,
        _ => IllustrationInk.Secondary
    };
}

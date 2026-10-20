using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// ArcDesign colors, shadows and type for the self-drawn charts of team B. Every value follows its token through
/// <see cref="ResourceNodeExtensions.GetResourceObservable(IResourceHost, object, Func{object?, object?}?)"/>, so a theme,
/// accent or dark-mode change repaints the owner. Small on purpose: swap for the chart foundation later.
/// </summary>
internal sealed class ChartsBPalette
{
    private static readonly ArcDesignTokenKind[] s_colorKinds =
    [
        ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.Background,
        ArcDesignTokenKind.Border, ArcDesignTokenKind.BorderSubtle, ArcDesignTokenKind.BorderStrong, ArcDesignTokenKind.Surface,
        ArcDesignTokenKind.SurfaceRaised, ArcDesignTokenKind.Accent, ArcDesignTokenKind.AccentStrong, ArcDesignTokenKind.Success,
        ArcDesignTokenKind.Warning, ArcDesignTokenKind.Danger, ArcDesignTokenKind.Series1, ArcDesignTokenKind.Series2,
        ArcDesignTokenKind.Series3, ArcDesignTokenKind.Series4, ArcDesignTokenKind.FocusRing
    ];

    private readonly Color[] _colors = new Color[s_colorKinds.Length];
    private readonly Dictionary<Color, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<(Color, double, PenLineCap), IPen> _pens = new();
    private readonly List<IDisposable> _subscriptions = new();
    private readonly Action _changed;

    public ChartsBPalette(Control owner, Action changed)
    {
        _changed = changed;
        _colors[0] = Colors.Black;
        _colors[7] = Colors.White;
        for (var i = 0; i < s_colorKinds.Length; i++)
        {
            var index = i;
            Watch(owner, s_colorKinds[i], value =>
            {
                if (ToColor(value) is { } color)
                {
                    _colors[index] = color;
                }
            });
        }
        Watch(owner, ArcDesignTokenKind.ShadowFloating, value => ShadowFloating = value is BoxShadows s ? s : default);
        Watch(owner, ArcDesignTokenKind.ShadowRaised, value => ShadowRaised = value is BoxShadows s ? s : default);
        Watch(owner, ArcDesignTokenKind.BodyFontFamily, value => BodyFont = value as FontFamily ?? FontFamily.Default);
        Watch(owner, ArcDesignTokenKind.DisplayFontFamily, value => DisplayFont = value as FontFamily ?? FontFamily.Default);
        Watch(owner, ArcDesignTokenKind.TypeXS, value => TypeXS = value is double d ? d : 12);
        Watch(owner, ArcDesignTokenKind.TypeSM, value => TypeSM = value is double d ? d : 14);
        Watch(owner, ArcDesignTokenKind.TypeBase, value => TypeBase = value is double d ? d : 16);
        Watch(owner, ArcDesignTokenKind.RadiusControl, value => RadiusControl = value is CornerRadius r ? r.TopLeft : 18);
    }

    /// <summary>Incremented on every token change; text and geometry caches compare against it.</summary>
    public int Version { get; private set; }

    public Color Foreground => _colors[0];
    public Color TextSecondary => _colors[1];
    public Color TextMuted => _colors[2];
    public Color Background => _colors[3];
    public Color Border => _colors[4];
    public Color BorderSubtle => _colors[5];
    public Color BorderStrong => _colors[6];
    public Color Surface => _colors[7];
    public Color SurfaceRaised => _colors[8];
    public Color Accent => _colors[9];
    public Color AccentStrong => _colors[10];
    public Color Success => _colors[11];
    public Color Warning => _colors[12];
    public Color Danger => _colors[13];
    public Color FocusRing => _colors[18];

    /// <summary>Series1..4 by zero-based index.</summary>
    public Color Series(int index) => _colors[14 + Math.Clamp(index, 0, 3)];

    public BoxShadows ShadowFloating { get; private set; }
    public BoxShadows ShadowRaised { get; private set; }
    public FontFamily BodyFont { get; private set; } = FontFamily.Default;
    public FontFamily DisplayFont { get; private set; } = FontFamily.Default;
    public double TypeXS { get; private set; } = 12;
    public double TypeSM { get; private set; } = 14;
    public double TypeBase { get; private set; } = 16;
    public double RadiusControl { get; private set; } = 18;

    /// <summary>A cached brush for a resting color (animated colors should use a fresh immutable brush instead).</summary>
    public IImmutableSolidColorBrush Brush(Color color)
    {
        if (!_brushes.TryGetValue(color, out var brush))
        {
            if (_brushes.Count > 512)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[color] = brush;
        }
        return brush;
    }

    /// <summary>A cached pen for a resting color.</summary>
    public IPen Pen(Color color, double thickness, PenLineCap cap = PenLineCap.Flat)
    {
        var key = (color, thickness, cap);
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count > 256)
            {
                _pens.Clear();
            }
            pen = new ImmutablePen(Brush(color), thickness, null, cap, PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    public Typeface Body(FontWeight weight = FontWeight.Normal) => new(BodyFont, FontStyle.Normal, weight);

    private void Watch(Control owner, ArcDesignTokenKind kind, Action<object?> apply)
        => _subscriptions.Add(owner.GetResourceObservable(kind).Subscribe(new Observer(value =>
        {
            if (value == AvaloniaProperty.UnsetValue)
            {
                return;
            }
            apply(value);
            Version++;
            _changed();
        })));

    private static Color? ToColor(object? value) => value switch
    {
        ISolidColorBrush brush => brush.Color,
        Color color => color,
        _ => null
    };

    private sealed class Observer(Action<object?> next) : IObserver<object?>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(object? value) => next(value);
    }
}

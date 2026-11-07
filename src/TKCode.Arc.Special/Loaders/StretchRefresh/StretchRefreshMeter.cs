using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The line of <see cref="StretchRefresh"/>: a 3 px pill track that clips a fill growing from its centre and a
/// running segment 40% as wide. The whole track scales horizontally about its centre (over-stretch to 1.12 when armed,
/// shrink to 0.4 when the result shows) and fades. Drawn in one pass; the owner writes the state fields and calls
/// <c>InvalidateVisual</c>.
/// </summary>
internal sealed class StretchRefreshMeter : Control
{
    public static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<StretchRefreshMeter, IBrush?>(nameof(TrackInk));

    public static readonly StyledProperty<IBrush?> FillInkProperty =
        AvaloniaProperty.Register<StretchRefreshMeter, IBrush?>(nameof(FillInk));

    public static readonly StyledProperty<IBrush?> ArmedInkProperty =
        AvaloniaProperty.Register<StretchRefreshMeter, IBrush?>(nameof(ArmedInk));

    private IBrush? _fillBrush;
    private double _armedMix = -1;
    private IBrush? _fillFaded;
    private IBrush? _fillFadedSource;
    private int _fillFadedKey = -1;
    private IBrush? _runFaded;
    private IBrush? _runFadedSource;
    private int _runFadedKey = -1;

    static StretchRefreshMeter()
    {
        AffectsRender<StretchRefreshMeter>(TrackInkProperty, FillInkProperty, ArmedInkProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<StretchRefreshMeter>(false);
    }

    /// <summary>The track (Border).</summary>
    public IBrush? TrackInk
    {
        get => GetValue(TrackInkProperty);
        set => SetValue(TrackInkProperty, value);
    }

    /// <summary>The fill while pulling (TextMuted).</summary>
    public IBrush? FillInk
    {
        get => GetValue(FillInkProperty);
        set => SetValue(FillInkProperty, value);
    }

    /// <summary>The fill once armed and the running segment (Foreground).</summary>
    public IBrush? ArmedInk
    {
        get => GetValue(ArmedInkProperty);
        set => SetValue(ArmedInkProperty, value);
    }

    /// <summary>Horizontal scale of the whole track about its centre (1, 1.12 armed, 0.4 shrunk).</summary>
    public double TrackScale { get; set; } = 1;

    /// <summary>Opacity of the whole track.</summary>
    public double TrackOpacity { get; set; } = 1;

    /// <summary>The fill's width as a fraction of the track, centred.</summary>
    public double FillScale { get; set; }

    public double FillOpacity { get; set; } = 1;

    /// <summary>0 = pull colour, 1 = armed colour.</summary>
    public double ArmedMix { get; set; }

    /// <summary>The running segment's left edge in segment widths (0 … 1.5) and its opacity.</summary>
    public double RunPosition { get; set; }

    public double RunOpacity { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillInkProperty || change.Property == ArmedInkProperty)
        {
            _armedMix = -1;
        }
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0 || TrackOpacity <= 0.001)
        {
            return;
        }
        var radius = h / 2;
        var track = new Rect(0, 0, w, h);
        var scale = Math.Max(0, TrackScale);
        // The whole track fades as a group only while it fades (result and reset); the fill and the segment are single
        // shapes, so their opacity goes into the brush instead of an opacity layer.
        using (TrackOpacity < 0.999 ? context.PushOpacity(Math.Clamp(TrackOpacity, 0, 1)) : default(DrawingContext.PushedState?))
        using (context.PushTransform(Matrix.CreateTranslation(-w / 2, 0) * Matrix.CreateScale(scale, 1) * Matrix.CreateTranslation(w / 2, 0)))
        {
            var shape = new RoundedRect(track, radius);
            if (TrackInk is { } trackInk)
            {
                context.DrawRectangle(trackInk, null, shape);
            }
            using (context.PushClip(shape))
            {
                if (FillOpacity > 0.001 && FillScale > 0)
                {
                    var fw = w * Math.Clamp(FillScale, 0, 1);
                    context.DrawRectangle(Faded(FillBrush(), FillOpacity, ref _fillFaded, ref _fillFadedKey, ref _fillFadedSource), null,
                        new RoundedRect(new Rect((w - fw) / 2, 0, fw, h), radius));
                }
                if (RunOpacity > 0.001 && ArmedInk is { } run)
                {
                    var rw = w * StretchRefreshMath.RunWidth;
                    context.DrawRectangle(Faded(run, RunOpacity, ref _runFaded, ref _runFadedKey, ref _runFadedSource), null,
                        new RoundedRect(new Rect(RunPosition * rw, 0, rw, h), radius));
                }
            }
        }
    }

    /// <summary>
    /// <paramref name="brush"/> at <paramref name="opacity"/> (in 1/255 steps), cached until the source or the step
    /// changes, so a steady line allocates nothing.
    /// </summary>
    private static IBrush? Faded(IBrush? brush, double opacity, ref IBrush? cache, ref int key, ref IBrush? source)
    {
        var step = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        if (step >= 255 || brush is not ISolidColorBrush solid)
        {
            return brush;
        }
        if (cache is null || key != step || !ReferenceEquals(source, brush))
        {
            cache = new ImmutableSolidColorBrush(solid.Color, solid.Opacity * step / 255.0);
            key = step;
            source = brush;
        }
        return cache;
    }

    /// <summary>The fill colour between the pull and armed inks (rebuilt only when the mix changes).</summary>
    private IBrush? FillBrush()
    {
        var mix = Math.Clamp(ArmedMix, 0, 1);
        if (_fillBrush is not null && Math.Abs(mix - _armedMix) < 0.002)
        {
            return _fillBrush;
        }
        _armedMix = mix;
        if (mix <= 0.002 || ArmedInk is not ISolidColorBrush armed || FillInk is not ISolidColorBrush fill)
        {
            _fillBrush = mix > 0.5 ? ArmedInk : FillInk;
            return _fillBrush;
        }
        if (mix >= 0.998)
        {
            _fillBrush = armed;
            return _fillBrush;
        }
        _fillBrush = new ImmutableSolidColorBrush(Lerp(fill.Color, armed.Color, mix), fill.Opacity);
        return _fillBrush;
    }

    private static Color Lerp(Color a, Color b, double t)
        => Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
}

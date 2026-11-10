using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// The tape of a <see cref="RulerSlider"/>: ticks, labels and the fixed needle drawn in one control. Ticks repeat, so each of
/// the three layers is one cached geometry a little wider than the view that is only translated by the tape's position
/// (modulo one labelled period) and clipped to the tape's ends. Labels are laid-out text kept per labelled tick. A frame of a
/// moving tape allocates nothing here; only a label that first enters the view lays out text.
/// </summary>
internal sealed class RulerTape : Control, ICustomHitTest
{
    /// <summary>The most labels kept; far more than fit in any view, so a long tape does not keep them all.</summary>
    private const int LabelCacheLimit = 64;

    // Ticks fade toward the edges, so the eye stays on the needle and the tape reads as continuing past view.
    private static readonly IBrush s_fade = new ImmutableLinearGradientBrush(
    [
        new ImmutableGradientStop(0, Colors.Transparent),
        new ImmutableGradientStop(0.22, Colors.Black),
        new ImmutableGradientStop(0.78, Colors.Black),
        new ImmutableGradientStop(1, Colors.Transparent)
    ],
    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
    endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<RulerTape, IBrush?>(nameof(TickBrush));

    public static readonly StyledProperty<IBrush?> MajorTickBrushProperty =
        AvaloniaProperty.Register<RulerTape, IBrush?>(nameof(MajorTickBrush));

    public static readonly StyledProperty<IBrush?> NeedleBrushProperty =
        AvaloniaProperty.Register<RulerTape, IBrush?>(nameof(NeedleBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<RulerTape, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<double> SmallTickHeightProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(SmallTickHeight), 12);

    public static readonly StyledProperty<double> HalfTickHeightProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(HalfTickHeight), 20);

    public static readonly StyledProperty<double> MajorTickHeightProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(MajorTickHeight), 28);

    public static readonly StyledProperty<double> TickWidthProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(TickWidth), 1);

    public static readonly StyledProperty<double> NeedleWidthProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(NeedleWidth), 2);

    public static readonly StyledProperty<double> NeedleHeightProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(NeedleHeight), 36);

    public static readonly StyledProperty<CornerRadius> NeedleRadiusProperty =
        AvaloniaProperty.Register<RulerTape, CornerRadius>(nameof(NeedleRadius));

    public static readonly StyledProperty<double> LabelTopProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(LabelTop), 36);

    public static readonly StyledProperty<double> LabelLineHeightProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(LabelLineHeight), 16.8);

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<RulerTape, double>(nameof(LabelFontSize), 12);

    public static readonly StyledProperty<FontFamily> LabelFontFamilyProperty =
        AvaloniaProperty.Register<RulerTape, FontFamily>(nameof(LabelFontFamily), FontFamily.Default);

    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public IBrush? MajorTickBrush { get => GetValue(MajorTickBrushProperty); set => SetValue(MajorTickBrushProperty, value); }
    public IBrush? NeedleBrush { get => GetValue(NeedleBrushProperty); set => SetValue(NeedleBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public double SmallTickHeight { get => GetValue(SmallTickHeightProperty); set => SetValue(SmallTickHeightProperty, value); }
    public double HalfTickHeight { get => GetValue(HalfTickHeightProperty); set => SetValue(HalfTickHeightProperty, value); }
    public double MajorTickHeight { get => GetValue(MajorTickHeightProperty); set => SetValue(MajorTickHeightProperty, value); }
    public double TickWidth { get => GetValue(TickWidthProperty); set => SetValue(TickWidthProperty, value); }
    public double NeedleWidth { get => GetValue(NeedleWidthProperty); set => SetValue(NeedleWidthProperty, value); }
    public double NeedleHeight { get => GetValue(NeedleHeightProperty); set => SetValue(NeedleHeightProperty, value); }
    public CornerRadius NeedleRadius { get => GetValue(NeedleRadiusProperty); set => SetValue(NeedleRadiusProperty, value); }
    public double LabelTop { get => GetValue(LabelTopProperty); set => SetValue(LabelTopProperty, value); }
    public double LabelLineHeight { get => GetValue(LabelLineHeightProperty); set => SetValue(LabelLineHeightProperty, value); }
    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
    public FontFamily LabelFontFamily { get => GetValue(LabelFontFamilyProperty); set => SetValue(LabelFontFamilyProperty, value); }

    private readonly Dictionary<int, TextLayout> _labels = new();
    private StreamGeometry? _small;
    private StreamGeometry? _half;
    private StreamGeometry? _major;
    private double _geometryWidth = double.NaN;

    static RulerTape()
    {
        AffectsRender<RulerTape>(TickBrushProperty, MajorTickBrushProperty, NeedleBrushProperty, NeedleWidthProperty, NeedleHeightProperty,
            NeedleRadiusProperty, LabelTopProperty, LabelLineHeightProperty);
    }

    internal RulerSlider? Owner { get; set; }

    // Read by tests: the same instances mean the frame reused the geometry and the text.
    internal StreamGeometry? SmallTicks => _small;

    internal int CachedLabelCount => _labels.Count;

    internal bool HasCachedLabel(int major) => _labels.ContainsKey(major);

    /// <summary>The scale changed: tick rhythm and label text are built again on the next draw.</summary>
    internal void InvalidateScale()
    {
        _small = _half = _major = null;
        DropLabels();
        InvalidateVisual();
    }

    private void DropLabels()
    {
        foreach (var layout in _labels.Values)
        {
            layout.Dispose();
        }
        _labels.Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SmallTickHeightProperty || change.Property == HalfTickHeightProperty ||
            change.Property == MajorTickHeightProperty || change.Property == TickWidthProperty)
        {
            InvalidateScale();
        }
        else if (change.Property == LabelBrushProperty || change.Property == LabelFontSizeProperty || change.Property == LabelFontFamilyProperty)
        {
            DropLabels();
            InvalidateVisual();
        }
    }

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Owner?.OnTapeWheel(e);
    }

    private StreamGeometry Ticks(double start, double every, int count, double height)
    {
        var geometry = new StreamGeometry();
        var width = TickWidth;
        using var context = geometry.Open();
        for (var i = 0; i < count; i++)
        {
            var x = start + i * every;
            context.BeginFigure(new Point(x, 0), true);
            context.LineTo(new Point(x + width, 0));
            context.LineTo(new Point(x + width, height));
            context.LineTo(new Point(x, height));
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>The three tick layers, covering the view plus one period so any translation inside a period fills it.</summary>
    private void EnsureTicks(RulerSlider owner, double width, double period)
    {
        if (_small is not null && _geometryWidth == width)
        {
            return;
        }
        _geometryWidth = width;
        var ticks = owner.Ticks;
        var periods = (int)Math.Ceiling(width / period) + 2;
        _small = Ticks(0, owner.Spacing, periods * ticks, SmallTickHeight);
        _half = ticks % 2 == 0 ? Ticks(period / 2, period, periods, HalfTickHeight) : null;
        _major = Ticks(0, period, periods, MajorTickHeight);
    }

    private TextLayout Label(RulerSlider owner, int major)
    {
        if (_labels.TryGetValue(major, out var cached))
        {
            return cached;
        }
        var layout = new TextLayout(owner.FormatLabel(owner.MajorValue(major)), new Typeface(LabelFontFamily), LabelFontSize, LabelBrush,
            TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, textDecorations: null, FlowDirection.LeftToRight,
            double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, 1);
        _labels[major] = layout;
        return layout;
    }

    /// <summary>Keeps the cache bounded on a long tape by forgetting what is out of view.</summary>
    private void TrimLabels(int first, int last)
    {
        if (_labels.Count <= LabelCacheLimit)
        {
            return;
        }
        foreach (var (major, layout) in _labels)
        {
            if (major < first || major > last)
            {
                layout.Dispose();
                _labels.Remove(major);
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner)
        {
            return;
        }
        var size = Bounds.Size;
        var bounds = new Rect(size);
        var period = owner.Spacing * owner.Ticks;
        // The tape starts at the needle, offset half a tick so a one pixel tick centers exactly under it.
        var center = size.Width / 2;
        var origin = center - TickWidth / 2 - owner.TapeOffset;
        EnsureTicks(owner, size.Width, period);

        using (context.PushClip(bounds))
        {
            using (context.PushOpacityMask(s_fade, bounds))
            {
                var tape = new Rect(origin, 0, owner.TapeLength + TickWidth, size.Height);
                using (context.PushClip(tape.Intersect(bounds)))
                {
                    var phase = origin - Math.Ceiling(origin / period) * period;
                    using (context.PushTransform(Matrix.CreateTranslation(phase, 0)))
                    {
                        context.DrawGeometry(MajorTickBrush, null, _major!);
                        if (_half is { } half)
                        {
                            context.DrawGeometry(TickBrush, null, half);
                        }
                        context.DrawGeometry(TickBrush, null, _small!);
                    }
                }
                DrawLabels(context, owner, origin, period, size.Width);
            }
            var needleWidth = NeedleWidth;
            var radius = Math.Min(NeedleRadius.BottomLeft, needleWidth / 2);
            var needle = new Rect(center - needleWidth / 2, 0, needleWidth, NeedleHeight);
            context.DrawRectangle(NeedleBrush, null, new RoundedRect(needle, 0, 0, radius, radius));
        }
    }

    private void DrawLabels(DrawingContext context, RulerSlider owner, double origin, double period, double width)
    {
        // A label is drawn while any of it can be seen; half a period past each edge covers any label narrower than a period.
        var first = Math.Max(0, (int)Math.Ceiling((-period / 2 - origin) / period));
        var last = Math.Min(owner.MajorCount - 1, (int)Math.Floor((width + period / 2 - origin) / period));
        TrimLabels(first, last);
        for (var major = first; major <= last; major++)
        {
            var layout = Label(owner, major);
            var x = origin + major * period - layout.WidthIncludingTrailingWhitespace / 2;
            layout.Draw(context, new Point(x, LabelTop + (LabelLineHeight - layout.Height) / 2));
        }
    }
}

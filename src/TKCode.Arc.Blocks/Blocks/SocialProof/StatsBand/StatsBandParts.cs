// Ported from Arc's free stats-band block (MIT, Copyright (c) 2026 Elia Kuratli): CountUp, Trend, Uptime, Distribution and WorldMap
// of stats-band.tsx and the matching rules of stats-band.module.css.

using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The times of the entrance (ms), the same CSS timings the band's parts read from its one clock.</summary>
internal static class StatsBandTiming
{
    public const double Stagger = 90;
    public const double RiseOpacity = 480;
    public const double RiseTravel = 640;
    public const double Wipe = 1100;
    public const double BarGrow = 560;
    public const double BarSweep = 420;
    public const double DotDelay = 1000;
    public const double DotFade = 320;
    public const double DotSpring = 580;
    public const double Rule = 900;
    public const double RuleBottomDelay = 120;
    public const double Hairline = 700;
    public const double HairlineDelay = 240;

    /// <summary>The fraction of a timed step at <paramref name="time"/>: 0 before it starts, 1 after it ends.</summary>
    public static double Progress(double time, double delay, double duration)
    {
        if (double.IsPositiveInfinity(time))
        {
            return 1;
        }
        return duration <= 0 ? (time >= delay ? 1 : 0) : Math.Clamp((time - delay) / duration, 0, 1);
    }
}

/// <summary>
/// One number. Each character comes from a cached layout and every digit takes the same width, so the counting writes no new
/// text layouts and the final value reserves its width from the first frame.
/// </summary>
internal sealed class StatsBandNumber : Control
{
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<StatsBandNumber, double>(nameof(FontSize), 40);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<StatsBandNumber, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<StatsBandNumber, FontWeight>(nameof(FontWeight), FontWeight.Medium);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<StatsBandNumber, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> UnitBrushProperty =
        AvaloniaProperty.Register<StatsBandNumber, IBrush?>(nameof(UnitBrush));

    public static readonly StyledProperty<double> TrackingProperty =
        AvaloniaProperty.Register<StatsBandNumber, double>(nameof(Tracking));

    private readonly Dictionary<char, TextLayout> _glyphs = new();
    private TextLayout? _prefixLayout;
    private TextLayout? _suffixLayout;
    private double _digitWidth;
    private double _ascent;
    private CultureInfo _culture = CultureInfo.InvariantCulture;
    private double _target;
    private double _scale = 1;
    private int _decimals;
    private string _prefix = string.Empty;
    private string _suffix = string.Empty;
    private string _final = "0";
    private string _text = "0";

    static StatsBandNumber()
    {
        AffectsMeasure<StatsBandNumber>(FontSizeProperty, FontFamilyProperty, FontWeightProperty, TrackingProperty);
        AffectsRender<StatsBandNumber>(ForegroundProperty, UnitBrushProperty);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? UnitBrush { get => GetValue(UnitBrushProperty); set => SetValue(UnitBrushProperty, value); }

    public double Tracking { get => GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }

    /// <summary>The digits now on screen.</summary>
    public string DisplayText => _text;

    /// <summary>The whole final value with its prefix, unit and suffix, as screen readers get it.</summary>
    public string FinalText => _prefix + _final + _suffix;

    /// <summary>Sets what to show. A compact stat counts inside its final unit (0.0M to 2.3M).</summary>
    public void Configure(StatsBandStat stat, CultureInfo culture)
    {
        _culture = culture;
        _target = stat.Value;
        _decimals = Math.Max(0, stat.Decimals);
        var size = Math.Abs(stat.Value);
        var unit = string.Empty;
        _scale = 1;
        if (stat.Notation == StatsBandNotation.Compact)
        {
            (_scale, unit) = size >= 1e12 ? (1e12, "T") : size >= 1e9 ? (1e9, "B") : size >= 1e6 ? (1e6, "M") : size >= 1e3 ? (1e3, "K") : (1d, string.Empty);
        }
        _prefix = stat.Prefix ?? string.Empty;
        _suffix = unit + (stat.Suffix ?? string.Empty);
        _final = Format(stat.Value);
        _text = _final;
        ClearLayouts();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows <paramref name="fraction"/> of the way from zero to the final value.</summary>
    public void SetProgress(double fraction)
    {
        var text = fraction >= 1 ? _final : Format(_target * fraction);
        if (text != _text)
        {
            _text = text;
            InvalidateVisual();
        }
    }

    private string Format(double value) => (value / _scale).ToString("N" + _decimals, _culture);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == FontFamilyProperty || change.Property == FontWeightProperty
            || change.Property == ForegroundProperty || change.Property == UnitBrushProperty)
        {
            ClearLayouts();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearLayouts();
    }

    private void ClearLayouts()
    {
        foreach (var layout in _glyphs.Values)
        {
            layout.Dispose();
        }
        _glyphs.Clear();
        _prefixLayout?.Dispose();
        _suffixLayout?.Dispose();
        _prefixLayout = null;
        _suffixLayout = null;
        _digitWidth = 0;
    }

    private Typeface Face => new(FontFamily, FontStyle.Normal, FontWeight);

    private TextLayout Glyph(char c)
    {
        if (!_glyphs.TryGetValue(c, out var layout))
        {
            layout = new TextLayout(c.ToString(), Face, FontSize, Foreground);
            _glyphs[c] = layout;
        }
        return layout;
    }

    private void EnsureMetrics()
    {
        if (_digitWidth > 0)
        {
            return;
        }
        for (var c = '0'; c <= '9'; c++)
        {
            _digitWidth = Math.Max(_digitWidth, Glyph(c).Width);
        }
        _ascent = Glyph('0').Baseline;
        var unitSize = FontSize * 0.5;
        if (_prefix.Length > 0)
        {
            _prefixLayout = new TextLayout(_prefix, Face, unitSize, UnitBrush);
        }
        if (_suffix.Length > 0)
        {
            _suffixLayout = new TextLayout(_suffix, Face, unitSize, UnitBrush);
        }
    }

    private double Advance(char c) => (char.IsDigit(c) ? _digitWidth : Glyph(c).Width) + Tracking * FontSize;

    private double TextWidth(string text)
    {
        var width = 0d;
        foreach (var c in text)
        {
            width += Advance(c);
        }
        return width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();
        var width = TextWidth(_final);
        if (_prefixLayout is not null)
        {
            width += _prefixLayout.Width + FontSize * 0.08;
        }
        if (_suffixLayout is not null)
        {
            width += _suffixLayout.Width + FontSize * 0.1;
        }
        // Line height 1, as the CSS sets it; the glyphs keep their baseline inside it.
        return new Size(width, FontSize);
    }

    public override void Render(DrawingContext context)
    {
        EnsureMetrics();
        var baseline = FontSize * 0.5 + _ascent * 0.5 - (Glyph('0').Height - _ascent) * 0.25;
        var x = 0d;
        if (_prefixLayout is not null)
        {
            _prefixLayout.Draw(context, new Point(x, baseline - _prefixLayout.Baseline));
            x += _prefixLayout.Width + FontSize * 0.08;
        }
        var reserved = TextWidth(_final);
        var start = x;
        foreach (var c in _text)
        {
            // Tabular figures: each digit is centered in the one digit width.
            var glyph = Glyph(c);
            var inset = char.IsDigit(c) ? (_digitWidth - glyph.Width) / 2 : 0;
            glyph.Draw(context, new Point(x + inset, baseline - _ascent));
            x += Advance(c);
        }
        if (_suffixLayout is not null)
        {
            _suffixLayout.Draw(context, new Point(start + reserved + FontSize * 0.1, baseline - _suffixLayout.Baseline));
        }
    }
}

/// <summary>A stat's picture, drawn from its data. The entrance reads <see cref="Time"/>; hover mixes in the accent through <see cref="ActiveAmount"/>.</summary>
internal sealed class StatsBandVisualView : Control
{
    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<StatsBandVisualView, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> SoftBrushProperty =
        AvaloniaProperty.Register<StatsBandVisualView, IBrush?>(nameof(SoftBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<StatsBandVisualView, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<StatsBandVisualView, IBrush?>(nameof(PageBrush));

    private MixCache _colors;
    private StatsBandVisual? _visual;
    private int _index;
    private double _time = double.PositiveInfinity;
    private double _activeAmount;
    private double _drawDelay = 672;
    private bool _alignEnd;
    private Geometry? _trendLine;
    private Geometry? _trendArea;
    private Point _trendEnd;
    private Size _geometrySize;
    private Geometry? _landDots;
    private Geometry? _litDots;
    private double _mapScale;

    static StatsBandVisualView()
    {
        AffectsRender<StatsBandVisualView>(InkBrushProperty, SoftBrushProperty, AccentBrushProperty, PageBrushProperty);
    }

    public StatsBandVisualView()
    {
        IsHitTestVisible = false;
        _colors = new MixCache();
    }

    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    public IBrush? SoftBrush { get => GetValue(SoftBrushProperty); set => SetValue(SoftBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }

    /// <summary>The picture that this view draws.</summary>
    public StatsBandVisual? Visual
    {
        get => _visual;
        set
        {
            _visual = value;
            _geometrySize = default;
            _landDots = null;
            _litDots = null;
            InvalidateVisual();
        }
    }

    /// <summary>The stat's place in the band (it staggers the wipe).</summary>
    public int Index { get => _index; set => _index = value; }

    /// <summary>Milliseconds from a stat's start to its wipe (the band's <c>--draw</c>).</summary>
    public double DrawDelay { get => _drawDelay; set => _drawDelay = value; }

    /// <summary>On a phone the map hugs the right edge.</summary>
    public bool AlignEnd
    {
        get => _alignEnd;
        set
        {
            _alignEnd = value;
            InvalidateVisual();
        }
    }

    /// <summary>Milliseconds since the band came into view; infinity is the resting picture.</summary>
    public double Time
    {
        get => _time;
        set
        {
            _time = value;
            InvalidateVisual();
        }
    }

    /// <summary>0 is neutral, 1 is the accent.</summary>
    public double ActiveAmount
    {
        get => _activeAmount;
        set
        {
            _activeAmount = value;
            _colors.Invalidate();
            InvalidateVisual();
        }
    }

    /// <summary>The mixed ink now used (tests read it).</summary>
    internal Color InkColor => ResolveColors().Ink;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InkBrushProperty || change.Property == SoftBrushProperty
            || change.Property == AccentBrushProperty || change.Property == PageBrushProperty)
        {
            _colors.Invalidate();
        }
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    private MixCache ResolveColors()
    {
        if (!_colors.IsValid)
        {
            var accent = ColorOf(AccentBrush);
            var softActive = Oklch.MixOklab(ColorOf(PageBrush), accent, 0.42);
            _colors.Set(
                Oklch.MixOklab(ColorOf(InkBrush), accent, _activeAmount),
                Oklch.MixOklab(ColorOf(SoftBrush), softActive, _activeAmount),
                ColorOf(PageBrush));
        }
        return _colors;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (_visual is null || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var colors = ResolveColors();
        var start = _index * StatsBandTiming.Stagger + _drawDelay;
        var wipe = ArcEasings.InOut.Ease(StatsBandTiming.Progress(_time, start, StatsBandTiming.Wipe));
        switch (_visual)
        {
            case StatsBandTrend trend:
                DrawTrend(context, trend, size, colors, wipe, start);
                break;
            case StatsBandUptime uptime:
                DrawUptime(context, uptime, size, colors, start);
                break;
            case StatsBandDistribution distribution:
                DrawDistribution(context, distribution, size, colors, start);
                break;
            case StatsBandMap map:
                DrawMap(context, map, size, colors, wipe);
                break;
        }
    }

    private void DrawTrend(DrawingContext context, StatsBandTrend trend, Size size, MixCache colors, double wipe, double start)
    {
        if (trend.Values.Count == 0)
        {
            return;
        }
        // The CSS keeps 3 px free on the right for the end dot.
        var width = Math.Max(1, size.Width - 3);
        if (_trendLine is null || _geometrySize != size)
        {
            BuildTrend(trend, width, size.Height);
            _geometrySize = size;
        }
        using (context.PushClip(new Rect(-4, -8, (width + 8) * wipe + 4, size.Height + 16)))
        {
            using (context.PushOpacity(0.22))
            {
                context.DrawGeometry(colors.SoftBrush, null, _trendArea!);
            }
            context.DrawGeometry(null, colors.LinePen, _trendLine!);
        }
        var fade = StatsBandTiming.Progress(_time, start + StatsBandTiming.DotDelay, StatsBandTiming.DotFade);
        if (fade <= 0)
        {
            return;
        }
        var grow = ArcEasings.CssSpring.Ease(StatsBandTiming.Progress(_time, start + StatsBandTiming.DotDelay, StatsBandTiming.DotSpring));
        var scale = 0.3 + 0.7 * grow;
        using (context.PushOpacity(ArcEasings.Standard.Ease(fade)))
        {
            context.DrawEllipse(colors.PageSolid, null, _trendEnd, 5.5 * scale, 5.5 * scale);
            context.DrawEllipse(colors.InkBrush, null, _trendEnd, 3.5 * scale, 3.5 * scale);
        }
    }

    private void BuildTrend(StatsBandTrend trend, double width, double height)
    {
        var values = trend.Values;
        var min = values.Min();
        var max = values.Max();
        var span = max - min;
        if (span == 0)
        {
            span = 1;
        }
        // viewBox 100 x 40: y = 36 - (v - min) / span * 32.
        Point At(int i)
        {
            var x = values.Count > 1 ? (double)i / (values.Count - 1) * width : width;
            var y = (36 - (values[i] - min) / span * 32) / 40 * height;
            return new Point(x, y);
        }
        var line = new StreamGeometry();
        using (var c = line.Open())
        {
            c.BeginFigure(At(0), false);
            for (var i = 1; i < values.Count; i++)
            {
                c.LineTo(At(i));
            }
            c.EndFigure(false);
        }
        var area = new StreamGeometry();
        using (var c = area.Open())
        {
            c.BeginFigure(At(0), true);
            for (var i = 1; i < values.Count; i++)
            {
                c.LineTo(At(i));
            }
            c.LineTo(new Point(width, height));
            c.LineTo(new Point(0, height));
            c.EndFigure(true);
        }
        _trendLine = line;
        _trendArea = area;
        _trendEnd = At(values.Count - 1);
    }

    private double GrowOf(int index, int count, double start)
    {
        var j = (double)index / Math.Max(1, count);
        return ArcEasings.Enter.Ease(StatsBandTiming.Progress(_time, start + j * StatsBandTiming.BarSweep, StatsBandTiming.BarGrow));
    }

    private void DrawUptime(DrawingContext context, StatsBandUptime uptime, Size size, MixCache colors, double start)
    {
        var count = uptime.Days.Count;
        if (count == 0)
        {
            return;
        }
        // 90 days never fit 90 fixed gaps: each bar carves its gap out of its own slot, and the strip is 56% of the visual's height.
        var slot = size.Width / count;
        var strip = size.Height * 0.56;
        for (var i = 0; i < count; i++)
        {
            var day = uptime.Days[i];
            var fraction = Math.Min(1, Math.Max(0.3, 1 - (100 - day) * 0.7)) * GrowOf(i, count, start);
            var height = strip * fraction;
            context.FillRectangle(day < 100 ? colors.InkBrush : colors.SoftBrush,
                new Rect(slot * (i + 0.22), size.Height - height, slot * 0.56, height));
        }
    }

    private void DrawDistribution(DrawingContext context, StatsBandDistribution distribution, Size size, MixCache colors, double start)
    {
        var count = distribution.Bins.Count;
        if (count == 0)
        {
            return;
        }
        var peak = distribution.Bins.Max();
        if (peak <= 0)
        {
            peak = 1;
        }
        const double gap = 3;
        var binWidth = distribution.Maximum / count;
        var width = Math.Max(0.5, (size.Width - gap * (count - 1)) / count);
        for (var i = 0; i < count; i++)
        {
            var fraction = Math.Max(0.04, distribution.Bins[i] / peak) * GrowOf(i, count, start);
            var height = size.Height * fraction;
            var ink = i * binWidth < distribution.Marker;
            context.DrawRectangle(ink ? colors.InkBrush : colors.SoftBrush, null,
                new RoundedRect(new Rect(i * (width + gap), size.Height - height, width, height), 1));
        }
    }

    private void DrawMap(DrawingContext context, StatsBandMap map, Size size, MixCache colors, double wipe)
    {
        var scale = size.Height / StatsBandWorld.Rows;
        if (_landDots is null || Math.Abs(_mapScale - scale) > 0.001)
        {
            BuildMap(map);
            _mapScale = scale;
        }
        var width = StatsBandWorld.Columns * scale;
        var x = _alignEnd ? Math.Max(0, size.Width - width) : 0;
        using (context.PushClip(new Rect(x - 4, -4, (width + 8) * wipe, size.Height + 8)))
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, 0)))
        {
            context.DrawGeometry(colors.SoftBrush, null, _landDots!);
            context.DrawGeometry(colors.InkBrush, null, _litDots!);
        }
    }

    private void BuildMap(StatsBandMap map)
    {
        var lit = new HashSet<int>();
        foreach (var point in map.Points)
        {
            lit.Add(StatsBandWorld.NearestLand(point.X, point.Y));
        }
        var land = new StreamGeometry();
        using (var c = land.Open())
        {
            for (var i = 0; i < StatsBandWorld.Land.Length; i++)
            {
                if (StatsBandWorld.Land[i] && !lit.Contains(i))
                {
                    AddDot(c, i, 0.3);
                }
            }
        }
        var bright = new StreamGeometry();
        using (var c = bright.Open())
        {
            foreach (var i in lit)
            {
                AddDot(c, i, 0.5);
            }
        }
        _landDots = land;
        _litDots = bright;
    }

    private static void AddDot(StreamGeometryContext c, int index, double radius)
    {
        var cx = index % StatsBandWorld.Columns + 0.5;
        var cy = index / StatsBandWorld.Columns + 0.5;
        c.BeginFigure(new Point(cx - radius, cy), true);
        c.ArcTo(new Point(cx + radius, cy), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        c.ArcTo(new Point(cx - radius, cy), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        c.EndFigure(true);
    }

    /// <summary>The brushes and pen of the current mix, rebuilt only when a color or the amount changes.</summary>
    private sealed class MixCache
    {
        public bool IsValid { get; private set; }

        public Color Ink { get; private set; }

        public IImmutableBrush InkBrush { get; private set; } = Brushes.Transparent.ToImmutable();

        public IImmutableBrush SoftBrush { get; private set; } = Brushes.Transparent.ToImmutable();

        public IImmutableBrush PageSolid { get; private set; } = Brushes.Transparent.ToImmutable();

        public IPen LinePen { get; private set; } = new ImmutablePen(Brushes.Transparent.ToImmutable());

        public void Invalidate() => IsValid = false;

        public void Set(Color ink, Color soft, Color page)
        {
            Ink = ink;
            InkBrush = new ImmutableSolidColorBrush(ink);
            SoftBrush = new ImmutableSolidColorBrush(soft);
            PageSolid = new ImmutableSolidColorBrush(page);
            LinePen = new ImmutablePen(InkBrush, 1.5, null, PenLineCap.Round, PenLineJoin.Round);
            IsValid = true;
        }
    }
}

/// <summary>The hairlines of the divided layout: rules that draw across the band, hairlines that grow between its stats.</summary>
internal sealed class StatsBandRules : Control
{
    public static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<StatsBandRules, IBrush?>(nameof(RuleBrush));

    private double _time = double.PositiveInfinity;
    private IReadOnlyList<StatsBandItem> _items = [];
    private int _columns = 1;
    private bool _isDivided;

    static StatsBandRules()
    {
        AffectsRender<StatsBandRules>(RuleBrushProperty);
    }

    public StatsBandRules()
    {
        IsHitTestVisible = false;
    }

    public IBrush? RuleBrush { get => GetValue(RuleBrushProperty); set => SetValue(RuleBrushProperty, value); }

    public void Configure(IReadOnlyList<StatsBandItem> items, int columns, bool isDivided)
    {
        _items = items;
        _columns = columns;
        _isDivided = isDivided;
        InvalidateVisual();
    }

    public double Time
    {
        get => _time;
        set
        {
            _time = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var brush = RuleBrush;
        if (!_isDivided || brush is null)
        {
            return;
        }
        var width = Bounds.Width;
        var top = ArcEasings.InOut.Ease(StatsBandTiming.Progress(_time, 0, StatsBandTiming.Rule));
        var bottom = ArcEasings.InOut.Ease(StatsBandTiming.Progress(_time, StatsBandTiming.RuleBottomDelay, StatsBandTiming.Rule));
        context.FillRectangle(brush, new Rect(0, 0, width * top, 1));
        context.FillRectangle(brush, new Rect(0, Bounds.Height - 1, width * bottom, 1));
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i].Bounds;
            if (i % _columns != 0)
            {
                var grow = ArcEasings.Enter.Ease(StatsBandTiming.Progress(_time, i * StatsBandTiming.Stagger + StatsBandTiming.HairlineDelay, StatsBandTiming.Hairline));
                context.FillRectangle(brush, new Rect(item.X, item.Y, 1, item.Height * grow));
            }
            else if (i >= _columns)
            {
                // Rows that wrap get a rule of their own, with no entrance (the CSS draws them at once).
                context.FillRectangle(brush, new Rect(0, item.Y, width, 1));
            }
        }
    }
}

/// <summary>One stat: its number, label, caption and picture. A stat with context takes focus, so keyboard and touch can reveal the line.</summary>
[TemplatePart(NumberPart, typeof(StatsBandNumber))]
[TemplatePart(VisualPart, typeof(StatsBandVisualView))]
[PseudoClasses(ActivePseudoClass, SwapsPseudoClass)]
internal sealed class StatsBandItem : TemplatedControl
{
    public const string NumberPart = "PART_Number";
    public const string VisualPart = "PART_Visual";
    public const string ActivePseudoClass = ":active";
    public const string SwapsPseudoClass = ":swaps";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatsBandItem, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<StatsBandItem, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> ContextProperty =
        AvaloniaProperty.Register<StatsBandItem, string?>(nameof(Context));

    public static readonly StyledProperty<double> NumberSizeProperty =
        AvaloniaProperty.Register<StatsBandItem, double>(nameof(NumberSize), 40);

    private readonly TranslateTransform _rise = new();
    private StatsBandNumber? _number;
    private StatsBandVisualView? _visual;
    private Grid? _grid;
    private MotionValue<double>? _active;
    private bool _pointerActive;
    private bool _touchFocus;
    private bool _isPhone;
    private double _time = double.PositiveInfinity;
    private double _countDuration = 1600;
    private double _drawDelay = 672;

    public StatsBandItem(StatsBandStat stat, int index, CultureInfo culture)
    {
        Stat = stat;
        Index = index;
        Culture = culture;
        Label = stat.Label;
        Detail = stat.Detail;
        Context = stat.Context;
        RenderTransform = _rise;
        Focusable = !string.IsNullOrEmpty(stat.Context);
        IsTabStop = Focusable;
        PseudoClasses.Set(SwapsPseudoClass, !string.IsNullOrEmpty(stat.Context) && !string.IsNullOrEmpty(stat.Detail));
    }

    public StatsBandStat Stat { get; }

    public int Index { get; }

    public CultureInfo Culture { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }

    public double NumberSize { get => GetValue(NumberSizeProperty); set => SetValue(NumberSizeProperty, value); }

    internal StatsBandNumber? Number => _number;

    internal StatsBandVisualView? VisualView => _visual;

    internal bool IsActive => PseudoClasses.Contains(ActivePseudoClass);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _number = e.NameScope.Find<StatsBandNumber>(NumberPart);
        _visual = e.NameScope.Find<StatsBandVisualView>(VisualPart);
        _grid = e.NameScope.Find<Grid>("PART_Grid");
        _number?.Configure(Stat, Culture);
        if (_visual is not null)
        {
            _visual.Visual = Stat.Visual;
            _visual.Index = Index;
            _visual.DrawDelay = _drawDelay;
        }
        var name = _number?.FinalText + " " + Label;
        foreach (var line in new[] { Detail, Context })
        {
            if (!string.IsNullOrEmpty(line))
            {
                name += ". " + line;
            }
        }
        AutomationProperties.SetName(this, name);
        ApplyPhone();
        ApplyTime();
    }

    /// <summary>Whether the row holds a picture (the band keeps the row in every stat if any stat has one).</summary>
    public void SetHasVisualRow(bool value)
    {
        if (_visual is not null)
        {
            _visual.IsVisible = value;
        }
    }

    /// <summary>On a phone the picture moves beside the number.</summary>
    public void SetPhone(bool value)
    {
        _isPhone = value;
        ApplyPhone();
    }

    private void ApplyPhone()
    {
        if (_grid is null || _visual is null)
        {
            return;
        }
        _grid.ColumnDefinitions = _isPhone ? new ColumnDefinitions("*,0.667*") : new ColumnDefinitions("*");
        _grid.ColumnSpacing = _isPhone ? 24 : 0;
        Grid.SetRow(_visual, _isPhone ? 0 : 4);
        Grid.SetColumn(_visual, _isPhone ? 1 : 0);
        _visual.Margin = _isPhone ? new Thickness(0, 0, 0, 12) : new Thickness(0, 32, 0, 0);
        _visual.Height = _isPhone ? 40 : 56;
        _visual.VerticalAlignment = _isPhone ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _visual.AlignEnd = _isPhone;
        if (_number is not null)
        {
            _number.Margin = _isPhone ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 20);
        }
    }

    /// <summary>Sets the entrance clock: the time in ms since the band came into view.</summary>
    public void SetTime(double time, double countDuration, double drawDelay)
    {
        _time = time;
        _countDuration = countDuration;
        _drawDelay = drawDelay;
        ApplyTime();
    }

    private void ApplyTime()
    {
        var delay = Index * StatsBandTiming.Stagger;
        Opacity = ArcEasings.Enter.Ease(StatsBandTiming.Progress(_time, delay, StatsBandTiming.RiseOpacity));
        _rise.Y = 8 * (1 - ArcEasings.Enter.Ease(StatsBandTiming.Progress(_time, delay, StatsBandTiming.RiseTravel)));
        _number?.SetProgress(ArcEasings.Enter.Ease(StatsBandTiming.Progress(_time, delay, _countDuration)));
        if (_visual is not null)
        {
            _visual.DrawDelay = _drawDelay;
            _visual.Time = _time;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's (hover: hover): a touch never counts as hovering.
        if (e.Pointer.Type != PointerType.Touch)
        {
            _pointerActive = true;
            UpdateActive();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerActive = false;
        UpdateActive();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Arc's (hover: none): a tap focuses the stat, which reveals its context.
        if (e.Pointer.Type == PointerType.Touch && Focusable)
        {
            _touchFocus = true;
            Focus(NavigationMethod.Pointer);
            UpdateActive();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateActive();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _touchFocus = false;
        UpdateActive();
    }

    private void UpdateActive()
    {
        var active = _pointerActive || _touchFocus || (IsFocused && Classes.Contains(":focus-visible"));
        if (active == IsActive)
        {
            return;
        }
        PseudoClasses.Set(ActivePseudoClass, active);
        // The accent's mix with the page has no token, so the amount is driven here and the view mixes the colors.
        if (_active is null)
        {
            _active = new MotionValue<double>(this, active ? 0 : 1, MotionVectors.Double);
            _active.Changed += value =>
            {
                if (_visual is not null)
                {
                    _visual.ActiveAmount = Math.Clamp(value, 0, 1);
                }
            };
        }
        _ = _active.AnimateTo(active ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
    }
}

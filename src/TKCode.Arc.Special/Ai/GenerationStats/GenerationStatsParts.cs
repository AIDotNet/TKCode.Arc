using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The Details button of <see cref="GenerationStats"/>: "Details" and a chevron that turns when the breakdown opens; below
/// 440px only the chevron (<see cref="IsCompact"/>), keeping its name ("Show details" / "Hide details"). Screen readers get
/// its expanded state (Arc's <c>aria-expanded</c>). Enter and Space press it.
/// </summary>
internal sealed class GenerationStatsDetailsButton : ToolCallDisclosureButton
{
    internal const string CompactPseudoClass = ":compact";
    internal const string ExpandedPseudoClass = ":expanded";

    /// <summary>The word on the button.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GenerationStatsDetailsButton, string?>(nameof(Text));

    /// <summary>Only the chevron (narrow strips).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<GenerationStatsDetailsButton, bool>(nameof(IsCompact));

    protected override Type StyleKeyOverride => typeof(GenerationStatsDetailsButton);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        }
    }
}

/// <summary>
/// The four cells of the card: one row above 600px of strip width, a two by two grid below it (so the speed cell keeps room
/// for its sparkline). Every cell gets the same width; every row the height of the tallest cell.
/// </summary>
internal sealed class GenerationStatsCells : Panel
{
    /// <summary>Two by two instead of one row.</summary>
    public static readonly StyledProperty<bool> IsGridProperty =
        AvaloniaProperty.Register<GenerationStatsCells, bool>(nameof(IsGrid));

    static GenerationStatsCells()
    {
        AffectsMeasure<GenerationStatsCells>(IsGridProperty);
    }

    /// <inheritdoc cref="IsGridProperty"/>
    public bool IsGrid { get => GetValue(IsGridProperty); set => SetValue(IsGridProperty, value); }

    private int Columns => IsGrid ? 2 : Math.Max(1, Children.Count);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns;
        var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / columns;
        var rows = (Children.Count + columns - 1) / columns;
        var rowHeight = 0.0;
        var widest = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        var total = double.IsInfinity(width) ? widest * columns : availableSize.Width;
        return new Size(total, rowHeight * rows);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns;
        var rows = Math.Max(1, (Children.Count + columns - 1) / columns);
        var width = finalSize.Width / columns;
        var height = finalSize.Height / rows;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(i % columns * width, i / columns * height, width, height));
        }
        return finalSize;
    }
}

/// <summary>The hairlines between the cells: three upright lines in a row; a cross in the two by two grid.</summary>
internal sealed class GenerationStatsCellLines : Control
{
    /// <summary>Two by two instead of one row.</summary>
    public static readonly StyledProperty<bool> IsGridProperty =
        AvaloniaProperty.Register<GenerationStatsCellLines, bool>(nameof(IsGrid));

    /// <summary>The cells in a row.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<GenerationStatsCellLines, int>(nameof(Count), 4);

    /// <summary>The hairline color.</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<GenerationStatsCellLines, IBrush?>(nameof(LineBrush));

    /// <summary>How far the upright lines stop short of the top and bottom.</summary>
    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<GenerationStatsCellLines, double>(nameof(Inset), 12);

    private IPen? _pen;

    static GenerationStatsCellLines()
    {
        AffectsRender<GenerationStatsCellLines>(IsGridProperty, CountProperty, LineBrushProperty, InsetProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<GenerationStatsCellLines>(false);
    }

    /// <inheritdoc cref="IsGridProperty"/>
    public bool IsGrid { get => GetValue(IsGridProperty); set => SetValue(IsGridProperty, value); }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="LineBrushProperty"/>
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    /// <inheritdoc cref="InsetProperty"/>
    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty)
        {
            _pen = null;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        if (LineBrush is not { } brush)
        {
            return;
        }
        _pen ??= new ImmutablePen(brush.ToImmutable(), 1);
        var w = Bounds.Width;
        var h = Bounds.Height;
        var inset = Inset;
        if (IsGrid)
        {
            var x = Math.Round(w / 2) + 0.5;
            var y = Math.Round(h / 2) + 0.5;
            context.DrawLine(_pen, new Point(x, inset), new Point(x, y - inset));
            context.DrawLine(_pen, new Point(x, y + inset), new Point(x, h - inset));
            context.DrawLine(_pen, new Point(inset, y), new Point(w - inset, y));
            return;
        }
        var count = Math.Max(1, Count);
        for (var i = 1; i < count; i++)
        {
            var x = Math.Round(w * i / count) + 0.5;
            context.DrawLine(_pen, new Point(x, inset), new Point(x, h - inset));
        }
    }
}

/// <summary>
/// The speed sparkline: the smoothed readings, oldest first, as a line across the whole width over a faint area, scaled to
/// the highest reading. It is redrawn only when a reading arrives (every 160ms while streaming). Decorative.
/// </summary>
internal sealed class GenerationStatsSparkline : Control
{
    /// <summary>The line color.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<GenerationStatsSparkline, IBrush?>(nameof(Stroke));

    private readonly List<double> _samples = new();
    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private Size _builtFor;
    private IPen? _pen;
    private IBrush? _fill;

    static GenerationStatsSparkline()
    {
        AffectsRender<GenerationStatsSparkline>(StrokeProperty);
    }

    /// <inheritdoc cref="StrokeProperty"/>
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    /// <summary>The readings drawn.</summary>
    internal IReadOnlyList<double> Samples => _samples;

    /// <summary>Replaces the readings (the list is copied).</summary>
    public void SetSamples(IReadOnlyList<double>? samples)
    {
        if (samples is null ? _samples.Count == 0 : samples.SequenceEqual(_samples))
        {
            return;
        }
        _samples.Clear();
        if (samples is not null)
        {
            _samples.AddRange(samples);
        }
        _line = null;
        _area = null;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty)
        {
            _pen = null;
            _fill = null;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _line = null;
        _area = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        if (Stroke is not { } stroke || _samples.Count < 2 || Bounds.Width <= 2 || Bounds.Height <= 2)
        {
            return;
        }
        if (_pen is null)
        {
            _pen = new ImmutablePen(stroke.ToImmutable(), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var color = stroke is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            _fill = new ImmutableSolidColorBrush(color, 0.12);
        }
        if (_line is null || _builtFor != Bounds.Size)
        {
            Build(Bounds.Size);
        }
        context.DrawGeometry(_fill, null, _area!);
        context.DrawGeometry(null, _pen, _line!);
    }

    private void Build(Size size)
    {
        _builtFor = size;
        var max = Math.Max(1, _samples.Max()) * 1.1;
        var pad = 1.5;
        var w = size.Width - 2 * pad;
        var h = size.Height - 2 * pad;
        Point At(int i) => new(pad + w * i / (_samples.Count - 1), pad + h - h * Math.Clamp(_samples[i] / max, 0, 1));
        _line = new StreamGeometry();
        using (var ctx = _line.Open())
        {
            ctx.BeginFigure(At(0), false);
            for (var i = 1; i < _samples.Count; i++)
            {
                ctx.LineTo(At(i));
            }
            ctx.EndFigure(false);
        }
        _area = new StreamGeometry();
        using (var ctx = _area.Open())
        {
            ctx.BeginFigure(new Point(pad, size.Height - pad), true);
            for (var i = 0; i < _samples.Count; i++)
            {
                ctx.LineTo(At(i));
            }
            ctx.LineTo(new Point(size.Width - pad, size.Height - pad));
            ctx.EndFigure(true);
        }
    }
}

/// <summary>
/// The latency bar of the breakdown: the wait for the first token, then the generation, as two pill segments on a track,
/// in proportion to the total. Decorative: the values are listed beside it in text.
/// </summary>
internal sealed class GenerationStatsLatencyBar : Control
{
    /// <summary>The first segment's color.</summary>
    public static readonly StyledProperty<IBrush?> FirstBrushProperty =
        AvaloniaProperty.Register<GenerationStatsLatencyBar, IBrush?>(nameof(FirstBrush));

    /// <summary>The second segment's color.</summary>
    public static readonly StyledProperty<IBrush?> GenerationBrushProperty =
        AvaloniaProperty.Register<GenerationStatsLatencyBar, IBrush?>(nameof(GenerationBrush));

    /// <summary>The track's color.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<GenerationStatsLatencyBar, IBrush?>(nameof(TrackBrush));

    private double? _first;
    private double? _total;

    static GenerationStatsLatencyBar()
    {
        AffectsRender<GenerationStatsLatencyBar>(FirstBrushProperty, GenerationBrushProperty, TrackBrushProperty);
    }

    /// <inheritdoc cref="FirstBrushProperty"/>
    public IBrush? FirstBrush { get => GetValue(FirstBrushProperty); set => SetValue(FirstBrushProperty, value); }

    /// <inheritdoc cref="GenerationBrushProperty"/>
    public IBrush? GenerationBrush { get => GetValue(GenerationBrushProperty); set => SetValue(GenerationBrushProperty, value); }

    /// <inheritdoc cref="TrackBrushProperty"/>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <summary>The share of the bar the wait for the first token takes (0 to 1), or null with no times.</summary>
    internal double? FirstShare => _total is > 0 && _first is { } f ? Math.Clamp(f / _total.Value, 0, 1) : null;

    /// <summary>Sets the times in milliseconds (null when unknown).</summary>
    public void SetTimes(double? firstMs, double? totalMs)
    {
        if (_first == firstMs && _total == totalMs)
        {
            return;
        }
        _first = firstMs;
        _total = totalMs;
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var radius = h / 2;
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, new RoundedRect(new Rect(0, 0, w, h), radius));
        }
        if (FirstShare is not { } share)
        {
            return;
        }
        const double gap = 2;
        var firstWidth = w * share;
        if (FirstBrush is { } first && firstWidth > 0.5)
        {
            context.DrawRectangle(first, null, new RoundedRect(new Rect(0, 0, Math.Max(h, firstWidth), h), radius));
        }
        var rest = w - firstWidth - (firstWidth > 0.5 ? gap : 0);
        if (GenerationBrush is { } generation && rest > 0.5 && share < 1)
        {
            context.DrawRectangle(generation, null, new RoundedRect(new Rect(w - rest, 0, Math.Max(h, rest), h), radius));
        }
    }
}

/// <summary>
/// The model's mark: the app's own glyph when the model has one (a 24 × 24 stroked geometry), otherwise a neutral monogram,
/// the first letter of the model's name on a rounded square. No brand marks are drawn.
/// </summary>
internal sealed class GenerationStatsMark : Control
{
    /// <summary>The app's glyph, or null for the monogram.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<GenerationStatsMark, Geometry?>(nameof(Glyph));

    /// <summary>The glyph's color; null uses <see cref="Foreground"/>.</summary>
    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<GenerationStatsMark, IBrush?>(nameof(GlyphBrush));

    /// <summary>The monogram's letter.</summary>
    public static readonly StyledProperty<string?> LetterProperty =
        AvaloniaProperty.Register<GenerationStatsMark, string?>(nameof(Letter));

    /// <summary>The monogram's square.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<GenerationStatsMark, IBrush?>(nameof(Background));

    /// <summary>The letter's color and the glyph's default color.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<GenerationStatsMark>();

    /// <summary>The letter's font.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<GenerationStatsMark>();

    private TextLayout? _letter;
    private readonly Dictionary<IBrush, IPen> _pens = new();

    static GenerationStatsMark()
    {
        AffectsRender<GenerationStatsMark>(GlyphProperty, GlyphBrushProperty, BackgroundProperty, ForegroundProperty);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="GlyphBrushProperty"/>
    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    /// <inheritdoc cref="LetterProperty"/>
    public string? Letter { get => GetValue(LetterProperty); set => SetValue(LetterProperty, value); }

    /// <inheritdoc cref="BackgroundProperty"/>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The monogram letter for a model name: its first letter or digit, upper case.</summary>
    internal static string LetterOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                return char.ToUpper(c, CultureInfo.InvariantCulture).ToString();
            }
        }
        return "?";
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LetterProperty || change.Property == FontFamilyProperty || change.Property == BoundsProperty
            || change.Property == ForegroundProperty)
        {
            _letter?.Dispose();
            _letter = null;
            InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        var origin = new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        if (Glyph is { } glyph)
        {
            var brush = GlyphBrush ?? Foreground;
            if (brush is null)
            {
                return;
            }
            if (!_pens.TryGetValue(brush, out var pen))
            {
                pen = new ImmutablePen(brush.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                _pens[brush] = pen;
            }
            using (context.PushTransform(Matrix.CreateScale(size / 24, size / 24) * Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.DrawGeometry(null, pen, glyph);
            }
            return;
        }
        if (Background is { } background)
        {
            context.DrawRectangle(background, null, new RoundedRect(new Rect(origin, new Size(size, size)), size * 0.3));
        }
        if (Foreground is not { } foreground)
        {
            return;
        }
        _letter ??= new TextLayout(Letter ?? "?", new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold), size * 0.55, foreground);
        var x = origin.X + (size - _letter.WidthIncludingTrailingWhitespace) / 2;
        var y = origin.Y + (size - _letter.Height) / 2;
        _letter.Draw(context, new Point(x, y));
    }
}

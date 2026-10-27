using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the hover card says: the month and three rows (the two values and their ratio).</summary>
internal sealed record ScatterCardContent(string Title, string XName, string XValue, string YName, string YValue, string RatioName,
    string RatioValue, bool IsHealthy);

/// <summary>
/// The hover card (private copy of the readout card because its ratio value takes the status colour): 176 wide, padding
/// 10 / 12, radius 14, floating shadow, the month in Medium and three rows. It fades in place in 160ms; moving between
/// points it glides on the 580ms CSS spring curve.
/// </summary>
internal sealed class ScatterCard
{
    private const double PaddingX = 12;
    private const double PaddingY = 10;
    private const double Line = 16.8;
    private const double TitleGap = 6;
    private const double RowGap = 2;
    private const double Radius = 14;

    private AnalyticsTween _x = new(0);
    private AnalyticsTween _y = new(0);
    private AnalyticsTween _opacity = new(0);
    private ScatterCardContent? _content;
    private bool _shown;
    private Size _size;
    private double _width;

    /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
    public bool IsShown => _shown;

    /// <summary>True while drawn (shown or fading out).</summary>
    public bool IsVisible => _shown || _opacity.Value > 0.001;

    /// <summary>The content drawn now.</summary>
    public ScatterCardContent? Content => _content;

    /// <summary>Current top-left position.</summary>
    public Point Position => new(_x.Value, _y.Value);

    /// <summary>Current opacity.</summary>
    public double Opacity => Math.Clamp(_opacity.Value, 0, 1);

    /// <summary>True while a fade or a glide runs.</summary>
    public bool IsMoving => _x.IsRunning || _y.IsRunning || _opacity.IsRunning;

    /// <summary>Forgets the measured size (a theme or font change).</summary>
    public void Invalidate() => _size = default;

    /// <summary>Replaces the content.</summary>
    public void SetContent(ScatterCardContent content, double width)
    {
        if (!Equals(_content, content) || Math.Abs(width - _width) > 0.01)
        {
            _size = default;
        }
        _content = content;
        _width = width;
    }

    /// <summary>Shows the card with its top-left at <paramref name="position"/>: in place when hidden, a glide when shown.</summary>
    public void Show(Point position, ArcMotionLevel level)
    {
        var wasVisible = IsVisible;
        if (!wasVisible || level != ArcMotionLevel.Full)
        {
            _x.Jump(position.X);
            _y.Jump(position.Y);
        }
        else
        {
            // The outer layer slides on the 580ms CSS spring when the card moves from one point to the next.
            var glide = TimeSpan.FromMilliseconds(580);
            _x.Start(position.X, glide, ArcEasings.CssSpring);
            _y.Start(position.Y, glide, ArcEasings.CssSpring);
        }
        if (_shown)
        {
            return;
        }
        _shown = true;
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(1);
            return;
        }
        if (!wasVisible)
        {
            _opacity.Jump(0);
        }
        _opacity.Start(1, ArcMotionPresets.Fast, ArcEasings.Standard);
    }

    /// <summary>Fades the card out where it is.</summary>
    public void Hide(ArcMotionLevel level)
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
            return;
        }
        _opacity.Start(0, ArcMotionPresets.Fast, ArcEasings.Standard);
    }

    /// <summary>Advances the fade and the glide; true while moving.</summary>
    public bool Advance(double dt) => _x.Advance(dt) | _y.Advance(dt) | _opacity.Advance(dt);

    /// <summary>The card's size at <paramref name="width"/>.</summary>
    public Size Measure(AnalyticsTextCache cache, AnalyticsPalette palette, double width)
    {
        if (_size.Width > 0 && Math.Abs(_size.Width - width) < 0.01)
        {
            return _size;
        }
        var height = PaddingY * 2 + Line + TitleGap + Line * 3 + RowGap * 2;
        _size = new Size(width, Math.Ceiling(height));
        return _size;
    }

    /// <summary>Draws the card.</summary>
    public void Draw(DrawingContext context, AnalyticsTextCache cache, AnalyticsPalette palette, double width)
    {
        if (!IsVisible || _content is not { } c || !palette.IsReady)
        {
            return;
        }
        var size = Measure(cache, palette, width);
        var rect = new Rect(Position, size);
        using (context.PushOpacity(Opacity))
        {
            context.DrawRectangle(palette.Brush(palette.SurfaceRaised), palette.Pen(palette.Border, 1),
                new RoundedRect(rect.Deflate(0.5), Radius), palette.ShadowFloating);
            var body = new Typeface(palette.BodyFont);
            var medium = new Typeface(palette.BodyFont, FontStyle.Normal, palette.Medium);
            var left = rect.X + PaddingX;
            var right = rect.Right - PaddingX;
            var y = rect.Y + PaddingY;
            var title = cache.GetTrimmed(c.Title, medium, 12, right - left);
            title.Draw(context, palette.Brush(palette.Foreground), new Point(left, y + (Line - title.Height) / 2));
            y += Line + TitleGap;
            DrawRow(context, cache, palette, body, medium, left, right, y, c.XName, c.XValue, palette.Foreground);
            y += Line + RowGap;
            DrawRow(context, cache, palette, body, medium, left, right, y, c.YName, c.YValue, palette.Foreground);
            y += Line + RowGap;
            DrawRow(context, cache, palette, body, medium, left, right, y, c.RatioName, c.RatioValue, c.IsHealthy ? palette.Success : palette.Warning);
        }
    }

    private static void DrawRow(DrawingContext context, AnalyticsTextCache cache, AnalyticsPalette palette, Typeface body, Typeface medium,
        double left, double right, double y, string label, string value, Color valueColor)
    {
        var valueText = cache.Get(value, medium, 12, true);
        valueText.Draw(context, palette.Brush(valueColor), new Point(right - valueText.Width, y + (Line - valueText.Height) / 2));
        var labelText = cache.GetTrimmed(label, body, 12, Math.Max(1, right - left - valueText.Width - 12));
        labelText.Draw(context, palette.Brush(palette.TextSecondary), new Point(left, y + (Line - labelText.Height) / 2));
    }
}

/// <summary>
/// Two children on one line, the first at the start and the second at the end (CSS <c>space-between</c> with
/// <c>flex-wrap</c>): when they do not fit beside each other with <see cref="ColumnGap"/> they stack at the start with
/// <see cref="RowGap"/>. Private copy of the Cohort retention panel; used for the header and the chart header.
/// </summary>
internal sealed class ConnectedScatterSplitPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ConnectedScatterSplitPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ConnectedScatterSplitPanel, double>(nameof(RowGap), 12);

    static ConnectedScatterSplitPanel()
    {
        AffectsMeasure<ConnectedScatterSplitPanel>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>True when the last layout stacked the children.</summary>
    public bool IsWrapped { get; private set; }

    private (Control? Lead, Control? Trail) Parts()
    {
        Control? lead = null;
        Control? trail = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (lead is null)
            {
                lead = child;
            }
            else if (trail is null)
            {
                trail = child;
            }
        }
        return (lead, trail);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (lead, trail) = Parts();
        var width = availableSize.Width;
        if (lead is null)
        {
            IsWrapped = false;
            return default;
        }
        if (trail is null)
        {
            IsWrapped = false;
            lead.Measure(availableSize);
            return lead.DesiredSize;
        }
        trail.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        lead.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        IsWrapped = lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width > width;
        if (IsWrapped)
        {
            lead.Measure(new Size(width, double.PositiveInfinity));
            trail.Measure(new Size(width, double.PositiveInfinity));
            return new Size(Math.Max(lead.DesiredSize.Width, trail.DesiredSize.Width), lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height);
        }
        lead.Measure(new Size(Math.Max(0, width - ColumnGap - trail.DesiredSize.Width), availableSize.Height));
        return new Size(lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width, Math.Max(lead.DesiredSize.Height, trail.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (lead, trail) = Parts();
        if (lead is null)
        {
            return finalSize;
        }
        if (trail is null)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), finalSize.Height));
            return finalSize;
        }
        if (IsWrapped)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), lead.DesiredSize.Height));
            trail.Arrange(new Rect(0, lead.DesiredSize.Height + RowGap, Math.Min(finalSize.Width, trail.DesiredSize.Width), trail.DesiredSize.Height));
            return finalSize;
        }
        lead.Arrange(new Rect(0, Offset(lead, finalSize.Height), lead.DesiredSize.Width, lead.DesiredSize.Height));
        var trailWidth = trail.DesiredSize.Width;
        trail.Arrange(new Rect(finalSize.Width - trailWidth, Offset(trail, finalSize.Height), trailWidth, trail.DesiredSize.Height));
        return finalSize;
    }

    private static double Offset(Control child, double height) => child.VerticalAlignment switch
    {
        VerticalAlignment.Center => (height - child.DesiredSize.Height) / 2,
        VerticalAlignment.Bottom => height - child.DesiredSize.Height,
        _ => 0
    };
}

/// <summary>What the readout strip shows: four cells, each a name, a value and a note; the last has a status dot.</summary>
internal sealed record ScatterReadoutState(string MonthName, string MonthValue, string MonthNote, string XName, string XValue, string XNote,
    string YName, string YValue, string YNote, string RatioName, string RatioValue, string RatioNote, bool IsHealthy, bool HasRatio)
{
    /// <summary>The state for no data: dashes.</summary>
    public static ScatterReadoutState Empty { get; } = new(string.Empty, AnalyticsFormat.Missing, string.Empty, string.Empty,
        AnalyticsFormat.Missing, string.Empty, string.Empty, AnalyticsFormat.Missing, string.Empty, string.Empty, AnalyticsFormat.Missing,
        string.Empty, true, false);
}

/// <summary>
/// The four readout cells above the chart (Arc's <c>dl</c>): month with "n of N", the two values with their change since
/// the first month, and the ratio with a status dot. Four equal columns divided by 1px lines from 720 wide, a 2 × 2 grid
/// under it; the value is 22px (18 under 380). Self-drawn: the values change on every frame while the head moves.
/// </summary>
internal sealed class ConnectedScatterReadoutStrip : Control
{
    public static readonly StyledProperty<Thickness> CellPaddingProperty =
        AvaloniaProperty.Register<ConnectedScatterReadoutStrip, Thickness>(nameof(CellPadding), new Thickness(16, 14));
    public static readonly StyledProperty<Thickness> TinyCellPaddingProperty =
        AvaloniaProperty.Register<ConnectedScatterReadoutStrip, Thickness>(nameof(TinyCellPadding), new Thickness(12));
    public static readonly StyledProperty<double> ValueSizeProperty =
        AvaloniaProperty.Register<ConnectedScatterReadoutStrip, double>(nameof(ValueSize), 22);
    public static readonly StyledProperty<double> TinyValueSizeProperty =
        AvaloniaProperty.Register<ConnectedScatterReadoutStrip, double>(nameof(TinyValueSize), 18);

    private const double NameLine = 16.8;
    private const double NameGap = 4;
    private const double ValueGap = 2;
    private const double NoteLine = 16.8;
    private const double DotSize = 6;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly LiveLoop _loop;
    private ScatterReadoutState _state = ScatterReadoutState.Empty;
    private AnalyticsTween _tone = new(0);
    private int _paletteVersion = -1;

    static ConnectedScatterReadoutStrip()
    {
        AffectsMeasure<ConnectedScatterReadoutStrip>(CellPaddingProperty, TinyCellPaddingProperty, ValueSizeProperty, TinyValueSizeProperty);
        AffectsRender<ConnectedScatterReadoutStrip>(CellPaddingProperty);
        FocusableProperty.OverrideDefaultValue<ConnectedScatterReadoutStrip>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ConnectedScatterReadoutStrip>(false);
    }

    public ConnectedScatterReadoutStrip()
    {
        _loop = new LiveLoop(this, dt =>
        {
            var moving = _tone.Advance(dt);
            InvalidateVisual();
            return moving;
        }) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
    }

    public Thickness CellPadding { get => GetValue(CellPaddingProperty); set => SetValue(CellPaddingProperty, value); }

    public Thickness TinyCellPadding { get => GetValue(TinyCellPaddingProperty); set => SetValue(TinyCellPaddingProperty, value); }

    public double ValueSize { get => GetValue(ValueSizeProperty); set => SetValue(ValueSizeProperty, value); }

    public double TinyValueSize { get => GetValue(TinyValueSizeProperty); set => SetValue(TinyValueSizeProperty, value); }

    /// <summary>The cells' contents.</summary>
    public ScatterReadoutState State => _state;

    /// <summary>The status dot's colour mix now: 0 = Success, 1 = Warning (tests).</summary>
    internal double ToneMix => _tone.Value;

    /// <summary>Shows new texts; the dot's colour moves in 160ms when the ratio crosses the target.</summary>
    public void SetState(ScatterReadoutState state)
    {
        var toneChanged = state.IsHealthy != _state.IsHealthy;
        var widthChanged = state.MonthValue.Length != _state.MonthValue.Length;
        _state = state;
        if (toneChanged)
        {
            var target = state.IsHealthy ? 0.0 : 1.0;
            if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
            {
                _tone.Jump(target);
            }
            else
            {
                _tone.Start(target, ArcMotionPresets.Fast, ArcEasings.Standard);
                _loop.Start();
            }
        }
        if (widthChanged)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private void Invalidate()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private bool IsTiny(double width) => new ScatterBreakpoints(width).IsTiny;

    private double CellHeight(bool tiny)
    {
        var pad = tiny ? TinyCellPadding : CellPadding;
        var valueSize = tiny ? TinyValueSize : ValueSize;
        return pad.Top + NameLine + NameGap + ValueLine(valueSize) + ValueGap + NoteLine + pad.Bottom;
    }

    private static double ValueLine(double size) => Math.Round(size * 1.4, 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        var tiny = IsTiny(width);
        var rows = new ScatterBreakpoints(width).HasFourColumns ? 1 : 2;
        return new Size(width, CellHeight(tiny) * rows + (rows - 1));
    }

    public override void Render(DrawingContext context)
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_paletteVersion < 0 || !_palette.IsReady)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
        var p = _palette;
        var width = Bounds.Width;
        var four = new ScatterBreakpoints(width).HasFourColumns;
        var tiny = IsTiny(width);
        var pad = tiny ? TinyCellPadding : CellPadding;
        var valueSize = tiny ? TinyValueSize : ValueSize;
        var cellHeight = CellHeight(tiny);
        var columns = four ? 4 : 2;
        var cellWidth = width / columns;
        var line = p.Brush(p.BorderSubtle);
        var s = _state;
        string[,] cells =
        {
            { s.MonthName, s.MonthValue, s.MonthNote },
            { s.XName, s.XValue, s.XNote },
            { s.YName, s.YValue, s.YNote },
            { s.RatioName, s.RatioValue, s.RatioNote }
        };
        var body = new Typeface(p.BodyFont);
        var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        for (var i = 0; i < 4; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var left = Math.Round(column * cellWidth);
            var top = row * (cellHeight + 1);
            if (column > 0)
            {
                context.FillRectangle(line, new Rect(left, top, 1, cellHeight));
            }
            if (row > 0)
            {
                context.FillRectangle(line, new Rect(column == 0 ? 0 : left, top - 1, column == 0 ? cellWidth : cellWidth + 1, 1));
            }
            var x = left + (column > 0 ? 1 : 0) + pad.Left;
            var maxWidth = Math.Max(1, cellWidth - pad.Left - pad.Right - (column > 0 ? 1 : 0));
            var y = top + pad.Top;
            var name = _text.GetTrimmed(cells[i, 0], body, 12, maxWidth);
            name.Draw(context, p.Brush(p.TextSecondary), new Point(x, y + (NameLine - name.Height) / 2));
            y += NameLine + NameGap;
            var value = _text.GetTrimmed(cells[i, 1], medium, valueSize, maxWidth, true);
            var valueLine = ValueLine(valueSize);
            value.Draw(context, p.Brush(p.Foreground), new Point(x, y + (valueLine - value.Height) / 2));
            y += valueLine + ValueGap;
            var noteX = x;
            if (i == 3 && s.HasRatio)
            {
                var color = AnalyticsColors.Lerp(p.Success, p.Warning, _tone.Value);
                context.DrawEllipse(p.Brush(color), null, new Point(x + DotSize / 2, y + NoteLine / 2), DotSize / 2, DotSize / 2);
                noteX += DotSize + 6;
            }
            var note = _text.GetTrimmed(cells[i, 2], body, 12, Math.Max(1, maxWidth - (noteX - x)), true);
            note.Draw(context, p.Brush(p.TextMuted), new Point(noteX, y + (NoteLine - note.Height) / 2));
        }
    }
}

/// <summary>
/// The chart legend: a solid sample for the series, a dashed one for the target line and a green square for the healthy
/// zone, each with its text (12px TextSecondary), 16 apart. Items wrap onto a second line when they do not fit.
/// </summary>
internal sealed class ConnectedScatterLegend : Control
{
    public static readonly StyledProperty<double> ItemGapProperty =
        AvaloniaProperty.Register<ConnectedScatterLegend, double>(nameof(ItemGap), 16);

    private const double Line = 16.8;
    private const double RowGap = 4;
    private const double Inner = 8;
    private const double SampleWidth = 16;
    private const double SquareSize = 12;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private string[] _items = [];
    private int _paletteVersion = -1;

    static ConnectedScatterLegend()
    {
        AffectsMeasure<ConnectedScatterLegend>(ItemGapProperty);
        FocusableProperty.OverrideDefaultValue<ConnectedScatterLegend>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ConnectedScatterLegend>(false);
    }

    public ConnectedScatterLegend()
    {
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
    }

    public double ItemGap { get => GetValue(ItemGapProperty); set => SetValue(ItemGapProperty, value); }

    /// <summary>The legend's texts: series, target line, healthy zone (the last two are left out without a target).</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>Sets the texts; the target and zone items disappear when <paramref name="showTarget"/> is false.</summary>
    public void SetItems(string series, string target, string zone, bool showTarget)
    {
        _items = showTarget ? [series, target, zone] : [series];
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void Invalidate()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private double ItemWidth(int index, Typeface face)
    {
        var sample = index == 2 ? SquareSize : SampleWidth;
        return sample + Inner + _text.Get(_items[index], face, 12).Width;
    }

    private void EnsurePalette()
    {
        if (_paletteVersion < 0 || !_palette.IsReady)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
    }

    private int RowCount(double maxWidth, Typeface face, Span<int> rowOf)
    {
        var rows = 0;
        var x = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            var w = ItemWidth(i, face);
            if (i > 0 && x + ItemGap + w > maxWidth)
            {
                rows++;
                x = 0;
            }
            else if (i > 0)
            {
                x += ItemGap;
            }
            rowOf[i] = rows;
            x += w;
        }
        return rows + 1;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_items.Length == 0 || !this.IsAttachedToVisualTree())
        {
            return new Size(0, _items.Length == 0 ? 0 : Line);
        }
        EnsurePalette();
        var face = new Typeface(_palette.BodyFont);
        var natural = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            natural += ItemWidth(i, face) + (i > 0 ? ItemGap : 0);
        }
        if (double.IsInfinity(availableSize.Width) || natural <= availableSize.Width)
        {
            return new Size(natural, Line);
        }
        Span<int> rowOf = stackalloc int[_items.Length];
        var rows = RowCount(availableSize.Width, face, rowOf);
        return new Size(availableSize.Width, rows * Line + (rows - 1) * RowGap);
    }

    public override void Render(DrawingContext context)
    {
        if (_items.Length == 0 || !this.IsAttachedToVisualTree())
        {
            return;
        }
        EnsurePalette();
        var p = _palette;
        var face = new Typeface(p.BodyFont);
        Span<int> rowOf = stackalloc int[_items.Length];
        RowCount(Math.Max(1, Bounds.Width), face, rowOf);
        var x = 0.0;
        var lastRow = 0;
        for (var i = 0; i < _items.Length; i++)
        {
            if (rowOf[i] != lastRow)
            {
                lastRow = rowOf[i];
                x = 0;
            }
            else if (i > 0)
            {
                x += ItemGap;
            }
            var top = rowOf[i] * (Line + RowGap);
            var mid = top + Line / 2;
            switch (i)
            {
                case 0:
                    context.DrawLine(p.Pen(p.Accent, 2), new Point(x + 1, mid), new Point(x + SampleWidth - 1, mid));
                    break;
                case 1:
                    context.DrawLine(p.Pen(p.TextMuted, 1, 1, 3, 2, PenLineCap.Flat), new Point(x, mid), new Point(x + SampleWidth, mid));
                    break;
                default:
                    var square = new Rect(x, mid - SquareSize / 2, SquareSize, SquareSize);
                    context.DrawRectangle(p.Brush(p.Success, 0.14), null, new RoundedRect(square, 3));
                    context.DrawRectangle(null, p.Pen(p.Success, 1, 0.22), new RoundedRect(square.Deflate(0.5), 2.5));
                    break;
            }
            var text = _text.Get(_items[i], face, 12);
            var sample = i == 2 ? SquareSize : SampleWidth;
            text.Draw(context, p.Brush(p.TextSecondary), new Point(x + sample + Inner, top + (Line - text.Height) / 2));
            x += ItemWidth(i, face);
        }
    }
}

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The three keyboard stops of the BrushChart overview.</summary>
internal enum BrushChartThumbKind
{
    Window,
    Start,
    End
}

/// <summary>
/// BrushChart's overview strip: the whole history (drawn once per width), everything outside the window stepped
/// back, the window ring and two grips. Grab an edge to resize, the window to pan, or empty track to draw a new
/// window; a tap recenters, a double tap resets. The window and its handles are separate keyboard stops.
/// </summary>
internal sealed class BrushChartOverview : Control, Avalonia.Rendering.ICustomHitTest
{
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double Grip = 14;
    private const double Radius = 10;
    private readonly BrushChartThumb _window;
    private readonly BrushChartThumb _start;
    private readonly BrushChartThumb _end;
    private readonly ChartDragTracker _drag = new();
    private (double Start, double End) _dragFrom;
    private double _anchor;
    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private readonly List<Point> _marks = new();
    private double _builtWidth = -1;
    private int _builtVersion;
    private int _dataVersion;
    private BrushChart? _owner;

    public BrushChartOverview()
    {
        _window = new BrushChartThumb(this, BrushChartThumbKind.Window) { Cursor = new Cursor(StandardCursorType.SizeAll) };
        _start = new BrushChartThumb(this, BrushChartThumbKind.Start) { Cursor = new Cursor(StandardCursorType.SizeWestEast) };
        _end = new BrushChartThumb(this, BrushChartThumbKind.End) { Cursor = new Cursor(StandardCursorType.SizeWestEast) };
        foreach (var thumb in new[] { _window, _start, _end })
        {
            VisualChildren.Add(thumb);
            LogicalChildren.Add(thumb);
        }
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = false;
    }

    internal BrushChart? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            Rebuild();
        }
    }

    internal void Rebuild()
    {
        _dataVersion++;
        var empty = _owner?.IsEmptySeries ?? true;
        _window.IsVisible = !empty;
        _start.IsVisible = !empty;
        _end.IsVisible = !empty;
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private double Ox(double time, double width)
    {
        var owner = _owner!;
        return (time - owner.First) / Math.Max(1, owner.Last - owner.First) * width;
    }

    private double ToTime(double x, double width)
    {
        var owner = _owner!;
        return owner.First + Math.Clamp(x / Math.Max(1, width), 0, 1) * (owner.Last - owner.First);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = _owner?.OverviewHeight ?? 52;
        foreach (var thumb in new[] { _window, _start, _end })
        {
            thumb.Measure(new Size(double.PositiveInfinity, height));
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_owner is null || _owner.IsEmptySeries)
        {
            return finalSize;
        }
        var (start, end) = _owner.View;
        var wx0 = Ox(start, finalSize.Width);
        var wx1 = Ox(end, finalSize.Width);
        _window.Arrange(new Rect(wx0, 0, Math.Max(0, wx1 - wx0), finalSize.Height));
        _start.Arrange(new Rect(wx0 - 14, (finalSize.Height - 44) / 2, 28, 44));
        _end.Arrange(new Rect(wx1 - 14, (finalSize.Height - 44) / 2, 28, 44));
        return finalSize;
    }

    /// <summary>The overview path: the whole series' seven-point average, built once per width.</summary>
    private void Build(double width, double height)
    {
        var owner = _owner!;
        _builtWidth = width;
        _builtVersion = _dataVersion;
        _marks.Clear();
        var smooth = owner.Smooth;
        var times = owner.Times;
        if (smooth.Length < 2)
        {
            _line = null;
            _area = null;
            return;
        }
        double peak = 0;
        foreach (var point in smooth)
        {
            peak = Math.Max(peak, point.Value);
        }
        var max = ChartTicks.TopFromZero(peak).Max;
        if (max <= 0)
        {
            max = 1;
        }
        double Oy(double value) => 4 + (1 - value / max) * (height - 4);
        var points = new List<Point>();
        var budget = width * 4;
        if (smooth.Length <= budget)
        {
            for (var i = 0; i < smooth.Length; i++)
            {
                points.Add(new Point(Ox(times[i], width), Oy(smooth[i].Value)));
            }
        }
        else
        {
            var column = int.MinValue;
            int low = -1, high = -1;
            void Flush()
            {
                if (low < 0)
                {
                    return;
                }
                var (a, b) = low < high ? (low, high) : (high, low);
                points.Add(new Point(Ox(times[a], width), Oy(smooth[a].Value)));
                if (b != a)
                {
                    points.Add(new Point(Ox(times[b], width), Oy(smooth[b].Value)));
                }
            }
            for (var i = 0; i < smooth.Length; i++)
            {
                var at = (int)Math.Floor(Ox(times[i], width));
                if (at != column)
                {
                    Flush();
                    column = at;
                    low = high = i;
                    continue;
                }
                if (smooth[i].Value < smooth[low].Value)
                {
                    low = i;
                }
                if (smooth[i].Value > smooth[high].Value)
                {
                    high = i;
                }
            }
            Flush();
        }
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(points);
        _line = ChartGeometry.Line(span);
        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(span[0], true);
            for (var i = 1; i < span.Length; i++)
            {
                context.LineTo(span[i]);
            }
            context.LineTo(new Point(width, height));
            context.LineTo(new Point(0, height));
            context.EndFigure(true);
        }
        _area = area;
        foreach (var e in owner.Events)
        {
            var at = Math.Clamp(BrushChart.LowerBound(times, e.Time), 0, smooth.Length - 1);
            _marks.Add(new Point(Ox(e.Time, width), Oy(smooth[at].Value)));
        }
    }

    public override void Render(DrawingContext context)
    {
        var owner = _owner;
        if (owner is null)
        {
            return;
        }
        owner.EnsurePalette();
        var palette = owner.Palette;
        var size = Bounds.Size;
        var strip = new RoundedRect(new Rect(size), Radius);
        context.DrawRectangle(palette.Brush(Oklch.WithAlpha(palette.Foreground, 0.025)), null, strip);
        if (owner.IsEmptySeries)
        {
            return;
        }
        if (Math.Abs(_builtWidth - size.Width) > 0.5 || _builtVersion != _dataVersion || _line is null)
        {
            Build(size.Width, size.Height);
        }
        var (start, end) = owner.View;
        var wx0 = Ox(start, size.Width);
        var wx1 = Ox(end, size.Width);
        var series = palette.Series[0];
        using (context.PushClip(strip))
        {
            if (_area is not null && _line is not null)
            {
                context.DrawGeometry(palette.Brush(Oklch.WithAlpha(palette.Foreground, 0.04)), null, _area);
                context.DrawGeometry(null, palette.Pen(ChartPalette.Mix(palette.Foreground, 0.28, palette.Surface), 1), _line);
                using (context.PushClip(new Rect(wx0, -2, Math.Max(0, wx1 - wx0), size.Height + 4)))
                {
                    context.DrawGeometry(palette.Brush(Oklch.WithAlpha(series, 0.12)), null, _area);
                    context.DrawGeometry(null, palette.Pen(series, 1), _line);
                }
            }
            var markPen = palette.Pen(palette.Surface, 1.5);
            var markBrush = palette.Brush(palette.Series[1]);
            foreach (var mark in _marks)
            {
                context.DrawEllipse(markBrush, markPen, mark, 2.5, 2.5);
            }
            // Everything outside the window steps back.
            var shade = palette.Brush(Oklch.WithAlpha(palette.Surface, 0.45));
            if (wx0 > 0)
            {
                context.DrawRectangle(shade, null, new Rect(0, 0, wx0, size.Height));
            }
            if (wx1 < size.Width)
            {
                context.DrawRectangle(shade, null, new Rect(wx1, 0, size.Width - wx1, size.Height));
            }
        }
    }

    #region Gestures

    private int ModeAt(double px, double width)
    {
        var (start, end) = _owner!.View;
        var wx0 = Ox(start, width);
        var wx1 = Ox(end, width);
        if (Math.Abs(px - wx0) <= Grip && px <= (wx0 + wx1) / 2)
        {
            return (int)BrushChartThumbKind.Start;
        }
        if (Math.Abs(px - wx1) <= Grip)
        {
            return (int)BrushChartThumbKind.End;
        }
        return px > wx0 && px < wx1 ? (int)BrushChartThumbKind.Window : 3;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var owner = _owner;
        if (owner is null || owner.IsEmptySeries || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.ClickCount == 2)
        {
            _drag.End();
            owner.Reset();
            e.Handled = true;
            return;
        }
        var x = e.GetPosition(this).X;
        _drag.Begin(e, this, ModeAt(x, Bounds.Width));
        _dragFrom = owner.View;
        _anchor = ToTime(x, Bounds.Width);
        owner.Commit(_dragFrom.Start, _dragFrom.End, false);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var owner = _owner;
        if (owner is null || !_drag.IsActive || !_drag.Move(e, this, out _))
        {
            return;
        }
        var width = Bounds.Width;
        var t = ToTime(e.GetPosition(this).X, width);
        var shift = t - _anchor;
        var (s, en) = _dragFrom;
        var least = Math.Min(owner.MinSpan.TotalMilliseconds, owner.Last - owner.First);
        (double, double) next = _drag.Mode switch
        {
            (int)BrushChartThumbKind.Window => (s + shift, en + shift),
            (int)BrushChartThumbKind.Start => (Math.Clamp(t, owner.First, en - least), en),
            (int)BrushChartThumbKind.End => (s, Math.Clamp(t, s + least, owner.Last)),
            _ => (Math.Min(_anchor, t), Math.Max(_anchor, t))
        };
        if (_drag.Mode == 3 && next.Item2 - next.Item1 < least)
        {
            next = t >= _anchor ? (next.Item1, next.Item1 + least) : (next.Item2 - least, next.Item2);
        }
        owner.Commit(next.Item1, next.Item2, false);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var owner = _owner;
        if (owner is null || !_drag.IsActive)
        {
            return;
        }
        var moved = _drag.HasMoved;
        var mode = _drag.Mode;
        _drag.End();
        if (!moved && mode == 3)
        {
            // A tap on empty track recenters the window there.
            var (s, en) = owner.View;
            var half = (en - s) / 2;
            owner.Commit(_anchor - half, _anchor + half, true);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag.Cancel();
    }

    #endregion
}

/// <summary>
/// A keyboard stop on the overview: the window (ring) or one of its grips. Arrows move by a tenth of the window
/// (or one point for a grip), Shift or PageUp/PageDown by half (or ten points), + / - zoom around the center,
/// Home / End go to the ends, Escape or 0 resets to the whole series.
/// </summary>
internal sealed class BrushChartThumb : Control, Avalonia.Rendering.ICustomHitTest
{
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private readonly BrushChartOverview _overview;
    private MotionValue<double>? _grow;

    public BrushChartThumb(BrushChartOverview overview, BrushChartThumbKind kind)
    {
        _overview = overview;
        Kind = kind;
        Focusable = true;
        IsTabStop = true;
        AutomationProperties.SetName(this, kind switch
        {
            BrushChartThumbKind.Start => "Window start",
            BrushChartThumbKind.End => "Window end",
            _ => "Window"
        });
    }

    internal BrushChartThumbKind Kind { get; }

    internal BrushChart? Owner => _overview.Owner;

    protected override Size MeasureOverride(Size availableSize) => Kind == BrushChartThumbKind.Window ? default : new Size(28, 44);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Owner?.HandleKey(Kind, e) == true)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            Grow(1.12);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Grow(1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Grow(1.12);
    }

    private void Grow(double to)
    {
        if (Kind == BrushChartThumbKind.Window)
        {
            return;
        }
        _grow ??= CreateGrow();
        _ = _grow.AnimateTo(to, ArcMotion.IsFull(this) ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard) : MotionSpec.Instant);
    }

    private MotionValue<double> CreateGrow()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        if (owner is null)
        {
            return;
        }
        var palette = owner.Palette;
        var size = Bounds.Size;
        var focus = IsFocused && Classes.Contains(":focus-visible");
        if (Kind == BrushChartThumbKind.Window)
        {
            var ring = new RoundedRect(new Rect(size).Deflate(0.5), 10);
            context.DrawRectangle(null, palette.Pen(Oklch.WithAlpha(palette.Series[0], focus ? 1 : 0.45), 1), ring);
            if (focus)
            {
                context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), new RoundedRect(new Rect(size).Inflate(2), 12));
            }
            return;
        }
        var grow = _grow?.Value ?? 1;
        var grip = new Rect((size.Width - 6) / 2, (size.Height - 22 * grow) / 2, 6, 22 * grow);
        var shape = new RoundedRect(grip, 3);
        context.DrawRectangle(palette.Brush(palette.SurfaceRaised), palette.Pen(Oklch.WithAlpha(palette.Foreground, 0.26), 1), shape, palette.RestingShadow);
        if (focus)
        {
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), shape.Inflate(3, 3));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BrushChartThumbAutomationPeer(this);
}

/// <summary>The window and its handles are sliders whose value text is a date.</summary>
internal sealed class BrushChartThumbAutomationPeer : ControlAutomationPeer, IRangeValueProvider, IValueProvider
{
    public BrushChartThumbAutomationPeer(BrushChartThumb owner) : base(owner)
    {
    }

    private BrushChartThumb Thumb => (BrushChartThumb)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    bool IRangeValueProvider.IsReadOnly => false;

    double IRangeValueProvider.Minimum => Thumb.Owner is { } o ? Thumb.Kind == BrushChartThumbKind.End ? o.View.Start : o.First : 0;

    double IRangeValueProvider.Maximum => Thumb.Owner is { } o ? Thumb.Kind == BrushChartThumbKind.Start ? o.View.End : o.Last : 0;

    double IRangeValueProvider.Value => Thumb.Owner is { } o ? Thumb.Kind == BrushChartThumbKind.End ? o.View.End : o.View.Start : 0;

    double IRangeValueProvider.LargeChange => ChartTime.Day * 10;

    double IRangeValueProvider.SmallChange => ChartTime.Day;

    void IRangeValueProvider.SetValue(double value)
    {
        if (Thumb.Owner is not { } owner)
        {
            return;
        }
        var (start, end) = owner.View;
        switch (Thumb.Kind)
        {
            case BrushChartThumbKind.Start:
                owner.Commit(value, end, false);
                break;
            case BrushChartThumbKind.End:
                owner.Commit(start, value, false);
                break;
            default:
                owner.Commit(value, value + (end - start), false);
                break;
        }
    }

    bool IValueProvider.IsReadOnly => true;

    string? IValueProvider.Value => Thumb.Owner is { } o
        ? Thumb.Kind switch
        {
            BrushChartThumbKind.Start => BrushChart.ShortDate(o.View.Start),
            BrushChartThumbKind.End => BrushChart.ShortDate(o.View.End),
            _ => o.WindowText
        }
        : null;

    void IValueProvider.SetValue(string? value)
    {
    }
}

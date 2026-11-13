using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawing surface of <see cref="GanttTimeline"/>: the sticky task column, the two-tier calendar header, the grid,
/// the bars, the phase summaries and the dependency arrows, all drawn in <see cref="Render"/> and driven by a few motion
/// values (day width, row layout, release spring) so everything moves together. Pointer and keyboard editing live here.
/// </summary>
internal sealed partial class GanttChart : Control
{
    public static readonly StyledProperty<double> TaskColumnWidthProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(TaskColumnWidth), 248);
    public static readonly StyledProperty<double> NarrowTaskColumnWidthProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(NarrowTaskColumnWidth), 132);
    public static readonly StyledProperty<double> HeaderHeightProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(HeaderHeight), 56);
    public static readonly StyledProperty<double> BarHeightProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(BarHeight), 24);
    public static readonly StyledProperty<double> BarRadiusProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(BarRadius), 8);
    public static readonly StyledProperty<double> DefaultHeightProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(DefaultHeight), 480);
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(SurfaceBrush));
    public static readonly StyledProperty<IBrush?> TextBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(TextBrush));
    public static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(MutedBrush));
    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(SecondaryBrush));
    public static readonly StyledProperty<IBrush?> RowHotBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(RowHotBrush));
    public static readonly StyledProperty<IBrush?> BarEdgeBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(BarEdgeBrush));
    public static readonly StyledProperty<IBrush?> SummaryTrackBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(SummaryTrackBrush));
    public static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(GridBrush));
    public static readonly StyledProperty<IBrush?> WeekendBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(WeekendBrush));
    public static readonly StyledProperty<IBrush?> BarBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(BarBrush));
    public static readonly StyledProperty<IBrush?> BarFillBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(BarFillBrush));
    public static readonly StyledProperty<IBrush?> SummaryBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(SummaryBrush));
    public static readonly StyledProperty<IBrush?> ArrowBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(ArrowBrush));
    public static readonly StyledProperty<IBrush?> ConflictBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(ConflictBrush));
    public static readonly StyledProperty<IBrush?> TodayBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(TodayBrush));
    public static readonly StyledProperty<IBrush?> TodayTextBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(TodayTextBrush));
    public static readonly StyledProperty<IBrush?> FocusBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(FocusBrush));

    // TextLayout cache: FormattedText re-shapes its text on every Draw, and a frame of a zoom or a drag draws ~130 labels.
    private readonly AnalyticsTextCache _text = new(600);
    private readonly Dictionary<(string Key, double? Hue), IBrush> _avatars = [];
    private readonly MotionValue<double> _dayWidth;
    private readonly MotionValue<double> _layoutT;
    private readonly MotionValue<double> _release;
    private readonly MotionValue<double> _scrollMotion;
    private GanttModel _model = new([], [], new HashSet<string>());
    private GanttZoom _zoom = GanttZoom.Week;
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly _first;
    private DateOnly _last;
    private double _scrollX;
    private double _scrollY;
    private string? _selectedId;
    private string? _focusId;
    private Dictionary<string, Snapshot> _before = [];
    private bool _viewportReady;
    private double _anchorDays;
    private bool _zooming;
    private string? _hoverId;
    private (double, double, double, double, double, double) _published;

    private readonly record struct Snapshot(double Y, double X0, double X1, bool Present);

    static GanttChart()
    {
        AffectsRender<GanttChart>(SurfaceBrushProperty, TextBrushProperty, MutedBrushProperty, GridBrushProperty, WeekendBrushProperty,
            BarBrushProperty, BarFillBrushProperty, SummaryBrushProperty, ArrowBrushProperty, ConflictBrushProperty, TodayBrushProperty,
            TodayTextBrushProperty, FocusBrushProperty, SecondaryBrushProperty, RowHotBrushProperty, BarEdgeBrushProperty,
            SummaryTrackBrushProperty, BarHeightProperty, BarRadiusProperty, HeaderHeightProperty);
        AffectsMeasure<GanttChart>(HeaderHeightProperty, DefaultHeightProperty);
        FocusableProperty.OverrideDefaultValue<GanttChart>(true);
        ClipToBoundsProperty.OverrideDefaultValue<GanttChart>(true);
    }

    public GanttChart()
    {
        _dayWidth = new MotionValue<double>(this, GanttModel.DayWidth(_zoom), MotionVectors.Double);
        _dayWidth.Changed += _ => OnScaleFrame();
        _layoutT = new MotionValue<double>(this, 1, MotionVectors.Double);
        _layoutT.Changed += _ => InvalidateVisual();
        _release = new MotionValue<double>(this, 0, MotionVectors.Double);
        _release.Changed += _ => InvalidateVisual();
        // The Today glide: a scroll offset has no property a ControlTheme transition could animate.
        _scrollMotion = new MotionValue<double>(this, 0, MotionVectors.Double);
        _scrollMotion.Changed += v =>
        {
            _scrollX = v;
            ClampScroll();
            InvalidateVisual();
        };
    }

    public double TaskColumnWidth { get => GetValue(TaskColumnWidthProperty); set => SetValue(TaskColumnWidthProperty, value); }
    public double NarrowTaskColumnWidth { get => GetValue(NarrowTaskColumnWidthProperty); set => SetValue(NarrowTaskColumnWidthProperty, value); }
    public double HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public double BarRadius { get => GetValue(BarRadiusProperty); set => SetValue(BarRadiusProperty, value); }
    public double DefaultHeight { get => GetValue(DefaultHeightProperty); set => SetValue(DefaultHeightProperty, value); }
    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    public IBrush? RowHotBrush { get => GetValue(RowHotBrushProperty); set => SetValue(RowHotBrushProperty, value); }
    public IBrush? BarEdgeBrush { get => GetValue(BarEdgeBrushProperty); set => SetValue(BarEdgeBrushProperty, value); }
    public IBrush? SummaryTrackBrush { get => GetValue(SummaryTrackBrushProperty); set => SetValue(SummaryTrackBrushProperty, value); }
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? WeekendBrush { get => GetValue(WeekendBrushProperty); set => SetValue(WeekendBrushProperty, value); }
    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? BarFillBrush { get => GetValue(BarFillBrushProperty); set => SetValue(BarFillBrushProperty, value); }
    public IBrush? SummaryBrush { get => GetValue(SummaryBrushProperty); set => SetValue(SummaryBrushProperty, value); }
    public IBrush? ArrowBrush { get => GetValue(ArrowBrushProperty); set => SetValue(ArrowBrushProperty, value); }
    public IBrush? ConflictBrush { get => GetValue(ConflictBrushProperty); set => SetValue(ConflictBrushProperty, value); }
    public IBrush? TodayBrush { get => GetValue(TodayBrushProperty); set => SetValue(TodayBrushProperty, value); }
    public IBrush? TodayTextBrush { get => GetValue(TodayTextBrushProperty); set => SetValue(TodayTextBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    /// <summary>Raised when an edit commits (drag release or keyboard step).</summary>
    public event Action<GanttTask, DateOnly, DateOnly, GanttChangeKind>? Edited;

    /// <summary>Raised when a phase row is toggled.</summary>
    public event Action<string>? GroupToggled;

    /// <summary>Raised with a sentence for the status line.</summary>
    public event Action<string>? Announce;

    /// <summary>Raised when the scroll offsets, the scrollable range or the viewport change, for the scroll bars.</summary>
    public event Action? ScrollChanged;

    public GanttModel Model => _model;

    internal double ScrollX => _scrollX;

    internal double ScrollY => _scrollY;

    internal double MaxScrollX => Math.Max(0, ContentWidth - ViewWidth);

    internal double MaxScrollY => Math.Max(0, ContentHeight - ViewHeight);

    internal double ViewHeight => Math.Max(0, Bounds.Height - HeaderHeight);

    internal double ViewportWidth => Bounds.Width;

    public DateOnly Today => _today;

    internal bool IsReduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    /// <summary>Where the today line rests in the view when the plan opens or Today is pressed: more plan ahead than behind.</summary>
    private const double TodayViewFraction = 0.3;

    private double ColumnWidth => Bounds.Width < 560 + 0 ? NarrowTaskColumnWidth : TaskColumnWidth;

    private double DayPx => _dayWidth.Value;

    private double ViewWidth => Math.Max(0, Bounds.Width - ColumnWidth);

    private double ContentWidth => ((_last.DayNumber - _first.DayNumber) + 1) * DayPx;

    private double ContentHeight => _model.Rows.Count * GanttModel.RowHeight;

    private double XOf(DateOnly d) => (d.DayNumber - _first.DayNumber) * DayPx;

    /// <summary>Replaces the data; the change from the old layout is animated unless <paramref name="animate"/> is false.</summary>
    public void SetData(GanttModel model, DateOnly today, bool animate)
    {
        var firstTime = _model.Rows.Count == 0;
        _before = Capture();
        _model = model;
        _today = today;
        var (first, last) = model.Range(today);
        if (firstTime || first != _first || last != _last)
        {
            var shift = firstTime ? 0 : (_first.DayNumber - first.DayNumber) * DayPx;
            _first = first;
            _last = last;
            _scrollX += shift;
        }
        if (_focusId is null || !model.ById.ContainsKey(_focusId))
        {
            _focusId = model.VisibleTasks().FirstOrDefault()?.Id;
        }
        if (_selectedId is not null && !model.ById.ContainsKey(_selectedId))
        {
            _selectedId = null;
        }
        ClampScroll();
        if (animate && !firstTime && !IsReduced)
        {
            _layoutT.Jump(0);
            _ = _layoutT.AnimateTo(1, MotionSpec.Spring(ProjectsSprings.Card));
        }
        else
        {
            _layoutT.Jump(1);
        }
        InvalidateVisual();
    }

    public void SetZoom(GanttZoom zoom, bool animate)
    {
        if (zoom == _zoom)
        {
            return;
        }
        _zoom = zoom;
        _scrollMotion.Stop();
        _anchorDays = DayPx > 0 ? (_scrollX + ViewWidth / 2) / DayPx : 0;
        var target = GanttModel.DayWidth(zoom);
        _zooming = true;
        if (animate && !IsReduced)
        {
            _ = _dayWidth.AnimateTo(target, MotionSpec.Spring(ProjectsSprings.CssSpring)).ContinueWith(_ => Dispatcher.UIThread.Post(() => _zooming = false));
        }
        else
        {
            _dayWidth.Jump(target);
            OnScaleFrame();
            _zooming = false;
        }
    }

    public void ScrollToToday(bool animate)
    {
        var target = Math.Clamp(XOf(_today) + DayPx / 2 - ViewWidth * TodayViewFraction, 0, MaxScrollX);
        if (animate && !IsReduced)
        {
            _scrollMotion.Jump(_scrollX);
            _ = _scrollMotion.AnimateTo(target, MotionSpec.Spring(ProjectsSprings.Card));
            return;
        }
        _scrollMotion.Stop();
        _scrollX = target;
        ClampScroll();
        InvalidateVisual();
    }

    public void SetCollapsedChanged() => InvalidateVisual();

    /// <summary>Scrolls from a scroll bar, ending any Today glide.</summary>
    internal void SetScroll(double x, double y)
    {
        _scrollMotion.Stop();
        _scrollX = x;
        _scrollY = y;
        ClampScroll();
        InvalidateVisual();
    }

    private void OnScaleFrame()
    {
        if (_zooming && ViewWidth > 0)
        {
            _scrollX = _anchorDays * DayPx - ViewWidth / 2;
        }
        ClampScroll();
        InvalidateVisual();
    }

    private void ClampScroll()
    {
        _scrollX = Math.Clamp(_scrollX, 0, MaxScrollX);
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
        var state = (_scrollX, _scrollY, MaxScrollX, MaxScrollY, ViewportWidth, ViewHeight);
        if (state != _published)
        {
            _published = state;
            ScrollChanged?.Invoke();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_viewportReady && Bounds.Width > 0)
        {
            _viewportReady = true;
            ScrollToToday(false);
        }
        ClampScroll();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? DefaultHeight : availableSize.Height;
        return new Size(w, h);
    }

    private Dictionary<string, Snapshot> Capture()
    {
        var map = new Dictionary<string, Snapshot>();
        var t = _layoutT.Value;
        var y = 0.0;
        foreach (var row in _model.Rows)
        {
            var id = RowId(row);
            var x0 = 0.0;
            var x1 = 0.0;
            if (row.Task is { } task)
            {
                x0 = XOf(task.Start);
                x1 = XOf(task.IsMilestone ? task.Start : task.End.AddDays(1));
            }
            map[id] = new Snapshot(y, x0, x1, true);
            y += GanttModel.RowHeight;
        }
        _ = t;
        return map;
    }

    private static string RowId(GanttRow row) => row.Key;

    protected override AutomationPeer OnCreateAutomationPeer() => new GanttChartPeer(this);

    /// <summary>One line, cut with an ellipsis at <paramref name="maxWidth"/> when given; the brush goes to <see cref="AnalyticsText.Draw"/>.</summary>
    private AnalyticsText Text(string text, double size, FontWeight weight = FontWeight.Normal, double maxWidth = double.PositiveInfinity)
    {
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, weight);
        return double.IsInfinity(maxWidth) ? _text.Get(text, typeface, size) : _text.GetTrimmed(text, typeface, size, Math.Max(1, Math.Round(maxWidth)));
    }

    private static void DrawText(DrawingContext ctx, AnalyticsText text, IBrush? brush, Point origin)
    {
        if (brush is not null)
        {
            text.Draw(ctx, brush, origin);
        }
    }

    private static string AvatarKey(ProjectPerson p) => string.IsNullOrEmpty(p.Key) ? p.Name : p.Key;

    private IBrush AvatarBrush(ProjectPerson p)
    {
        var cacheKey = (AvatarKey(p), p.Hue);
        if (!_avatars.TryGetValue(cacheKey, out var brush))
        {
            brush = MakeAvatarBrush(p);
            _avatars[cacheKey] = brush;
        }
        return brush;
    }

    private static IBrush MakeAvatarBrush(ProjectPerson p)
    {
        var key = AvatarKey(p);
        var hash = 17;
        foreach (var c in key)
        {
            hash = unchecked(hash * 31 + c);
        }
        var hue = p.Hue ?? Math.Abs(hash % 360);
        var a = new HslColor(1, hue, 0.72, 0.58).ToRgb();
        var b = new HslColor(1, (hue + 70) % 360, 0.78, 0.62).ToRgb();
        var d = new HslColor(1, (hue + 140) % 360, 0.7, 0.68).ToRgb();
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(a, 0), new GradientStop(b, 0.55), new GradientStop(d, 1) },
        };
    }
}

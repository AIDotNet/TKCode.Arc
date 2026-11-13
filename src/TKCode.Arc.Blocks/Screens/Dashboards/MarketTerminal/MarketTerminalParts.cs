using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A watchlist sparkline: today's 5 minute closes across the width, in the up or down color by today's change, over a dotted
/// line at yesterday's close. Drawn in one pass; a tick only invalidates it.
/// </summary>
internal sealed class MarketSparkline : Control
{
    public static readonly StyledProperty<IBrush?> UpBrushProperty = AvaloniaProperty.Register<MarketSparkline, IBrush?>(nameof(UpBrush));
    public static readonly StyledProperty<IBrush?> DownBrushProperty = AvaloniaProperty.Register<MarketSparkline, IBrush?>(nameof(DownBrush));
    public static readonly StyledProperty<IBrush?> BaselineBrushProperty = AvaloniaProperty.Register<MarketSparkline, IBrush?>(nameof(BaselineBrush));

    private MarketSeries? _series;
    private double _previousClose;
    private IPen? _up, _down, _baseline;

    static MarketSparkline()
    {
        AffectsRender<MarketSparkline>(UpBrushProperty, DownBrushProperty, BaselineBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MarketSparkline>(false);
    }

    public IBrush? UpBrush { get => GetValue(UpBrushProperty); set => SetValue(UpBrushProperty, value); }
    public IBrush? DownBrush { get => GetValue(DownBrushProperty); set => SetValue(DownBrushProperty, value); }
    public IBrush? BaselineBrush { get => GetValue(BaselineBrushProperty); set => SetValue(BaselineBrushProperty, value); }

    public void SetSeries(MarketSeries series, double previousClose)
    {
        _series = series;
        _previousClose = previousClose;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UpBrushProperty || change.Property == DownBrushProperty || change.Property == BaselineBrushProperty)
        {
            _up = _down = _baseline = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_series is null || _series.Count < 2 || Bounds.Width <= 0)
        {
            return;
        }
        _up ??= new ImmutablePen(Solid(UpBrush), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _down ??= new ImmutablePen(Solid(DownBrush), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _baseline ??= new ImmutablePen(Solid(BaselineBrush), 1, new ImmutableDashStyle(new double[] { 1, 3 }, 0));
        var series = _series;
        double lo = _previousClose, hi = _previousClose;
        for (var i = 0; i < series.Count; i++)
        {
            lo = Math.Min(lo, series[i].Close);
            hi = Math.Max(hi, series[i].Close);
        }
        var span = Math.Max(hi - lo, hi * 0.001);
        var width = Bounds.Width;
        var height = Bounds.Height - 3;
        double Y(double price) => 1.5 + (hi - price) / span * height;
        var baseline = Math.Round(Y(_previousClose)) + 0.5;
        context.DrawLine(_baseline, new Point(0, baseline), new Point(width, baseline));
        var pen = series.Last.Close >= _previousClose ? _up : _down;
        var step = width / (series.Count - 1);
        var previous = new Point(0, Y(series[0].Close));
        for (var i = 1; i < series.Count; i++)
        {
            var point = new Point(i * step, Y(series[i].Close));
            context.DrawLine(pen, previous, point);
            previous = point;
        }
    }

    private static IImmutableBrush Solid(IBrush? brush) => brush is ISolidColorBrush solid
        ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity)
        : new ImmutableSolidColorBrush(Colors.Gray);
}

/// <summary>
/// The live button's dot: solid in the up color while streaming (with a slow pulse ring, the block's only loop, because it
/// reports live status) and in the muted color while paused. Reduced motion drops the pulse.
/// </summary>
internal sealed class MarketLiveDot : Control
{
    public static readonly StyledProperty<bool> IsLiveProperty = AvaloniaProperty.Register<MarketLiveDot, bool>(nameof(IsLive), true);
    public static readonly StyledProperty<IBrush?> LiveBrushProperty = AvaloniaProperty.Register<MarketLiveDot, IBrush?>(nameof(LiveBrush));
    public static readonly StyledProperty<IBrush?> PausedBrushProperty = AvaloniaProperty.Register<MarketLiveDot, IBrush?>(nameof(PausedBrush));

    private const double PulseSeconds = 1.8;
    private readonly LiveLoop _pulse;
    private double _phase;

    static MarketLiveDot()
    {
        AffectsRender<MarketLiveDot>(IsLiveProperty, LiveBrushProperty, PausedBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MarketLiveDot>(false);
        ClipToBoundsProperty.OverrideDefaultValue<MarketLiveDot>(false);
    }

    public MarketLiveDot()
    {
        _pulse = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt / PulseSeconds) % 1;
            InvalidateVisual();
            return true;
        });
        _pulse.RunningChanged += _ => InvalidateVisual();
        _pulse.Start();
    }

    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }
    public IBrush? LiveBrush { get => GetValue(LiveBrushProperty); set => SetValue(LiveBrushProperty, value); }
    public IBrush? PausedBrush { get => GetValue(PausedBrushProperty); set => SetValue(PausedBrushProperty, value); }

    /// <summary>True while the pulse ring is animating.</summary>
    public bool IsPulsing => _pulse.IsRunning;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLiveProperty)
        {
            if (IsLive)
            {
                _pulse.Start();
            }
            else
            {
                _pulse.Stop();
                _phase = 0;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(8, 8);

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var brush = IsLive ? LiveBrush : PausedBrush;
        if (brush is null)
        {
            return;
        }
        if (IsLive && _pulse.IsRunning)
        {
            var radius = 4 + 5 * _phase;
            using (context.PushOpacity(0.45 * (1 - _phase)))
            {
                context.DrawEllipse(brush, null, center, radius, radius);
            }
        }
        context.DrawEllipse(brush, null, center, 4, 4);
    }
}

/// <summary>
/// A watchlist row (themed in MarketTerminalTheme): ticker and name, a sparkline, the price and today's change. The price
/// flashes a tint in the direction it moved, as a single opacity fade; reduced motion holds the tint briefly instead.
/// Code-driven flash: each tick restarts the fade from full, which a ControlTheme transition cannot express.
/// </summary>
internal sealed class MarketWatchRow : AvaloniaButton
{
    public static readonly StyledProperty<string?> TickerProperty = AvaloniaProperty.Register<MarketWatchRow, string?>(nameof(Ticker));
    public static readonly StyledProperty<string?> SymbolNameProperty = AvaloniaProperty.Register<MarketWatchRow, string?>(nameof(SymbolName));
    public static readonly StyledProperty<string?> PriceTextProperty = AvaloniaProperty.Register<MarketWatchRow, string?>(nameof(PriceText));
    public static readonly StyledProperty<string?> ChangeTextProperty = AvaloniaProperty.Register<MarketWatchRow, string?>(nameof(ChangeText));
    public static readonly StyledProperty<bool> IsUpProperty = AvaloniaProperty.Register<MarketWatchRow, bool>(nameof(IsUp), true);
    public static readonly StyledProperty<bool> IsCurrentProperty = AvaloniaProperty.Register<MarketWatchRow, bool>(nameof(IsCurrent));
    public static readonly StyledProperty<double> FlashOpacityProperty = AvaloniaProperty.Register<MarketWatchRow, double>(nameof(FlashOpacity));
    public static readonly StyledProperty<bool> IsFlashUpProperty = AvaloniaProperty.Register<MarketWatchRow, bool>(nameof(IsFlashUp), true);

    private static readonly TimeSpan s_flash = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan s_hold = TimeSpan.FromMilliseconds(400);
    private MarketSparkline? _sparkline;
    private MotionValue<double>? _flash;
    private MarketSeries? _pendingSeries;
    private double _pendingClose;

    public string? Ticker { get => GetValue(TickerProperty); set => SetValue(TickerProperty, value); }
    public string? SymbolName { get => GetValue(SymbolNameProperty); set => SetValue(SymbolNameProperty, value); }
    public string? PriceText { get => GetValue(PriceTextProperty); set => SetValue(PriceTextProperty, value); }
    public string? ChangeText { get => GetValue(ChangeTextProperty); set => SetValue(ChangeTextProperty, value); }
    public bool IsUp { get => GetValue(IsUpProperty); set => SetValue(IsUpProperty, value); }
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    public double FlashOpacity { get => GetValue(FlashOpacityProperty); set => SetValue(FlashOpacityProperty, value); }
    public bool IsFlashUp { get => GetValue(IsFlashUpProperty); set => SetValue(IsFlashUpProperty, value); }

    protected override Type StyleKeyOverride => typeof(MarketWatchRow);

    public void SetSeries(MarketSeries series, double previousClose)
    {
        _pendingSeries = series;
        _pendingClose = previousClose;
        _sparkline?.SetSeries(series, previousClose);
    }

    public void InvalidateSparkline() => _sparkline?.InvalidateVisual();

    /// <summary>Flashes the price tint: up (green) or down (red).</summary>
    public void Flash(bool up)
    {
        IsFlashUp = up;
        _flash ??= CreateFlash();
        _flash.Jump(1);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _flash.Jump(0);
            return;
        }
        _ = level == ArcMotionLevel.Full
            ? _flash.AnimateTo(0, MotionSpec.Tween(s_flash, ArcEasings.Standard))
            : _flash.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(1), ArcEasings.Standard, s_hold));
    }

    private MotionValue<double> CreateFlash()
    {
        var flash = new MotionValue<double>(this, 0, MotionVectors.Double);
        flash.Changed += value => SetCurrentValue(FlashOpacityProperty, value);
        return flash;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _sparkline = e.NameScope.Find<MarketSparkline>("PART_Sparkline");
        if (_pendingSeries is not null)
        {
            _sparkline?.SetSeries(_pendingSeries, _pendingClose);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsUpProperty)
        {
            PseudoClasses.Set(":down", !IsUp);
        }
        else if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(":current", IsCurrent);
        }
        else if (change.Property == IsFlashUpProperty)
        {
            PseudoClasses.Set(":flash-down", !IsFlashUp);
        }
    }
}

/// <summary>A moving average toggle of the toolbar: a short line in the average's color and its name; dimmed while off.</summary>
internal sealed class MarketAverageToggle : ToggleButton
{
    public static readonly StyledProperty<IBrush?> SwatchProperty = AvaloniaProperty.Register<MarketAverageToggle, IBrush?>(nameof(Swatch));

    public IBrush? Swatch { get => GetValue(SwatchProperty); set => SetValue(SwatchProperty, value); }

    protected override Type StyleKeyOverride => typeof(MarketAverageToggle);
}

/// <summary>A round icon button (zoom, reset, cancel an order), themed in MarketTerminalTheme.</summary>
internal sealed class MarketIconButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty = AvaloniaProperty.Register<MarketIconButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(MarketIconButton);
}

/// <summary>The live button: the dot, "Live" and a pause or play glyph.</summary>
internal sealed class MarketLiveButton : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsLiveProperty = AvaloniaProperty.Register<MarketLiveButton, bool>(nameof(IsLive), true);

    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    protected override Type StyleKeyOverride => typeof(MarketLiveButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLiveProperty)
        {
            PseudoClasses.Set(":paused", !IsLive);
        }
    }
}

/// <summary>One order of the ticket's list: what, how and when on two lines, its status and, while working, a cancel button.</summary>
internal sealed class MarketOrderRow : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<MarketOrderRow, string?>(nameof(Title));
    public static readonly StyledProperty<string?> DetailProperty = AvaloniaProperty.Register<MarketOrderRow, string?>(nameof(Detail));
    public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<MarketOrderRow, string?>(nameof(StatusText));
    public static readonly StyledProperty<MarketOrderStatus> StatusProperty = AvaloniaProperty.Register<MarketOrderRow, MarketOrderStatus>(nameof(Status));
    public static readonly StyledProperty<string?> CancelNameProperty = AvaloniaProperty.Register<MarketOrderRow, string?>(nameof(CancelName));

    private MarketIconButton? _cancel;

    public MarketOrderRow()
    {
        UpdateStatusClasses();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public MarketOrderStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public string? CancelName { get => GetValue(CancelNameProperty); set => SetValue(CancelNameProperty, value); }

    public MarketOrder? Order { get; set; }

    /// <summary>Raised when the cancel button is pressed.</summary>
    public event EventHandler? CancelRequested;

    public MarketIconButton? CancelButton => _cancel;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        base.OnApplyTemplate(e);
        _cancel = e.NameScope.Find<MarketIconButton>("PART_Cancel");
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            UpdateStatusClasses();
        }
    }

    private void UpdateStatusClasses()
    {
        PseudoClasses.Set(":working", Status == MarketOrderStatus.Working);
        PseudoClasses.Set(":filled", Status == MarketOrderStatus.Filled);
        PseudoClasses.Set(":canceled", Status == MarketOrderStatus.Canceled);
    }
}

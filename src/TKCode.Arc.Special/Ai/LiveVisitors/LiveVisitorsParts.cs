using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The status dot of a <see cref="LiveVisitors"/> card. While live and on screen a ring leaves it every 1.6s, growing to
/// 2.6 times the dot and fading (a <see cref="LiveLoop"/> that pauses off screen; Full motion only). Reduced motion, Off,
/// paused and the other states draw the dot alone.
/// </summary>
internal sealed class LiveVisitorsStatusDot : Control
{
    /// <summary>The pulse's period (s).</summary>
    internal const double PulsePeriod = 1.6;

    /// <summary>How much of the period the ring takes to grow and fade (the rest is a rest).</summary>
    internal const double PulseShare = 0.75;

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LiveVisitorsStatusDot, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<LiveVisitorsStatusDot, double>(nameof(DotSize), 8);

    public static readonly StyledProperty<double> PulseScaleProperty =
        AvaloniaProperty.Register<LiveVisitorsStatusDot, double>(nameof(PulseScale), 2.6);

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<LiveVisitorsStatusDot, bool>(nameof(IsPulsing));

    private readonly LiveLoop _pulse;
    private double _phase;

    static LiveVisitorsStatusDot()
    {
        AffectsRender<LiveVisitorsStatusDot>(FillProperty, DotSizeProperty, PulseScaleProperty);
        AffectsMeasure<LiveVisitorsStatusDot>(DotSizeProperty, PulseScaleProperty);
    }

    public LiveVisitorsStatusDot()
    {
        IsHitTestVisible = false;
        _pulse = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt) % PulsePeriod;
            InvalidateVisual();
            return true;
        });
        _pulse.RunningChanged += _ => InvalidateVisual();
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    public double PulseScale { get => GetValue(PulseScaleProperty); set => SetValue(PulseScaleProperty, value); }

    public bool IsPulsing { get => GetValue(IsPulsingProperty); set => SetValue(IsPulsingProperty, value); }

    /// <summary>Whether the pulse loop wants frames (it may still be paused off screen or by the motion level).</summary>
    internal bool WantsPulse => _pulse.IsActive;

    /// <summary>Whether the pulse is drawing frames.</summary>
    internal bool IsPulseRunning => _pulse.IsRunning;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPulsingProperty)
        {
            if (IsPulsing)
            {
                _phase = 0;
                _pulse.Start();
            }
            else
            {
                _pulse.Stop();
                InvalidateVisual();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = DotSize * Math.Max(1, PulseScale);
        return new Size(size, size);
    }

    public override void Render(DrawingContext context)
    {
        if (Fill is not { } fill)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = DotSize / 2;
        if (_pulse.IsRunning && _phase < PulsePeriod * PulseShare)
        {
            var t = _phase / (PulsePeriod * PulseShare);
            var eased = 1 - Math.Pow(1 - t, 2);
            var reach = r * (1 + (PulseScale - 1) * eased);
            using (context.PushOpacity(0.45 * (1 - t)))
            {
                context.DrawEllipse(fill, null, center, reach, reach);
            }
        }
        context.DrawEllipse(fill, null, center, r, r);
    }
}

/// <summary>
/// The pause button of a <see cref="LiveVisitors"/> card: a toggle (screen readers get its pressed state, Arc's
/// <c>aria-pressed</c>) showing Pause while live and Play while paused, named after the action.
/// </summary>
internal sealed class LiveVisitorsPauseButton : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(LiveVisitorsPauseButton);
}

/// <summary>
/// The card's two lists: side by side with a gap, or one under the other when <see cref="IsStacked"/> (below 520px card
/// width). The two columns share the width equally.
/// </summary>
internal sealed class LiveVisitorsColumns : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<LiveVisitorsColumns, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<LiveVisitorsColumns, double>(nameof(ColumnGap), 24);

    public static readonly StyledProperty<double> StackedGapProperty =
        AvaloniaProperty.Register<LiveVisitorsColumns, double>(nameof(StackedGap), 16);

    static LiveVisitorsColumns()
    {
        AffectsMeasure<LiveVisitorsColumns>(IsStackedProperty, ColumnGapProperty, StackedGapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double StackedGap { get => GetValue(StackedGapProperty); set => SetValue(StackedGapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        if (IsStacked || visible.Count < 2)
        {
            var height = 0.0;
            for (var i = 0; i < visible.Count; i++)
            {
                visible[i].Measure(new Size(width, double.PositiveInfinity));
                height += visible[i].DesiredSize.Height + (i > 0 ? StackedGap : 0);
            }
            return new Size(width, height);
        }
        var column = Math.Max(0, (width - ColumnGap * (visible.Count - 1)) / visible.Count);
        var tallest = 0.0;
        foreach (var child in visible)
        {
            child.Measure(new Size(column, double.PositiveInfinity));
            tallest = Math.Max(tallest, child.DesiredSize.Height);
        }
        return new Size(width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        if (IsStacked || visible.Count < 2)
        {
            var y = 0.0;
            foreach (var child in visible)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + StackedGap;
            }
            return finalSize;
        }
        var column = Math.Max(0, (finalSize.Width - ColumnGap * (visible.Count - 1)) / visible.Count);
        var x = 0.0;
        foreach (var child in visible)
        {
            child.Arrange(new Rect(x, 0, column, finalSize.Height));
            x += column + ColumnGap;
        }
        return finalSize;
    }
}

/// <summary>A placeholder bar that holds the space of a value while the stream connects.</summary>
internal sealed class LiveVisitorsPlaceholder : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LiveVisitorsPlaceholder, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<LiveVisitorsPlaceholder, CornerRadius>(nameof(CornerRadius), new CornerRadius(6));

    static LiveVisitorsPlaceholder()
    {
        AffectsRender<LiveVisitorsPlaceholder>(FillProperty, CornerRadiusProperty);
    }

    public LiveVisitorsPlaceholder()
    {
        IsHitTestVisible = false;
        VerticalAlignment = VerticalAlignment.Center;
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (Fill is { } fill)
        {
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(Bounds.Size), CornerRadius));
        }
    }
}

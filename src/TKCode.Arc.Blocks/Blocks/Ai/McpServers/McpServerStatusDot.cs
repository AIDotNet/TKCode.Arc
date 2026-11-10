using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The live dot of a server's status line, drawn in one <see cref="Render"/>: a filled dot with a halo that breathes out
/// (connected), a dot that breathes (connecting), a filled dot (error) and a hollow ring (off). The shape differs between
/// on and off and the status line always says the word, so the dot never carries the meaning alone.
/// <para>
/// The breathing is continuous, so it runs on a <see cref="LiveLoop"/> (only while shown, on screen and at full motion; it
/// rests at the plain dot otherwise). Nothing is allocated per frame: the opacity is pushed, the pen is cached.
/// </para>
/// </summary>
internal sealed class McpServerStatusDot : Control
{
    public static readonly StyledProperty<McpServerStatus> StatusProperty =
        AvaloniaProperty.Register<McpServerStatusDot, McpServerStatus>(nameof(Status));

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<McpServerStatusDot, double>(nameof(DotSize), 8);

    public static readonly StyledProperty<double> HaloSpreadProperty =
        AvaloniaProperty.Register<McpServerStatusDot, double>(nameof(HaloSpread), 5);

    public static readonly StyledProperty<TimeSpan> ConnectedPulseProperty =
        AvaloniaProperty.Register<McpServerStatusDot, TimeSpan>(nameof(ConnectedPulse), TimeSpan.FromMilliseconds(2400));

    public static readonly StyledProperty<TimeSpan> ConnectingPulseProperty =
        AvaloniaProperty.Register<McpServerStatusDot, TimeSpan>(nameof(ConnectingPulse), TimeSpan.FromMilliseconds(900));

    public static readonly StyledProperty<IBrush?> ConnectedBrushProperty =
        AvaloniaProperty.Register<McpServerStatusDot, IBrush?>(nameof(ConnectedBrush));

    public static readonly StyledProperty<IBrush?> ConnectingBrushProperty =
        AvaloniaProperty.Register<McpServerStatusDot, IBrush?>(nameof(ConnectingBrush));

    public static readonly StyledProperty<IBrush?> ErrorBrushProperty =
        AvaloniaProperty.Register<McpServerStatusDot, IBrush?>(nameof(ErrorBrush));

    public static readonly StyledProperty<IBrush?> OffBrushProperty =
        AvaloniaProperty.Register<McpServerStatusDot, IBrush?>(nameof(OffBrush));

    private readonly LiveLoop _loop;
    private double _phase;
    private IPen? _offPen;

    static McpServerStatusDot()
    {
        AffectsMeasure<McpServerStatusDot>(DotSizeProperty, HaloSpreadProperty);
        AffectsRender<McpServerStatusDot>(StatusProperty, ConnectedBrushProperty, ConnectingBrushProperty, ErrorBrushProperty, OffBrushProperty);
    }

    public McpServerStatusDot()
    {
        IsHitTestVisible = false;
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += _ =>
        {
            _phase = 0;
            InvalidateVisual();
        };
    }

    public McpServerStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    public double HaloSpread { get => GetValue(HaloSpreadProperty); set => SetValue(HaloSpreadProperty, value); }

    public TimeSpan ConnectedPulse { get => GetValue(ConnectedPulseProperty); set => SetValue(ConnectedPulseProperty, value); }

    public TimeSpan ConnectingPulse { get => GetValue(ConnectingPulseProperty); set => SetValue(ConnectingPulseProperty, value); }

    public IBrush? ConnectedBrush { get => GetValue(ConnectedBrushProperty); set => SetValue(ConnectedBrushProperty, value); }

    public IBrush? ConnectingBrush { get => GetValue(ConnectingBrushProperty); set => SetValue(ConnectingBrushProperty, value); }

    public IBrush? ErrorBrush { get => GetValue(ErrorBrushProperty); set => SetValue(ErrorBrushProperty, value); }

    public IBrush? OffBrush { get => GetValue(OffBrushProperty); set => SetValue(OffBrushProperty, value); }

    /// <summary>Whether the breathing loop runs now (tests).</summary>
    internal bool IsBreathing => _loop.IsRunning;

    /// <summary>Whether the breathing is wanted (connected or connecting); the loop decides whether it can run.</summary>
    internal bool WantsBreathing => _loop.IsActive;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            if (Status is McpServerStatus.Connected or McpServerStatus.Connecting)
            {
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
        }
        else if (change.Property == OffBrushProperty)
        {
            _offPen = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Status is McpServerStatus.Connected or McpServerStatus.Connecting)
        {
            _loop.Start();
        }
    }

    private bool Step(double elapsed)
    {
        var period = (Status == McpServerStatus.Connecting ? ConnectingPulse : ConnectedPulse).TotalSeconds;
        _phase = period > 0 ? (_phase + elapsed / period) % 1 : 0;
        InvalidateVisual();
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = DotSize + 2 * HaloSpread;
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = DotSize / 2;
        var running = _loop.IsRunning;
        switch (Status)
        {
            case McpServerStatus.Connected when ConnectedBrush is { } brush:
                if (running)
                {
                    // The halo grows from the dot's edge to the spread while it fades out.
                    var t = _phase;
                    var eased = 1 - (1 - t) * (1 - t);
                    using (context.PushOpacity(0.35 * (1 - t)))
                    {
                        var halo = r + HaloSpread * eased;
                        context.DrawEllipse(brush, null, center, halo, halo);
                    }
                }
                context.DrawEllipse(brush, null, center, r, r);
                break;
            case McpServerStatus.Connecting when ConnectingBrush is { } brush:
                {
                    var opacity = running ? 0.4 + 0.6 * (0.5 + 0.5 * Math.Cos(_phase * 2 * Math.PI)) : 1;
                    using (context.PushOpacity(opacity))
                    {
                        context.DrawEllipse(brush, null, center, r, r);
                    }
                    break;
                }
            case McpServerStatus.Error when ErrorBrush is { } brush:
                context.DrawEllipse(brush, null, center, r, r);
                break;
            case McpServerStatus.Disabled when OffBrush is { } brush:
                _offPen ??= new ImmutablePen(brush.ToImmutable(), 1.5);
                context.DrawEllipse(null, _offPen, center, r - 0.75, r - 0.75);
                break;
        }
    }
}

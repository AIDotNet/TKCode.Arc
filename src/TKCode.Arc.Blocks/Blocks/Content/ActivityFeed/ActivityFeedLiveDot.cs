using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The "live" mark of an <see cref="ActivityFeed"/>: a dot with a ring that swells and fades once every 1.6 s while the
/// feed is live. The pulse runs on a <see cref="LiveLoop"/>, so it stops when the dot is off screen, hidden or motion is
/// reduced, and the resting frame is a plain dot. Paused, the dot turns muted. Drawn in one pass with cached brushes.
/// </summary>
internal sealed class ActivityFeedLiveDot : Control
{
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<ActivityFeedLiveDot, bool>(nameof(IsLive));

    public static readonly StyledProperty<IBrush?> LiveBrushProperty =
        AvaloniaProperty.Register<ActivityFeedLiveDot, IBrush?>(nameof(LiveBrush));

    public static readonly StyledProperty<IBrush?> PausedBrushProperty =
        AvaloniaProperty.Register<ActivityFeedLiveDot, IBrush?>(nameof(PausedBrush));

    private const double Period = 1.6;
    private const double Dot = 8;
    private readonly LiveLoop _loop;
    private readonly ImmutableSolidColorBrush?[] _ring = new ImmutableSolidColorBrush?[16];
    private double _phase;
    private Color _ringColor;

    static ActivityFeedLiveDot()
    {
        AffectsRender<ActivityFeedLiveDot>(IsLiveProperty, LiveBrushProperty, PausedBrushProperty);
    }

    public ActivityFeedLiveDot()
    {
        Width = 16;
        Height = 16;
        IsHitTestVisible = false;
        ArcTokens.Bind(this, LiveBrushProperty, ArcDesignTokenKind.Success);
        ArcTokens.Bind(this, PausedBrushProperty, ArcDesignTokenKind.TextMuted);
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                _phase = 0;
                InvalidateVisual();
            }
        };
    }

    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    public IBrush? LiveBrush { get => GetValue(LiveBrushProperty); set => SetValue(LiveBrushProperty, value); }

    public IBrush? PausedBrush { get => GetValue(PausedBrushProperty); set => SetValue(PausedBrushProperty, value); }

    /// <summary>Whether the pulse is running (tests).</summary>
    public bool IsPulsing => _loop.IsRunning;

    /// <summary>Whether the pulse is wanted (tests).</summary>
    public bool IsPulseActive => _loop.IsActive;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLiveProperty)
        {
            if (IsLive)
            {
                _loop.Start();
            }
            else
            {
                _loop.Stop();
                _phase = 0;
            }
        }
    }

    private bool Step(double seconds)
    {
        _phase = seconds % Period / Period;
        InvalidateVisual();
        return true;
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var fill = IsLive ? LiveBrush : PausedBrush;
        if (fill is null)
        {
            return;
        }
        if (IsLive && _phase > 0 && fill is ISolidColorBrush solid)
        {
            if (solid.Color != _ringColor)
            {
                _ringColor = solid.Color;
                Array.Clear(_ring);
            }
            // The ring grows from the dot to the full box and fades out; 16 cached alpha steps, no per-frame brush.
            var step = Math.Min(_ring.Length - 1, (int)(_phase * _ring.Length));
            var brush = _ring[step] ??= new ImmutableSolidColorBrush(_ringColor, 0.45 * (1 - (double)step / _ring.Length));
            var radius = Dot / 2 + (Bounds.Width / 2 - Dot / 2) * _phase;
            context.DrawEllipse(brush, null, center, radius, radius);
        }
        context.DrawEllipse(fill, null, center, Dot / 2, Dot / 2);
    }
}

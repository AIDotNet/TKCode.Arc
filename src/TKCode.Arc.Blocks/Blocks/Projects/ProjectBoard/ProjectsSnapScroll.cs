using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Proximity snapping for a sideways <see cref="ScrollViewer"/> (CSS <c>scroll-snap-type: x proximity</c>): once the
/// scrolling has been still for <see cref="IdleDelay"/>, an offset within <see cref="Threshold"/> of a snap point glides to
/// it on <see cref="Spring"/>; farther away it stays where it is. Touch and pen contacts and their momentum are never
/// fought: nothing snaps while a contact is down, and momentum keeps the offset changing, which keeps postponing the
/// snap. Any scrolling by the person stops a glide that is running. <see cref="ScrollTo"/> glides to any offset (bring a
/// column into view). Written for the Project board; it knows nothing about boards, so it can move to
/// <c>Projects/_Shared/</c> when the Kanban board needs it.
/// </summary>
/// <remarks>
/// Code-driven motion: the glide's target is measured at run time (the nearest column start after the person stopped
/// scrolling) and the scroll offset is not a styleable property a ControlTheme transition could drive.
/// </remarks>
internal sealed class ProjectsSnapScroll : IDisposable
{
    private readonly ScrollViewer _viewer;
    private readonly Func<IReadOnlyList<double>> _points;
    private readonly MotionValue<double> _offset;
    private readonly DispatcherTimer _idle;
    private double _written = double.NaN;
    private bool _writing;
    private int _contacts;
    private bool _disposed;

    /// <summary>Starts snapping <paramref name="viewer"/> to <paramref name="points"/> (offsets, read when needed).</summary>
    public ProjectsSnapScroll(ScrollViewer viewer, Func<IReadOnlyList<double>> points)
    {
        _viewer = viewer;
        _points = points;
        _offset = new MotionValue<double>(viewer, viewer.Offset.X, MotionVectors.Double);
        _offset.Changed += Write;
        _idle = new DispatcherTimer { Interval = IdleDelay };
        _idle.Tick += OnIdle;
        viewer.ScrollChanged += OnScrollChanged;
        viewer.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        viewer.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>How long the offset must stay still before it snaps (Chrome waits for the scroll to end; ~140ms).</summary>
    public static TimeSpan IdleDelay { get; } = TimeSpan.FromMilliseconds(140);

    /// <summary>The glide to a snap point or a requested offset (Smooth: no overshoot, about 0.4s).</summary>
    public SpringSpec Spring { get; set; } = ArcMotionPresets.Smooth;

    /// <summary>How close (in DIPs) the offset must be to a snap point to snap; zero or less never snaps.</summary>
    public double Threshold { get; set; }

    /// <summary>False stops snapping (the content fits, or the owner scrolls by itself).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>True while a glide runs.</summary>
    public bool IsGliding => _offset.IsAnimating;

    /// <summary>Where the running or last glide ends.</summary>
    public double Target => _offset.Target;

    /// <summary>True while a touch or pen contact is down on the viewer.</summary>
    public bool IsTouching => _contacts > 0;

    /// <summary>
    /// The snap point within <paramref name="threshold"/> of <paramref name="offset"/>, or null when none is close enough
    /// or the offset is already on one.
    /// </summary>
    public static double? Choose(double offset, IReadOnlyList<double> points, double threshold)
    {
        if (points.Count == 0 || !(threshold > 0))
        {
            return null;
        }
        var best = points[0];
        foreach (var p in points)
        {
            if (Math.Abs(p - offset) < Math.Abs(best - offset))
            {
                best = p;
            }
        }
        var distance = Math.Abs(best - offset);
        return distance <= threshold && distance > 0.5 ? best : null;
    }

    /// <summary>Glides (or jumps) to <paramref name="offset"/>, clamped to the scroll range.</summary>
    public void ScrollTo(double offset, bool animate = true)
    {
        _idle.Stop();
        var target = Clamp(offset);
        if (!animate)
        {
            _offset.Jump(target);
            return;
        }
        if (!_offset.IsAnimating)
        {
            _offset.Jump(_viewer.Offset.X);
        }
        _ = _offset.AnimateTo(target, MotionSpec.Spring(Spring));
    }

    /// <summary>Snaps now if the offset is close to a snap point (what the idle timer does; tests call it).</summary>
    public void SnapNow()
    {
        _idle.Stop();
        if (!IsEnabled || _contacts > 0 || _offset.IsAnimating)
        {
            return;
        }
        var current = _viewer.Offset.X;
        if (Choose(current, _points(), Threshold) is { } point)
        {
            ScrollTo(point);
        }
    }

    private double Clamp(double offset)
    {
        var max = Math.Max(0, _viewer.Extent.Width - _viewer.Viewport.Width);
        return Math.Clamp(double.IsFinite(offset) ? offset : 0, 0, max);
    }

    private void Write(double value)
    {
        if (_disposed)
        {
            return;
        }
        _writing = true;
        try
        {
            _written = Clamp(value);
            _viewer.Offset = new Vector(_written, _viewer.Offset.Y);
        }
        finally
        {
            _writing = false;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_writing || Math.Abs(e.OffsetDelta.X) < 0.01)
        {
            return;
        }
        if (Math.Abs(_viewer.Offset.X - _written) < 0.5)
        {
            // Our own glide, reported after layout.
            return;
        }
        // The person scrolled (wheel, keys, a drag or its momentum): stop gliding and wait for the scrolling to settle.
        if (_offset.IsAnimating)
        {
            _offset.Stop();
        }
        _written = double.NaN;
        Restart();
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            _contacts++;
            _idle.Stop();
            if (_offset.IsAnimating)
            {
                _offset.Stop();
            }
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if ((e.Pointer.Type is PointerType.Touch or PointerType.Pen) && _contacts > 0)
        {
            _contacts--;
            Restart();
        }
    }

    private void Restart()
    {
        if (!IsEnabled || _disposed)
        {
            return;
        }
        _idle.Stop();
        _idle.Start();
    }

    private void OnIdle(object? sender, EventArgs e) => SnapNow();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _idle.Stop();
        _idle.Tick -= OnIdle;
        _viewer.ScrollChanged -= OnScrollChanged;
        _viewer.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _viewer.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _offset.Dispose();
    }
}

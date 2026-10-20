using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Pointer velocity over the last ~100ms of a drag (px/s), used for the drop spring.</summary>
internal sealed class ReorderableListVelocityTracker
{
    private const int Capacity = 8;
    private readonly double[] _times = new double[Capacity];
    private readonly double[] _values = new double[Capacity];
    private int _count;
    private int _head;

    public void Reset()
    {
        _count = 0;
        _head = 0;
    }

    public void Add(double seconds, double value)
    {
        _times[_head] = seconds;
        _values[_head] = value;
        _head = (_head + 1) % Capacity;
        _count = Math.Min(_count + 1, Capacity);
    }

    public double Velocity
    {
        get
        {
            if (_count < 2)
            {
                return 0;
            }
            var newest = (_head - 1 + Capacity) % Capacity;
            var oldest = newest;
            for (var i = 1; i < _count; i++)
            {
                var index = (newest - i + Capacity) % Capacity;
                if (_times[newest] - _times[index] > 0.1)
                {
                    break;
                }
                oldest = index;
            }
            var dt = _times[newest] - _times[oldest];
            return dt > 0.001 ? (_values[newest] - _values[oldest]) / dt : 0;
        }
    }
}

/// <summary>
/// Scrolls the nearest scrolling ancestor while a dragged row is held near its top or bottom edge. Runs on
/// animation frames only while armed; the speed grows the deeper the pointer sits in the 48px edge zone.
/// </summary>
internal sealed class ReorderableListAutoScroller
{
    private const double Zone = 48;
    private const double MaxSpeed = 900;

    private readonly Control _owner;
    private readonly Action _scrolled;
    private ScrollViewer? _scroller;
    private TopLevel? _topLevel;
    private Point _pointer;
    private bool _armed;
    private bool _requested;
    private TimeSpan? _last;

    public ReorderableListAutoScroller(Control owner, Action scrolled)
    {
        _owner = owner;
        _scrolled = scrolled;
    }

    /// <summary>Pointer position in top-level coordinates.</summary>
    public void Update(Point pointerInTopLevel)
    {
        _pointer = pointerInTopLevel;
        if (!_armed)
        {
            _armed = true;
            _topLevel = TopLevel.GetTopLevel(_owner);
            _scroller = _owner.FindAncestorOfType<ScrollViewer>();
            _last = null;
        }
        Request();
    }

    public void Stop()
    {
        _armed = false;
        _scroller = null;
    }

    private void Request()
    {
        if (_requested || !_armed || _topLevel is null || _scroller is null || Speed() == 0)
        {
            return;
        }
        _requested = true;
        _topLevel.RequestAnimationFrame(OnFrame);
    }

    private double Speed()
    {
        if (_scroller is null || _topLevel is null || _scroller.TranslatePoint(default, _topLevel) is not { } origin)
        {
            return 0;
        }
        var top = origin.Y;
        var bottom = origin.Y + _scroller.Bounds.Height;
        var offset = _scroller.Offset.Y;
        var max = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        if (_pointer.Y < top + Zone && offset > 0)
        {
            var depth = Math.Clamp((top + Zone - _pointer.Y) / Zone, 0, 1);
            return -MaxSpeed * depth * depth;
        }
        if (_pointer.Y > bottom - Zone && offset < max)
        {
            var depth = Math.Clamp((_pointer.Y - (bottom - Zone)) / Zone, 0, 1);
            return MaxSpeed * depth * depth;
        }
        return 0;
    }

    private void OnFrame(TimeSpan time)
    {
        _requested = false;
        if (!_armed || _scroller is null)
        {
            return;
        }
        var dt = _last is { } last ? Math.Clamp((time - last).TotalSeconds, 0, 0.05) : 1.0 / 60;
        _last = time;
        var speed = Speed();
        if (speed == 0)
        {
            _last = null;
            return;
        }
        var max = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        var next = Math.Clamp(_scroller.Offset.Y + speed * dt, 0, max);
        _scroller.Offset = new Vector(_scroller.Offset.X, next);
        _scrolled();
        Request();
    }
}

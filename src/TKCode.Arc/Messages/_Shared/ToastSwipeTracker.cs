using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Horizontal swipe tracking for toasts: a 6px slop decides between a horizontal drag and a vertical scroll, the
/// drag starts from where the slop was crossed (no jump), and the release velocity is estimated from the samples
/// of the last 100ms. A finger that stopped before lifting carries no throw.
/// </summary>
internal sealed class ToastSwipeTracker
{
    private const double Slop = 6;
    private const int Capacity = 16;
    private readonly double[] _sampleX = new double[Capacity];
    private readonly ulong[] _sampleT = new ulong[Capacity];
    private int _sampleStart;
    private int _sampleCount;
    private Point _start;
    private double _origin;

    /// <summary>A pointer is down and may become a drag.</summary>
    public bool IsTracking { get; private set; }

    /// <summary>The slop was crossed horizontally: the toast follows the pointer.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>The raw horizontal offset (origin plus pointer travel) of the last move.</summary>
    public double RawOffset { get; private set; }

    public void Begin(Point position, double origin)
    {
        IsTracking = true;
        IsDragging = false;
        _start = position;
        _origin = origin;
        RawOffset = origin;
        _sampleStart = 0;
        _sampleCount = 0;
    }

    public void Cancel()
    {
        IsTracking = false;
        IsDragging = false;
    }

    /// <summary>Feeds a move. Returns true when this move started the drag.</summary>
    public bool Move(Point position, ulong timestamp)
    {
        if (!IsTracking)
        {
            return false;
        }
        var started = false;
        if (!IsDragging)
        {
            var dx = position.X - _start.X;
            var dy = position.Y - _start.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < Slop)
            {
                return false;
            }
            if (Math.Abs(dy) > Math.Abs(dx))
            {
                // A vertical gesture belongs to the page.
                IsTracking = false;
                return false;
            }
            // Follow from here, so crossing the slop never makes the toast jump.
            IsDragging = true;
            started = true;
            _start = position;
        }
        RawOffset = _origin + position.X - _start.X;
        AddSample(timestamp, RawOffset);
        return started;
    }

    /// <summary>Ends the gesture and returns the release velocity in px/s (0 when cancelled or stopped).</summary>
    public double End(ulong timestamp, bool cancelled)
    {
        var dragging = IsDragging;
        IsTracking = false;
        IsDragging = false;
        if (!dragging || cancelled || _sampleCount < 2)
        {
            return 0;
        }
        var first = _sampleStart;
        var last = (_sampleStart + _sampleCount - 1) % Capacity;
        var t0 = _sampleT[first];
        var t1 = _sampleT[last];
        if (t1 <= t0 || timestamp > t1 + 60)
        {
            return 0;
        }
        return (_sampleX[last] - _sampleX[first]) / (t1 - t0) * 1000.0;
    }

    private void AddSample(ulong timestamp, double x)
    {
        var index = (_sampleStart + _sampleCount) % Capacity;
        if (_sampleCount == Capacity)
        {
            _sampleStart = (_sampleStart + 1) % Capacity;
            _sampleCount--;
            index = (_sampleStart + _sampleCount) % Capacity;
        }
        _sampleT[index] = timestamp;
        _sampleX[index] = x;
        _sampleCount++;
        // Keep only the last 100ms (but at least two samples).
        while (_sampleCount > 2 && timestamp - _sampleT[_sampleStart] > 100)
        {
            _sampleStart = (_sampleStart + 1) % Capacity;
            _sampleCount--;
        }
    }

    /// <summary>Where a release at this velocity would come to rest (fast scroll deceleration, 0.99 per ms).</summary>
    public static double Project(double velocity) => velocity / 1000.0 * 0.99 / (1 - 0.99);

    /// <summary>Resistance past an edge: the pull keeps moving, but less and less (overscroll).</summary>
    public static double RubberBand(double distance, double dimension)
        => dimension <= 0 ? 0 : (1 - 1 / (distance * 0.55 / dimension + 1)) * dimension;
}

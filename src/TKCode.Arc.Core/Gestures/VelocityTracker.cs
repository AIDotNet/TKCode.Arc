using Avalonia;

namespace TKCode.Arc.Gestures;

/// <summary>
/// Estimates pointer velocity (px/s) from recent samples (S10). Keeps a fixed ring of samples, so adding a sample never
/// allocates. The estimate is a recency-weighted least-squares slope over the samples inside <see cref="Window"/>
/// (100ms by default): steady motion gives its exact speed and a single jittery event cannot dominate.
/// A pause longer than <see cref="StaleAfter"/> before release drops the momentum (Arc: a slow, deliberate release
/// settles where it is).
/// </summary>
public sealed class VelocityTracker
{
    private const int Capacity = 24;

    private readonly double[] _t = new double[Capacity];
    private readonly double[] _x = new double[Capacity];
    private readonly double[] _y = new double[Capacity];
    private int _head;
    private int _count;

    /// <summary>Creates a tracker.</summary>
    /// <param name="window">How far back samples count (default 100ms).</param>
    /// <param name="staleAfter">A release later than this after the last sample reports zero velocity (default 60ms).</param>
    public VelocityTracker(TimeSpan? window = null, TimeSpan? staleAfter = null)
    {
        Window = window ?? TimeSpan.FromMilliseconds(100);
        StaleAfter = staleAfter ?? TimeSpan.FromMilliseconds(60);
        if (Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }
    }

    /// <summary>How far back samples are used for the estimate.</summary>
    public TimeSpan Window { get; }

    /// <summary>Samples older than this at query time mean the pointer paused: the velocity is zero.</summary>
    public TimeSpan StaleAfter { get; }

    /// <summary>Number of stored samples (bounded by the internal ring size).</summary>
    public int Count => _count;

    /// <summary>Time of the newest sample, or null when empty.</summary>
    public TimeSpan? LastSampleTime => _count == 0 ? null : TimeSpan.FromSeconds(_t[(_head - 1 + Capacity) % Capacity]);

    /// <summary>Velocity at the newest sample (never stale).</summary>
    public Vector Velocity => _count == 0 ? default : GetVelocity(LastSampleTime!.Value);

    /// <summary>Adds a sample. Times must not go backwards; a sample at the same time replaces the previous one.</summary>
    public void AddSample(Point position, TimeSpan time)
    {
        var t = time.TotalSeconds;
        if (_count > 0)
        {
            var last = (_head - 1 + Capacity) % Capacity;
            if (t < _t[last])
            {
                // Out-of-order input (a different clock): start over rather than produce a wild estimate.
                Reset();
            }
            else if (t == _t[last])
            {
                _x[last] = position.X;
                _y[last] = position.Y;
                return;
            }
        }
        _t[_head] = t;
        _x[_head] = position.X;
        _y[_head] = position.Y;
        _head = (_head + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }

    /// <summary>Adds a one-dimensional sample (stored on the X axis).</summary>
    public void AddSample(double value, TimeSpan time) => AddSample(new Point(value, 0), time);

    /// <summary>Clears all samples.</summary>
    public void Reset()
    {
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Velocity in px/s as seen at <paramref name="now"/> (usually the release time). Zero when there are fewer than
    /// two samples or the newest sample is older than <see cref="StaleAfter"/>.
    /// </summary>
    public Vector GetVelocity(TimeSpan now)
    {
        if (_count < 2)
        {
            return default;
        }
        var newest = (_head - 1 + Capacity) % Capacity;
        var tNewest = _t[newest];
        if (now.TotalSeconds - tNewest > StaleAfter.TotalSeconds)
        {
            return default;
        }
        var window = Window.TotalSeconds;
        double sw = 0, st = 0, sx = 0, sy = 0, stt = 0, stx = 0, sty = 0;
        var used = 0;
        for (var i = 0; i < _count; i++)
        {
            var index = (newest - i + Capacity) % Capacity;
            var age = tNewest - _t[index];
            if (age > window)
            {
                break;
            }
            // Newer samples weigh more (1 at the window edge, 2 at the newest sample).
            var w = 2 - age / window;
            var t = -age;
            sw += w;
            st += w * t;
            sx += w * _x[index];
            sy += w * _y[index];
            stt += w * t * t;
            stx += w * t * _x[index];
            sty += w * t * _y[index];
            used++;
        }
        if (used < 2)
        {
            return default;
        }
        var denominator = sw * stt - st * st;
        if (denominator <= 1e-12)
        {
            return default;
        }
        return new Vector((sw * stx - st * sx) / denominator, (sw * sty - st * sy) / denominator);
    }
}

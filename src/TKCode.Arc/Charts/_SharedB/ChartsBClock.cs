using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One linear clock per chart: staggered per-item animations (cells, lines, tiles) are evaluated analytically against
/// <see cref="Time"/> instead of running hundreds of motion values. Off and detached owners jump to the end.
/// </summary>
internal sealed class ChartsBClock
{
    private readonly MotionValue<double> _time;
    private double _duration;

    public ChartsBClock(Visual owner, Action tick)
    {
        _time = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _time.Changed += _ => tick();
    }

    /// <summary>Seconds since the last <see cref="Start"/>; equals the duration once finished.</summary>
    public double Time => _time.Value;

    public bool IsRunning => _time.IsAnimating;

    /// <summary>Restarts the clock and runs it for <paramref name="seconds"/>.</summary>
    public void Start(double seconds)
    {
        _duration = Math.Max(0.001, seconds);
        _time.Jump(0);
        _ = _time.AnimateTo(_duration, MotionSpec.Tween(TimeSpan.FromSeconds(_duration), ChartsBMath.Linear));
    }

    /// <summary>Stops and moves to the end of the current run.</summary>
    public void Finish() => _time.Jump(_duration);
}

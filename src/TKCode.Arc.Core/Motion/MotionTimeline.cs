using TKCode.Arc.Primitives;
using Avalonia;

namespace TKCode.Arc.Motion;

/// <summary>
/// P22 pausable timeline: a value that runs linearly to 1 over a duration (a hold fill, a countdown, a rotation
/// clock). The run is timed by the S12 <see cref="PausableTimer"/>, so <see cref="Completed"/> lands on time even while
/// no frames are drawn, and is reported once per frame by a <see cref="LiveLoop"/> while the owner is on screen. It runs
/// at every motion level because it reports function, not decoration. Between runs the value can spring back
/// (<see cref="Rewind"/>, <see cref="SettleTo"/>; these follow the motion level like any <see cref="MotionValue{T}"/>,
/// so Reduced and Off land at once) or jump.
/// </summary>
public sealed class MotionTimeline : IDisposable
{
    private readonly PausableTimer _timer;
    private readonly LiveLoop _frames;
    private readonly MotionValue<double> _settle;
    private double _from;
    private double _value;
    private bool _active;

    /// <param name="owner">The visual whose frames report the run and whose motion level the springs follow.</param>
    public MotionTimeline(Visual owner)
    {
        _timer = new PausableTimer(TimeSpan.FromSeconds(1), OnElapsed);
        _frames = new LiveLoop(owner, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
        _settle = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _settle.Changed += OnSettleChanged;
    }

    /// <summary>Raised with the value whenever it changes: every frame of a run or a spring, and on start, pause, stop and jumps.</summary>
    public event Action<double>? Changed;

    /// <summary>Raised when a run reaches 1.</summary>
    public event Action? Completed;

    /// <summary>The current value (0 to 1 while running; a spring may overshoot slightly).</summary>
    public double Value => _active ? RunValue : _value;

    /// <summary>A run was started and has neither been stopped nor completed (it may be paused).</summary>
    public bool IsActive => _active;

    /// <summary>A run is advancing (active and not paused).</summary>
    public bool IsRunning => _active && _timer.IsRunning;

    /// <summary>Time left in the current run.</summary>
    public TimeSpan Remaining => _active ? _timer.Remaining : TimeSpan.Zero;

    private double RunValue => _from + (1 - _from) * _timer.Progress;

    /// <summary>
    /// Runs from <paramref name="from"/> to 1, at the pace of a full 0 → 1 run taking <paramref name="duration"/>
    /// (a run from 0.4 takes 60% of it). Stops any spring first.
    /// </summary>
    public void Start(TimeSpan duration, double from = 0)
    {
        _settle.Stop();
        _from = Math.Clamp(from, 0, 1);
        _active = true;
        var remaining = TimeSpan.FromTicks((long)(duration.Ticks * (1 - _from)));
        // A run with nothing left completes inside Restart.
        _timer.Restart(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        if (!_active)
        {
            return;
        }
        Set(RunValue);
        _frames.Start();
    }

    /// <summary>Holds the run where it is; <see cref="Resume"/> finishes the rest.</summary>
    public void Pause()
    {
        if (!_active || !_timer.IsRunning)
        {
            return;
        }
        _timer.Pause();
        _frames.Stop();
        Set(RunValue);
    }

    /// <summary>Continues a paused run.</summary>
    public void Resume()
    {
        if (!_active || _timer.IsRunning)
        {
            return;
        }
        _timer.Resume();
        if (_active)
        {
            _frames.Start();
        }
    }

    /// <summary>Ends the run (or spring) where it is; <see cref="Value"/> keeps the value reached.</summary>
    public void Stop()
    {
        _settle.Stop();
        if (!_active)
        {
            return;
        }
        var value = RunValue;
        _active = false;
        _timer.Stop();
        _frames.Stop();
        Set(value);
    }

    /// <summary>Ends any run and springs from the current value, at rest, to <paramref name="target"/>.</summary>
    public void SettleTo(double target, SpringSpec spring)
    {
        Stop();
        _settle.Jump(_value);
        _ = _settle.AnimateTo(target, MotionSpec.Spring(spring), 0.0);
    }

    /// <summary>Ends any run and springs back to 0 (a hold let go early); defaults to Arc's smooth spring.</summary>
    public void Rewind(SpringSpec? spring = null) => SettleTo(0, spring ?? ArcMotionPresets.Smooth);

    /// <summary>Ends any run or spring and sets the value at once.</summary>
    public void Jump(double value)
    {
        Stop();
        _settle.Jump(value);
        Set(value);
    }

    private bool OnFrame(double dt)
    {
        if (!_active || !_timer.IsRunning)
        {
            return false;
        }
        Set(RunValue);
        return true;
    }

    private void OnElapsed()
    {
        if (!_active)
        {
            return;
        }
        _active = false;
        _frames.Stop();
        Set(1);
        Completed?.Invoke();
    }

    private void OnSettleChanged(double value)
    {
        if (!_active)
        {
            Set(value);
        }
    }

    private void Set(double value)
    {
        if (Math.Abs(value - _value) < 1e-9)
        {
            return;
        }
        _value = value;
        Changed?.Invoke(value);
    }

    public void Dispose()
    {
        _active = false;
        _timer.Dispose();
        _frames.Dispose();
        _settle.Dispose();
    }
}

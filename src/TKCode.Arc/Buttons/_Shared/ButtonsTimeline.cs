using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// P22 pausable timeline: a linear 0 → 1 progress over a duration, timed by the S12 <see cref="PausableTimer"/> and
/// reported once per frame while running (<see cref="Tick"/>). It runs at every motion level because it reports
/// function (a hold fill, a countdown, a timeout), not decoration.
/// </summary>
internal sealed class ButtonsTimeline : IDisposable
{
    private readonly Visual _owner;
    private readonly Action _completed;
    private readonly PausableTimer _timer;
    private double _from;
    private double _value;
    private bool _active;
    private bool _frameRequested;

    public ButtonsTimeline(Visual owner, Action completed)
    {
        _owner = owner;
        _completed = completed;
        _timer = new PausableTimer(TimeSpan.FromSeconds(1), OnElapsed);
    }

    /// <summary>Raised every frame while running, and on start, pause and completion.</summary>
    public event Action<double>? Tick;

    /// <summary>Started and neither stopped nor completed (running or paused).</summary>
    public bool IsActive => _active;

    public bool IsRunning => _active && _timer.IsRunning;

    /// <summary>Current progress, 0 to 1.</summary>
    public double Progress => _active ? _from + (1 - _from) * _timer.Progress : _value;

    /// <summary>Remaining time of the current run.</summary>
    public TimeSpan Remaining => _active ? _timer.Remaining : TimeSpan.Zero;

    /// <summary>Runs from <paramref name="from"/> to 1; a full run takes <paramref name="duration"/>.</summary>
    public void Start(TimeSpan duration, double from = 0)
    {
        _from = Math.Clamp(from, 0, 1);
        _value = _from;
        _active = true;
        var remaining = TimeSpan.FromTicks((long)(duration.Ticks * (1 - _from)));
        _timer.Restart(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        if (!_active)
        {
            return;
        }
        Tick?.Invoke(Progress);
        RequestFrame();
    }

    public void Pause()
    {
        if (!_active || !_timer.IsRunning)
        {
            return;
        }
        _timer.Pause();
        Tick?.Invoke(Progress);
    }

    public void Resume()
    {
        if (!_active || _timer.IsRunning)
        {
            return;
        }
        _timer.Resume();
        RequestFrame();
    }

    /// <summary>Stops where it is; <see cref="Progress"/> keeps the value reached.</summary>
    public void Stop()
    {
        if (!_active)
        {
            return;
        }
        _value = Progress;
        _active = false;
        _timer.Stop();
    }

    private void OnElapsed()
    {
        if (!_active)
        {
            return;
        }
        _active = false;
        _value = 1;
        Tick?.Invoke(1);
        _completed();
    }

    private void RequestFrame()
    {
        if (_frameRequested || !IsRunning)
        {
            return;
        }
        var clock = MotionScheduler.ClockOverride;
        if (clock is not null)
        {
            _frameRequested = true;
            clock.RequestFrame(OnFrame);
            return;
        }
        if (TopLevel.GetTopLevel(_owner) is { } topLevel)
        {
            _frameRequested = true;
            topLevel.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan timestamp)
    {
        _frameRequested = false;
        if (!IsRunning)
        {
            return;
        }
        Tick?.Invoke(Progress);
        RequestFrame();
    }

    public void Dispose()
    {
        _active = false;
        _timer.Dispose();
    }
}

/// <summary>
/// Press-and-hold progress shared by HoldToConfirm and ConfirmMorph's hold mode: holding runs a
/// <see cref="ButtonsTimeline"/> from wherever the fill is (a quick re-press carries on); letting go early rewinds
/// on a spring that starts at rest. Reduced and Off rewind at once; the fill itself follows the hold at every level.
/// </summary>
internal sealed class ButtonsHoldProgress : IDisposable
{
    private readonly ButtonsTimeline _timeline;
    private readonly MotionValue<double> _settle;
    private double _value;

    public ButtonsHoldProgress(Visual owner, Action completed)
    {
        _timeline = new ButtonsTimeline(owner, completed);
        _timeline.Tick += Set;
        _settle = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _settle.Changed += v =>
        {
            if (!_timeline.IsActive)
            {
                Set(v);
            }
        };
    }

    /// <summary>Raised with the fill (0 to 1) whenever it changes.</summary>
    public event Action<double>? Changed;

    public double Value => _value;

    public bool IsHolding => _timeline.IsActive;

    /// <summary>Starts or resumes the fill; a full hold takes <paramref name="duration"/>.</summary>
    public void Begin(TimeSpan duration)
    {
        _settle.Stop();
        _timeline.Start(duration, _value);
    }

    /// <summary>Ends a hold early and rewinds on <paramref name="spring"/> (initial velocity zero).</summary>
    public void Release(SpringSpec spring)
    {
        if (!_timeline.IsActive)
        {
            return;
        }
        _timeline.Stop();
        Settle(0, spring);
    }

    /// <summary>Springs the fill to <paramref name="target"/> (a reset rewinds, an external confirmation fills).</summary>
    public void Settle(double target, SpringSpec spring)
    {
        _timeline.Stop();
        _settle.Jump(_value);
        _ = _settle.AnimateTo(target, MotionSpec.Spring(spring), 0.0);
    }

    public void Jump(double value)
    {
        _timeline.Stop();
        _settle.Jump(value);
        Set(value);
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
        _timeline.Dispose();
        _settle.Dispose();
    }
}

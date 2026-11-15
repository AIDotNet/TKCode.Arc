using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

// The motion state and the frame loop. Why code and not the theme: the thumb follows the pointer and keeps its velocity
// into the release, the liquid's tail chases the thumb on a second spring, the track's swell and the thumb's stretch are
// functions of the press spring and the speed, and the confirm choreography is keyed to the track's retreat. All of it
// feeds outlines that are rebuilt per frame, so one loop integrates everything and the control draws itself.
public partial class SlideToConfirm
{
    private enum Mode
    {
        Rest,
        Drag,
        Spring,
        Glide,
        Hint
    }

    private readonly LiveLoop _loop;

    // Geometry from the last arrange: width, track height, thumb travel.
    private double _w = 350;
    private double _h = 56;
    private double _travel = 294;

    // Thumb (offset from the start, px) and how it moves.
    private Mode _mode;
    private double _pos;
    private double _vel;
    private double _target;
    private SpringSpec _spec = SlideToConfirmMath.Release;
    private double _dragTarget;
    private double _glideTarget;
    private Key? _glideKey;
    private double _keyValue = -1;

    // The liquid's tail, the press swell, the smoothed speed and the track's retreat (0 full track … 1 a circle at the end).
    private double _tail;
    private double _tailVel;
    private double _press;
    private double _pressVel;
    private double _pressTarget;
    private double _speed;
    private double _retreat;
    private double _retreatVel;
    private double _retreatTarget;

    // Hover / keyboard focus tint of the track (150 ms Standard).
    private bool _hovered;
    private double _tint;
    private double _tintFrom;
    private double _tintTo;
    private double _tintClock = -1;

    // The idle nudge: scheduled by a timer, played by the loop.
    private DispatcherTimer? _hintTimer;
    private bool _hintFirst = true;
    private bool _touched;
    private double _hintClock = -1;
    private double _hintAmp = 1;
    private double _hintRate = 1;

    // The confirm cycle.
    private bool _committing;
    private bool _returning;
    private bool _returnFailed;
    private bool _awaitThumbReturn;
    private bool _showError;
    private double _doneClock = -1;
    private double _returnClock = -1;
    private double _spinnerAtEnd;
    private double _spinnerShown;
    private double _spinAngle;
    private Exception? _lastError;
    private string _statusText = "";

    // What the frame draws (written by UpdateVisuals).
    private double _vLabel = 1;
    private double _vStatus;
    private double _vSpinner;
    private double _vInk = 1;
    private double _vCheck;
    private double _vMorph;
    private double _vSuccess;
    private double _vDisc = 1;
    private double _vRx = 23;
    private double _vRy = 23;
    private double _vHead = 23;
    private double _vSwell;

    private double Radius => (_h / 2) - SlideToConfirmMath.Inset;

    private double Scale => Radius / SlideToConfirmMath.ReferenceRadius;

    private bool CanAnimate => IsAttachedToVisualTree() && ArcMotion.GetEffectiveLevel(this) >= ArcMotionLevel.Reduced;

    private bool _attached;

    private bool IsAttachedToVisualTree() => _attached;

    private void SetMode(Mode mode, double target, SpringSpec spec)
    {
        _mode = mode;
        _target = target;
        _spec = spec;
    }

    /// <summary>Starts the frame loop, or puts everything at its target at once when there is no motion.</summary>
    private void Kick()
    {
        if (CanAnimate)
        {
            _loop.Start();
            if (!_loop.CanRun)
            {
                // Hidden or scrolled away: the loop resumes later; the frame still shows the current state.
                UpdateVisuals();
            }
        }
        else
        {
            SnapAll();
        }
        InvalidateVisual();
    }

    private bool StepFrame(double seconds)
    {
        if (!CanAnimate)
        {
            SnapAll();
            InvalidateVisual();
            return false;
        }
        var full = ArcMotion.IsFull(this);
        var dt = Math.Min(seconds, 0.05);
        var steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            Advance(h, full);
        }
        AdvanceClocks(dt * 1000, full);
        UpdateVisuals();
        InvalidateVisual();
        var settled = IsSettled();
        if (settled)
        {
            UpdateHintTimer();
        }
        return !settled;
    }

    private void Advance(double h, bool full)
    {
        var before = _pos;
        switch (_mode)
        {
            case Mode.Drag:
                FollowTarget(_dragTarget, SlideToConfirmMath.Follow, h, full);
                break;
            case Mode.Glide:
                _glideTarget += SlideToConfirmMath.GlideSpeed * _travel * h;
                if (_glideTarget >= _travel)
                {
                    _glideTarget = _travel;
                    FollowTarget(_glideTarget, SlideToConfirmMath.Follow, h, full);
                    BeginCommit();
                    return;
                }
                FollowTarget(_glideTarget, SlideToConfirmMath.Follow, h, full);
                break;
            case Mode.Spring:
                FollowTarget(_target, _spec, h, full);
                break;
            case Mode.Hint:
                _hintClock += h * 1000;
                var ms = _hintClock * _hintRate;
                if (ms >= SlideToConfirmMath.HintDuration)
                {
                    _hintClock = -1;
                    _mode = Mode.Rest;
                    _pos = 0;
                    _vel = 0;
                }
                else
                {
                    _pos = SlideToConfirmMath.HintOffset(ms) * _hintAmp * Scale;
                    _vel = (_pos - before) / h;
                }
                break;
        }
        // The thumb may press a little into the start wall (it squashes there) but never further.
        if (_pos < -Radius * 0.3)
        {
            _pos = -Radius * 0.3;
            _vel = Math.Max(0, _vel);
        }

        if (full)
        {
            SlideToConfirmMath.Step(SlideToConfirmMath.Tail, ref _tail, ref _tailVel, _pos, h);
            SlideToConfirmMath.Step(SlideToConfirmMath.Press, ref _press, ref _pressVel, _pressTarget, h);
            SlideToConfirmMath.Step(SlideToConfirmMath.Retreat, ref _retreat, ref _retreatVel, _retreatTarget, h);
            _speed += (_vel - _speed) * (1 - Math.Exp(-h / 0.03));
        }
        else
        {
            _tail = _pos;
            _tailVel = 0;
            _press = 0;
            _pressVel = 0;
            _retreat = _retreatTarget;
            _retreatVel = 0;
            _speed = 0;
        }

        if (_committing && _retreatTarget == 0 && _pos >= _travel - (12 * Scale))
        {
            _retreatTarget = 1;
            if (!full)
            {
                _retreat = 1;
            }
        }
        if (_returning && _awaitThumbReturn && _retreat < 0.1)
        {
            _awaitThumbReturn = false;
            SetMode(Mode.Spring, 0, SlideToConfirmMath.Release);
            if (!full)
            {
                _pos = 0;
                _vel = 0;
            }
        }
    }

    private void FollowTarget(double target, SpringSpec spec, double h, bool full)
    {
        if (full)
        {
            SlideToConfirmMath.Step(spec, ref _pos, ref _vel, target, h);
        }
        else
        {
            _pos = target;
            _vel = 0;
        }
    }

    private void AdvanceClocks(double ms, bool full)
    {
        if (_tintClock >= 0)
        {
            _tintClock += ms;
            if (_tintClock >= 150)
            {
                _tintClock = -1;
                _tint = _tintTo;
            }
            else
            {
                _tint = _tintFrom + ((_tintTo - _tintFrom) * SlideToConfirmMath.EaseOut(_tintClock / 150));
            }
        }
        if (_state == SlideToConfirmState.Pending && full)
        {
            _spinAngle = (_spinAngle + (0.36 * ms)) % 360;
        }
        if (_state == SlideToConfirmState.Done && !_returning)
        {
            if (_doneClock < 0 && _retreat >= 0.9)
            {
                _doneClock = 0;
                _spinnerAtEnd = _spinnerShown;
            }
            else if (_doneClock >= 0)
            {
                _doneClock += full ? ms : 10000;
            }
        }
        if (_returning)
        {
            _returnClock += full ? ms : 10000;
            if (!_awaitThumbReturn && Math.Abs(_retreat) < 0.002 && _mode != Mode.Drag
                && SlideToConfirmMath.IsSettled(_pos, _vel, 0, 0.05) && _returnClock > 700)
            {
                FinishReturn();
            }
        }
    }

    private bool IsSettled()
    {
        if (_mode is Mode.Drag or Mode.Glide or Mode.Hint || _tintClock >= 0 || _returning)
        {
            return false;
        }
        if (_state == SlideToConfirmState.Pending && ArcMotion.IsFull(this))
        {
            return false;
        }
        if (_state == SlideToConfirmState.Done && (_doneClock < 0 || _doneClock < 400))
        {
            return false;
        }
        var settled = (_mode != Mode.Spring || SlideToConfirmMath.IsSettled(_pos, _vel, _target))
                      && SlideToConfirmMath.IsSettled(_tail, _tailVel, _pos, 0.05)
                      && SlideToConfirmMath.IsSettled(_press, _pressVel, _pressTarget, 0.002)
                      && SlideToConfirmMath.IsSettled(_retreat, _retreatVel, _retreatTarget, 0.0005)
                      && Math.Abs(_speed) < 1;
        if (settled && _mode == Mode.Spring)
        {
            _pos = _target;
            _vel = 0;
            _tail = _pos;
            _tailVel = 0;
            _speed = 0;
            _mode = Mode.Rest;
            UpdateVisuals();
        }
        return settled;
    }

    /// <summary>Puts every motion at its target at once (motion off, detached, or the loop cannot run).</summary>
    private void SnapAll()
    {
        switch (_mode)
        {
            case Mode.Drag:
                _pos = _dragTarget;
                break;
            case Mode.Glide:
                _pos = _travel;
                _mode = Mode.Rest;
                BeginCommitSnapped();
                break;
            case Mode.Spring:
                _pos = _target;
                _mode = Mode.Rest;
                break;
            case Mode.Hint:
                _pos = 0;
                _hintClock = -1;
                _mode = Mode.Rest;
                break;
        }
        _vel = 0;
        if (_committing)
        {
            _pos = _travel;
            _retreatTarget = 1;
        }
        _retreat = _retreatTarget;
        _retreatVel = 0;
        _tail = _pos;
        _tailVel = 0;
        _press = 0;
        _pressVel = 0;
        _speed = 0;
        _tint = _tintTo;
        _tintClock = -1;
        if (_state == SlideToConfirmState.Done && !_returning)
        {
            _doneClock = 10000;
        }
        if (_returning)
        {
            _awaitThumbReturn = false;
            _retreat = 0;
            _retreatTarget = 0;
            _pos = 0;
            _tail = 0;
            _mode = Mode.Rest;
            FinishReturn();
        }
        UpdateVisuals();
    }

    private void BeginCommitSnapped()
    {
        if (!_committing)
        {
            BeginCommit();
        }
    }

    /// <summary>Derives what the frame draws from the motion state.</summary>
    private void UpdateVisuals()
    {
        var full = ArcMotion.IsFull(this);
        var progress = _travel > 0 ? Math.Clamp(_pos / _travel, 0, 1) : 0;
        var oldValue = AutomationValue;
        Progress = progress;
        if (Math.Abs(AutomationValue - oldValue) > 0.5)
        {
            RaiseValueChanged(oldValue, AutomationValue);
        }

        var label = SlideToConfirmMath.LabelOpacity(progress);
        if (_returning)
        {
            label = Math.Min(label, Math.Clamp((_returnClock - 190) / 400, 0, 1));
        }
        else if (_committing || _state is SlideToConfirmState.Pending or SlideToConfirmState.Done)
        {
            label = Math.Min(label, SlideToConfirmMath.LabelRetreatCap(_retreat));
        }
        _vLabel = label;
        _vStatus = SlideToConfirmMath.StatusOpacity(_retreat);

        double spinner;
        if (_state == SlideToConfirmState.Pending && !_returning)
        {
            spinner = SlideToConfirmMath.SpinnerOpacity(_retreat);
            _spinnerShown = spinner;
        }
        else if (_doneClock >= 0 && !_returning)
        {
            spinner = _spinnerAtEnd * (1 - SlideToConfirmMath.EaseOut(_doneClock / 160));
        }
        else if (_returning)
        {
            spinner = _spinnerAtEnd * (1 - SlideToConfirmMath.EaseOut(_returnClock / 160));
        }
        else
        {
            spinner = 0;
        }
        _vSpinner = spinner;

        if (_doneClock >= 0 && !_returning)
        {
            var t = _doneClock;
            _vSuccess = SlideToConfirmMath.EaseOut(t / 130);
            _vDisc = 1 - SlideToConfirmMath.EaseOut((t - 20) / 140);
            _vCheck = SlideToConfirmMath.EaseOut((t - 65) / 160);
            _vMorph = full ? SlideToConfirmMath.EaseInOut((t - 95) / 245) : 1;
        }
        else if (_returning && !_returnFailed)
        {
            var t = _returnClock;
            _vSuccess = 1 - SlideToConfirmMath.EaseOut((t - 80) / 290);
            _vDisc = SlideToConfirmMath.EaseOut((t - 60) / 200);
            _vCheck = 1 - SlideToConfirmMath.EaseOut(t / 125);
            _vMorph = full ? 1 - SlideToConfirmMath.EaseInOut(t / 125) : 0;
        }
        else
        {
            _vSuccess = 0;
            _vDisc = 1;
            _vCheck = 0;
            _vMorph = 0;
        }
        _vInk = (1 - spinner) * (1 - _vCheck);

        var r0 = Radius;
        var r = r0 * (1 + (0.039 * _press));
        var rx = r;
        var ry = r;
        if (full)
        {
            var ratio = SlideToConfirmMath.StretchRatio(_speed);
            rx = r * Math.Pow(ratio, 0.55);
            ry = r * Math.Pow(ratio, -0.42);
            // The start wall: past its resting place the thumb's left edge stays put and the thumb squashes instead.
            var cx = (_h / 2) + _pos;
            var wall = (_h / 2) - r;
            if (cx - rx < wall)
            {
                var squashed = Math.Max(cx - wall, r * 0.8);
                if (squashed < rx)
                {
                    ry = r * Math.Sqrt(r / squashed);
                    rx = squashed;
                }
            }
        }
        _vRx = rx;
        _vRy = ry;
        _vHead = r0 + (full ? 1.9 * _press * Scale : 0);
        _vSwell = full ? SlideToConfirmMath.SwellAmplitude(_press, _speed, Scale) : 0;
    }

    // --- test and snapshot hooks ---

    /// <summary>Runs the frame loop for <paramref name="ms"/> in fixed frames (tests and the snapshot harness drive motion this way).</summary>
    internal void AdvanceFrames(double ms, double frameMs = 1000.0 / 60)
    {
        while (ms > 1e-6)
        {
            var step = Math.Min(frameMs, ms);
            StepFrame(step / 1000);
            ms -= step;
        }
    }

    /// <summary>The thumb's offset from the start (px), the tail's, and the track's retreat (0–1).</summary>
    internal double ThumbOffset => _pos;

    internal double TailOffset => _tail;

    internal double RetreatProgress => _retreat;

    /// <summary>The thumb's drawn radii, the liquid head's radius and the track's swell amplitude.</summary>
    internal (double Rx, double Ry, double Head, double Swell) ThumbShape => (_vRx, _vRy, _vHead, _vSwell);

    /// <summary>What the frame draws: label, status, spinner, chevron, check, success circle and disc opacities, and the morph.</summary>
    internal (double Label, double Status, double Spinner, double Ink, double Check, double Success, double Disc, double Morph) Visuals
        => (_vLabel, _vStatus, _vSpinner, _vInk, _vCheck, _vSuccess, _vDisc, _vMorph);

    /// <summary>The track height and thumb travel from the last arrange.</summary>
    internal (double Height, double Travel) TrackSize => (_h, _travel);

    /// <summary>True while the label shows <see cref="ErrorLabel"/>.</summary>
    internal bool ShowsError => _showError;

    /// <summary>True while the idle nudge's timer is armed.</summary>
    internal bool IsHintScheduled => _hintTimer is { IsEnabled: true };

    // --- the idle nudge ---

    private void UpdateHintTimer()
    {
        var wanted = IsHintEnabled && !_touched && _state == SlideToConfirmState.Idle && !_returning && !_committing
                     && IsAttachedToVisualTree() && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        if (!wanted)
        {
            StopHintTimer();
            return;
        }
        if (_hintTimer is { IsEnabled: true })
        {
            return;
        }
        _hintTimer ??= new DispatcherTimer();
        _hintTimer.Tick -= OnHintTick;
        _hintTimer.Tick += OnHintTick;
        _hintTimer.Interval = TimeSpan.FromMilliseconds(_hintFirst ? 2000 : 4400);
        _hintTimer.Start();
    }

    private void StopHintTimer() => _hintTimer?.Stop();

    private void OnHintTick(object? sender, EventArgs e)
    {
        if (_hintTimer is not null)
        {
            _hintTimer.Interval = TimeSpan.FromMilliseconds(4400);
        }
        _hintFirst = false;
        if (_touched || _hovered || !IsHintEnabled || _state != SlideToConfirmState.Idle || _mode != Mode.Rest
            || Math.Abs(_pos) > 0.01 || !ArcMotion.IsFull(this))
        {
            UpdateHintTimer();
            return;
        }
        PlayNudge(1, 1);
    }

    /// <summary>Plays the nudge: <paramref name="amplitude"/> of the 18 px hint, <paramref name="rate"/> times as fast.</summary>
    private void PlayNudge(double amplitude, double rate)
    {
        if (!ArcMotion.IsFull(this) || !CanAnimate)
        {
            return;
        }
        _hintAmp = amplitude;
        _hintRate = rate;
        _hintClock = 0;
        _mode = Mode.Hint;
        Kick();
    }

    private void CancelHint()
    {
        if (_mode == Mode.Hint)
        {
            _mode = Mode.Rest;
            _hintClock = -1;
        }
        StopHintTimer();
    }

    private void SetTint(bool on)
    {
        var goal = on ? 1.0 : 0.0;
        if (Math.Abs(goal - _tintTo) < 1e-6)
        {
            return;
        }
        _tintFrom = _tint;
        _tintTo = goal;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !CanAnimate)
        {
            _tint = goal;
            _tintClock = -1;
            InvalidateVisual();
            return;
        }
        _tintClock = 0;
        Kick();
    }
}

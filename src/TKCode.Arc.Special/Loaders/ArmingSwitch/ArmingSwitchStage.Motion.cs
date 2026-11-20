using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

// The motion state and the frame loop. Why code and not the theme: the cover is a 3D box whose hinge angle is solved
// from the pointer and keeps its speed into spring-loaded stops, the bat follows the finger until an over-centre spring
// takes it, the panel leans towards the pointer, and the lamp, ring and bursts run on timelines keyed to the hold and
// the result. Everything feeds one projected drawing, so one loop integrates it all (240 Hz substeps) and sleeps when
// nothing moves; the idle drift, ready light and breath only keep it at 30 fps while the plate is on screen.
internal sealed partial class ArmingSwitchStage
{
    // Arc Arming switch (design reference), fitted from per-frame samples.
    internal const double CoverOpenAngle = 96;
    internal const double CoverLiftAngle = 5;
    internal const double BatThrow = 32;
    private const double Halfway = 48;
    private const double CatchAngle = 22;

    private readonly LiveLoop _loop;

    // Cover hinge angle (degrees; 0 closed, 96 open), its speed and how it moves.
    private enum CoverMode { Rest, Open, Close, Drag }

    private double _cov;
    private double _covVel;
    private CoverMode _covMode = CoverMode.Rest;
    private double _covDragTarget;

    // Bat: tilt from the plate's normal (+32 safe = down, -32 armed = up).
    private enum BatMode { Rest, Drive, Drag, Held }

    private double _bat = BatThrow;
    private double _batVel;
    private BatMode _batMode = BatMode.Rest;
    private double _batGoal = BatThrow;
    private double _batDragTarget;

    // Panel lean (degrees) and its spring; rest rotateX 6, rotateY -6.
    private const double RestTiltX = 6;
    private const double RestTiltY = -6;
    private double _tiltX = RestTiltX;
    private double _tiltXVel;
    private double _tiltY = RestTiltY;
    private double _tiltYVel;
    private double _tiltXGoal = RestTiltX;
    private double _tiltYGoal = RestTiltY;
    private double _shakeClock = -1;

    // Lens press, hold progress (raw and shown), lamp level and colour mix.
    private double _lens = 1;
    private double _lensVel;
    private bool _holding;
    private double _raw;
    private double _hold;
    private double _holdVel;
    private double _level;
    private double _amber = 1;
    private double _green;
    private double _red;
    private double _armClock = -1;
    private double _runClock = -1;
    private double _doneClock = -1;
    private double _failClock = -1;
    private double _completeClock = -1;
    private double _spin;
    private double _spinOpacity;
    private double _fillOpacity;
    private double _burstAccent = -1;
    private double _burstGreen = -1;
    private double _burstRed = -1;
    private double _glint = -1;
    private double _ambient;
    private double _ambientSinceDraw;
    private double _shownClock = -1;
    private bool _hopDone;

    private bool Full => ArcMotion.IsFull(this);

    private bool AnyMotion => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;

    private void InitMotion()
    {
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                InvalidateVisual();
            }
        };
    }

    /// <summary>Puts every part at the pose of the owner's state at once (attach, detach, motion off).</summary>
    private void SnapToState()
    {
        var owner = Owner;
        var open = owner?.IsCoverOpen == true;
        var armed = owner?.IsArmed == true;
        _cov = open ? CoverOpenAngle : 0;
        _covVel = 0;
        _covMode = CoverMode.Rest;
        _bat = armed ? -BatThrow : BatThrow;
        _batVel = 0;
        _batMode = BatMode.Rest;
        _batGoal = _bat;
        _tiltX = _tiltXGoal = RestTiltX;
        _tiltY = _tiltYGoal = RestTiltY;
        _tiltXVel = _tiltYVel = 0;
        _shakeClock = -1;
        _lens = 1;
        _lensVel = 0;
        _hold = _raw = _holdVel = 0;
        _armClock = _completeClock = _doneClock = _failClock = -1;
        _burstAccent = _burstGreen = _burstRed = _glint = -1;
        var status = owner?.Status ?? ArmingSwitchStatus.Safe;
        SetColourAt(status, snap: true);
        _level = LevelGoal(status);
        _fillOpacity = 0;
        _spinOpacity = status == ArmingSwitchStatus.Running ? 1 : 0;
        _runClock = status == ArmingSwitchStatus.Running ? 0 : -1;
        UpdateView();
        InvalidateVisual();
    }

    /// <summary>Starts the frame loop, or settles everything at once when there is no motion.</summary>
    internal void Kick()
    {
        _loop.Start();
        if (!_loop.CanRun && !_holding)
        {
            SettleNow();
        }
        InvalidateVisual();
    }

    private void SettleNow()
    {
        if (_covMode != CoverMode.Drag)
        {
            _cov = Owner?.IsCoverOpen == true ? CoverOpenAngle : 0;
            _covVel = 0;
            _covMode = CoverMode.Rest;
        }
        if (_batMode != BatMode.Drag)
        {
            _bat = Owner?.IsArmed == true ? -BatThrow : BatThrow;
            _batVel = 0;
            _batMode = BatMode.Rest;
        }
        UpdateView();
        PlaceHits(force: false);
    }

    private void UpdateView()
    {
        var tx = _tiltX;
        var ty = _tiltY;
        if (_shakeClock >= 0 && Full)
        {
            ty += 1.6 * Math.Sin(_shakeClock * 2 * Math.PI / 0.09) * Math.Exp(-_shakeClock / 0.15);
        }
        _view.Update(_w, _plateOffset, tx, ty);
    }

    // --- events from the owner ---

    public void OnCoverChanged(bool open, bool fromDrag)
    {
        if (!AnyMotion)
        {
            SettleNow();
            return;
        }
        if (!Full)
        {
            // Reduced motion: the cover jumps to its new pose.
            _covMode = CoverMode.Rest;
            _cov = open ? CoverOpenAngle : 0;
            _covVel = 0;
        }
        else if (_covMode != CoverMode.Drag || !fromDrag)
        {
            _covMode = open ? CoverMode.Open : CoverMode.Close;
        }
        if (!open && _bat < 0)
        {
            // The falling cover catches the bat on its way down and throws it to safe.
            _batMode = Full ? BatMode.Held : BatMode.Drive;
            _batGoal = BatThrow;
            if (!Full)
            {
                _bat = BatThrow;
                _batVel = 0;
                _batMode = BatMode.Rest;
            }
        }
        Kick();
    }

    public void OnArmedChanged(bool armed)
    {
        _batGoal = armed ? -BatThrow : BatThrow;
        if (armed)
        {
            _armClock = 0;
        }
        if (_batMode == BatMode.Held && armed)
        {
            _batMode = BatMode.Drive;
        }
        if (!Full)
        {
            if (_batMode != BatMode.Held)
            {
                _bat = _batGoal;
                _batVel = 0;
                _batMode = BatMode.Rest;
            }
        }
        else if (_batMode is BatMode.Rest or BatMode.Drive)
        {
            _batMode = BatMode.Drive;
        }
        Kick();
    }

    public void OnStatusChanged(ArmingSwitchStatus old, ArmingSwitchStatus status)
    {
        if (status == ArmingSwitchStatus.Running)
        {
            _runClock = 0;
        }
        else
        {
            _runClock = -1;
        }
        if (status is ArmingSwitchStatus.Safe or ArmingSwitchStatus.Open)
        {
            _armClock = -1;
        }
        if (old == ArmingSwitchStatus.Failed && status == ArmingSwitchStatus.Running)
        {
            _failClock = -1;
        }
        SetColourAt(status, snap: false);
        Kick();
    }

    public void OnHoldCompleted()
    {
        _holding = false;
        _raw = 0;
        _completeClock = 0;
        _hold = 1;
        _holdVel = 0;
        if (Full)
        {
            _burstAccent = 0;
            _tiltXVel += 18;
        }
        Kick();
    }

    public void OnResult(bool success)
    {
        _completeClock = -1;
        if (success)
        {
            _doneClock = 0;
            _failClock = -1;
            if (Full)
            {
                _burstGreen = 0;
                _glint = 0;
            }
        }
        else
        {
            _failClock = 0;
            _doneClock = -1;
            _hold = 1;
            _holdVel = 0;
            if (Full)
            {
                _burstRed = 0;
                _shakeClock = 0;
            }
        }
        Kick();
    }

    /// <summary>The bat bumps the closed lid (switch thrown under a closed cover).</summary>
    public void Knock()
    {
        if (!Full)
        {
            return;
        }
        _batMode = BatMode.Drive;
        _batGoal = BatThrow;
        _batVel = -900;
        _covVel += 70;
        if (_covMode == CoverMode.Rest)
        {
            _covMode = CoverMode.Close;
        }
        Kick();
    }

    /// <summary>A refused attempt while locked: the cover rattles in its seat.</summary>
    public void Rattle()
    {
        if (!Full)
        {
            return;
        }
        _covVel += Owner?.IsCoverOpen == true ? -120 : 140;
        if (_covMode == CoverMode.Rest)
        {
            _covMode = Owner?.IsCoverOpen == true ? CoverMode.Open : CoverMode.Close;
        }
        _shakeClock = 0;
        Kick();
    }

    public void CancelHold()
    {
        if (_holding)
        {
            _holding = false;
            _raw = 0;
            Kick();
        }
    }

    internal void OnHitFocusChanged() => Kick();

    // --- the loop ---

    /// <summary>When set, only <see cref="AdvanceFrames"/> moves time (tests and snapshots); the frame loop idles.</summary>
    internal bool IsManualClock { get; set; }

    private bool OnFrame(double seconds) => IsManualClock || StepFrame(seconds);

    private bool StepFrame(double seconds)
    {
        var dt = Math.Min(seconds, 0.05);
        var full = Full;
        var steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
        var h = dt / steps;
        var moving = false;
        for (var i = 0; i < steps; i++)
        {
            moving |= Integrate(h, full);
        }
        moving |= AdvanceClocks(dt, full);
        UpdateView();
        PlaceHits(force: false);
        var ambient = full && IsVisible && Owner is not null;
        if (moving)
        {
            _ambientSinceDraw = 0;
            InvalidateVisual();
        }
        else if (ambient)
        {
            // Only the idle drift, the ready light and the lamp's breath: 30 fps is plenty.
            _ambientSinceDraw += dt;
            if (_ambientSinceDraw >= 1.0 / 30)
            {
                _ambientSinceDraw = 0;
                InvalidateVisual();
            }
        }
        return moving || ambient || _holding;
    }

    private static void Spring(ref double x, ref double v, double goal, double k, double c, double h)
    {
        var a = (k * (goal - x)) - (c * v);
        v += a * h;
        x += v * h;
    }

    private bool Integrate(double h, bool full)
    {
        var owner = Owner;
        var moving = false;

        // Lean towards the pointer.
        if (full)
        {
            Spring(ref _tiltX, ref _tiltXVel, _tiltXGoal, 62, 12.4, h);
            Spring(ref _tiltY, ref _tiltYVel, _tiltYGoal, 62, 12.4, h);
        }
        else
        {
            _tiltX = RestTiltX;
            _tiltY = RestTiltY;
            _tiltXVel = _tiltYVel = 0;
        }
        moving |= Math.Abs(_tiltX - _tiltXGoal) > 0.005 || Math.Abs(_tiltXVel) > 0.05
                  || Math.Abs(_tiltY - _tiltYGoal) > 0.005 || Math.Abs(_tiltYVel) > 0.05;

        // Cover.
        var lift = owner is not null && !owner.IsCoverOpen && (_coverHit.IsPointerOver || _coverHit.IsFocused) && !owner.IsLocked
            ? CoverLiftAngle : 0;
        switch (_covMode)
        {
            case CoverMode.Open:
                StepStop(ref _cov, ref _covVel, 43, 9.2, 218, CoverOpenAngle, 0.26, up: true, h);
                if (Math.Abs(_cov - CoverOpenAngle) < 0.02 && Math.Abs(_covVel) < 1)
                {
                    _cov = CoverOpenAngle;
                    _covVel = 0;
                    _covMode = CoverMode.Rest;
                }
                break;
            case CoverMode.Close:
                var wasUp = _cov > 0.001;
                StepStop(ref _cov, ref _covVel, 22.3, 5.3, -124, 0, 0.23, up: false, h);
                if (wasUp && _cov <= 0 && Math.Abs(_covVel) > 30)
                {
                    _tiltXVel += Math.Min(14, Math.Abs(_covVel) / 60);
                }
                if (_cov <= 0.02 && Math.Abs(_covVel) < 1)
                {
                    _cov = 0;
                    _covVel = 0;
                    _covMode = CoverMode.Rest;
                }
                break;
            case CoverMode.Drag:
                Spring(ref _cov, ref _covVel, _covDragTarget, 2000, 90, h);
                break;
            default:
                if (owner?.IsCoverOpen == true)
                {
                    _cov = CoverOpenAngle;
                    _covVel = 0;
                }
                else if (full)
                {
                    Spring(ref _cov, ref _covVel, lift, 300, 35, h);
                    if (_cov < 0)
                    {
                        _cov = 0;
                        _covVel = Math.Abs(_covVel) * 0.2;
                    }
                }
                else
                {
                    _cov = 0;
                    _covVel = 0;
                }
                break;
        }
        moving |= _covMode != CoverMode.Rest || Math.Abs(_covVel) > 0.5
                  || (_covMode == CoverMode.Rest && owner?.IsCoverOpen != true && Math.Abs(_cov - (full ? lift : 0)) > 0.01);

        // Bat.
        switch (_batMode)
        {
            case BatMode.Held:
                if (_cov <= CatchAngle || _covMode != CoverMode.Close)
                {
                    _batMode = BatMode.Drive;
                    _batGoal = BatThrow;
                    _batVel += 300;
                }
                break;
            case BatMode.Drive:
                var target = _batGoal > 0 ? 591 : -591;
                var before = _bat;
                StepStop(ref _bat, ref _batVel, 246, 122, target, _batGoal, 0.74, up: _batGoal > 0, h);
                if (Math.Abs(before - _batGoal) > 0.5 && Math.Abs(_bat - _batGoal) < 0.001)
                {
                    _tiltXVel += 3;
                }
                if (Math.Abs(_bat - _batGoal) < 0.02 && Math.Abs(_batVel) < 1)
                {
                    _bat = _batGoal;
                    _batVel = 0;
                    _batMode = BatMode.Rest;
                }
                break;
            case BatMode.Drag:
                Spring(ref _bat, ref _batVel, _batDragTarget, 3000, 110, h);
                break;
        }
        moving |= _batMode != BatMode.Rest;

        // Lens press, hold ring.
        Spring(ref _lens, ref _lensVel, _holding ? 0.945 : 1, 950, 29.6, h);
        moving |= Math.Abs(_lens - (_holding ? 0.945 : 1)) > 0.0005 || Math.Abs(_lensVel) > 0.005;
        if (_holding && owner is not null)
        {
            _raw = Math.Min(1, _raw + (h / Math.Max(0.3, owner.HoldSeconds)));
            if (_raw >= 1)
            {
                _holding = false;
                owner.CompleteHold();
                return true;
            }
            if (full)
            {
                Spring(ref _hold, ref _holdVel, _raw, 600, 49, h);
            }
            else
            {
                _hold = _raw;
                _holdVel = 0;
            }
            moving = true;
        }
        else if (owner?.Status is ArmingSwitchStatus.Done or ArmingSwitchStatus.Running && _doneClock < 0 && _completeClock < 0)
        {
            _hold = 0;
            _holdVel = 0;
            _raw = 0;
        }
        else if (_completeClock < 0 && _failClock < 0 && _doneClock < 0)
        {
            if (full)
            {
                Spring(ref _hold, ref _holdVel, 0, 115, 22, h);
            }
            else
            {
                _hold = Math.Max(0, _hold - (h * 3));
                _holdVel = 0;
            }
            if (_hold < 0.0005 && Math.Abs(_holdVel) < 0.005)
            {
                _hold = 0;
                _holdVel = 0;
            }
            _raw = _hold;
            moving |= _hold > 0;
        }
        else if (_failClock >= 0 && _failClock > 0.35)
        {
            // The red ring drains after the blink.
            Spring(ref _hold, ref _holdVel, 0, 115, 22, h);
            if (_hold < 0.0005)
            {
                _hold = 0;
                _holdVel = 0;
            }
            moving |= _hold > 0;
        }
        return moving;
    }

    /// <summary>A spring towards <paramref name="drive"/> that hits a stop at <paramref name="stop"/> and bounces back with <paramref name="restitution"/>.</summary>
    private static void StepStop(ref double x, ref double v, double k, double c, double drive, double stop, double restitution, bool up, double h)
    {
        var a = (k * (drive - x)) - (c * v);
        v += a * h;
        x += v * h;
        if (up && x > stop)
        {
            x = stop;
            v = -Math.Abs(v) * restitution;
        }
        else if (!up && x < stop)
        {
            x = stop;
            v = Math.Abs(v) * restitution;
        }
    }

    /// <summary>Advances the timelines; returns whether any of them is still playing.</summary>
    private bool AdvanceClocks(double dt, bool full)
    {
        var owner = Owner;
        var status = owner?.Status ?? ArmingSwitchStatus.Safe;
        var moving = false;
        _ambient += dt;
        if (_shownClock >= 0)
        {
            _shownClock += dt;
            if (!_hopDone && _shownClock > 0.7)
            {
                _hopDone = true;
                if (full && owner is not null && !owner.IsCoverOpen && _covMode == CoverMode.Rest)
                {
                    _covVel += 300;
                }
            }
        }
        Tick(ref _armClock, dt, 1.0, ref moving);
        Tick(ref _completeClock, dt, 0.8, ref moving);
        Tick(ref _doneClock, dt, 1.2, ref moving);
        Tick(ref _failClock, dt, 1.2, ref moving);
        Tick(ref _burstAccent, dt, 0.8, ref moving);
        Tick(ref _burstGreen, dt, 0.8, ref moving);
        Tick(ref _burstRed, dt, 0.8, ref moving);
        Tick(ref _glint, dt, 1.0, ref moving);
        Tick(ref _shakeClock, dt, 0.45, ref moving);
        if (_runClock >= 0)
        {
            _runClock += dt;
            moving = true;
        }

        // Lamp level and colour.
        var goal = LevelGoal(status);
        var rate = goal > _level ? (_holding ? 0.35 : 4) : (_holding ? 0.17 : 6);
        if (!AnyMotion)
        {
            _level = goal;
        }
        else if (status == ArmingSwitchStatus.Armed && _armClock >= 0 && full && !_holding)
        {
            _level = Flicker(_armClock);
        }
        else if (status == ArmingSwitchStatus.Armed && _holding)
        {
            _level = Math.Min(1, _level + (dt * 0.35));
        }
        else if (status == ArmingSwitchStatus.Armed && _level > goal && _completeClock < 0)
        {
            _level = Math.Max(goal, _level - (dt * 0.17));
        }
        else
        {
            _level = Approach(_level, goal, dt * rate * (full ? 1 : 2.5));
        }
        moving |= Math.Abs(_level - goal) > 0.002;

        var (a, g, r) = ColourGoal(status);
        var k = AnyMotion ? 1 - Math.Exp(-dt / (full ? 0.07 : 0.1)) : 1;
        _amber += (a - _amber) * k;
        _green += (g - _green) * k;
        _red += (r - _red) * k;
        moving |= Math.Abs(_amber - a) + Math.Abs(_green - g) + Math.Abs(_red - r) > 0.004;

        // Ring fill and spinner.
        if (status == ArmingSwitchStatus.Running)
        {
            _spin = (_spin + (dt * 420)) % 360;
            var t = _runClock;
            _fillOpacity = full ? Math.Exp(-t / 0.11) : Math.Max(0, 1 - (t / 0.2));
            _spinOpacity = 1 - _fillOpacity;
        }
        else if (_doneClock >= 0)
        {
            var t = _doneClock;
            var rise = full ? 1 - Math.Exp(-t / 0.11) : 1;
            _fillOpacity = t < 0.53 ? rise : Math.Max(0, rise * (1 - ((t - 0.53) / 0.6)));
            _spinOpacity = Math.Max(0, _spinOpacity - (dt / 0.2));
            _spin = (_spin + (dt * 420 * _spinOpacity)) % 360;
            _hold = 1;
        }
        else if (_failClock >= 0)
        {
            _fillOpacity = 1;
            _spinOpacity = Math.Max(0, _spinOpacity - (dt / 0.2));
            _spin = (_spin + (dt * 420 * _spinOpacity)) % 360;
        }
        else
        {
            _spinOpacity = Math.Max(0, _spinOpacity - (dt / 0.2));
            _fillOpacity = _hold > 0.001 ? 1 : 0;
        }
        moving |= _spinOpacity > 0;
        return moving;
    }

    private static void Tick(ref double clock, double dt, double end, ref bool moving)
    {
        if (clock < 0)
        {
            return;
        }
        clock += dt;
        if (clock > end)
        {
            clock = -1;
        }
        else
        {
            moving = true;
        }
    }

    private static double Approach(double x, double goal, double step) =>
        x < goal ? Math.Min(goal, x + step) : Math.Max(goal, x - step);

    /// <summary>The lamp's resting brightness for a status (before the arm flicker, the hold and the run pulse).</summary>
    private double LevelGoal(ArmingSwitchStatus status) => status switch
    {
        ArmingSwitchStatus.Armed => _holding ? 1 : (Full ? 0.92 : 0.9),
        ArmingSwitchStatus.Running => RunPulse(),
        ArmingSwitchStatus.Done => 1,
        ArmingSwitchStatus.Failed => FailBlink(),
        _ => 0
    };

    private double RunPulse()
    {
        if (_runClock < 0)
        {
            return 1;
        }
        if (!AnyMotion)
        {
            return 1;
        }
        // Arc: 1 → 0.70 → 1 every ~633 ms after a 350 ms hold at full (about 1.4 s with reduced motion).
        var period = Full ? 0.633 : 1.4;
        var t = _runClock - 0.35;
        return t <= 0 ? 1 : 0.85 + (0.15 * Math.Cos(t * 2 * Math.PI / period));
    }

    private double FailBlink()
    {
        if (_failClock < 0 || !Full)
        {
            return 0.92;
        }
        // Three blinks in 0.6 s, then steady.
        var t = _failClock;
        if (t > 0.6)
        {
            return 0.92;
        }
        return ((int)(t / 0.1) % 2) == 0 ? 1 : 0.25;
    }

    private (double Amber, double Green, double Red) ColourGoal(ArmingSwitchStatus status) => status switch
    {
        ArmingSwitchStatus.Done => (0, 1, 0),
        ArmingSwitchStatus.Failed => (0, 0, 1),
        _ => (1, 0, 0)
    };

    private void SetColourAt(ArmingSwitchStatus status, bool snap)
    {
        if (snap || !AnyMotion)
        {
            (_amber, _green, _red) = ColourGoal(status);
        }
    }

    // Arc's filament: two quick flickers, then up to 0.92 (keyframes of the measured lamp opacity, seconds).
    private static readonly double[] FlickerTimes = { 0, 0.016, 0.033, 0.049, 0.067, 0.083, 0.1, 0.117, 0.133, 0.15, 0.167, 0.183, 0.2, 0.217, 0.233, 0.25, 0.267, 0.283, 0.3, 0.317, 0.333, 0.35, 0.383, 0.433 };
    private static readonly double[] FlickerValues = { 0, 0.083, 0.23, 0.384, 0.445, 0.412, 0.441, 0.543, 0.665, 0.776, 0.796, 0.731, 0.638, 0.605, 0.639, 0.70, 0.763, 0.816, 0.857, 0.884, 0.899, 0.907, 0.915, 0.92 };

    private static double Flicker(double t)
    {
        if (t >= FlickerTimes[^1])
        {
            return FlickerValues[^1];
        }
        for (var i = 1; i < FlickerTimes.Length; i++)
        {
            if (t <= FlickerTimes[i])
            {
                var u = (t - FlickerTimes[i - 1]) / (FlickerTimes[i] - FlickerTimes[i - 1]);
                return FlickerValues[i - 1] + ((FlickerValues[i] - FlickerValues[i - 1]) * u);
            }
        }
        return FlickerValues[^1];
    }

    // --- read by the drawing and the tests ---

    /// <summary>Steps the motion by <paramref name="ms"/> in 60 fps frames, as the loop would (tests and snapshots).</summary>
    internal void AdvanceFrames(double ms, double frameMs = 1000.0 / 60)
    {
        while (ms > 1e-6)
        {
            var step = Math.Min(frameMs, ms);
            StepFrame(step / 1000);
            ms -= step;
        }
    }

    internal double CoverAngle => _cov;

    internal double BatAngle => _bat;

    internal double HoldProgress => _hold;

    internal bool IsHolding => _holding;

    internal double LampLevel => _level;

    internal (double X, double Y) Tilt => (_tiltX, _tiltY);

    /// <summary>The breath of a lit lamp: 1 → 0.78 → 1 every 2.2 s (ease in-out), after 0.4 s; 1 without full motion.</summary>
    private double Breath()
    {
        if (!Full || _holding)
        {
            return 1;
        }
        var t = _ambient - 0.4;
        if (t < 0)
        {
            return 1;
        }
        var u = (t % 2.2) / 2.2;
        var tri = u < 0.5 ? u * 2 : (1 - u) * 2;
        var eased = tri < 0.5 ? 4 * tri * tri * tri : 1 - (Math.Pow((-2 * tri) + 2, 3) / 2);
        return 1 - (0.22 * eased);
    }

    /// <summary>The ready light's brightness: Arc's 3.2 s heartbeat while idle, a flicker while running, lit when done.</summary>
    private double Heartbeat(ArmingSwitchStatus status)
    {
        if (status == ArmingSwitchStatus.Done)
        {
            return 1;
        }
        if (!Full)
        {
            return status == ArmingSwitchStatus.Running ? 0.6 : 0.12;
        }
        if (status == ArmingSwitchStatus.Running)
        {
            var f = Math.Sin(_ambient * 37) * Math.Sin(_ambient * 23.3);
            return 0.35 + (0.65 * Math.Abs(f));
        }
        var u = (_ambient % 3.2) / 3.2;
        return u switch
        {
            < 0.66 => 0.12,
            < 0.70 => Lerp(0.12, 1, (u - 0.66) / 0.04),
            < 0.76 => Lerp(1, 0.3, (u - 0.70) / 0.06),
            < 0.80 => Lerp(0.3, 0.92, (u - 0.76) / 0.04),
            < 0.88 => Lerp(0.92, 0.12, (u - 0.80) / 0.08),
            _ => 0.12
        };
    }

    /// <summary>The brushed streaks' drift: ±0.035 W across and 0.006 W down, 13 s each way (ease in-out); none without full motion.</summary>
    private (double X, double Y) Drift()
    {
        if (!Full)
        {
            return (0, 0);
        }
        var u = (_ambient % 26) / 13;
        var t = u <= 1 ? u : 2 - u;
        var e = t < 0.5 ? 4 * t * t * t : 1 - (Math.Pow((-2 * t) + 2, 3) / 2);
        return (Lerp(-0.035, 0.035, e), Lerp(0, 0.006, e));
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}

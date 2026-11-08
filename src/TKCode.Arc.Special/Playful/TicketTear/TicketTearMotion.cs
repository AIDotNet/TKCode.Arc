using Avalonia;

namespace TKCode.Arc;

/// <summary>Where a stub is in its short life.</summary>
internal enum TicketTearPhase
{
    /// <summary>On the ticket, nobody holding it. It may already be torn part of the way.</summary>
    Attached,

    /// <summary>A pointer is pulling it.</summary>
    Held,

    /// <summary>The rest of the perforation is ripping on its own.</summary>
    Ripping,

    /// <summary>Free of the ticket, falling and fading.</summary>
    Falling,

    /// <summary>Gone from the ticket.</summary>
    Gone
}

/// <summary>
/// The state of a stub, free of any control so it can be tested without a window. The tear only grows; the flap follows
/// the pull on a spring; once free, the stub falls and spins from its own speed. Hand-integrated because the motion mixes
/// the pointer, a growing rip, gravity and two springs: no ControlTheme transition or single MotionValue can express it.
/// </summary>
internal sealed class TicketTearMotion
{
    /// <summary>The most a flap bends, in radians.</summary>
    public const double MaxAngle = 24 * Math.PI / 180;

    /// <summary>A release faster than this, in px/s down or away, finishes the tear.</summary>
    public const double FlickSpeed = 700;

    /// <summary>How long a free stub takes to fade, in seconds.</summary>
    public const double FallDuration = 0.5;

    /// <summary>How long the reduced motion fade takes, in seconds.</summary>
    public const double FadeDuration = 0.16;

    /// <summary>How long a fresh stub takes to appear, in seconds.</summary>
    public const double FreshDuration = 0.18;

    /// <summary>The share of its bend a released flap keeps: it returns most of the way, not all of it.</summary>
    public const double RestFraction = 0.18;

    private const double RipStartSpeed = 420;
    private const double RipAcceleration = 2600;
    private const double HoleGiveWay = 1.08;
    private const double HoleJolt = 0.7;
    private const double AngleStiffness = 320;
    private const double AngleDamping = 24;
    private const double SpinStiffness = 70;
    private const double SpinDamping = 8;
    private const double Gravity = 2200;
    private const double PopSpeed = 260;
    private const double SpinAngle = 34 * Math.PI / 180;
    private const double MaxStep = 1.0 / 30;

    private IReadOnlyList<double> _holes = [];
    private double _start;
    private double _span = 1;
    private double _height;
    private double _grabProgress;
    private double _ripSpeed;
    private double _angleVelocity;
    private Vector _fallVelocity;
    private double _spinTarget;
    private double _spinVelocity;
    private double _fallTime;
    private Vector _lastVelocity;

    public TicketTearPhase Phase { get; private set; } = TicketTearPhase.Attached;

    /// <summary>Whether things may move. Without it there is no bend and no fall, and the stub only fades.</summary>
    public bool CanMove { get; set; } = true;

    /// <summary>How far the tear has run, from 0 to 1. It never goes back.</summary>
    public double Progress { get; private set; }

    /// <summary>The height the tear has reached, from the top of the ticket.</summary>
    public double Front => Phase is TicketTearPhase.Falling or TicketTearPhase.Gone ? _height : _start + Progress * _span;

    /// <summary>How many holes the tear has gone through.</summary>
    public int HolesTorn { get; private set; }

    /// <summary>The flap's bend, in radians.</summary>
    public double Angle { get; private set; }

    /// <summary>The bend the flap is heading for, in radians.</summary>
    public double AngleTarget { get; private set; }

    /// <summary>A falling stub's distance from where it came off.</summary>
    public Vector FallOffset { get; private set; }

    /// <summary>A falling stub's spin, in radians.</summary>
    public double FallRotation { get; private set; }

    /// <summary>A falling stub's opacity.</summary>
    public double FallOpacity { get; private set; } = 1;

    /// <summary>A newly placed stub's opacity.</summary>
    public double FreshOpacity { get; private set; } = 1;

    /// <summary>Raised when the stub comes free of the ticket.</summary>
    public event Action? Freed;

    /// <summary>Raised when the stub has finished leaving.</summary>
    public event Action? Finished;

    /// <summary>Sets the size; the tear keeps its share of the perforation.</summary>
    public void Configure(double height, double notch, IReadOnlyList<double> holes)
    {
        _height = height;
        _start = notch;
        _span = Math.Max(1, height - 2 * notch);
        _holes = holes;
        HolesTorn = 0;
        CountHoles(false);
    }

    public void Grab()
    {
        if (Phase is TicketTearPhase.Attached)
        {
            Phase = TicketTearPhase.Held;
            _grabProgress = Progress;
        }
    }

    /// <summary>The hand has moved <paramref name="pull"/> from where it pressed, at <paramref name="velocity"/> px/s.</summary>
    public void Drag(Vector pull, Vector velocity)
    {
        if (Phase is not TicketTearPhase.Held)
        {
            return;
        }
        _lastVelocity = velocity;
        // Down or away from the body both tear. Whichever the hand has done more of leads, 1:1.
        var along = Math.Max(0, Math.Max(pull.X, pull.Y));
        Advance(_grabProgress + along / _span);
        if (Phase is not TicketTearPhase.Held)
        {
            return;
        }
        AngleTarget = CanMove ? BendFor(pull) : 0;
    }

    public void Release(Vector velocity)
    {
        if (Phase is not TicketTearPhase.Held)
        {
            return;
        }
        var speed = Math.Max(velocity.X, velocity.Y);
        if (speed >= FlickSpeed && Progress > 0)
        {
            Rip(velocity);
            return;
        }
        Phase = TicketTearPhase.Attached;
        AngleTarget = CanMove ? Angle * RestFraction : 0;
    }

    /// <summary>Tears the rest of the way, faster the harder the flick was.</summary>
    public void Rip(Vector velocity = default)
    {
        if (Phase is TicketTearPhase.Falling or TicketTearPhase.Gone or TicketTearPhase.Ripping)
        {
            return;
        }
        _lastVelocity = velocity;
        if (!CanMove)
        {
            Advance(1);
            return;
        }
        _ripSpeed = Math.Max(RipStartSpeed, Math.Max(velocity.X, velocity.Y));
        Phase = TicketTearPhase.Ripping;
    }

    /// <summary>Advances one frame. Returns whether anything is still moving.</summary>
    public bool Step(double dt)
    {
        dt = Math.Min(dt, MaxStep);
        if (FreshOpacity < 1)
        {
            FreshOpacity = Math.Min(1, FreshOpacity + dt / FreshDuration);
        }
        if (Phase is TicketTearPhase.Ripping)
        {
            _ripSpeed += RipAcceleration * dt;
            Advance(Progress + _ripSpeed * dt / _span);
            if (Phase is TicketTearPhase.Ripping)
            {
                AngleTarget = Math.Max(AngleTarget, MaxAngle * (0.35 + 0.45 * Progress));
            }
        }
        if (Phase is TicketTearPhase.Falling)
        {
            StepFall(dt);
        }
        if (Phase is TicketTearPhase.Gone)
        {
            return FreshOpacity < 1;
        }
        var angleMoving = StepAngle(dt);
        return Phase is TicketTearPhase.Ripping or TicketTearPhase.Falling || angleMoving || FreshOpacity < 1;
    }

    /// <summary>Jumps to where the stub is headed (motion off).</summary>
    public void Snap()
    {
        FreshOpacity = 1;
        Angle = AngleTarget = 0;
        _angleVelocity = 0;
        if (Phase is TicketTearPhase.Ripping)
        {
            Advance(1);
        }
        if (Phase is TicketTearPhase.Falling)
        {
            Finish();
        }
    }

    /// <summary>The stub is gone already, with no motion and no events (a ticket that starts torn).</summary>
    public void JumpGone()
    {
        Progress = 1;
        Phase = TicketTearPhase.Gone;
        Angle = AngleTarget = 0;
        _angleVelocity = 0;
        FallOpacity = 0;
        FreshOpacity = 1;
        CountHoles(false);
    }

    /// <summary>A fresh stub takes the place of the old one.</summary>
    public void Reset(bool fade)
    {
        Phase = TicketTearPhase.Attached;
        Progress = 0;
        HolesTorn = 0;
        Angle = AngleTarget = 0;
        _angleVelocity = 0;
        _ripSpeed = 0;
        FallOffset = default;
        _fallVelocity = default;
        FallRotation = 0;
        _spinVelocity = 0;
        _fallTime = 0;
        FallOpacity = 1;
        FreshOpacity = fade ? 0 : 1;
    }

    private static double BendFor(Vector pull)
    {
        var away = Math.Max(0, pull.X) * 0.8 + Math.Max(0, pull.Y) * 0.25;
        return MaxAngle * (1 - Math.Exp(-away / 70));
    }

    private void Advance(double progress)
    {
        progress = Math.Min(progress, 1);
        if (progress <= Progress)
        {
            return;
        }
        Progress = progress;
        CountHoles(true);
        if (Progress >= 1)
        {
            Free();
        }
    }

    private void CountHoles(bool felt)
    {
        var front = _start + Progress * _span;
        while (HolesTorn < _holes.Count && front >= _holes[HolesTorn])
        {
            HolesTorn++;
            if (!felt)
            {
                continue;
            }
            // A hole lets go: the rip speeds up and the flap gives a small jolt.
            _ripSpeed *= HoleGiveWay;
            if (CanMove)
            {
                _angleVelocity += HoleJolt;
            }
        }
    }

    private void Free()
    {
        Phase = TicketTearPhase.Falling;
        _fallTime = 0;
        FallOpacity = 1;
        if (CanMove)
        {
            _fallVelocity = new Vector(Math.Clamp(_lastVelocity.X * 0.35, -520, 520), -PopSpeed + Math.Clamp(_lastVelocity.Y * 0.12, 0, 160));
            _spinTarget = (_lastVelocity.X < -20 ? -1 : 1) * SpinAngle;
            _spinVelocity = _spinTarget * 2;
            AngleTarget = MaxAngle * 0.3;
        }
        Freed?.Invoke();
    }

    private void StepFall(double dt)
    {
        _fallTime += dt;
        if (CanMove)
        {
            _fallVelocity += new Vector(0, Gravity * dt);
            FallOffset += _fallVelocity * dt;
            _spinVelocity += (SpinStiffness * (_spinTarget - FallRotation) - SpinDamping * _spinVelocity) * dt;
            FallRotation += _spinVelocity * dt;
            var u = _fallTime / FallDuration;
            FallOpacity = Math.Max(0, 1 - u * u);
            if (u >= 1)
            {
                Finish();
            }
        }
        else
        {
            FallOpacity = Math.Max(0, 1 - _fallTime / FadeDuration);
            if (_fallTime >= FadeDuration)
            {
                Finish();
            }
        }
    }

    private void Finish()
    {
        Phase = TicketTearPhase.Gone;
        FallOpacity = 0;
        Finished?.Invoke();
    }

    private bool StepAngle(double dt)
    {
        if (!CanMove)
        {
            Angle = AngleTarget = 0;
            _angleVelocity = 0;
            return false;
        }
        _angleVelocity += (AngleStiffness * (AngleTarget - Angle) - AngleDamping * _angleVelocity) * dt;
        Angle = Math.Max(0, Angle + _angleVelocity * dt);
        if (Math.Abs(AngleTarget - Angle) < 1e-3 && Math.Abs(_angleVelocity) < 1e-2)
        {
            Angle = AngleTarget;
            _angleVelocity = 0;
            return false;
        }
        return true;
    }
}

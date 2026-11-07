namespace TKCode.Arc;

/// <summary>What a call or a step of <see cref="ChargeSessionModel"/> changed.</summary>
internal enum ChargeSessionTransition
{
    None,
    Stopped,
    Resumed,
    Completed,
    NewSession,
}

/// <summary>
/// The charging session of <see cref="ChargeSession"/> as plain state (Arc Charge session, design reference): the mode, the
/// state of charge, the availability level and the limit, advanced by <see cref="Step"/>. Power is the peak power times
/// the curve times availability; availability follows its target (1 while charging, 0 otherwise) with τ 0.384 s up and
/// 0.237 s down and snaps to 0 below 0.004; the charge integrates the power while not complete (so it creeps on a little
/// while a stop drains), and reaching the limit while charging completes the session at exactly the limit. No UI, no
/// clock: the control feeds it frame times.
/// </summary>
internal sealed class ChargeSessionModel
{
    /// <summary>Time constant of availability ramping up after start, resume or a new session (fitted .383/.384/.384).</summary>
    public const double RampTau = 0.384;

    /// <summary>Time constant of availability draining after stop or completion (fitted, RMS .0009).</summary>
    public const double DrainTau = 0.237;

    /// <summary>Availability below this counts as drained.</summary>
    public const double DrainedBelow = 0.004;

    private double _limit = 80;
    private readonly Func<double, double> _shareOfFraction;

    public ChargeSessionModel()
    {
        // One cached delegate for the per-frame time integral (a lambda there would allocate a closure every frame).
        _shareOfFraction = s => Share(s * 100);
    }

    /// <summary>Usable capacity in kWh.</summary>
    public double Capacity { get; set; } = 82;

    /// <summary>Peak power in kW.</summary>
    public double MaxPower { get; set; } = 180;

    /// <summary>Charge in percent where a session starts.</summary>
    public double StartCharge { get; set; } = 28;

    /// <summary>Lowest limit in percent.</summary>
    public double MinLimit { get; set; } = 50;

    /// <summary>Charge in percent where the taper starts.</summary>
    public double TaperAt { get; set; } = 55;

    /// <summary>A custom curve (charge 0..1 to power share 0..1); null uses <see cref="ChargeSessionCurve.Default"/>.</summary>
    public Func<double, double>? Curve { get; set; }

    /// <summary>Charging, stopped or complete.</summary>
    public ChargeSessionState State { get; private set; } = ChargeSessionState.Charging;

    /// <summary>State of charge in percent.</summary>
    public double Charge { get; private set; } = 28;

    /// <summary>How much of the curve's power flows now, 0..1.</summary>
    public double Availability { get; private set; }

    /// <summary>The limit in whole percent, between <see cref="MinLimit"/> and 100.</summary>
    public double Limit => _limit;

    /// <summary>The curve's power share at <paramref name="charge"/> percent.</summary>
    public double Share(double charge)
    {
        var s = Math.Clamp(charge / 100, 0, 1);
        var value = Curve is { } curve ? curve(s) : ChargeSessionCurve.Default(s, Math.Clamp(TaperAt / 100, 0, 1));
        return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    }

    /// <summary>The power in kW flowing now.</summary>
    public double Power => MaxPower * Share(Charge) * Availability;

    /// <summary>The power in kW the curve gives at the current charge (availability left out).</summary>
    public double CurvePower => MaxPower * Share(Charge);

    /// <summary>Energy added this session in kWh.</summary>
    public double EnergyAdded => Math.Max(0, Charge - StartCharge) / 100 * Capacity;

    /// <summary>Minutes from the charge to the limit at full availability (0 when the limit is reached).</summary>
    public double MinutesToLimit => ChargeSessionCurve.MinutesToLimit(Charge, _limit, Capacity, MaxPower, _shareOfFraction);

    /// <summary>Whether the session still moves: charging, or availability still draining.</summary>
    public bool IsMoving => State == ChargeSessionState.Charging || Availability > 0;

    /// <summary>Rounds and clamps a limit to whole percent between <see cref="MinLimit"/> and 100.</summary>
    public double Coerce(double limit)
    {
        var min = Math.Clamp(Math.Round(MinLimit), 0, 100);
        return double.IsFinite(limit) ? Math.Clamp(Math.Round(limit), min, 100) : min;
    }

    /// <summary>Starts a session from scratch: <paramref name="charge"/>, charging, availability 0 (or 1 when instant).</summary>
    public void Reset(double charge, double limit, bool instant)
    {
        Charge = Math.Clamp(charge, 0, 100);
        _limit = Coerce(limit);
        State = ChargeSessionState.Charging;
        Availability = instant ? 1 : 0;
        if (_limit <= Charge)
        {
            State = ChargeSessionState.Complete;
            Availability = 0;
        }
    }

    /// <summary>Charging → stopped.</summary>
    public ChargeSessionTransition Stop(bool instant)
    {
        if (State != ChargeSessionState.Charging)
        {
            return ChargeSessionTransition.None;
        }
        State = ChargeSessionState.Stopped;
        if (instant)
        {
            Availability = 0;
        }
        return ChargeSessionTransition.Stopped;
    }

    /// <summary>Stopped → charging, or straight to complete when the limit is already reached.</summary>
    public ChargeSessionTransition Resume(bool instant)
    {
        if (State != ChargeSessionState.Stopped)
        {
            return ChargeSessionTransition.None;
        }
        if (_limit <= Charge)
        {
            Charge = Math.Min(Charge, 100);
            State = ChargeSessionState.Complete;
            return ChargeSessionTransition.Completed;
        }
        State = ChargeSessionState.Charging;
        if (instant)
        {
            Availability = 1;
        }
        return ChargeSessionTransition.Resumed;
    }

    /// <summary>Any state → charging from <see cref="StartCharge"/> (energy back to zero); the limit stays.</summary>
    public ChargeSessionTransition StartNew(bool instant)
    {
        Charge = Math.Clamp(StartCharge, 0, 100);
        if (_limit <= Charge)
        {
            State = ChargeSessionState.Complete;
            Availability = 0;
            return ChargeSessionTransition.NewSession;
        }
        State = ChargeSessionState.Charging;
        Availability = instant ? 1 : 0;
        return ChargeSessionTransition.NewSession;
    }

    /// <summary>
    /// Moves the limit (rounded and clamped). Charging with the limit at or under the charge completes at once; complete with
    /// the limit above the charge resumes; stopped stays stopped.
    /// </summary>
    public ChargeSessionTransition SetLimit(double limit, bool instant)
    {
        _limit = Coerce(limit);
        if (State == ChargeSessionState.Charging && _limit <= Charge)
        {
            State = ChargeSessionState.Complete;
            if (instant)
            {
                Availability = 0;
            }
            return ChargeSessionTransition.Completed;
        }
        if (State == ChargeSessionState.Complete && _limit > Charge)
        {
            State = ChargeSessionState.Charging;
            if (instant)
            {
                Availability = 1;
            }
            return ChargeSessionTransition.Resumed;
        }
        return ChargeSessionTransition.None;
    }

    /// <summary>Puts availability at its target at once (reduced motion).</summary>
    public void Settle() => Availability = State == ChargeSessionState.Charging ? 1 : 0;

    /// <summary>
    /// Advances <paramref name="dt"/> seconds on screen: availability follows its target; the charge integrates the power at
    /// <paramref name="speed"/> times simulated time unless complete. Returns <see cref="ChargeSessionTransition.Completed"/>
    /// when the charge reached the limit.
    /// </summary>
    public ChargeSessionTransition Step(double dt, double speed)
    {
        if (dt <= 0)
        {
            return ChargeSessionTransition.None;
        }
        var target = State == ChargeSessionState.Charging ? 1.0 : 0.0;
        var a = ChargeSessionSmoother.Approach(Availability, target, dt, target > Availability ? RampTau : DrainTau);
        if (target == 0 && a < DrainedBelow)
        {
            a = 0;
        }
        Availability = Math.Clamp(a, 0, 1);
        if (State == ChargeSessionState.Complete || speed <= 0 || Availability <= 0)
        {
            return ChargeSessionTransition.None;
        }
        var rate = ChargeSessionCurve.PercentPerSecond(Power, Capacity, speed);
        var next = Math.Min(100, Charge + rate * dt);
        if (State == ChargeSessionState.Charging && next >= _limit)
        {
            Charge = _limit;
            State = ChargeSessionState.Complete;
            return ChargeSessionTransition.Completed;
        }
        // A draining stop creeps on a little but never past the limit.
        Charge = State == ChargeSessionState.Stopped ? Math.Max(Charge, Math.Min(next, _limit)) : next;
        return ChargeSessionTransition.None;
    }
}

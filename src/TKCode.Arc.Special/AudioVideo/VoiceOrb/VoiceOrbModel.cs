using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The orb's animated state, free of any UI so tests can step it: ten shape parameters, each on its own spring toward the
/// targets of the current state (Arc Voice orb, design reference: k 55, c 17, so ω ≈ 7.4 rad/s and ζ ≈ 1.15, fitted on
/// six recorded listening → thinking changes; no overshoot, 99 % in about 800 ms). A change of state while a spring
/// moves keeps its velocity. Besides the springs it runs the continuous phases: the 5.03 s breath, the drifting outline
/// harmonics, the ripple phase (faster with a louder voice), the thinking band (≈160°/s clockwise), the swirl that winds
/// up while thinking and slowly unwinds, and the level envelope (fast attack, slower release).
/// </summary>
internal sealed class VoiceOrbModel
{
    /// <summary>The number of outline points.</summary>
    public const int Points = 96;

    /// <summary>The breathing period in seconds (two independent fits: 5.027 and 5.026 s).</summary>
    public const double BreathPeriod = 5.027;

    /// <summary>The thinking band's angular speed (rad/s): 160°/s, clockwise on screen.</summary>
    public const double BandSpeed = 160 * Math.PI / 180;

    /// <summary>The outer edge of the halo as a share of the size: the halo always fades out by 0.47·size.</summary>
    public const double HaloExtent = 0.47;

    /// <summary>The shared shape spring (k 55, c 17).</summary>
    public static readonly SpringSpec ShapeSpring = SpringSpec.FromPhysical(55, 17);

    // Parameter slots.
    public const int Radius = 0;
    public const int Breath = 1;
    public const int Gain = 2;
    public const int Wobble = 3;
    public const int Lobes = 4;
    public const int Halo = 5;
    public const int Ripple = 6;
    public const int Band = 7;
    public const int Swirl = 8;
    public const int Saturation = 9;
    public const int Count = 10;

    // Targets per state (radius, breath and gain as a share of the size; halo relative to idle; see the spec's table).
    private static readonly double[] s_idle = [0.302, 0.0085, 0, 1, 0, 1, 0, 0, 0.04, 1];
    private static readonly double[] s_listening = [0.31, 0.004, 0.03, 1.6, 0.45, 1.65, 1, 0, 0.12, 1];
    private static readonly double[] s_thinking = [0.275, 0.003, 0, 2.05, 0, 1.75, 0, 1, 1, 1];
    private static readonly double[] s_speaking = [0.31, 0.004, 0.022, 2, 1, 2.3, 0, 0, 0.6, 1];
    private static readonly double[] s_muted = [0.295, 0.004, 0, 1.2, 0, 0.55, 0, 0, 0.08, 0.35];

    // Outline harmonics in px at size 200 (measured): idle k2 .47, k3 .25, k5 .14 (scaled by Wobble); speaking adds
    // k4 .56 and k7 .39 (scaled by Lobes). Each drifts at its own angular speed.
    private static readonly int[] s_orders = [2, 3, 4, 5, 7];
    private static readonly double[] s_wobble = [0.47 / 200, 0.25 / 200, 0, 0.14 / 200, 0];
    private static readonly double[] s_lobes = [0, 0, 0.56 / 200, 0.13 / 200, 0.39 / 200];
    private static readonly double[] s_drift = [0.35, -0.5, 0.8, -0.62, 1.1];
    private static readonly double[] s_phase0 = [0.4, 2.1, 4.0, 1.3, 5.2];

    private readonly double[] _value = new double[Count];
    private readonly double[] _velocity = new double[Count];
    private readonly double[] _target = new double[Count];
    private readonly double[] _harmonicPhase = new double[5];

    public VoiceOrbModel()
    {
        Array.Copy(s_idle, _value, Count);
        Array.Copy(s_idle, _target, Count);
        for (var i = 0; i < _harmonicPhase.Length; i++)
        {
            _harmonicPhase[i] = s_phase0[i];
        }
    }

    /// <summary>The orb's clock in seconds (the level source reads it).</summary>
    public double Time { get; private set; }

    /// <summary>The marble's flow time (it runs faster with more energy).</summary>
    public double Flow { get; private set; }

    /// <summary>The swirl's twist at the centre, in radians.</summary>
    public double Twist { get; private set; }

    /// <summary>The thinking band's angle (radians, screen coordinates: growing = clockwise).</summary>
    public double BandAngle { get; private set; } = -2.3;

    /// <summary>The ripple phase: ring i is at frac(RipplePhase + i/2).</summary>
    public double RipplePhase { get; private set; }

    /// <summary>The level envelope (0 to 1) after attack and release.</summary>
    public double Envelope { get; private set; }

    /// <summary>The state whose targets the springs follow.</summary>
    public VoiceOrbState State { get; private set; }

    /// <summary>Whether the muted targets apply (only while listening).</summary>
    public bool IsMuted { get; private set; }

    /// <summary>The current value of parameter <paramref name="slot"/>.</summary>
    public double this[int slot] => _value[slot];

    /// <summary>The current velocity of parameter <paramref name="slot"/> (tests).</summary>
    public double VelocityOf(int slot) => _velocity[slot];

    /// <summary>True while any shape spring still moves.</summary>
    public bool IsSettling
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                if (Math.Abs(_value[i] - _target[i]) > 1e-4 || Math.Abs(_velocity[i]) > 1e-3)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The targets for <paramref name="state"/> (muted applies only to listening).</summary>
    public static double[] TargetsOf(VoiceOrbState state, bool muted) => state switch
    {
        VoiceOrbState.Listening => muted ? s_muted : s_listening,
        VoiceOrbState.Thinking => s_thinking,
        VoiceOrbState.Speaking => s_speaking,
        _ => s_idle
    };

    /// <summary>Points the springs at the targets of <paramref name="state"/>; velocity is kept.</summary>
    public void SetState(VoiceOrbState state, bool muted)
    {
        State = state;
        IsMuted = muted && state == VoiceOrbState.Listening;
        Array.Copy(TargetsOf(state, muted), _target, Count);
    }

    /// <summary>Jumps every parameter to its target (reduced motion, the first frame).</summary>
    public void Snap()
    {
        Array.Copy(_target, _value, Count);
        Array.Clear(_velocity);
    }

    /// <summary>
    /// Poses the still frame shown under reduced motion: breath at its mean, a fixed marble, the swirl half wound, the
    /// band at the upper left, one ripple ring a third of the way out and a moderate voice level.
    /// </summary>
    public void PoseStill()
    {
        Time = 0;
        Flow = 3.2;
        Twist = 1.8 * _value[Swirl];
        BandAngle = -2.36;
        RipplePhase = 0.32;
        Envelope = State switch
        {
            VoiceOrbState.Listening => IsMuted ? 0 : 0.35,
            VoiceOrbState.Speaking => 0.3,
            _ => 0
        };
    }

    /// <summary>
    /// Advances the model by <paramref name="dt"/> seconds with <paramref name="rawLevel"/> as this frame's level (0 to 1;
    /// the caller picks input or output by state). <paramref name="sharpAttack"/> makes the envelope rise within a frame
    /// (word pulses).
    /// </summary>
    public void Step(double dt, double rawLevel, bool sharpAttack = false)
    {
        if (dt <= 0)
        {
            return;
        }
        Time += dt;
        // Shape springs: semi-implicit Euler in sub-steps of at most 1/240 s (stable and deterministic for k 55, c 17).
        var k = ShapeSpring.Stiffness;
        var c = ShapeSpring.Damping;
        var steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
        var h = dt / steps;
        for (var s = 0; s < steps; s++)
        {
            for (var i = 0; i < Count; i++)
            {
                var a = k * (_target[i] - _value[i]) - c * _velocity[i];
                _velocity[i] += a * h;
                _value[i] += _velocity[i] * h;
            }
        }

        // The level envelope: rises in two or three frames (one for word pulses), falls over about half a second.
        var level = Math.Clamp(double.IsNaN(rawLevel) ? 0 : rawLevel, 0, 1);
        var tau = level > Envelope ? (sharpAttack ? 0.008 : 0.025) : (State == VoiceOrbState.Speaking ? 0.45 : 0.22);
        Envelope += (level - Envelope) * (1 - Math.Exp(-dt / tau));

        var energy = _value[Wobble] + _value[Lobes];
        Flow += dt * (0.55 + 0.22 * energy + 0.8 * Envelope * _value[Gain] / 0.03);
        Twist += dt * (0.9 * _value[Swirl] - 0.25 * Twist);
        BandAngle += dt * BandSpeed;
        if (BandAngle > Math.PI * 64)
        {
            BandAngle -= Math.PI * 64;
        }
        RipplePhase += dt * (0.55 + 0.6 * Envelope);
        if (RipplePhase > 1024)
        {
            RipplePhase -= 1024;
        }
        var drift = 1 + 0.5 * _value[Lobes];
        for (var i = 0; i < _harmonicPhase.Length; i++)
        {
            _harmonicPhase[i] += dt * s_drift[i] * drift;
        }
    }

    /// <summary>The mean radius in px for an orb of <paramref name="size"/>: base, breath and the level swell.</summary>
    public double MeanRadius(double size)
    {
        var breath = Math.Sin(2 * Math.PI * Time / BreathPeriod);
        return size * (_value[Radius] + _value[Breath] * breath + _value[Gain] * Envelope);
    }

    /// <summary>Fills <paramref name="radii"/> (length <see cref="Points"/>) with the outline radius in px per direction.</summary>
    public void Outline(double size, Span<double> radii)
    {
        var mean = MeanRadius(size);
        var wobble = _value[Wobble];
        var lobes = _value[Lobes] * (1 + 0.6 * Envelope);
        for (var p = 0; p < radii.Length; p++)
        {
            var theta = 2 * Math.PI * p / radii.Length;
            var offset = 0.0;
            for (var i = 0; i < s_orders.Length; i++)
            {
                var amplitude = wobble * s_wobble[i] + lobes * s_lobes[i];
                if (amplitude != 0)
                {
                    offset += amplitude * Math.Sin(s_orders[i] * theta + _harmonicPhase[i]);
                }
            }
            radii[p] = mean + size * offset;
        }
    }

    /// <summary>
    /// Ring <paramref name="ring"/> (0 or 1) of the listening ripple: its distance outside the mean radius (px at size 200
    /// scaled to <paramref name="size"/>) and its opacity. Rings are born 4 px out and ease out to 19 px while fading.
    /// </summary>
    public (double Offset, double Alpha) RippleRing(int ring, double size)
    {
        var strength = _value[Ripple];
        if (strength <= 0.003)
        {
            return (0, 0);
        }
        var phase = RipplePhase + ring * 0.5;
        phase -= Math.Floor(phase);
        var eased = 1 - (1 - phase) * (1 - phase);
        var offset = (4 + 15 * eased) * size / 200;
        // Born at 45-80 /255 with the voice, fading linearly; a short fade-in hides the birth.
        var birth = Math.Min(1, phase / 0.08);
        var alpha = strength * (0.17 + 0.15 * Envelope) * (1 - phase) * birth;
        return (offset, alpha);
    }

    /// <summary>The halo's opacity factor (1 = idle).</summary>
    public double HaloStrength => _value[Halo];

    /// <summary>The band's strength (0 to 1).</summary>
    public double BandStrength => Math.Clamp(_value[Band], 0, 1.2);

    /// <summary>The saturation factor (1 = full color; muted listening ≈ 0.35).</summary>
    public double SaturationFactor => Math.Clamp(_value[Saturation], 0, 1.2);
}

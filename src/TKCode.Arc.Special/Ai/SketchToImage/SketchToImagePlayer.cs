using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a <see cref="SketchToImagePlayer"/> is doing.</summary>
internal enum SketchToImagePhase
{
    /// <summary>Nothing moves: holding a stage, the photo, or blank paper.</summary>
    Rest,

    /// <summary>The timeline advances toward the target at the pace of one pass per duration.</summary>
    Play,

    /// <summary>The uncontrolled loop holds the finished photo before it starts again.</summary>
    Hold,

    /// <summary>The canvas fades to blank paper (520ms), resets while invisible and plays to the target again.</summary>
    FadeToPaper,

    /// <summary>Reduced motion: the old end state fades out (160ms).</summary>
    ReducedOut,

    /// <summary>Reduced motion: the new end state fades in (160ms).</summary>
    ReducedIn
}

/// <summary>
/// The clock logic of <see cref="SketchToImage"/>, apart from frames and drawing: one position <see cref="T"/> on the 0 → 1
/// timeline and the canvas's <see cref="Fade"/> toward blank paper. A controlled player plays to its target and holds there;
/// a target behind the position fades to paper first and plays again, so a change always travels or fades, never cuts.
/// An uncontrolled player plays the whole pass, holds the photo for two seconds, fades to paper and loops. Under reduced
/// motion a change is a short fade between end states; with motion off it lands at once.
/// </summary>
internal sealed class SketchToImagePlayer
{
    private double _hold;

    /// <summary>The position on the timeline (0 blank paper, 1 the finished photo).</summary>
    public double T { get; private set; }

    /// <summary>How much of the canvas shows (1 all, 0 blank paper).</summary>
    public double Fade { get; private set; } = 1;

    public SketchToImagePhase Phase { get; private set; }

    /// <summary>Where the player is heading.</summary>
    public double Target { get; private set; }

    /// <summary>Whether the app drives the stage; otherwise the player loops on its own.</summary>
    public bool IsControlled { get; private set; }

    /// <summary>Whether an uncontrolled player starts over after holding the photo.</summary>
    public bool Loops { get; set; } = true;

    /// <summary>Seconds for one full pass.</summary>
    public double Duration { get; set; } = 9;

    /// <summary>Whether frames are needed.</summary>
    public bool IsMoving => Phase != SketchToImagePhase.Rest;

    /// <summary>The progress the line shows: it drains with the canvas while fading to paper.</summary>
    public double Progress => Phase == SketchToImagePhase.FadeToPaper ? T * Fade : T;

    /// <summary>Raised each time the photo is fully revealed.</summary>
    public event Action? Completed;

    /// <summary>Puts the player where a freshly shown control starts: done shows the photo, anything else starts on paper.</summary>
    public void Begin(double? target, ArcMotionLevel level)
    {
        IsControlled = target is not null;
        Target = target ?? 1;
        Fade = 1;
        _hold = 0;
        var still = level != ArcMotionLevel.Full;
        if (Target >= 1 && (IsControlled || still))
        {
            // Mounting with done, or a loop that does not move, shows the photo straight away.
            T = 1;
            Phase = SketchToImagePhase.Rest;
            return;
        }
        T = still ? Target : 0;
        Phase = !still && Target > 0 ? SketchToImagePhase.Play : SketchToImagePhase.Rest;
    }

    /// <summary>Heads for a new target (null: loop on its own).</summary>
    public void Retarget(double? target, ArcMotionLevel level)
    {
        var controlled = target is not null;
        var next = target ?? 1;
        if (controlled == IsControlled && Math.Abs(next - Target) < 1e-9)
        {
            return;
        }
        IsControlled = controlled;
        Target = next;
        Go(level, restart: false);
    }

    /// <summary>Fades to blank paper and paints again up to the target.</summary>
    public void Replay(ArcMotionLevel level) => Go(level, restart: true);

    private void Go(ArcMotionLevel level, bool restart)
    {
        if (level == ArcMotionLevel.Off)
        {
            Land();
            if (restart && T >= 1)
            {
                Completed?.Invoke();
            }
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            if (!restart && Math.Abs(T - Target) < 1e-9 && Fade >= 1)
            {
                Phase = SketchToImagePhase.Rest;
                return;
            }
            Phase = SketchToImagePhase.ReducedOut;
            return;
        }
        if (restart || Target < T - 1e-9)
        {
            Phase = SketchToImagePhase.FadeToPaper;
            return;
        }
        if (Phase == SketchToImagePhase.FadeToPaper)
        {
            // Already on the way to paper: it plays to the new target after the reset.
            return;
        }
        Fade = 1;
        Phase = Target > T + 1e-9 ? SketchToImagePhase.Play : SketchToImagePhase.Rest;
    }

    /// <summary>Lands on the end state at once (motion off, or frames stopped by turning motion off).</summary>
    public void Land()
    {
        var revealed = T < 1 && TargetReached() >= 1;
        T = TargetReached();
        Fade = 1;
        _hold = 0;
        Phase = SketchToImagePhase.Rest;
        if (revealed)
        {
            Completed?.Invoke();
        }
    }

    private double TargetReached() => IsControlled ? Target : 1;

    /// <summary>Advances by <paramref name="dt"/> seconds; returns whether frames are still needed.</summary>
    public bool Step(double dt, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Reduced && Phase is SketchToImagePhase.Play or SketchToImagePhase.FadeToPaper or SketchToImagePhase.Hold)
        {
            // Motion was reduced mid-pass: no drawing or travel, a short fade to the end state.
            Phase = SketchToImagePhase.ReducedOut;
        }
        switch (Phase)
        {
            case SketchToImagePhase.Play:
                T = Math.Min(Target, T + dt / Math.Max(0.05, Duration));
                if (T >= Target - 1e-9)
                {
                    T = Target;
                    Arrive();
                }
                break;
            case SketchToImagePhase.Hold:
                _hold -= dt;
                if (_hold <= 0)
                {
                    Phase = SketchToImagePhase.FadeToPaper;
                }
                break;
            case SketchToImagePhase.FadeToPaper:
                Fade -= dt / SketchToImageTimeline.FadeToPaper.TotalSeconds;
                if (Fade <= 0)
                {
                    // Reset while invisible, then start the next sketch on blank paper: no visible seam.
                    T = 0;
                    Fade = 1;
                    Phase = Target > 0 ? SketchToImagePhase.Play : SketchToImagePhase.Rest;
                }
                break;
            case SketchToImagePhase.ReducedOut:
                Fade -= dt / SketchToImageTimeline.ReducedFade.TotalSeconds;
                if (Fade <= 0)
                {
                    Fade = 0;
                    T = TargetReached();
                    Phase = SketchToImagePhase.ReducedIn;
                }
                break;
            case SketchToImagePhase.ReducedIn:
                Fade += dt / SketchToImageTimeline.ReducedFade.TotalSeconds;
                if (Fade >= 1)
                {
                    Fade = 1;
                    Phase = SketchToImagePhase.Rest;
                    if (T >= 1)
                    {
                        Completed?.Invoke();
                    }
                }
                break;
        }
        return IsMoving;
    }

    private void Arrive()
    {
        if (T < 1)
        {
            Phase = SketchToImagePhase.Rest;
            return;
        }
        if (!IsControlled && Loops)
        {
            _hold = SketchToImageTimeline.LoopHold.TotalSeconds;
            Phase = SketchToImagePhase.Hold;
        }
        else
        {
            Phase = SketchToImagePhase.Rest;
        }
        Completed?.Invoke();
    }
}

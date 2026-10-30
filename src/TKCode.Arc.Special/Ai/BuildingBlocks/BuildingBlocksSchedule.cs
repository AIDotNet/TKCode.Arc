namespace TKCode.Arc;

/// <summary>What the stage of a <see cref="BuildingBlocks"/> shows at a moment of its schedule.</summary>
internal enum BuildingBlocksStep
{
    /// <summary>Dashed slots only.</summary>
    Empty,

    /// <summary>The blocks fly in and land as tinted placeholders.</summary>
    Layout,

    /// <summary>The placeholders cross to neutral content.</summary>
    Components,

    /// <summary>The styling pass: tint, photos, the switch.</summary>
    Styling,

    /// <summary>Everything in place.</summary>
    Ready,

    /// <summary>A finished layout fades out in place before it builds again.</summary>
    Clearing
}

/// <summary>Who drives a <see cref="BuildingBlocksSchedule"/>.</summary>
internal enum BuildingBlocksMode
{
    /// <summary>No status from the app: the built-in simulated build (loops with a hold on Ready, or plays once).</summary>
    Sample,

    /// <summary>The app says idle.</summary>
    Idle,

    /// <summary>The app says building: the phases play and re-assemble while looping, or hold on Styling.</summary>
    Building,

    /// <summary>The app says done: Ready at once.</summary>
    Done
}

/// <summary>
/// The timing of a <see cref="BuildingBlocks"/> build as a pure function of the seconds since it started, so the clock can
/// pause (off screen, a hidden window) and tests can step it. One build splits its duration evenly over Laying out, Adding
/// components and Styling. The sample then holds Ready for <see cref="Hold"/>, fades the layout out in place over
/// <see cref="Fade"/> and builds again; a looping app build skips Ready and re-assembles at once. A restart over blocks
/// that are still on screen fades them out first (negative time).
/// </summary>
internal sealed class BuildingBlocksSchedule
{
    /// <summary>The shortest build (seconds).</summary>
    public const double MinimumDuration = 2.4;

    /// <summary>The default build (seconds).</summary>
    public const double DefaultDuration = 4.8;

    /// <summary>How long the sample rests on Ready before it builds again (seconds).</summary>
    public const double Hold = 1.6;

    /// <summary>How long a finished layout takes to fade out in place before the next build (seconds).</summary>
    public const double Fade = 0.32;

    private double _duration = DefaultDuration;
    private double _t;
    private bool _loops = true;

    /// <summary>Who drives the schedule.</summary>
    public BuildingBlocksMode Mode { get; private set; } = BuildingBlocksMode.Idle;

    /// <summary>Whether a finished build starts again (the sample after its hold, an app build at once).</summary>
    public bool Loops
    {
        get => _loops;
        set
        {
            _loops = value;
            if (!value && _t > _duration)
            {
                _t = _duration;
            }
            Evaluate();
        }
    }

    /// <summary>Seconds for one build, from the first block to the end of styling; never under <see cref="MinimumDuration"/>.</summary>
    public double Duration
    {
        get => _duration;
        set
        {
            _duration = double.IsFinite(value) ? Math.Max(MinimumDuration, value) : DefaultDuration;
            Evaluate();
        }
    }

    /// <summary>Seconds into the current build (negative while a previous layout clears).</summary>
    public double Time => _t;

    /// <summary>Counts the builds started; a new number means the stage resets its blocks.</summary>
    public int Cycle { get; private set; }

    /// <summary>What the stage shows.</summary>
    public BuildingBlocksStep Step { get; private set; } = BuildingBlocksStep.Empty;

    /// <summary>What the status line says (a clearing layout keeps the phase it had).</summary>
    public BuildingBlocksPhase Phase { get; private set; } = BuildingBlocksPhase.Idle;

    /// <summary>Time still moves the schedule (it does not when idle, done, or holding at the end of a build that does not loop).</summary>
    public bool IsRunning => Mode is BuildingBlocksMode.Sample or BuildingBlocksMode.Building && (Loops || _t < _duration);

    /// <summary>Hands the schedule to a new driver. <paramref name="blocksShown"/>: blocks are on screen, so a new build clears them first.</summary>
    public void SetMode(BuildingBlocksMode mode, bool blocksShown)
    {
        Mode = mode;
        switch (mode)
        {
            case BuildingBlocksMode.Idle:
            case BuildingBlocksMode.Done:
                Evaluate();
                break;
            default:
                Restart(blocksShown);
                break;
        }
    }

    /// <summary>Starts a new build (the sample's Replay, a new app build).</summary>
    public void Restart(bool clearFirst)
    {
        Cycle++;
        _t = clearFirst ? -Fade : 0;
        Evaluate();
    }

    /// <summary>Moves the schedule on by <paramref name="seconds"/>; true when the step or the build changed.</summary>
    public bool Advance(double seconds)
    {
        if (!IsRunning || seconds <= 0)
        {
            return false;
        }
        var step = Step;
        var cycle = Cycle;
        _t += seconds;
        var length = CycleLength;
        if (Loops && _t >= length)
        {
            _t -= length;
            if (_t >= length)
            {
                _t %= length;
            }
            Cycle++;
        }
        else if (!Loops && _t > _duration)
        {
            _t = _duration;
        }
        Evaluate();
        return step != Step || cycle != Cycle;
    }

    /// <summary>The seconds from one build's start to the next one's, when looping.</summary>
    private double CycleLength => Mode == BuildingBlocksMode.Sample ? _duration + Hold + Fade : _duration + Fade;

    /// <summary>The step at <paramref name="t"/> seconds into a build of <paramref name="duration"/> seconds.</summary>
    public static BuildingBlocksStep StepAt(double t, double duration, BuildingBlocksMode mode, bool loops)
    {
        if (mode == BuildingBlocksMode.Idle)
        {
            return BuildingBlocksStep.Empty;
        }
        if (mode == BuildingBlocksMode.Done)
        {
            return BuildingBlocksStep.Ready;
        }
        if (t < 0)
        {
            return BuildingBlocksStep.Clearing;
        }
        var third = duration / 3;
        if (t < third)
        {
            return BuildingBlocksStep.Layout;
        }
        if (t < 2 * third)
        {
            return BuildingBlocksStep.Components;
        }
        if (t < duration)
        {
            return BuildingBlocksStep.Styling;
        }
        if (mode == BuildingBlocksMode.Building)
        {
            return loops ? BuildingBlocksStep.Clearing : BuildingBlocksStep.Styling;
        }
        return loops && t >= duration + Hold ? BuildingBlocksStep.Clearing : BuildingBlocksStep.Ready;
    }

    /// <summary>The phase a step announces; null for a clearing layout, which keeps the phase it had.</summary>
    public static BuildingBlocksPhase? PhaseOf(BuildingBlocksStep step) => step switch
    {
        BuildingBlocksStep.Empty => BuildingBlocksPhase.Idle,
        BuildingBlocksStep.Layout => BuildingBlocksPhase.Layout,
        BuildingBlocksStep.Components => BuildingBlocksPhase.Components,
        BuildingBlocksStep.Styling => BuildingBlocksPhase.Styling,
        BuildingBlocksStep.Ready => BuildingBlocksPhase.Ready,
        _ => null
    };

    private void Evaluate()
    {
        Step = StepAt(_t, _duration, Mode, Loops);
        if (PhaseOf(Step) is { } phase)
        {
            Phase = phase;
        }
    }
}

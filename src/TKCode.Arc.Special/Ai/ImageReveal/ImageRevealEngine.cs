using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The pose of an <see cref="ImageReveal"/> as a pure function of time: the shown status, how far the band has travelled
/// (<see cref="Reveal"/>), how visible the soft preview and the sharp layers are, the queued shimmer's phase, the sheen and
/// the pill. The control feeds it what the app asked for and one <see cref="Tick"/> per frame; the canvas draws whatever it
/// holds. Every change starts from the current pose: a restart, a failure or a new run blurs the sharp layers back out
/// (<see cref="Focus"/> to 0) before the reveal resets underneath, so nothing ever cuts.
/// </summary>
/// <remarks>
/// Code-driven rather than ControlTheme transitions: the band position follows a progress value with an exponential
/// follow, the auto mode is a timed script (queue, generate, hold, loop) and several values depend on each other within a
/// frame; one loop advancing one pose keeps it allocation-free and testable step by step.
/// </remarks>
internal sealed class ImageRevealEngine
{
    /// <summary>Time constant of the exponential follow that smooths coarse progress updates (200ms).</summary>
    public const double ProgressFollow = 0.2;

    /// <summary>The band never crosses the whole canvas faster than this (seconds), so an image that arrives late still sweeps.</summary>
    public const double FastestSweep = 0.9;

    /// <summary>The soft preview fades in and out over this long (seconds).</summary>
    public const double PreviewFade = 0.42;

    /// <summary>How long the sharp layers take to blur back out on a restart or failure (seconds).</summary>
    public const double BlurOut = 0.5;

    /// <summary>The completion sheen's sweep (1.1s).</summary>
    public const double SheenDuration = 1.1;

    /// <summary>The pill fades out this long after the image resolves (after the sheen; seconds).</summary>
    public const double PillFadeDelay = 1.1;

    /// <summary>The pill's fade-out (seconds).</summary>
    public const double PillFade = 0.24;

    /// <summary>One pass of the queued shimmer, off canvas to off canvas (2.6s).</summary>
    public const double ShimmerPeriod = 2.6;

    /// <summary>How much of the soft preview stays under the error state.</summary>
    public const double ErrorDim = 0.4;

    /// <summary>A time-estimated reveal with no progress events stops here until the app says Done.</summary>
    public const double EstimateCap = 0.94;

    private double _clock;
    private double _resolvedClock;
    private bool _completed;
    private bool _started;
    private bool _resetting;
    private bool _resetFadesPreview;
    private Action? _afterReset;

    /// <summary>Raised when the shown status changes (old, new).</summary>
    public event Action<ImageRevealStatus, ImageRevealStatus>? StatusChanged;

    /// <summary>Raised once per run when the image is fully resolved and crisp.</summary>
    public event Action? Completed;

    /// <summary>Auto mode: raised when a run starts, after the previous image has blurred out (swap the picture here).</summary>
    public event Action? RunStarting;

    /// <summary>True for the simulation (queued, generating over <see cref="Duration"/>, done, hold, loop).</summary>
    public bool IsAuto { get; set; } = true;

    /// <summary>Nothing to show and no job: the plain surface, without the queued shimmer (frames stop once it rests).</summary>
    public bool IsBlank { get; set; }

    /// <summary>Controlled progress (0 to 1), or null to estimate it from <see cref="Duration"/>.</summary>
    public double? RequestedProgress { get; set; }

    /// <summary>Whether an image is there to reveal; without one the canvas keeps the calm queued look.</summary>
    public bool HasImage { get; set; }

    /// <summary>Seconds one generation takes when progress is not given.</summary>
    public double Duration { get; set; } = 5.2;

    /// <summary>Auto mode: seconds in the queue before each run.</summary>
    public double QueueSeconds { get; set; } = 0.9;

    /// <summary>Auto mode: extra queue seconds before each run.</summary>
    public double StartDelay { get; set; }

    /// <summary>Auto mode: seconds the finished image holds before the loop restarts.</summary>
    public double HoldSeconds { get; set; } = 2.6;

    /// <summary>Auto mode: restart after the hold.</summary>
    public bool Loops { get; set; } = true;

    /// <summary>Plays the completion sheen (full motion only).</summary>
    public bool SheenEnabled { get; set; } = true;

    /// <summary>The motion level: Off jumps every fade, Reduced keeps the follow but drops the sheen.</summary>
    public ArcMotionLevel Level { get; set; } = ArcMotionLevel.Full;

    /// <summary>The status on screen.</summary>
    public ImageRevealStatus Status { get; private set; } = ImageRevealStatus.Queued;

    /// <summary>Where the progress is heading (0 to 1).</summary>
    public double Target { get; private set; }

    /// <summary>The band's position, from 0 (all soft) to 1 (all sharp).</summary>
    public double Reveal { get; private set; }

    /// <summary>The smoothed progress the pill shows (it counts even before an image is there).</summary>
    public double PillValue { get; private set; }

    /// <summary>How visible the soft preview is (0 to 1).</summary>
    public double Preview { get; private set; }

    /// <summary>How visible the sharp layers are (they blur back out towards 0).</summary>
    public double Focus { get; private set; } = 1;

    /// <summary>The preview's brightness: 1, or <see cref="ErrorDim"/> under the error state.</summary>
    public double Dim { get; private set; } = 1;

    /// <summary>The sheen's progress (0 to 1) while it plays, otherwise -1.</summary>
    public double Sheen { get; private set; } = -1;

    /// <summary>The queued shimmer's phase (0 to 1).</summary>
    public double ShimmerPhase { get; private set; }

    /// <summary>Seconds of frames seen (the drift and the grain read it).</summary>
    public double Time { get; private set; }

    /// <summary>The pill's opacity (it fades out after the image resolves).</summary>
    public double PillOpacity { get; private set; } = 1;

    /// <summary>True while the sharp layers blur back out before a reset.</summary>
    public bool IsResetting => _resetting;

    /// <summary>True when the run has completed: done, revealed and crisp.</summary>
    public bool IsResolved => _completed && Status == ImageRevealStatus.Done;

    /// <summary>Auto mode: true once a run was asked for (auto play or <see cref="Restart"/>).</summary>
    public bool IsStarted => _started;

    /// <summary>True while frames still change something (the loop stops otherwise).</summary>
    public bool WantsFrames
    {
        get
        {
            if (_resetting)
            {
                return true;
            }
            return Status switch
            {
                ImageRevealStatus.Queued => (IsAuto && _started) || Preview > 0 || Dim < 1 || (Level == ArcMotionLevel.Full && !IsBlank),
                ImageRevealStatus.Generating => true,
                ImageRevealStatus.Done => _completed
                    ? Sheen >= 0 || PillOpacity > 0 || (IsAuto && Loops && _started) || Dim < 1
                    : HasImage || PillValue < 1 || Preview > 0 || Dim < 1,
                _ => Focus > 0 || Dim > ErrorDim || Math.Abs(Preview - (HasImage ? 1 : 0)) > 0,
            };
        }
    }

    /// <summary>The step the pill shows out of <paramref name="steps"/>.</summary>
    public int StepOf(int steps)
    {
        if (steps <= 0)
        {
            return 0;
        }
        return Math.Clamp((int)Math.Floor(PillValue * steps + 1e-6), 0, steps);
    }

    /// <summary>The auto mode's progress curve: eases out so the composition resolves early.</summary>
    public static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - t, 2.4);
    }

    /// <summary>Auto mode: blurs whatever is shown back out, then queues and runs again.</summary>
    public void Restart()
    {
        _started = true;
        SetStatus(ImageRevealStatus.Queued);
        _clock = 0;
        BeginReset(fadesPreview: true, () => RunStarting?.Invoke());
    }

    /// <summary>Auto mode without auto play: shows the calm queued look and waits for <see cref="Restart"/>.</summary>
    public void Idle()
    {
        _started = false;
        SetStatus(ImageRevealStatus.Queued);
        _clock = 0;
        BeginReset(fadesPreview: true, null);
    }

    /// <summary>Controlled mode: the app (or the generator) asks for <paramref name="status"/>; the pose follows from where it is.</summary>
    public void Request(ImageRevealStatus status)
    {
        var current = Status;
        if (status == current)
        {
            return;
        }
        switch (status)
        {
            case ImageRevealStatus.Queued:
                BeginReset(fadesPreview: true, null);
                break;
            case ImageRevealStatus.Generating:
                if (current is ImageRevealStatus.Done or ImageRevealStatus.Error || Reveal > 0)
                {
                    BeginReset(fadesPreview: false, null);
                }
                break;
            case ImageRevealStatus.Done:
                if (current == ImageRevealStatus.Error)
                {
                    BeginReset(fadesPreview: false, null);
                }
                break;
        }
        _clock = 0;
        SetStatus(status);
    }

    /// <summary>
    /// Controlled mode, first status: rests in its pose at once (a finished image from history is simply there, a failed
    /// one shows the error state) instead of transitioning into it.
    /// </summary>
    public void Settle(ImageRevealStatus status)
    {
        _resetting = false;
        _afterReset = null;
        _clock = 0;
        Sheen = -1;
        Focus = 1;
        Dim = 1;
        Reveal = Target = PillValue = 0;
        Preview = 0;
        PillOpacity = 1;
        _completed = false;
        switch (status)
        {
            case ImageRevealStatus.Done when HasImage:
                Reveal = Target = PillValue = 1;
                Preview = 1;
                _completed = true;
                PillOpacity = 0;
                break;
            case ImageRevealStatus.Error:
                Focus = 0;
                Dim = ErrorDim;
                Preview = HasImage ? 1 : 0;
                break;
        }
        SetStatus(status);
    }

    /// <summary>Advances the pose by <paramref name="dt"/> seconds of frames.</summary>
    public void Tick(double dt)
    {
        dt = double.IsFinite(dt) ? Math.Clamp(dt, 0, 0.25) : 0;
        Time += dt;
        ShimmerPhase = (ShimmerPhase + dt / ShimmerPeriod) % 1;
        Dim = Approach(Dim, Status == ImageRevealStatus.Error ? ErrorDim : 1, dt, PreviewFade);
        if (_resetting)
        {
            Focus = Approach(Focus, 0, dt, BlurOut);
            if (_resetFadesPreview)
            {
                Preview = Approach(Preview, 0, dt, BlurOut);
            }
            if (Focus <= 0 && (!_resetFadesPreview || Preview <= 0))
            {
                FinishReset();
            }
            return;
        }
        switch (Status)
        {
            case ImageRevealStatus.Queued:
                Preview = Approach(Preview, 0, dt, PreviewFade);
                PillValue = 0;
                if (IsAuto && _started)
                {
                    _clock += dt;
                    if (_clock >= QueueSeconds + StartDelay)
                    {
                        _clock = 0;
                        SetStatus(ImageRevealStatus.Generating);
                    }
                }
                break;
            case ImageRevealStatus.Generating:
                _clock += dt;
                Preview = Approach(Preview, HasImage ? 1 : 0, dt, PreviewFade);
                var t = Duration > 0 ? _clock / Duration : 1;
                Target = IsAuto
                    ? Ease(t)
                    : Math.Clamp(RequestedProgress ?? Math.Min(EstimateCap, Ease(t)), 0, 1);
                Follow(dt);
                if (IsAuto && t >= 1)
                {
                    SetStatus(ImageRevealStatus.Done);
                }
                break;
            case ImageRevealStatus.Done:
                Target = 1;
                Preview = Approach(Preview, HasImage ? 1 : 0, dt, PreviewFade);
                Follow(dt);
                if (HasImage && Reveal >= 1 && !_completed)
                {
                    _completed = true;
                    _resolvedClock = 0;
                    Sheen = SheenEnabled && Level == ArcMotionLevel.Full ? 0 : -1;
                    Completed?.Invoke();
                }
                else if (_completed)
                {
                    _resolvedClock += dt;
                    if (Sheen >= 0)
                    {
                        Sheen += dt / SheenDuration;
                        if (Sheen >= 1)
                        {
                            Sheen = -1;
                        }
                    }
                    if (_resolvedClock >= PillFadeDelay || Level == ArcMotionLevel.Off)
                    {
                        PillOpacity = Approach(PillOpacity, 0, dt, PillFade);
                    }
                    if (IsAuto && Loops && _started && _resolvedClock >= HoldSeconds)
                    {
                        Restart();
                    }
                }
                break;
            case ImageRevealStatus.Error:
                Focus = Approach(Focus, 0, dt, BlurOut);
                Preview = Approach(Preview, HasImage ? 1 : 0, dt, PreviewFade);
                break;
        }
    }

    private void Follow(double dt)
    {
        var smoothing = Level == ArcMotionLevel.Off ? 1 : 1 - Math.Exp(-dt / ProgressFollow);
        PillValue += (Target - PillValue) * smoothing;
        if (Math.Abs(Target - PillValue) < 0.001)
        {
            PillValue = Target;
        }
        if (!HasImage)
        {
            return;
        }
        var step = (Target - Reveal) * smoothing;
        if (Level != ArcMotionLevel.Off)
        {
            var most = dt / FastestSweep;
            step = Math.Clamp(step, -most, most);
        }
        Reveal = Math.Clamp(Reveal + step, 0, 1);
        if (Math.Abs(Target - Reveal) < 0.001)
        {
            Reveal = Target;
        }
    }

    private double Approach(double value, double target, double dt, double duration)
    {
        if (Level == ArcMotionLevel.Off || duration <= 0)
        {
            return target;
        }
        var step = dt / duration;
        return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
    }

    private void BeginReset(bool fadesPreview, Action? after)
    {
        _resetFadesPreview = fadesPreview;
        _afterReset = after;
        var blank = (Reveal <= 0 || Focus <= 0) && (!fadesPreview || Preview <= 0);
        if (blank || Level == ArcMotionLevel.Off)
        {
            if (fadesPreview)
            {
                Preview = 0;
            }
            FinishReset();
            return;
        }
        _resetting = true;
    }

    private void FinishReset()
    {
        _resetting = false;
        Reveal = 0;
        Target = 0;
        PillValue = 0;
        Focus = 1;
        Sheen = -1;
        PillOpacity = 1;
        _completed = false;
        _clock = 0;
        var after = _afterReset;
        _afterReset = null;
        after?.Invoke();
    }

    private void SetStatus(ImageRevealStatus status)
    {
        if (Status == status)
        {
            return;
        }
        var old = Status;
        Status = status;
        if (status != ImageRevealStatus.Done)
        {
            PillOpacity = 1;
        }
        StatusChanged?.Invoke(old, status);
    }
}

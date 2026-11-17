using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Exact frame stepping for spring assertions against <see cref="SpringSolver"/>.</summary>
internal static class CoreFrames
{
    public static readonly TimeSpan Step = TimeSpan.FromMilliseconds(1000.0 / 60);

    /// <summary>
    /// Runs exactly <paramref name="frames"/> frames and returns the seconds a motion value advanced (the scheduler's
    /// first frame counts as 1/60s).
    /// </summary>
    public static double Run(ManualFrameClock clock, int frames)
    {
        clock.Advance(TimeSpan.FromTicks(Step.Ticks * frames), Step);
        return 1.0 / 60 + (frames - 1) * Step.TotalSeconds;
    }
}

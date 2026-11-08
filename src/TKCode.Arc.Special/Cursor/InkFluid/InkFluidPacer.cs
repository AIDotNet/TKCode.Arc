namespace TKCode.Arc;

/// <summary>
/// Decides which frames of <see cref="InkFluidCanvas"/> run a simulation step. A step's cost is repaid at
/// <see cref="ThreadShare"/>: a step that fits in its share of a frame runs on every frame, a dearer one skips frames, so
/// the UI thread always keeps idle time. Displays above 100 Hz step on every other frame or so, because soft ink shows
/// nothing from the extra steps and they would only spend the battery.
/// </summary>
internal sealed class InkFluidPacer
{
    /// <summary>The largest share of the UI thread's time the simulation takes while it runs.</summary>
    public const double ThreadShare = 0.5;

    /// <summary>Frames closer together than this share a step.</summary>
    public const double MinStepInterval = 0.01;

    // The most time one step simulates after skipped frames; a longer gap (the loop resumed) is not caught up.
    private const double MaxCatchUp = 0.1;

    private double _owed;
    private double _unsimulated;

    /// <summary>A frame went by: returns whether to step now, and how much time that step simulates.</summary>
    public bool TryStep(double frameDt, out double dt)
    {
        _unsimulated += frameDt;
        _owed = Math.Max(0, _owed - frameDt);
        if (_owed > 0 || _unsimulated < MinStepInterval)
        {
            dt = 0;
            return false;
        }
        dt = Math.Min(_unsimulated, MaxCatchUp);
        _unsimulated = 0;
        return true;
    }

    /// <summary>The step took <paramref name="cost"/> seconds; later frames repay it, the delayed frame included.</summary>
    public void Stepped(double cost) => _owed = cost / ThreadShare;

    public void Reset()
    {
        _owed = 0;
        _unsimulated = 0;
    }
}

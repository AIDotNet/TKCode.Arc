using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P16 path morph: holds an outline on screen and morphs it to the next one on an interruptible
/// <see cref="MotionValue{T}"/> spring. The outlines are resampled into one point layout
/// (<see cref="PathMorphSampler"/>); retargeting mid-flight starts from the outline currently drawn and carries its
/// speed (least squares over the points), so toggling quickly reverses from wherever the shape is.
/// The first shape never animates, and motion levels Reduced and Off jump to the end.
/// </summary>
public sealed class PathMorph : IDisposable
{
    private readonly MotionValue<double> _progress;
    private readonly int _samples;
    private PathMorphShape? _target;
    private PathMorphFrame? _from;
    private PathMorphFrame? _to;
    private PathMorphFrame? _current;
    private double _base;

    /// <param name="owner">Visual whose motion level and frame clock drive the spring.</param>
    /// <param name="samples">Points per contour (more for long or detailed outlines).</param>
    public PathMorph(Visual owner, int samples = PathMorphSampler.DefaultSamples)
    {
        _samples = Math.Max(2, samples);
        _progress = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _progress.Changed += OnProgress;
    }

    /// <summary>Raised whenever <see cref="Current"/> changes; invalidate rendering here.</summary>
    public event Action? Changed;

    /// <summary>The outline as drawn right now (null before the first shape).</summary>
    public PathMorphFrame? Current => _current;

    /// <summary>The shape being morphed to (or shown at rest).</summary>
    public PathMorphShape? Target => _target;

    public bool IsAnimating => _progress.IsAnimating;

    /// <summary>Shows <paramref name="shape"/> at once.</summary>
    public void Jump(PathMorphShape shape)
    {
        _target = shape;
        var (from, to) = PathMorphSampler.Align(shape, shape, _samples);
        _from = from;
        _to = to;
        _current = to.Clone();
        _base = 0;
        _progress.Jump(1);
    }

    /// <summary>Morphs to <paramref name="shape"/> on <paramref name="spring"/>.</summary>
    public Task<bool> MorphTo(PathMorphShape shape, ArcSpring spring = ArcSpring.Morph) => MorphTo(shape, MotionSpec.Spring(spring));

    /// <summary>Morphs to <paramref name="shape"/>. Completes with false when interrupted by another morph.</summary>
    public Task<bool> MorphTo(PathMorphShape shape, MotionSpec spec)
    {
        if (_current is null || _from is null || _to is null || _target is null)
        {
            Jump(shape);
            return Task.FromResult(true);
        }
        PathMorphFrame from;
        PathMorphFrame? to;
        double rate = 0;
        var resting = !_progress.IsAnimating;
        if (resting)
        {
            // At rest the source is an exact shape: align the two structures directly (vertex to vertex when possible).
            (from, to) = PathMorphSampler.Align(_target, shape, _samples);
        }
        else
        {
            from = _current.Clone();
            to = PathMorphSampler.SampleInto(shape, from);
            if (to is null)
            {
                (from, to) = PathMorphSampler.Align(PathMorphShape.FromFrame(_current), shape, _samples);
            }
            else
            {
                rate = PathMorphFrame.CarryRate(_from, _to, _progress.Velocity, from, to);
            }
        }
        _target = shape;
        _from = from;
        _to = to;
        if (!_current.HasSameLayout(from))
        {
            _current = from.Clone();
        }
        if (resting)
        {
            _base = 0;
            _progress.Jump(0);
        }
        else
        {
            _base = _progress.Value;
        }
        return _progress.AnimateTo(_base + 1, spec, rate);
    }

    private void OnProgress(double value)
    {
        if (_from is null || _to is null || _current is null)
        {
            return;
        }
        PathMorphFrame.Interpolate(_from, _to, value - _base, _current);
        Changed?.Invoke();
    }

    /// <summary>The current outline as a geometry (null before the first shape).</summary>
    public Geometry? BuildGeometry() => _current?.ToGeometry();

    public void Dispose()
    {
        _progress.Changed -= OnProgress;
        _progress.Dispose();
    }
}

using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// P16 for line shapes: every series lives on one shared x grid. A new dataset is resampled onto the union of
/// a dense uniform grid and the new shape's own vertices, the on-screen values (and their velocities) are
/// carried over to that grid, and every point springs to its new value, so a morph starts from what is on
/// screen, keeps velocity when retargeted mid-flight, and lands exactly on the new vertices.
/// </summary>
internal sealed class ChartLineMorph
{
    private readonly ChartSpringArray _springs;
    private double[] _grid = [];
    private string[] _keys = [];

    public ChartLineMorph(Visual owner)
    {
        _springs = new ChartSpringArray(owner);
        _springs.Changed += () => Changed?.Invoke();
    }

    /// <summary>Raised on every morph frame and on jumps.</summary>
    public event Action? Changed;

    public ReadOnlySpan<double> Grid => _grid;

    public int SeriesCount => _keys.Length;

    public bool IsAnimating => _springs.IsAnimating;

    public bool HasShape => _grid.Length > 0;

    public int IndexOf(string key) => Array.IndexOf(_keys, key);

    /// <summary>On-screen values of series <paramref name="series"/> at each grid x.</summary>
    public ReadOnlySpan<double> Values(int series) => _springs.Values.Slice(series * _grid.Length, _grid.Length);

    /// <summary>On-screen value of a series at <paramref name="x"/> (0..1).</summary>
    public double ValueAt(int series, double x) => ChartCurves.ValueAt(_grid, Values(series), x);

    /// <summary>
    /// Sets the target shapes. Series are matched by key: a series that existed morphs from its on-screen shape,
    /// a new one appears at its target. <paramref name="animate"/> false jumps (first data, reduced motion).
    /// </summary>
    public void SetTargets(string[] keys, double[][] xs, double[][] ys, int uniform, bool animate, SpringSpec spec)
    {
        double[] targetXs = [];
        foreach (var x in xs)
        {
            targetXs = targetXs.Length == 0 ? x : ChartCurves.Union(targetXs, x);
        }
        var empty = targetXs.Length == 0;
        var morph = animate && _grid.Length > 0 && !empty;
        var grid = morph ? ChartCurves.Union(ChartCurves.UniformGrid(uniform), targetXs) : targetXs;
        var n = grid.Length;
        var target = new double[n * keys.Length];
        for (var s = 0; s < keys.Length; s++)
        {
            for (var k = 0; k < n; k++)
            {
                target[s * n + k] = ChartCurves.ValueAt(xs[s], ys[s], grid[k]);
            }
        }
        var oldGrid = _grid;
        var oldKeys = _keys;
        var oldN = oldGrid.Length;
        _springs.Reshape(n * keys.Length, (oldValues, oldVelocities, values, velocities) =>
        {
            for (var s = 0; s < keys.Length; s++)
            {
                var from = morph ? Array.IndexOf(oldKeys, keys[s]) : -1;
                for (var k = 0; k < n; k++)
                {
                    if (from < 0 || oldN == 0)
                    {
                        values[s * n + k] = target[s * n + k];
                        velocities[s * n + k] = 0;
                    }
                    else
                    {
                        values[s * n + k] = ChartCurves.ValueAt(oldGrid, oldValues.Slice(from * oldN, oldN), grid[k]);
                        velocities[s * n + k] = ChartCurves.ValueAt(oldGrid, oldVelocities.Slice(from * oldN, oldN), grid[k]);
                    }
                }
            }
        });
        _grid = grid;
        _keys = keys;
        if (morph)
        {
            _springs.AnimateTo(target, spec);
        }
        else
        {
            _springs.Jump(target);
        }
    }
}

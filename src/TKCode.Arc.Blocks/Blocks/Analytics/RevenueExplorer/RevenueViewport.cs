using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The time window shared by the chart and the overview strip: the chart's x domain (start and end month, fractional while
/// it slides) and the overview window's own edges. Both ride the same Smooth spring when the range changes (labels,
/// paths, window and handles stay in step), but while a drag is on the window follows the pointer one to one and the chart
/// keeps chasing it; on release the window snaps to whole months on a Gentle spring.
/// </summary>
/// <remarks>
/// Why code-driven: the x domain, the y domain and the series factors are separate springs that every plot reads in the
/// same frame to rebuild its paths, labels and the window; a ControlTheme transition cannot animate coordinates inside
/// a drawing, nor chase a target that the pointer moves every frame.
/// </remarks>
internal sealed class RevenueViewport
{
    private readonly Visual _owner;
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel _chartStart = new(0, 0.0005, 0.005);
    private AnalyticsSpringChannel _chartEnd = new(1, 0.0005, 0.005);
    private AnalyticsSpringChannel _windowStart = new(0, 0.0005, 0.005);
    private AnalyticsSpringChannel _windowEnd = new(1, 0.0005, 0.005);
    private bool _hasRange;

    public RevenueViewport(Visual owner)
    {
        _owner = owner;
        // Function as well as decoration: the loop also lands the springs when the level drops.
        _loop = new LiveLoop(owner, Advance) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>Raised after every frame that moved something (and after a jump).</summary>
    public event Action? Changed;

    /// <summary>The chart's first month (index, fractional while sliding).</summary>
    public double ChartStart => _chartStart.Value;

    /// <summary>The chart's last month.</summary>
    public double ChartEnd => _chartEnd.Value;

    /// <summary>Where the window's left edge is drawn (month index).</summary>
    public double WindowStart => _windowStart.Value;

    /// <summary>Where the window's right edge is drawn.</summary>
    public double WindowEnd => _windowEnd.Value;

    /// <summary>The window is held by the pointer.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>True while anything moves.</summary>
    public bool IsMoving => _chartStart.IsMoving || _chartEnd.IsMoving || _windowStart.IsMoving || _windowEnd.IsMoving;

    /// <summary>Moves chart and window to a whole-month range: Smooth for both (the first call and Reduced / Off jump).</summary>
    public void SetRange(int start, int end, bool animate)
    {
        IsDragging = false;
        if (!_hasRange || !animate || !ArcMotion.IsFull(_owner) || !(_owner.IsAttachedToVisualTree()))
        {
            Jump(start, end);
            return;
        }
        _chartStart.SpringTo(start, AnalyticsSprings.Smooth);
        _chartEnd.SpringTo(end, AnalyticsSprings.Smooth);
        _windowStart.SpringTo(start, AnalyticsSprings.Smooth);
        _windowEnd.SpringTo(end, AnalyticsSprings.Smooth);
        _loop.Start();
    }

    /// <summary>A drag moved the window to continuous edges: the window follows at once, the chart chases (Smooth).</summary>
    public void Drag(double start, double end)
    {
        IsDragging = true;
        _windowStart.Jump(start);
        _windowEnd.Jump(end);
        if (!_hasRange || !ArcMotion.IsFull(_owner))
        {
            _chartStart.Jump(start);
            _chartEnd.Jump(end);
            Changed?.Invoke();
            return;
        }
        _chartStart.SpringTo(start, AnalyticsSprings.Smooth);
        _chartEnd.SpringTo(end, AnalyticsSprings.Smooth);
        _loop.Start();
        Changed?.Invoke();
    }

    /// <summary>The drag ended: the window snaps to the whole-month range on Gentle, the chart follows on Smooth.</summary>
    public void Release(int start, int end)
    {
        IsDragging = false;
        if (!ArcMotion.IsFull(_owner))
        {
            Jump(start, end);
            return;
        }
        _windowStart.SpringTo(start, AnalyticsSprings.Gentle);
        _windowEnd.SpringTo(end, AnalyticsSprings.Gentle);
        _chartStart.SpringTo(start, AnalyticsSprings.Smooth);
        _chartEnd.SpringTo(end, AnalyticsSprings.Smooth);
        _loop.Start();
    }

    /// <summary>Sets everything at once.</summary>
    public void Jump(double start, double end)
    {
        _chartStart.Jump(start);
        _chartEnd.Jump(end);
        _windowStart.Jump(start);
        _windowEnd.Jump(end);
        _hasRange = true;
        _loop.Stop();
        Changed?.Invoke();
    }

    /// <summary>Advances the springs by <paramref name="dt"/> seconds (the loop calls it; tests may too).</summary>
    public bool Advance(double dt)
    {
        if (!IsMoving)
        {
            return false;
        }
        if (!ArcMotion.IsFull(_owner))
        {
            Jump(_chartStart.Target, _chartEnd.Target);
            _windowStart.Jump(_windowStart.Target);
            _windowEnd.Jump(_windowEnd.Target);
            return false;
        }
        var moving = _chartStart.Step(dt);
        moving |= _chartEnd.Step(dt);
        moving |= _windowStart.Step(dt);
        moving |= _windowEnd.Step(dt);
        Changed?.Invoke();
        return moving;
    }
}

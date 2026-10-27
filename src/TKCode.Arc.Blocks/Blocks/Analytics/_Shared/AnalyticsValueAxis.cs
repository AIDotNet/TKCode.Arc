using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>A gridline of an <see cref="AnalyticsValueAxis"/>: its value and current opacity (0–1).</summary>
/// <param name="Value">Tick value.</param>
/// <param name="Opacity">Fade state: rising after a domain change, falling while it leaves.</param>
/// <param name="IsLeaving">The tick is no longer in the tick set and is fading out (it keeps riding the domain).</param>
internal readonly record struct AnalyticsAxisTick(double Value, double Opacity, bool IsLeaving);

/// <summary>
/// The state of an animated value axis (a model: the block draws gridlines and labels from it). The domain rides a
/// spring (Smooth: Revenue explorer, MRR waterfall, KPI drilldown, Usage forecast), ticks are keyed by value: a tick in
/// both sets stays and slides with the domain, a new tick fades in (240ms (.16, 1, .3, 1) after 60ms) while sliding in
/// from where the moving domain puts it, a dropped tick fades out (160ms ease-out) while still sliding, then is removed
/// (MRR waterfall 6.1; Revenue explorer measured a 50ms delay, set <see cref="EnterDelay"/>). The first
/// <see cref="Set"/> never animates. Reduced motion: the domain jumps and new ticks show at once while dropped ones still
/// fade out (Uptime status); Off: everything jumps. Drive it from the plot's frame loop with <see cref="Advance"/>.
/// </summary>
internal sealed class AnalyticsValueAxis
{
    private readonly List<Entry> _ticks = new();
    private AnalyticsSpringChannel _lo = new(0);
    private AnalyticsSpringChannel _hi = new(1);
    private bool _hasDomain;

    /// <param name="domainSpring">The domain spring (default <see cref="AnalyticsSprings.Smooth"/>).</param>
    public AnalyticsValueAxis(SpringSpec? domainSpring = null)
    {
        DomainSpring = domainSpring ?? AnalyticsSprings.Smooth;
    }

    /// <summary>Spring of the domain.</summary>
    public SpringSpec DomainSpring { get; set; }

    /// <summary>Fade-in time of a new tick (240ms).</summary>
    public TimeSpan EnterDuration { get; set; } = ArcMotionPresets.Standard;

    /// <summary>Delay before a new tick fades in (60ms; Revenue explorer 50ms).</summary>
    public TimeSpan EnterDelay { get; set; } = TimeSpan.FromMilliseconds(60);

    /// <summary>Fade-out time of a dropped tick (160ms).</summary>
    public TimeSpan ExitDuration { get; set; } = ArcMotionPresets.Fast;

    /// <summary>Easing of a new tick ((.16, 1, .3, 1)).</summary>
    public Easing EnterEasing { get; set; } = AnalyticsEasings.Enter;

    /// <summary>Easing of a dropped tick (ease-out).</summary>
    public Easing ExitEasing { get; set; } = AnalyticsEasings.EaseOut;

    /// <summary>Current (animated) domain bottom.</summary>
    public double DomainMin => _lo.Value;

    /// <summary>Current (animated) domain top.</summary>
    public double DomainMax => _hi.Value;

    /// <summary>Target domain bottom.</summary>
    public double TargetMin => _lo.Target;

    /// <summary>Target domain top.</summary>
    public double TargetMax => _hi.Target;

    /// <summary>True while the domain or a tick fade moves.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Ticks to draw (current ones and leaving ones).</summary>
    public int TickCount => _ticks.Count;

    /// <summary>Tick <paramref name="index"/> (values ascending among current ticks; leaving ticks keep their slots).</summary>
    public AnalyticsAxisTick this[int index]
    {
        get
        {
            var e = _ticks[index];
            return new AnalyticsAxisTick(e.Value, Math.Clamp(e.Opacity.Value, 0, 1), e.Leaving);
        }
    }

    /// <summary>
    /// Sets the target domain and ticks. <paramref name="level"/> is the owner's effective motion level
    /// (<c>ArcMotion.GetEffectiveLevel(plot)</c>); <paramref name="animate"/> false jumps (first data, resize).
    /// </summary>
    public void Set(AnalyticsTickSet ticks, ArcMotionLevel level, bool animate = true)
    {
        var full = animate && _hasDomain && level == ArcMotionLevel.Full;
        var fades = animate && _hasDomain && level != ArcMotionLevel.Off;
        if (full)
        {
            _lo.SpringTo(ticks.Min, DomainSpring);
            _hi.SpringTo(ticks.Max, DomainSpring);
        }
        else
        {
            _lo.Jump(ticks.Min);
            _hi.Jump(ticks.Max);
        }
        _hasDomain = true;

        // Mark every tick leaving, then revive the ones the new set keeps.
        foreach (var entry in _ticks)
        {
            entry.Seen = false;
        }
        foreach (var value in ticks.Ticks)
        {
            var existing = Find(value);
            if (existing is not null)
            {
                existing.Seen = true;
                if (existing.Leaving)
                {
                    existing.Leaving = false;
                    if (fades && full)
                    {
                        existing.Opacity.Start(1, EnterDuration, EnterEasing);
                    }
                    else
                    {
                        existing.Opacity.Jump(1);
                    }
                }
                continue;
            }
            var entry = new Entry(value) { Seen = true };
            if (full)
            {
                entry.Opacity.Jump(0);
                entry.Opacity.Start(1, EnterDuration, EnterEasing, EnterDelay);
            }
            else
            {
                entry.Opacity.Jump(1);
            }
            _ticks.Add(entry);
        }
        for (var i = _ticks.Count - 1; i >= 0; i--)
        {
            var entry = _ticks[i];
            if (entry.Seen || entry.Leaving)
            {
                continue;
            }
            if (fades)
            {
                entry.Leaving = true;
                entry.Opacity.Start(0, ExitDuration, ExitEasing);
            }
            else
            {
                _ticks.RemoveAt(i);
            }
        }
        _ticks.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        IsMoving = _lo.IsMoving || _hi.IsMoving || _ticks.Exists(static t => t.Opacity.IsRunning);
    }

    /// <summary>Advances the domain spring and tick fades by <paramref name="dt"/> seconds; true while anything moves.</summary>
    public bool Advance(double dt)
    {
        if (!IsMoving)
        {
            return false;
        }
        var moving = _lo.Step(dt) | _hi.Step(dt);
        for (var i = _ticks.Count - 1; i >= 0; i--)
        {
            var entry = _ticks[i];
            moving |= entry.Opacity.Advance(dt);
            if (entry.Leaving && !entry.Opacity.IsRunning)
            {
                _ticks.RemoveAt(i);
            }
        }
        IsMoving = moving;
        return moving;
    }

    /// <summary>The scale of the current domain over a pixel range (bottom pixel first for a y axis).</summary>
    public AnalyticsLinearScale Scale(double bottomPixel, double topPixel) => new(DomainMin, DomainMax, bottomPixel, topPixel);

    private Entry? Find(double value)
    {
        foreach (var entry in _ticks)
        {
            if (Math.Abs(entry.Value - value) <= 1e-9 * Math.Max(1, Math.Abs(value)))
            {
                return entry;
            }
        }
        return null;
    }

    private sealed class Entry(double value)
    {
        public double Value { get; } = value;
        public AnalyticsTween Opacity = new(1);
        public bool Leaving;
        public bool Seen;
    }
}

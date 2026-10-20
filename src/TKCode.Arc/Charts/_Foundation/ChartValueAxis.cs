using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A value axis whose scale rides springs: gridlines and tick labels follow the animated domain, so a taller
/// range slides them down while new rows fade in above (standard) and old ones fade out (instant). Tick
/// entries are created only when the tick set changes.
/// </summary>
internal sealed class ChartValueAxis
{
    private sealed class Entry
    {
        public required double Value { get; init; }
        public required string Label { get; set; }
        public required MotionValue<double> Opacity { get; init; }
        public bool Leaving { get; set; }
    }

    private readonly Visual _host;
    private readonly Action _invalidate;
    private readonly List<Entry> _entries = new();
    private readonly MotionValue<double> _min;
    private readonly MotionValue<double> _max;
    private bool _initialized;

    /// <summary>Smooth spring in physical form so a retarget mid-flight keeps velocity.</summary>
    public static readonly SpringSpec Settle = SpringSpec.FromVisual(0.4, 0);

    public ChartValueAxis(Visual host, Action invalidate)
    {
        _host = host;
        _invalidate = invalidate;
        _min = new MotionValue<double>(host, 0, MotionVectors.Double);
        _max = new MotionValue<double>(host, 100, MotionVectors.Double);
        _min.Changed += _ => OnScaleChanged();
        _max.Changed += _ => OnScaleChanged();
    }

    private void OnScaleChanged()
    {
        ScaleChanged?.Invoke();
        _invalidate();
    }

    /// <summary>Raised on every frame of the scale spring (charts rebuild geometry that depends on the scale).</summary>
    public event Action? ScaleChanged;

    public bool IsAnimating => _min.IsAnimating || _max.IsAnimating;

    public double Min => _min.Value;

    public double Max => _max.Value;

    public double TargetMin => _min.Target;

    public double TargetMax => _max.Target;

    public ChartTickSet Ticks { get; private set; } = ChartTickSet.Default;

    /// <summary>Pixel row of <paramref name="value"/> between <paramref name="top"/> (domain max) and <paramref name="bottom"/> (domain min).</summary>
    public double Map(double value, double top, double bottom)
    {
        var lo = _min.Value;
        var span = _max.Value - lo;
        if (Math.Abs(span) < 1e-12)
        {
            span = 1;
        }
        return top + (1 - (value - lo) / span) * (bottom - top);
    }

    /// <summary>Same as <see cref="Map"/>, snapped to the pixel center for crisp 1px lines.</summary>
    public double Row(double value, double top, double bottom) => Math.Round(Map(value, top, bottom)) + 0.5;

    public void SetTicks(ChartTickSet ticks, Func<double, string> format, bool animate)
    {
        _entries.RemoveAll(static e => e.Leaving && !e.Opacity.IsAnimating && e.Opacity.Value <= 0.001);
        Ticks = ticks;
        if (!_initialized || !animate)
        {
            _min.Jump(ticks.Min);
            _max.Jump(ticks.Max);
        }
        else
        {
            if (Math.Abs(_min.Target - ticks.Min) > 1e-12)
            {
                _ = _min.AnimateTo(ticks.Min, MotionSpec.Spring(Settle));
            }
            if (Math.Abs(_max.Target - ticks.Max) > 1e-12)
            {
                _ = _max.AnimateTo(ticks.Max, MotionSpec.Spring(Settle));
            }
        }
        var level = ArcMotion.GetEffectiveLevel(_host);
        var fadeIn = level == ArcMotionLevel.Off || !_initialized || !animate
            ? MotionSpec.Instant
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);
        var fadeOut = level == ArcMotionLevel.Off || !animate
            ? MotionSpec.Instant
            : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        foreach (var entry in _entries)
        {
            var keep = Array.IndexOf(ticks.Ticks, entry.Value) >= 0;
            if (keep)
            {
                entry.Label = format(entry.Value);
                if (entry.Leaving)
                {
                    entry.Leaving = false;
                    _ = entry.Opacity.AnimateTo(1, fadeIn);
                }
            }
            else if (!entry.Leaving)
            {
                entry.Leaving = true;
                _ = entry.Opacity.AnimateTo(0, fadeOut);
            }
        }
        foreach (var value in ticks.Ticks)
        {
            if (_entries.Exists(e => e.Value == value))
            {
                continue;
            }
            var opacity = new MotionValue<double>(_host, fadeIn is MotionSpec.InstantSpec ? 1 : 0, MotionVectors.Double);
            opacity.Changed += _ => _invalidate();
            var entry = new Entry { Value = value, Label = format(value), Opacity = opacity };
            _entries.Add(entry);
            if (fadeIn is not MotionSpec.InstantSpec)
            {
                _ = opacity.AnimateTo(1, fadeIn);
            }
        }
        _initialized = true;
        _invalidate();
    }

    /// <summary>
    /// Draws gridlines across <paramref name="plot"/> (skipping rows at or below the baseline) and labels in the
    /// gutter starting at <paramref name="labelX"/>, vertically centered on their row.
    /// </summary>
    public void Render(DrawingContext context, ChartPalette palette, ChartTextCache texts, Rect plot, double top,
                       bool grid, bool labels, double labelX, double avoidY = double.NaN)
    {
        var gridPen = palette.Pen(palette.Border, 1);
        var bottom = plot.Bottom;
        foreach (var entry in _entries)
        {
            var opacity = entry.Opacity.Value;
            if (opacity <= 0.001)
            {
                continue;
            }
            var y = Row(entry.Value, top, bottom);
            using var fade = context.PushOpacity(opacity);
            if (grid && y < bottom - 1 && y >= plot.Top - 0.5)
            {
                context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            }
            if (labels && y >= plot.Top - 8 && y <= bottom + 8)
            {
                var text = texts.Get(palette, entry.Label, palette.SizeXS, palette.TextMuted);
                var avoid = double.IsNaN(avoidY) ? 1 : Math.Clamp((Math.Abs(y - avoidY) - 10) / 6, 0, 1);
                if (avoid <= 0.001)
                {
                    continue;
                }
                using var make = context.PushOpacity(avoid);
                context.DrawText(text, new Point(labelX, y - text.Height / 2));
            }
        }
    }
}

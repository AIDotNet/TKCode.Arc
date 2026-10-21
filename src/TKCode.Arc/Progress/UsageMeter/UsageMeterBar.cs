using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One category of a <see cref="UsageMeter"/> (Arc <c>segments[]</c> entry).</summary>
public class UsageSegment : AvaloniaObject
{
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<UsageSegment, string?>(nameof(Id));

    /// <summary>Category name shown for the segment (Arc <c>label</c>).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<UsageSegment, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<UsageSegment, double>(nameof(Value));

    public UsageSegment()
    {
    }

    public UsageSegment(string id, string label, double value)
    {
        Id = id;
        Label = label;
        Value = value;
    }

    /// <summary>Stable identity: keep it while the value changes, so the segment re-flows instead of being replaced.</summary>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Amount used, in the meter's unit.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The key used for motion (the id, or the label when no id is set).</summary>
    internal string Key => Id ?? Label ?? string.Empty;
}

/// <summary>
/// The usage bar, drawn directly: each category is a run of an accent step that starts where the ones before it end,
/// so a change in any of them pushes the rest along in the same frame (P23); runs stop 2px short of the next. Past the
/// limit the bar spans everything used and the overage is struck through with a surface-colored hatch beside a limit
/// marker, both fading in when usage crosses the limit (P29). Pointing at the bar (mouse) previews a category; a tap
/// with touch or pen pins it.
/// </summary>
internal sealed class UsageMeterBar : Control, Avalonia.Rendering.ICustomHitTest
{
    internal const double Gap = 2;
    internal const double TrackHeight = 14;
    internal const double MarkerOverhang = 5;

    private sealed class Run
    {
        public required string Key { get; init; }
        public MotionValue<double>? Amount { get; set; }
        public MotionValue<double>? Dim { get; set; }
        public int Index { get; set; }
    }

    private readonly UsageMeter _owner;
    private readonly List<Run> _runs = new();
    private readonly Dictionary<string, Run> _byKey = new(StringComparer.Ordinal);
    private MotionValue<double>? _limit;
    private MotionValue<double>? _marker;
    private MotionValue<double>? _tint;
    private bool _over;
    private IPen? _hatchPen;
    private IBrush? _hatchBrush;

    public UsageMeterBar(UsageMeter owner)
    {
        _owner = owner;
        Height = TrackHeight + 2 * MarkerOverhang;
    }

    /// <summary>The whole bar answers the pointer, including the track's empty part.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    internal double Used
    {
        get
        {
            var used = 0.0;
            foreach (var run in _runs)
            {
                used += run.Amount?.Value ?? 0;
            }
            return used;
        }
    }

    private double UsedTarget
    {
        get
        {
            var used = 0.0;
            foreach (var run in _runs)
            {
                used += run.Amount?.Target ?? 0;
            }
            return used;
        }
    }

    internal double ShownLimit => _limit?.Value ?? _owner.Limit;

    internal double MarkerOpacity => _marker?.Value ?? 0;

    internal IReadOnlyList<double> Amounts => _runs.Select(r => r.Amount?.Value ?? 0).ToList();

    /// <summary>Moves the runs to new amounts: the first reveal grows left to right (0.58s, 70ms apart), later changes settle.</summary>
    public void SetData(IReadOnlyList<UsageSegment> segments, double limit, bool reveal, bool ready, ArcMotionLevel level)
    {
        var animate = IsLoaded && level == ArcMotionLevel.Full;
        var next = new List<Run>(segments.Count);
        for (var i = 0; i < segments.Count; i++)
        {
            var key = segments[i].Key;
            if (!_byKey.TryGetValue(key, out var run))
            {
                run = new Run { Key = key, Amount = CreateValue(0), Dim = CreateValue(1) };
                _byKey[key] = run;
            }
            run.Index = i;
            next.Add(run);
            var target = ready ? Math.Max(0, segments[i].Value) : 0;
            if (Math.Abs(run.Amount!.Target - target) < 1e-9)
            {
                continue;
            }
            if (!animate)
            {
                run.Amount.Jump(target);
            }
            else if (reveal)
            {
                var spec = SpringSpec.FromVisual(ArcMotionPresets.Considered.TotalSeconds + 0.1, 0);
                _ = run.Amount.AnimateTo(target, MotionSpec.Spring(spec, TimeSpan.FromSeconds(i * 0.07)));
            }
            else
            {
                _ = run.Amount.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
        }
        foreach (var gone in _runs.Where(r => !next.Contains(r)).ToList())
        {
            _byKey.Remove(gone.Key);
        }
        _runs.Clear();
        _runs.AddRange(next);
        _limit ??= CreateValue(limit);
        if (Math.Abs(_limit.Target - limit) > 1e-9)
        {
            if (animate)
            {
                _ = _limit.AnimateTo(limit, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _limit.Jump(limit);
            }
        }
        CheckOver(level);
        InvalidateVisual();
    }

    /// <summary>Dims the runs that are not highlighted (opacity 0.28) and floods the overage when it is.</summary>
    public void SetHighlight(string? active, ArcMotionLevel level)
    {
        var spec = level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        foreach (var run in _runs)
        {
            var target = active is null || active == run.Key ? 1 : 0.28;
            if (level != ArcMotionLevel.Full)
            {
                run.Dim?.Jump(target);
            }
            else
            {
                _ = run.Dim?.AnimateTo(target, spec);
            }
        }
        _tint ??= CreateValue(0);
        var tint = active == UsageMeter.FreeKey ? 1 : 0;
        if (level != ArcMotionLevel.Full)
        {
            _tint.Jump(tint);
        }
        else
        {
            _ = _tint.AnimateTo(tint, spec);
        }
    }

    private void CheckOver(ArcMotionLevel level)
    {
        var over = Used > ShownLimit + 1e-9 || (UsedTarget > (_limit?.Target ?? 0) + 1e-9 && level != ArcMotionLevel.Full);
        _marker ??= CreateValue(over ? 1 : 0);
        if (over == _over)
        {
            return;
        }
        _over = over;
        if (level == ArcMotionLevel.Full && IsLoaded)
        {
            _ = _marker.AnimateTo(over ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _marker.Jump(over ? 1 : 0);
        }
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            CheckOver(ArcMotion.GetEffectiveLevel(this));
            InvalidateVisual();
        };
        return value;
    }

    /// <summary>The category under <paramref name="x"/> (DIPs from the left of the track); past the limit it is the overage.</summary>
    internal string? KeyAt(double x)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return null;
        }
        var segments = _owner.CurrentSegments;
        var limit = _owner.Limit;
        var total = segments.Sum(s => s.Value);
        var at = x / width * Math.Max(limit, total);
        if (at > limit)
        {
            return UsageMeter.FreeKey;
        }
        var end = 0.0;
        foreach (var segment in segments)
        {
            end += segment.Value;
            if (at <= end)
            {
                return segment.Key;
            }
        }
        return UsageMeter.FreeKey;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _owner.SetHovered(KeyAt(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.SetHovered(null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            _owner.TogglePin(KeyAt(e.GetPosition(this).X));
        }
    }

    public override void Render(DrawingContext context)
    {
        var palette = _owner.Palette;
        var width = Bounds.Width;
        if (palette is null || width <= 0)
        {
            return;
        }
        var track = new Rect(0, MarkerOverhang, width, TrackHeight);
        var pill = new RoundedRect(track, TrackHeight / 2);
        context.DrawRectangle(palette.Track, null, pill);
        var used = Used;
        var limit = ShownLimit;
        var span = Math.Max(Math.Max(limit, used), 1e-6);
        using (context.PushClip(pill))
        {
            var start = 0.0;
            foreach (var run in _runs)
            {
                var amount = run.Amount?.Value ?? 0;
                var x = start / span * width;
                var size = amount / span * width;
                var rest = width - x - size;
                var drawn = Math.Max(0, size - Math.Min(Gap, Math.Min(size, Math.Max(0, rest))));
                start += amount;
                if (drawn <= 0.01)
                {
                    continue;
                }
                var brush = palette.Segments[Math.Min(run.Index, palette.Segments.Length - 1)];
                using (context.PushOpacity(Math.Clamp(run.Dim?.Value ?? 1, 0, 1)))
                {
                    context.DrawRectangle(brush, null, new Rect(track.X + x, track.Y, drawn, track.Height));
                }
            }
            var marker = Math.Clamp(MarkerOpacity, 0, 1);
            if (marker > 0.001)
            {
                var limitX = Math.Round(limit / span * width);
                var overRect = new Rect(limitX + Gap, track.Y, Math.Max(0, width - limitX - Gap), track.Height);
                if (overRect.Width > 0)
                {
                    using (context.PushOpacity(marker))
                    using (context.PushClip(overRect))
                    {
                        var tint = Math.Clamp(_tint?.Value ?? 0, 0, 1);
                        if (tint > 0.001)
                        {
                            using (context.PushOpacity(tint))
                            {
                                context.DrawRectangle(palette.Danger, null, overRect);
                            }
                        }
                        DrawHatch(context, overRect, palette.Surface);
                    }
                }
            }
        }
        var markerOpacity = Math.Clamp(MarkerOpacity, 0, 1);
        if (markerOpacity > 0.001)
        {
            var limitX = Math.Round(limit / span * width);
            using (context.PushOpacity(markerOpacity))
            {
                context.DrawRectangle(palette.Foreground, null, new RoundedRect(new Rect(limitX - 1, 0, 2, Bounds.Height), 1));
            }
        }
    }

    /// <summary>Surface-colored stripes 2.5px wide every 6px, rotated 45° (the SVG pattern Arc uses).</summary>
    private void DrawHatch(DrawingContext context, Rect area, IBrush brush)
    {
        if (!ReferenceEquals(_hatchBrush, brush) || _hatchPen is null)
        {
            _hatchBrush = brush;
            _hatchPen = new Pen(brush, 2.5);
        }
        var step = 6 * Math.Sqrt(2);
        var h = area.Height;
        var x0 = Math.Floor((area.X - h) / step) * step;
        for (var x = x0; x < area.Right + h; x += step)
        {
            context.DrawLine(_hatchPen, new Point(x, area.Bottom), new Point(x + h, area.Top));
        }
    }
}

/// <summary>Brushes for the meter (the accent steps are computed from the accent and the surface).</summary>
internal sealed class UsageMeterPalette
{
    public IBrush[] Segments { get; init; } = [Brushes.Black];
    public IBrush Track { get; init; } = Brushes.LightGray;
    public IBrush Surface { get; init; } = Brushes.White;
    public IBrush Danger { get; init; } = Brushes.Red;
    public IBrush Foreground { get; init; } = Brushes.Black;
}

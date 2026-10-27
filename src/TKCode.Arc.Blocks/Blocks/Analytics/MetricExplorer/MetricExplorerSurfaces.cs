using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The rounded surfaces behind the cards and behind the open chart: one per metric, drawn in one control. A surface follows
/// the rectangle its layout gives it on a Smooth spring, so the opened card's surface grows into the chart's rectangle and
/// shrinks back on close (the shared element), while the others vanish at once on open and fade back in on close.
/// Drawn at its animated rectangle instead of scaled, so the corner radius and the 1px edge never distort.
/// </summary>
/// <remarks>
/// Code-driven motion: four rectangle edges and an opacity per surface spring with velocity carried across retargets
/// (a close started mid-open turns around smoothly), which a ControlTheme transition on one element cannot express.
/// </remarks>
internal sealed class MetricExplorerSurfaces : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel[] _left = [];
    private AnalyticsSpringChannel[] _top = [];
    private AnalyticsSpringChannel[] _right = [];
    private AnalyticsSpringChannel[] _bottom = [];
    private AnalyticsSpringChannel[] _opacity = [];
    private AnalyticsTween[] _hover = [];
    private bool[] _placed = [];
    private double _radius = 16;
    private int _paletteVersion = -1;

    static MetricExplorerSurfaces()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<MetricExplorerSurfaces>(false);
    }

    public MetricExplorerSurfaces()
    {
        // Function, not decoration: the loop also lands the surfaces after the motion level drops.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
    }

    /// <summary>Corner radius (16).</summary>
    public double Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            InvalidateVisual();
        }
    }

    /// <summary>Surfaces.</summary>
    public int Count => _left.Length;

    /// <summary>True while a surface moves (tests).</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>The rectangle of surface <paramref name="index"/> on screen now (tests).</summary>
    internal Rect RectOf(int index) => new(new Point(_left[index].Value, _top[index].Value), new Point(_right[index].Value, _bottom[index].Value));

    /// <summary>The opacity of surface <paramref name="index"/> now (tests).</summary>
    internal double OpacityOf(int index) => _opacity[index].Value;

    /// <summary>Sets how many surfaces there are (their state restarts).</summary>
    public void SetCount(int count)
    {
        if (count == _left.Length)
        {
            return;
        }
        _left = new AnalyticsSpringChannel[count];
        _top = new AnalyticsSpringChannel[count];
        _right = new AnalyticsSpringChannel[count];
        _bottom = new AnalyticsSpringChannel[count];
        _opacity = new AnalyticsSpringChannel[count];
        _hover = new AnalyticsTween[count];
        _placed = new bool[count];
        for (var i = 0; i < count; i++)
        {
            _opacity[i] = new AnalyticsSpringChannel(1, 0.002, 0.01);
            _hover[i] = new AnalyticsTween(0);
        }
        InvalidateVisual();
    }

    /// <summary>Hover on a card: its edge goes from Border to BorderStrong in 160ms.</summary>
    public void SetHover(int index, bool hovered)
    {
        if ((uint)index >= (uint)_hover.Length)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var target = hovered ? 1.0 : 0.0;
        if (level == ArcMotionLevel.Full && this.IsAttachedToVisualTree())
        {
            _hover[index].Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _hover[index].Jump(target);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Heads surface <paramref name="index"/> for <paramref name="rect"/> with <paramref name="opacity"/>. The first placement,
    /// a hidden surface, motion levels below Full and a plain re-layout (<paramref name="animate"/> false: a resize) land at once; an opacity of 0 is reached at once (the other cards of an
    /// opening chart unmount without an exit).
    /// </summary>
    public void SetTarget(int index, Rect rect, double opacity, bool animate = true)
    {
        if ((uint)index >= (uint)_left.Length)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        animate &= level == ArcMotionLevel.Full && _placed[index] && this.IsAttachedToVisualTree();
        var hidden = _opacity[index].Value < 0.01 && opacity < 0.01;
        if (!animate || hidden)
        {
            _left[index].Jump(rect.Left);
            _top[index].Jump(rect.Top);
            _right[index].Jump(rect.Right);
            _bottom[index].Jump(rect.Bottom);
        }
        else
        {
            _left[index].SpringTo(rect.Left, AnalyticsSprings.Smooth);
            _top[index].SpringTo(rect.Top, AnalyticsSprings.Smooth);
            _right[index].SpringTo(rect.Right, AnalyticsSprings.Smooth);
            _bottom[index].SpringTo(rect.Bottom, AnalyticsSprings.Smooth);
        }
        if (!animate || opacity < _opacity[index].Value && opacity < 0.01)
        {
            _opacity[index].Jump(opacity);
        }
        else
        {
            _opacity[index].SpringTo(opacity, AnalyticsSprings.Smooth);
        }
        _placed[index] = true;
        if (IsAnyMoving())
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    private bool IsAnyMoving()
    {
        for (var i = 0; i < _left.Length; i++)
        {
            if (_left[i].IsMoving || _top[i].IsMoving || _right[i].IsMoving || _bottom[i].IsMoving || _opacity[i].IsMoving || _hover[i].IsRunning)
            {
                return true;
            }
        }
        return false;
    }

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        for (var i = 0; i < _left.Length; i++)
        {
            moving |= Step(ref _left[i], dt, full);
            moving |= Step(ref _top[i], dt, full);
            moving |= Step(ref _right[i], dt, full);
            moving |= Step(ref _bottom[i], dt, full);
            moving |= Step(ref _opacity[i], dt, full);
            moving |= _hover[i].Advance(dt);
        }
        InvalidateVisual();
        return moving;
    }

    private static bool Step(ref AnalyticsSpringChannel channel, double dt, bool full)
    {
        if (!channel.IsMoving)
        {
            return false;
        }
        if (full)
        {
            channel.Step(dt);
        }
        else
        {
            channel.Jump(channel.Target);
        }
        return channel.IsMoving;
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_paletteVersion < 0 || !_palette.IsReady)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
        for (var i = 0; i < _left.Length; i++)
        {
            var opacity = Math.Clamp(_opacity[i].Value, 0, 1);
            if (opacity < 0.004 || _right[i].Value - _left[i].Value < 1)
            {
                continue;
            }
            var rect = RectOf(i);
            var edge = AnalyticsColors.Lerp(_palette.Border, _palette.BorderStrong, Math.Clamp(_hover[i].Value, 0, 1));
            // The 1px edge is drawn inside the rectangle (an inset box-shadow), so the stroke is centred half a pixel in.
            var radius = Math.Max(0, _radius);
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(_palette.Brush(_palette.Surface), null, new RoundedRect(rect, radius));
                context.DrawRectangle(null, _palette.Pen(edge, 1, cap: PenLineCap.Flat), new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
            }
        }
    }
}

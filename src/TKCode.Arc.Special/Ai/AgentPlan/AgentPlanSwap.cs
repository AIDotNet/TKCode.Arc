using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Layers in one cell, one shown at a time; showing another swaps them with the plan's single short crossfade (notes 6.1):
/// the old layer fades out in 60ms while the new one fades in and rises 3px in 145ms on the enter curve, both starting
/// together. Reduced motion crossfades both in 120ms without the rise; motion off swaps at once. Hidden layers take no
/// space and no focus; a leaving layer keeps its space until it is gone (the outer height then follows).
/// Code-driven: the layers are persistent controls (buttons that hold the focus), which ArcContentSwap cannot re-host
/// safely when a swap is reversed mid-flight.
/// </summary>
internal sealed class AgentPlanSwap : Panel
{
    private sealed class Layer
    {
        public required MotionValue<double> Fade { get; init; }
        public required TranslateTransform Shift { get; init; }
        public bool Leaving { get; set; }
    }

    private readonly Dictionary<Control, Layer> _layers = new();
    private Control? _shown;

    /// <summary>The layer shown (or arriving).</summary>
    public Control? Shown => _shown;

    /// <summary>Shows <paramref name="child"/> (one of the children; null hides all).</summary>
    public void Show(Control? child, bool animate = true)
    {
        if (ReferenceEquals(child, _shown))
        {
            return;
        }
        var previous = _shown;
        _shown = child;
        var level = ArcMotion.GetEffectiveLevel(this);
        var moving = animate && IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        if (previous is not null)
        {
            var layer = LayerOf(previous);
            layer.Leaving = true;
            if (moving)
            {
                Leave(previous, layer, full);
            }
            else
            {
                layer.Fade.Jump(0);
                previous.IsVisible = false;
            }
        }
        if (child is not null)
        {
            var layer = LayerOf(child);
            layer.Leaving = false;
            child.IsVisible = true;
            if (moving)
            {
                if (layer.Fade.Value > 0.99)
                {
                    layer.Fade.Jump(0);
                }
                layer.Shift.Y = full ? AgentPlanMotion.SwapRise * (1 - layer.Fade.Value) : 0;
                _ = layer.Fade.AnimateTo(1, full
                    ? AgentPlanMotion.Enter(AgentPlanMotion.SwapEnter)
                    : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            else
            {
                layer.Fade.Jump(1);
            }
        }
        InvalidateMeasure();
    }

    private async void Leave(Control control, Layer layer, bool full)
    {
        var done = await layer.Fade.AnimateTo(0, full
            ? AgentPlanMotion.Enter(AgentPlanMotion.SwapExit)
            : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        if (done && layer.Leaving)
        {
            control.IsVisible = false;
            InvalidateMeasure();
        }
    }

    private Layer LayerOf(Control child)
    {
        if (_layers.TryGetValue(child, out var layer))
        {
            return layer;
        }
        var shift = new TranslateTransform();
        var fade = new MotionValue<double>(child, ReferenceEquals(child, _shown) ? 1 : 0, MotionVectors.Double);
        layer = new Layer { Fade = fade, Shift = shift };
        fade.Changed += v =>
        {
            child.Opacity = v;
            if (!layer.Leaving && shift.Y != 0)
            {
                shift.Y = AgentPlanMotion.IsFull(this) ? AgentPlanMotion.SwapRise * (1 - v) : 0;
            }
        };
        child.RenderTransform = shift;
        _layers[child] = layer;
        return layer;
    }

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        if (e.NewItems is not null)
        {
            foreach (Control child in e.NewItems)
            {
                var shown = ReferenceEquals(child, _shown);
                child.IsVisible = shown;
                child.Opacity = shown ? 1 : 0;
                LayerOf(child).Fade.Jump(shown ? 1 : 0);
            }
        }
        if (e.OldItems is not null)
        {
            foreach (Control child in e.OldItems)
            {
                if (_layers.Remove(child, out var layer))
                {
                    layer.Fade.Dispose();
                }
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size();
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            if (child.IsVisible)
            {
                size = new Size(Math.Max(size.Width, child.DesiredSize.Width), Math.Max(size.Height, child.DesiredSize.Height));
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Replaces one piece of content with another the way Arc's cart does (measured): the old one fades and drifts by
/// <see cref="ExitOffset"/> over <see cref="ExitDuration"/> while the new one fades in from <see cref="EnterOffset"/> (and
/// <see cref="EnterScale"/>) over <see cref="EnterDuration"/>, both starting at once (popLayout-style overlap). Only the
/// current content is measured, so a surrounding height or width spring follows the new size; leaving content keeps its
/// own size. Reduced motion crossfades in 120ms without movement; Off swaps at once.
/// </summary>
/// <remarks>
/// Code-driven because the two timings differ (160ms out on the standard curve, 240ms in on the enter curve, 5.6px
/// travel) and must overlap; <see cref="Primitives.ArcContentSwap"/>'s Label mode keeps the old content still.
/// </remarks>
internal sealed class CartDrawerSwap : Control
{
    private sealed class Layer
    {
        public required Control Child { get; init; }
        public required Decorator Host { get; init; }
        public required MotionValue<double> Progress { get; init; }
        public required TranslateTransform Translate { get; init; }
        public ScaleTransform? Scale { get; set; }
        public bool Leaving { get; set; }
        public double Offset { get; set; }
        public double FromScale { get; set; } = 1;
    }

    private readonly List<Layer> _layers = new();
    private Layer? _current;

    /// <summary>How long the old content takes to fade (default 160ms).</summary>
    public TimeSpan ExitDuration { get; set; } = TimeSpan.FromMilliseconds(160);

    /// <summary>Where the old content drifts to (px, negative is up; default −5.6).</summary>
    public double ExitOffset { get; set; } = -5.6;

    /// <summary>How long the new content takes to arrive (default 240ms).</summary>
    public TimeSpan EnterDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>Where the new content comes from (px, positive is below; default 5.6).</summary>
    public double EnterOffset { get; set; } = 5.6;

    /// <summary>The scale the new content grows from (default 1: no scale).</summary>
    public double EnterScale { get; set; } = 1;

    /// <summary>The scale the old content shrinks to (default 1: no scale).</summary>
    public double ExitScale { get; set; } = 1;

    /// <summary>Delay before the new content appears (default none).</summary>
    public TimeSpan EnterDelay { get; set; }

    /// <summary>The content on screen (null for none).</summary>
    public Control? Current => _current?.Child;

    /// <summary>Shows <paramref name="next"/>; the current content leaves. <paramref name="animate"/> false swaps at once.</summary>
    public void Show(Control? next, bool animate = true)
    {
        if (ReferenceEquals(next, _current?.Child))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var moving = animate && level != ArcMotionLevel.Off && VisualRoot is not null;
        var full = level == ArcMotionLevel.Full;
        if (_current is not null)
        {
            var old = _current;
            _current = null;
            if (moving)
            {
                old.Leaving = true;
                old.Offset = full ? ExitOffset : 0;
                old.FromScale = full ? ExitScale : 1;
                _ = Leave(old, full);
            }
            else
            {
                Remove(old);
            }
        }
        if (next is null)
        {
            InvalidateMeasure();
            return;
        }
        // A child still fading out is taken back rather than added twice.
        var existing = _layers.Find(l => ReferenceEquals(l.Child, next));
        if (existing is not null)
        {
            Remove(existing);
        }
        var translate = new TranslateTransform();
        // The opacity and transforms go on a host so the content keeps its own (a disabled button still dims).
        var host = new Decorator
        {
            Child = next,
            HorizontalAlignment = next.HorizontalAlignment,
            VerticalAlignment = next.VerticalAlignment
        };
        var layer = new Layer
        {
            Child = next,
            Host = host,
            Translate = translate,
            Progress = new MotionValue<double>(this, moving ? 0 : 1, MotionVectors.Double)
        };
        layer.Offset = moving && full ? EnterOffset : 0;
        layer.FromScale = moving && full ? EnterScale : 1;
        if (layer.FromScale != 1)
        {
            layer.Scale = new ScaleTransform();
            host.RenderTransform = new TransformGroup { Children = { layer.Scale, translate } };
        }
        else
        {
            host.RenderTransform = translate;
        }
        layer.Progress.Changed += _ => Apply(layer);
        _layers.Add(layer);
        _current = layer;
        LogicalChildren.Add(host);
        VisualChildren.Add(host);
        Apply(layer);
        InvalidateMeasure();
        if (moving)
        {
            var duration = full ? EnterDuration : TimeSpan.FromMilliseconds(120);
            var easing = full ? ArcEasings.Enter : (Easing)ArcEasings.Standard;
            _ = layer.Progress.AnimateTo(1, MotionSpec.Tween(duration, easing, EnterDelay));
        }
    }

    private async Task Leave(Layer layer, bool full)
    {
        layer.Progress.Stop();
        var duration = full ? ExitDuration : TimeSpan.FromMilliseconds(120);
        await layer.Progress.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard));
        if (layer.Leaving)
        {
            Remove(layer);
        }
    }

    private static void Apply(Layer layer)
    {
        var p = layer.Progress.Value;
        layer.Host.Opacity = Math.Clamp(p, 0, 1);
        layer.Translate.Y = layer.Offset * (1 - p);
        if (layer.Scale is not null)
        {
            var s = layer.FromScale + (1 - layer.FromScale) * p;
            layer.Scale.ScaleX = s;
            layer.Scale.ScaleY = s;
        }
    }

    private void Remove(Layer layer)
    {
        layer.Leaving = false;
        layer.Progress.Dispose();
        _layers.Remove(layer);
        if (ReferenceEquals(_current, layer))
        {
            _current = null;
        }
        VisualChildren.Remove(layer.Host);
        LogicalChildren.Remove(layer.Host);
        layer.Host.Child = null;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        foreach (var layer in _layers)
        {
            layer.Host.Measure(availableSize);
            if (ReferenceEquals(layer, _current))
            {
                size = layer.Host.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _layers)
        {
            var desired = layer.Host.DesiredSize;
            var rect = ReferenceEquals(layer, _current)
                ? new Rect(finalSize)
                : new Rect(0, 0, Math.Max(finalSize.Width, desired.Width), Math.Max(finalSize.Height, desired.Height));
            layer.Host.Arrange(rect);
        }
        return finalSize;
    }
}

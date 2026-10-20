using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a swapped glyph starts its entry or ends its exit (Arc's <c>initial</c> / <c>exit</c> targets).</summary>
internal readonly record struct ButtonsGlyphPose(double Scale = 1, double Rotate = 0, double X = 0, double Y = 0, double Blur = 0)
{
    /// <summary>At rest (scale 1). Use this, never <c>new()</c> or <c>default</c>: a struct's parameterless value is all
    /// zeros, so it would mean scale 0 and the glyph would shrink to nothing.</summary>
    public static readonly ButtonsGlyphPose Rest = new(Scale: 1);

    /// <summary>P2 icon swap: from 0.6 scale out of a subtle 2px blur.</summary>
    public static readonly ButtonsGlyphPose Pop = new(Scale: 0.6, Blur: 2);

    /// <summary>Arc ActionButton arrow: returns from 6px behind.</summary>
    public static readonly ButtonsGlyphPose ArrowIn = new(X: -6, Blur: 2);

    /// <summary>Arc ActionButton arrow: leaves 8px in the direction of the action.</summary>
    public static readonly ButtonsGlyphPose ArrowOut = new(X: 8, Blur: 2);
}

/// <summary>
/// AnimatePresence for one glyph slot in the Buttons category (P2 / P26): the new glyph rides a snappy spring from its
/// entry pose (scale, rotation, offset) while opacity and blur tween over fast + enter (so blur never overshoots), and
/// the old glyph tweens to its exit pose over fast + standard and is removed. Reduced motion crossfades over instant
/// (120ms); Off and swaps before the slot is loaded replace at once. Out of layout: only the current glyph sizes the slot.
/// </summary>
internal sealed class ButtonsGlyphSwap : Control
{
    private sealed class Layer
    {
        public required Decorator Host { get; init; }
        public required ScaleTransform Scale { get; init; }
        public required RotateTransform Rotate { get; init; }
        public required TranslateTransform Translate { get; init; }
        public BlurEffect? Blur { get; set; }
        public MotionValue<double>? Spatial { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public ButtonsGlyphPose Pose { get; set; }
    }

    private Layer? _current;
    private readonly List<Layer> _exiting = new();

    public ButtonsGlyphSwap()
    {
        IsHitTestVisible = false;
    }

    /// <summary>The glyph currently shown (null when empty).</summary>
    public Control? Current => _current?.Host.Child;

    /// <summary>Shows <paramref name="content"/>; the previous glyph leaves toward <paramref name="exitTo"/>.</summary>
    /// <param name="content">The new glyph, or null to clear.</param>
    /// <param name="enterFrom">Pose the new glyph comes from; null fades it in at rest.</param>
    /// <param name="exitTo">Pose the old glyph leaves to; null fades it out at rest.</param>
    /// <param name="spring">Spring that carries the new glyph to rest.</param>
    public void Show(Control? content, ButtonsGlyphPose? enterFrom, ButtonsGlyphPose? exitTo, ArcSpring spring = ArcSpring.Snappy)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        if (_current is not null)
        {
            var outgoing = _current;
            _current = null;
            if (animate)
            {
                StartExit(outgoing, exitTo ?? ButtonsGlyphPose.Rest, full);
            }
            else
            {
                RemoveLayer(outgoing);
            }
        }
        if (content is null)
        {
            InvalidateMeasure();
            return;
        }
        var layer = CreateLayer(content);
        _current = layer;
        if (animate)
        {
            StartEnter(layer, enterFrom ?? ButtonsGlyphPose.Rest, full, spring);
        }
        InvalidateMeasure();
    }

    private Layer CreateLayer(Control content)
    {
        var scale = new ScaleTransform();
        var rotate = new RotateTransform();
        var translate = new TranslateTransform();
        var host = new Decorator
        {
            Child = content,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new TransformGroup { Children = { scale, rotate, translate } }
        };
        LogicalChildren.Add(host);
        VisualChildren.Add(host);
        return new Layer { Host = host, Scale = scale, Rotate = rotate, Translate = translate };
    }

    private void RemoveLayer(Layer layer)
    {
        layer.Spatial?.Dispose();
        layer.Fade?.Dispose();
        _exiting.Remove(layer);
        LogicalChildren.Remove(layer.Host);
        VisualChildren.Remove(layer.Host);
        layer.Host.Child = null;
    }

    private static void ApplyPose(Layer layer, ButtonsGlyphPose pose)
    {
        layer.Pose = pose;
        layer.Scale.ScaleX = pose.Scale;
        layer.Scale.ScaleY = pose.Scale;
        layer.Rotate.Angle = pose.Rotate;
        layer.Translate.X = pose.X;
        layer.Translate.Y = pose.Y;
        if (pose.Blur < 0.05)
        {
            layer.Host.Effect = null;
        }
        else
        {
            layer.Blur ??= new BlurEffect();
            layer.Blur.Radius = pose.Blur;
            layer.Host.Effect = layer.Blur;
        }
    }

    private static ButtonsGlyphPose Lerp(ButtonsGlyphPose a, ButtonsGlyphPose b, double t) => new(
        a.Scale + (b.Scale - a.Scale) * t,
        a.Rotate + (b.Rotate - a.Rotate) * t,
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        Math.Max(0, a.Blur + (b.Blur - a.Blur) * t));

    private void StartEnter(Layer layer, ButtonsGlyphPose from, bool full, ArcSpring spring)
    {
        layer.Host.Opacity = 0;
        if (!full)
        {
            var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
            layer.Fade = fade;
            fade.Changed += v => layer.Host.Opacity = v;
            _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        var rest = ButtonsGlyphPose.Rest;
        ApplyPose(layer, from);
        var spatial = new MotionValue<double>(this, 0, MotionVectors.Double);
        var opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Spatial = spatial;
        layer.Fade = opacity;
        void Update()
        {
            var pose = Lerp(from, rest, spatial.Value);
            // Blur follows the tweened opacity so it never overshoots below zero.
            ApplyPose(layer, pose with { Blur = from.Blur * (1 - opacity.Value) });
            layer.Host.Opacity = opacity.Value;
        }
        spatial.Changed += _ => Update();
        opacity.Changed += _ => Update();
        _ = spatial.AnimateTo(1, MotionSpec.Spring(spring));
        _ = opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
    }

    private void StartExit(Layer layer, ButtonsGlyphPose to, bool full)
    {
        layer.Spatial?.Stop();
        layer.Fade?.Stop();
        _exiting.Add(layer);
        var start = layer.Pose;
        var startOpacity = layer.Host.Opacity;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Spatial?.Dispose();
        layer.Spatial = progress;
        progress.Changed += p =>
        {
            layer.Host.Opacity = startOpacity * (1 - p);
            if (full)
            {
                ApplyPose(layer, Lerp(start, to, p));
            }
            if (p >= 1)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_exiting.Contains(layer))
                    {
                        RemoveLayer(layer);
                    }
                });
            }
        };
        var spec = full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _ = progress.AnimateTo(1, spec);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _exiting)
        {
            layer.Host.Measure(availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Host.Measure(availableSize);
        return _current.Host.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            Center(layer.Host, finalSize);
        }
        if (_current is not null)
        {
            Center(_current.Host, finalSize);
        }
        return finalSize;
    }

    private static void Center(Control host, Size finalSize)
    {
        var size = host.DesiredSize;
        host.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _exiting.ToArray())
        {
            RemoveLayer(layer);
        }
        if (_current is not null)
        {
            _current.Spatial?.Stop();
            _current.Fade?.Stop();
            ApplyPose(_current, ButtonsGlyphPose.Rest);
            _current.Host.Opacity = 1;
        }
    }
}

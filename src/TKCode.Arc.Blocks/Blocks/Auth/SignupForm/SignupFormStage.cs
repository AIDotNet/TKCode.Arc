// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): stepMotion and useStepHeight of signup-form.tsx.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The card's viewport with its two steps (children 0 = form, 1 = completion), Arc's <c>AnimatePresence mode="popLayout"</c>
/// with <c>stepMotion</c> and <c>useStepHeight</c>. A step change pops the old step out of layout at its place and moves it
/// 16px against the direction on the smooth spring while it fades (180 ms, standard) into a 4px blur (180 ms, ease-out); the
/// new step comes from 24px along the direction on the smooth spring, out of a 4px blur (240 ms, enter) and fades in after
/// 50 ms (240 ms, enter). Meanwhile the viewport's height glides from the old height to the new step's on the smooth spring,
/// following the new step if it resizes; at rest the height is the current step's own again, so rows that open inside it
/// (field errors) move the card at once. Reduced motion fades the new step in over 120 ms with the old one gone at once and
/// the height in place; Off and the first step switch at once.
/// </summary>
/// <remarks>
/// Code-driven because the outgoing step must stay alive outside layout while the incoming one takes its place, the spatial
/// motion is a spring that keeps its velocity when the step changes back, and the height targets a measured size.
/// </remarks>
internal sealed class SignupFormStage : Panel
{
    private const double EnterOffset = 24;
    private const double ExitOffset = 16;
    private const double StepBlur = 4;

    private static readonly SpringSpec StepSpring = SpringSpec.FromVisual(0.4, 0);

    private readonly Dictionary<Control, Layer> _layers = new();
    private int _current = -1;
    private MotionValue<double>? _height;
    private bool _gliding;
    private double _glideTarget;
    private int _glideRun;

    /// <summary>The index of the step on screen.</summary>
    public int Current => _current;

    /// <summary>True while the height follows the spring instead of the step (tests read it).</summary>
    public bool IsGliding => _gliding;

    /// <summary>True while a step other than <see cref="Current"/> is still leaving (tests read it).</summary>
    public bool HasExiting => Children.Any(c => c.IsVisible && Children.IndexOf(c) != _current);

    private sealed class Layer
    {
        public required Control Step { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? X { get; set; }
        public MotionValue<double>? Opacity { get; set; }
        public MotionValue<double>? Blur { get; set; }
        public bool Exiting { get; set; }
        public int Run { get; set; }
    }

    private Layer LayerOf(Control step)
    {
        if (!_layers.TryGetValue(step, out var layer))
        {
            var translate = new TranslateTransform();
            step.RenderTransform = translate;
            layer = new Layer { Step = step, Translate = translate };
            _layers[step] = layer;
        }
        return layer;
    }

    /// <summary>Shows step <paramref name="index"/>; <paramref name="direction"/> is 1 forward and −1 back.</summary>
    public void Show(int index, int direction)
    {
        if (index < 0 || index >= Children.Count)
        {
            return;
        }
        if (_current < 0 || _current >= Children.Count)
        {
            _current = index;
            ShowOnly(index);
            return;
        }
        if (index == _current)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var previous = _current;
        _current = index;
        if (!IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            ShowOnly(index);
            return;
        }
        var outgoing = LayerOf(Children[previous]);
        var incoming = LayerOf(Children[index]);
        if (level == ArcMotionLevel.Reduced)
        {
            // Reduced: the old step leaves with a zero duration, the new one only fades; the height jumps.
            StopGlide();
            Hide(outgoing);
            Enter(incoming, direction, full: false);
            InvalidateMeasure();
            return;
        }
        StartGlide();
        Exit(outgoing, direction);
        Enter(incoming, direction, full: true);
        InvalidateMeasure();
    }

    private void ShowOnly(int index)
    {
        StopGlide();
        for (var i = 0; i < Children.Count; i++)
        {
            var layer = LayerOf(Children[i]);
            layer.Run++;
            StopValues(layer);
            Reset(layer);
            layer.Exiting = false;
            layer.Step.IsVisible = i == index;
            layer.Step.IsHitTestVisible = true;
        }
        InvalidateMeasure();
    }

    private static void StopValues(Layer layer)
    {
        layer.X?.Stop();
        layer.Opacity?.Stop();
        layer.Blur?.Stop();
    }

    private static void Reset(Layer layer)
    {
        layer.Translate.X = 0;
        layer.Step.Opacity = 1;
        layer.Step.Effect = null;
        layer.X?.Jump(0);
        layer.Opacity?.Jump(1);
        layer.Blur?.Jump(0);
    }

    private void Hide(Layer layer)
    {
        layer.Run++;
        StopValues(layer);
        Reset(layer);
        layer.Exiting = false;
        layer.Step.IsVisible = false;
        layer.Step.IsHitTestVisible = true;
    }

    private void Ensure(Layer layer)
    {
        if (layer.X is null)
        {
            layer.X = new MotionValue<double>(this, 0, MotionVectors.Double);
            layer.X.Changed += x => layer.Translate.X = x;
        }
        if (layer.Opacity is null)
        {
            layer.Opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            layer.Opacity.Changed += o => layer.Step.Opacity = o;
        }
        if (layer.Blur is null)
        {
            layer.Blur = new MotionValue<double>(this, 0, MotionVectors.Double);
            layer.Blur.Changed += b => layer.Step.Effect = ArcBlur.Get(b);
        }
    }

    private void Enter(Layer layer, int direction, bool full)
    {
        Ensure(layer);
        var run = ++layer.Run;
        var wasLeaving = layer.Exiting && layer.Step.IsVisible;
        layer.Exiting = false;
        layer.Step.IsVisible = true;
        layer.Step.IsHitTestVisible = true;
        if (!full)
        {
            layer.X!.Jump(0);
            layer.Blur!.Jump(0);
            layer.Opacity!.Jump(0);
            _ = layer.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, SignupFormSwap.EaseOut));
            return;
        }
        if (!wasLeaving)
        {
            // A step that comes back while it is still leaving turns around from where it is (AnimatePresence re-entry).
            layer.X!.Jump(direction * EnterOffset);
            layer.Opacity!.Jump(0);
            layer.Blur!.Jump(StepBlur);
        }
        _ = layer.X!.AnimateTo(0, MotionSpec.Spring(StepSpring));
        _ = layer.Opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(50)));
        _ = layer.Blur!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    private void Exit(Layer layer, int direction)
    {
        Ensure(layer);
        var run = ++layer.Run;
        layer.Exiting = true;
        layer.Step.IsHitTestVisible = false;
        var x = layer.X!.AnimateTo(direction * -ExitOffset, MotionSpec.Spring(StepSpring));
        var opacity = layer.Opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        _ = layer.Blur!.AnimateTo(StepBlur, MotionSpec.Tween(ArcMotionPresets.Exit, SignupFormSwap.EaseOut));
        _ = HideWhenDone(layer, run, x, opacity);
    }

    private async Task HideWhenDone(Layer layer, int run, Task<bool> x, Task<bool> opacity)
    {
        // The step leaves the tree when its slowest value (the spring) has settled; resumes on the UI thread.
        await Task.WhenAll(x, opacity);
        if (layer.Run == run && layer.Exiting)
        {
            Hide(layer);
            InvalidateMeasure();
        }
    }

    private void StartGlide()
    {
        _height ??= CreateHeight();
        var from = Bounds.Height;
        if (from <= 0)
        {
            StopGlide();
            return;
        }
        if (!_gliding)
        {
            _height.Jump(from);
        }
        _gliding = true;
        _glideTarget = double.NaN;
        _glideRun++;
    }

    private MotionValue<double> CreateHeight()
    {
        var height = new MotionValue<double>(this, 0, MotionVectors.Double);
        height.Changed += _ => InvalidateMeasure();
        return height;
    }

    private void Glide(double target)
    {
        if (_height is null || target <= 0)
        {
            StopGlide();
            return;
        }
        if (!double.IsNaN(_glideTarget) && Math.Abs(target - _glideTarget) < 0.01)
        {
            return;
        }
        _glideTarget = target;
        var run = _glideRun;
        // A new target keeps the spring's velocity (useStepHeight retargets on every resize while gliding).
        _ = EndGlideWhenDone(run, _height.AnimateTo(target, MotionSpec.Spring(StepSpring)));
    }

    private async Task EndGlideWhenDone(int run, Task<bool> glide)
    {
        if (await glide && run == _glideRun && _gliding)
        {
            StopGlide();
        }
    }

    private void StopGlide()
    {
        if (!_gliding)
        {
            return;
        }
        _gliding = false;
        _glideRun++;
        _height?.Stop();
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var current = _current >= 0 && _current < Children.Count ? Children[_current] : null;
        var width = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(availableSize.WithHeight(double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
        }
        if (current is null)
        {
            return default;
        }
        var natural = current.DesiredSize.Height;
        if (_gliding && _height is not null)
        {
            Glide(natural);
            return new Size(width, Math.Max(0, _height.Value));
        }
        return new Size(width, natural);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            // popLayout: the leaving step keeps its own height at its old place; the current one is in flow at the top.
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ClipToBounds = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_current >= 0)
        {
            ShowOnly(_current);
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Moves one layer of the step area (a step or the success block): it slides on X with Arc's smooth spring (visual duration
/// .4, no bounce; fitted ω 13.1 on the preview), rises on Y and fades. Entering fades in over 240 ms (enter curve), leaving
/// fades out in 120 ms (standard curve) and hides the layer once the slide has settled. A layer that is leaving and is asked
/// to enter again turns around from where it is, keeping the slide's velocity (Arc reuses the presenter when a step is
/// undone mid-swap). While it leaves, a layer takes no input and is skipped by Tab.
/// </summary>
/// <remarks>
/// Code-driven because the slide must keep its velocity when reversed and the layer must be hidden only after the spring
/// settles, which ControlTheme transitions cannot express.
/// </remarks>
internal sealed class MultiStepLayerMotion
{
    private readonly Control _layer;
    private readonly TranslateTransform _translate = new();
    private readonly MotionValue<double> _x;
    private readonly MotionValue<double> _y;
    private readonly MotionValue<double> _opacity;
    private int _generation;

    public MultiStepLayerMotion(Control layer)
    {
        _layer = layer;
        layer.RenderTransform = _translate;
        _x = new MotionValue<double>(layer, 0, MotionVectors.Double);
        _x.Changed += v => _translate.X = v;
        _y = new MotionValue<double>(layer, 0, MotionVectors.Double);
        _y.Changed += v => _translate.Y = v;
        _opacity = new MotionValue<double>(layer, 1, MotionVectors.Double);
        _opacity.Changed += v => _layer.Opacity = Math.Clamp(v, 0, 1);
    }

    /// <summary>True from the start of an exit until the layer enters again.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>The layer's current offset (tests read it).</summary>
    public double X => _x.Value;

    public double Y => _y.Value;

    /// <summary>The layer's current opacity (tests read it).</summary>
    public double Opacity => _opacity.Value;

    /// <summary>Shows the layer at rest at once.</summary>
    public void Show()
    {
        _generation++;
        SetLeaving(false);
        _x.Jump(0);
        _y.Jump(0);
        _opacity.Jump(1);
        _layer.IsVisible = true;
    }

    /// <summary>Hides the layer at once.</summary>
    public void Hide()
    {
        _generation++;
        SetLeaving(false);
        _x.Jump(0);
        _y.Jump(0);
        _opacity.Jump(0);
        _layer.IsVisible = false;
    }

    /// <summary>
    /// Brings the layer to rest. A hidden layer starts at (<paramref name="fromX"/>, <paramref name="fromY"/>) and fully
    /// transparent; a visible one (leaving, or caught mid-way) turns around from where it is.
    /// </summary>
    public void Enter(double fromX, double fromY)
    {
        _generation++;
        SetLeaving(false);
        if (!_layer.IsVisible)
        {
            _x.Jump(fromX);
            _y.Jump(fromY);
            _opacity.Jump(0);
            _layer.IsVisible = true;
        }
        _ = _x.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Smooth));
        _ = _y.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    /// <summary>Slides the layer to <paramref name="toX"/> while it fades out, then hides it and calls <paramref name="hidden"/>.</summary>
    public async void Exit(double toX, Action? hidden = null)
    {
        if (!_layer.IsVisible)
        {
            return;
        }
        var generation = ++_generation;
        SetLeaving(true);
        var slide = _x.AnimateTo(toX, MotionSpec.Spring(ArcMotionPresets.Smooth));
        var fade = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        await fade;
        await slide;
        if (generation != _generation)
        {
            return;
        }
        SetLeaving(false);
        _x.Jump(0);
        _y.Jump(0);
        _layer.IsVisible = false;
        hidden?.Invoke();
    }

    private void SetLeaving(bool leaving)
    {
        IsLeaving = leaving;
        _layer.IsHitTestVisible = !leaving;
        KeyboardNavigation.SetTabNavigation(_layer, leaving ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
    }
}

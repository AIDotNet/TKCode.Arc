using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The pill's body: it holds the start group and the live group on top of each other and swaps them (Arc Voice orb,
/// design reference). Its width springs from the old group's width to the new one's (start: k 122, c 20.5; end: ω 10.6,
/// ζ 0.9) while the old group leaves in 120 ms (fade, scale to 0.92, 4 px blur, ease-out, taking no room) and the new one
/// arrives at the same moment (fade and unblur in 160 ms ease-out, scale from 0.92 on a spring). Reduced motion: 120 ms
/// fades and an instant width.
/// </summary>
internal sealed class VoiceControlsPill : Panel
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<VoiceControlsPill, bool>(nameof(IsActive));

    /// <summary>The width spring when the session starts (measured k 122, c 20.5: ω 11.0, ζ 0.93).</summary>
    public static readonly SpringSpec StartSpring = SpringSpec.FromPhysical(122, 20.5);

    /// <summary>The width spring when the session ends (measured ω 10.6, ζ 0.90).</summary>
    public static readonly SpringSpec EndSpring = SpringSpec.FromPhysical(10.6 * 10.6, 2 * 0.9 * 10.6);

    /// <summary>The entering group's scale spring (reaches 0.999 in about 400 ms).</summary>
    public static readonly SpringSpec ScaleSpring = SpringSpec.FromPhysical(225, 30);

    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    private readonly Group _idle = new();
    private readonly Group _live = new();
    private MotionValue<double>? _width;
    private double _target = double.NaN;
    private bool _swapped;

    private sealed class Group
    {
        public Control? View;
        public MotionValue<double>? Fade;
        public MotionValue<double>? Scale;
        public readonly ScaleTransform Transform = new(1, 1);
        public double FadeValue = 1;
        public double ScaleValue = 1;
    }

    public VoiceControlsPill()
    {
        ClipToBounds = true;
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>The width the pill is drawn at (tests).</summary>
    internal double CurrentWidth => _width?.Value ?? _target;

    /// <summary>The width it is heading for (tests).</summary>
    internal double TargetWidth => _target;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Bind();
        Show(IsActive ? _live : _idle, IsActive ? _idle : _live, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            Bind();
            _swapped = true;
            Show(IsActive ? _live : _idle, IsActive ? _idle : _live, VisualRoot is not null && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off);
            InvalidateMeasure();
        }
    }

    private void Bind()
    {
        if (Children.Count >= 2)
        {
            _idle.View = Children[0];
            _live.View = Children[1];
        }
    }

    private void Show(Group entering, Group leaving, bool animate)
    {
        if (entering.View is not { } enter || leaving.View is not { } leave)
        {
            return;
        }
        var full = animate && ArcMotion.IsFull(this);
        enter.IsVisible = true;
        enter.IsHitTestVisible = true;
        KeyboardNavigation.SetTabNavigation(enter, KeyboardNavigationMode.Continue);
        leave.IsHitTestVisible = false;
        KeyboardNavigation.SetTabNavigation(leave, KeyboardNavigationMode.None);
        if (!animate)
        {
            entering.Fade?.Stop();
            entering.Scale?.Stop();
            leaving.Fade?.Stop();
            leaving.Scale?.Stop();
            Apply(entering, 1, 1, false);
            Apply(leaving, 0, 1, false);
            leave.IsVisible = false;
            return;
        }
        // Why code and not the ControlTheme: the two groups cross with different curves (a 120 ms exit, a 160 ms fade-in
        // with a spring scale) and the scale spring must keep its velocity when the session toggles quickly.
        entering.Fade ??= NewValue(entering, true);
        entering.Scale ??= NewValue(entering, false);
        leaving.Fade ??= NewValue(leaving, true);
        leaving.Scale ??= NewValue(leaving, false);
        if (entering.FadeValue <= 0.001)
        {
            entering.Scale.Jump(full ? 0.92 : 1);
        }
        var enterFade = full ? TimeSpan.FromMilliseconds(160) : ArcMotionPresets.Instant;
        _ = entering.Fade.AnimateTo(1, MotionSpec.Tween(enterFade, EaseOut));
        if (full)
        {
            _ = entering.Scale.AnimateTo(1, MotionSpec.Spring(ScaleSpring));
        }
        else
        {
            entering.Scale.Jump(1);
        }
        _ = leaving.Scale.AnimateTo(full ? 0.92 : 1, MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut));
        _ = HideAfter(leaving, leaving.Fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut)));
    }

    private async Task HideAfter(Group group, Task<bool> fade)
    {
        var done = await fade;
        if (done && group.FadeValue <= 0.001 && group.View is { } view && !ReferenceEquals(group, IsActive ? _live : _idle))
        {
            view.IsVisible = false;
        }
    }

    private MotionValue<double> NewValue(Group group, bool fade)
    {
        var value = new MotionValue<double>(this, fade ? group.FadeValue : group.ScaleValue, MotionVectors.Double);
        value.Changed += v =>
        {
            if (fade)
            {
                Apply(group, v, group.ScaleValue, ArcMotion.IsFull(this));
            }
            else
            {
                Apply(group, group.FadeValue, v, true);
            }
        };
        return value;
    }

    private void Apply(Group group, double fade, double scale, bool blur)
    {
        group.FadeValue = fade;
        group.ScaleValue = scale;
        if (group.View is not { } view)
        {
            return;
        }
        view.Opacity = fade;
        group.Transform.ScaleX = scale;
        group.Transform.ScaleY = scale;
        view.RenderTransformOrigin = RelativePoint.Center;
        view.RenderTransform = Math.Abs(scale - 1) < 0.0005 ? null : group.Transform;
        var radius = blur ? 4 * (1 - fade) : 0;
        view.Effect = radius > 0.05 && fade > 0.001 ? ArcBlur.Get(radius) : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Bind();
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var active = IsActive ? _live.View : _idle.View;
        var target = active?.DesiredSize.Width ?? 0;
        if (Math.Abs(target - _target) > 0.01 || double.IsNaN(_target))
        {
            var animate = _swapped && !double.IsNaN(_target) && VisualRoot is not null &&
                          ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
            _target = target;
            if (animate)
            {
                if (_width is null)
                {
                    _width = new MotionValue<double>(this, CurrentWidthOr(target), MotionVectors.Double);
                    _width.Changed += _ => InvalidateMeasure();
                }
                _ = _width.AnimateTo(target, MotionSpec.Spring(IsActive ? StartSpring : EndSpring));
            }
            else
            {
                _width?.Jump(target);
            }
        }
        _swapped = false;
        var width = _width?.Value ?? _target;
        return new Size(width, height);
    }

    private double CurrentWidthOr(double fallback)
    {
        // The pill had no spring yet: it was resting at the other group's width.
        var other = IsActive ? _idle.View : _live.View;
        return other?.DesiredSize.Width is > 0 and var w ? w : fallback;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var w = child.DesiredSize.Width;
            child.Arrange(new Rect((finalSize.Width - w) / 2, (finalSize.Height - child.DesiredSize.Height) / 2, w, child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// A button of the voice pill (start or end): a borderless face whose fill comes from the theme, a keyboard focus ring
/// drawn 2 px inside its edge (the pill clips everything outside), and hover styles only for a mouse or pen.
/// </summary>
internal class VoiceControlsButton : Button
{
    public const string HoverPseudoClass = ":hover-fine";

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(VoiceControlsButton);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= VoiceControlsFocus.Ring(this, Classes.Contains("start"));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    internal bool IsFocusRingShown => _focusRing?.Ring?.IsVisible == true;
}

/// <summary>The mute toggle of the voice pill: a <see cref="ToggleButton"/> (pressed = muted) with the same face rules.</summary>
internal sealed class VoiceControlsMuteButton : ToggleButton
{
    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(VoiceControlsMuteButton);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= VoiceControlsFocus.Ring(this, false);
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(VoiceControlsButton.HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(VoiceControlsButton.HoverPseudoClass, false);
    }

    internal bool IsFocusRingShown => _focusRing?.Ring?.IsVisible == true;
}

internal static class VoiceControlsFocus
{
    public static LazyFocusRing Ring(Control owner, bool inset) =>
        new(owner, VoiceControlsTokens.Key(inset ? VoiceControlsTokenKind.StartFocusRingMargin : VoiceControlsTokenKind.FocusRingMargin),
            VoiceControlsTokens.Key(VoiceControlsTokenKind.FocusRingRadius));
}

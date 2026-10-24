using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Back and submit buttons of the multi-step form, drawn the way Arc's form draws them (36 high, radius 18, padding 0 12,
/// a 16px arrow 8px from the label): the submit button is filled with the accent and darkens to the strong accent on hover;
/// Back is a surface button with a strong border that turns to the foreground on hover. A disabled button is drawn at 45%
/// at once. The label swaps in place (<see cref="MultiStepSwap"/>) while its slot's width follows on Arc's smooth spring.
/// A press scales the button to 0.98 in 100 ms (ease-out) and the release springs back (fitted ω 13.8, no bounce); reduced
/// motion and Off do not scale. Hover only answers a mouse or pen (Arc's <c>(hover: hover)</c>).
/// </summary>
/// <remarks>
/// The press is code-driven because the press and the release run on different curves (a short tween in, a spring out),
/// which one ControlTheme transition cannot express.
/// </remarks>
[PseudoClasses(HoverPseudoClass, LoadingPseudoClass)]
internal sealed class MultiStepButton : AvaloniaButton
{
    /// <summary>A mouse or pen is over the button (touch never sets it).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The button shows a spinner instead of its label.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MultiStepButton, string?>(nameof(Label));

    /// <summary>Defines the <see cref="IsLoading"/> property.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<MultiStepButton, bool>(nameof(IsLoading));

    /// <summary>The pressed scale and the two curves (press: 100 ms ease-out; release: fitted ω 13.8, ζ 1).</summary>
    internal const double PressedScale = 0.98;

    internal static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(100);

    internal static readonly Easing PressEasing = new SplineEasing(0, 0, 0.58, 1);

    internal static readonly SpringSpec ReleaseSpring = SpringSpec.FromVisual(0.38, 0);

    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _press;

    public MultiStepButton()
    {
        RenderTransform = _scale;
    }

    /// <summary>The visible label (also the accessible name).</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>True shows a spinner and ignores presses while keeping the keyboard focus.</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>The current press scale (tests read it).</summary>
    internal double Scale => _scale.ScaleX;

    protected override Type StyleKeyOverride => typeof(MultiStepButton);

    protected override void OnClick()
    {
        if (IsLoading)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        }
        else if (change.Property == IsPressedProperty)
        {
            AnimatePress(IsPressed);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    private void AnimatePress(bool pressed)
    {
        _press ??= CreatePress();
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _press.Jump(1);
            return;
        }
        _ = pressed
            ? _press.AnimateTo(PressedScale, MotionSpec.Tween(PressDuration, PressEasing))
            : _press.AnimateTo(1, MotionSpec.Spring(ReleaseSpring));
    }

    private MotionValue<double> CreatePress()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += s => _scale.ScaleX = _scale.ScaleY = s;
        return value;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

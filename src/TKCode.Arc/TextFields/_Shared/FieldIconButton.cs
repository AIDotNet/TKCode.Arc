using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A small icon button that lives inside a <see cref="FieldFrame"/> (clear, reveal, save, cancel): muted glyph,
/// <c>SurfaceMuted</c> fill and <c>Foreground</c> glyph on hover, a press that scales to <see cref="PressScale"/>,
/// and the 2px focus ring for keyboard focus. With <see cref="IsToggle"/> it reports a pressed state
/// (<c>aria-pressed</c>) through <see cref="IsOn"/>, which also keeps the glyph at full contrast.
/// </summary>
[PseudoClasses(OnPseudoClass)]
public class FieldIconButton : AvaloniaButton
{
    /// <summary>Set while <see cref="IsOn"/> is true.</summary>
    public const string OnPseudoClass = ":on";

    /// <summary>When true the button reports a toggle state to assistive technology.</summary>
    public static readonly StyledProperty<bool> IsToggleProperty =
        AvaloniaProperty.Register<FieldIconButton, bool>(nameof(IsToggle));

    /// <summary>The pressed (<c>aria-pressed</c>) state of a toggle; also keeps the glyph at full contrast.</summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<FieldIconButton, bool>(nameof(IsOn));

    /// <summary>Scale while pressed (Arc: 0.96; PasswordStrength: 0.94). Full motion only.</summary>
    public static readonly StyledProperty<double> PressScaleProperty =
        AvaloniaProperty.Register<FieldIconButton, double>(nameof(PressScale), 0.96);

    /// <summary>
    /// Corner radius of the 2px keyboard focus ring, which sits 4px outside the button. Keep it at the button's radius
    /// plus 4 so the ring stays concentric when a host changes the button's <c>CornerRadius</c>.
    /// </summary>
    public static readonly StyledProperty<CornerRadius> FocusRingRadiusProperty =
        AvaloniaProperty.Register<FieldIconButton, CornerRadius>(nameof(FocusRingRadius));

    /// <summary>When true the button reports a toggle state to assistive technology.</summary>
    public bool IsToggle
    {
        get => GetValue(IsToggleProperty);
        set => SetValue(IsToggleProperty, value);
    }

    /// <summary>The pressed state of a toggle.</summary>
    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>Scale while pressed.</summary>
    public double PressScale
    {
        get => GetValue(PressScaleProperty);
        set => SetValue(PressScaleProperty, value);
    }

    /// <summary>Corner radius of the keyboard focus ring.</summary>
    public CornerRadius FocusRingRadius
    {
        get => GetValue(FocusRingRadiusProperty);
        set => SetValue(FocusRingRadiusProperty, value);
    }

    private static readonly Avalonia.Media.Transformation.TransformOperations s_identity =
        Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");

    private Avalonia.Media.Transformation.TransformOperations? _pressed;

    public FieldIconButton()
    {
        // Start from an explicit identity so the first press animates too.
        RenderTransform = s_identity;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PressScaleProperty)
        {
            _pressed = null;
        }
        if (change.Property == IsOnProperty)
        {
            PseudoClasses.Set(OnPseudoClass, IsOn);
        }
        else if (change.Property == IsPressedProperty || change.Property == PressScaleProperty)
        {
            UpdatePressTransform();
        }
    }

    private void UpdatePressTransform()
    {
        // Applied as a value so the theme's transition (full motion only) animates it; reduced motion never scales.
        if (IsPressed && Classes.Contains(Motion.ArcMotion.FullPseudoClass))
        {
            _pressed ??= Avalonia.Media.Transformation.TransformOperations.Parse(FormattableString.Invariant($"scale({PressScale})"));
            RenderTransform = _pressed;
        }
        else
        {
            RenderTransform = s_identity;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => IsToggle ? new FieldIconButtonTogglePeer(this) : base.OnCreateAutomationPeer();

    private sealed class FieldIconButtonTogglePeer : ControlAutomationPeer, IToggleProvider, IInvokeProvider
    {
        public FieldIconButtonTogglePeer(FieldIconButton owner)
            : base(owner)
        {
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsOnProperty)
                {
                    RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                        ToState(e.GetOldValue<bool>()), ToState(e.GetNewValue<bool>()));
                }
            };
        }

        private new FieldIconButton Owner => (FieldIconButton)base.Owner;

        public ToggleState ToggleState => ToState(Owner.IsOn);

        public void Toggle() => Invoke();

        public void Invoke()
        {
            if (Owner.IsEffectivelyEnabled)
            {
                Owner.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(ClickEvent));
                Owner.Command?.Execute(Owner.CommandParameter);
            }
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        private static ToggleState ToState(bool on) => on ? ToggleState.On : ToggleState.Off;
    }
}

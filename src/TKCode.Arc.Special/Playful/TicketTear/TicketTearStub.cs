using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The stub of a <see cref="TicketTear"/>: the button you pull, or press Enter on. It takes the input over its whole box;
/// the paper inside it is clipped to the part still on the ticket, and pointer capture keeps the tear going when the hand
/// leaves the stub.
/// </summary>
[TemplatePart(PiecePart, typeof(TicketTearPiece))]
[TemplatePart(RootPart, typeof(Panel))]
internal sealed class TicketTearStub : ContentControl
{
    public const string PiecePart = "PART_Piece";
    public const string RootPart = "PART_Root";

    /// <summary>Radius of the notches, set by the theme.</summary>
    public static readonly StyledProperty<double> NotchRadiusProperty =
        AvaloniaProperty.Register<TicketTearStub, double>(nameof(NotchRadius), 9);

    /// <summary>Radius of the holes, set by the theme.</summary>
    public static readonly StyledProperty<double> HoleRadiusProperty =
        AvaloniaProperty.Register<TicketTearStub, double>(nameof(HoleRadius), 2.5);

    /// <summary>Distance between holes, set by the theme.</summary>
    public static readonly StyledProperty<double> HolePitchProperty =
        AvaloniaProperty.Register<TicketTearStub, double>(nameof(HolePitch), 13);

    /// <summary>How far inside the stub the perforation runs, set by the theme.</summary>
    public static readonly StyledProperty<double> PerforationInsetProperty =
        AvaloniaProperty.Register<TicketTearStub, double>(nameof(PerforationInset), 4.5);

    private readonly VelocityTracker _velocity = new();
    private LazyFocusRing? _focusRing;
    private Point _pressedAt;
    private bool _held;

    static TicketTearStub()
    {
        FocusableProperty.OverrideDefaultValue<TicketTearStub>(true);
    }

    public double NotchRadius
    {
        get => GetValue(NotchRadiusProperty);
        set => SetValue(NotchRadiusProperty, value);
    }

    public double HoleRadius
    {
        get => GetValue(HoleRadiusProperty);
        set => SetValue(HoleRadiusProperty, value);
    }

    public double HolePitch
    {
        get => GetValue(HolePitchProperty);
        set => SetValue(HolePitchProperty, value);
    }

    public double PerforationInset
    {
        get => GetValue(PerforationInsetProperty);
        set => SetValue(PerforationInsetProperty, value);
    }

    internal TicketTear? Owner { get; set; }

    internal TicketTearPiece? Piece { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Piece = e.NameScope.Find<TicketTearPiece>(PiecePart);
        _focusRing ??= new LazyFocusRing(this, TicketTearTokens.Key(TicketTearTokenKind.FocusRingMargin),
            TicketTearTokens.Key(TicketTearTokenKind.FocusRingRadius));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
        Owner?.OnStubTemplated();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NotchRadiusProperty || change.Property == HoleRadiusProperty || change.Property == HolePitchProperty || change.Property == PerforationInsetProperty)
        {
            Owner?.InvalidateShapes();
        }
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Owner is not { } owner || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        _held = true;
        _pressedAt = e.GetPosition(owner);
        _velocity.Reset();
        _velocity.AddSample(_pressedAt, TimeSpan.FromMilliseconds(e.Timestamp));
        e.Pointer.Capture(this);
        owner.BeginPull();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_held || Owner is not { } owner)
        {
            return;
        }
        var position = e.GetPosition(owner);
        var time = TimeSpan.FromMilliseconds(e.Timestamp);
        _velocity.AddSample(position, time);
        owner.Pull(position - _pressedAt, _velocity.GetVelocity(time));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_held)
        {
            return;
        }
        _held = false;
        Owner?.EndPull(_velocity.GetVelocity(TimeSpan.FromMilliseconds(e.Timestamp)));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_held)
        {
            _held = false;
            Owner?.EndPull(default);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
        {
            Owner?.Tear();
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StubAutomationPeer(this);

    private sealed class StubAutomationPeer(TicketTearStub owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => owner.Owner?.Label;

        public void Invoke() => owner.Owner?.Tear();
    }
}

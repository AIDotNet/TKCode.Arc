using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Which part of the plate an <see cref="ArmingSwitchHit"/> stands for.</summary>
internal enum ArmingSwitchPart
{
    Cover,
    Switch,
    Lamp
}

/// <summary>
/// One of the three real controls laid over the drawn plate of an <see cref="ArmingSwitch"/>: the safety cover, the
/// switch and the lamp. Each is focusable, takes the pointer over the part it stands for and has its own automation
/// peer (a button; the switch also toggles). The stage handles their input and places them over the projected part.
/// They draw only the keyboard focus ring (2 px, the foreground at 72%, a rounded rect around the part).
/// </summary>
internal sealed class ArmingSwitchHit : Control
{
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ArmingSwitchHit, IBrush?>(nameof(FocusRingBrush));

    private Pen? _ringPen;

    static ArmingSwitchHit()
    {
        FocusableProperty.OverrideDefaultValue<ArmingSwitchHit>(true);
        AffectsRender<ArmingSwitchHit>(FocusRingBrushProperty);
    }

    public ArmingSwitchHit(ArmingSwitchStage stage, ArmingSwitchPart part)
    {
        Stage = stage;
        Part = part;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public ArmingSwitchStage Stage { get; }

    public ArmingSwitchPart Part { get; }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    /// <summary>The corner radius of the focus ring and the hit area (a circle for the lamp).</summary>
    public double Radius { get; set; } = 12;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FocusRingBrushProperty)
        {
            _ringPen = null;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
        Stage.OnHitFocusChanged();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
        Stage.OnHitFocusChanged();
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        // Transparent fill so the whole area takes the pointer, not only drawn pixels.
        context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(rect, Radius));
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            _ringPen ??= new Pen(FocusRingBrush ?? Brushes.White, 2);
            context.DrawRectangle(null, _ringPen, new RoundedRect(rect.Deflate(1), Math.Max(0, Radius - 1)));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => Part switch
    {
        ArmingSwitchPart.Cover => new CoverPeer(this),
        ArmingSwitchPart.Switch => new SwitchPeer(this),
        _ => new HitPeer(this)
    };

    private class HitPeer : ControlAutomationPeer
    {
        public HitPeer(ArmingSwitchHit owner)
            : base(owner)
        {
        }

        protected ArmingSwitchHit Hit => (ArmingSwitchHit)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Hit.Stage.NameOf(Hit.Part);

        protected override string? GetHelpTextCore() => Hit.Stage.Owner?.CurrentHint;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    private sealed class CoverPeer : HitPeer, IInvokeProvider
    {
        public CoverPeer(ArmingSwitchHit owner)
            : base(owner)
        {
        }

        public void Invoke() => Hit.Stage.Activate(ArmingSwitchPart.Cover);
    }

    private sealed class SwitchPeer : HitPeer, IToggleProvider
    {
        public SwitchPeer(ArmingSwitchHit owner)
            : base(owner)
        {
        }

        public ToggleState ToggleState => Hit.Stage.Owner?.IsArmed == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Hit.Stage.Activate(ArmingSwitchPart.Switch);
    }
}

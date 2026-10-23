using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A focusable, self-drawn button of a <see cref="MixtureOfExperts"/>: an expert tile or a sequence token. Hovering
/// (mouse or pen) or keyboard-focusing it inspects it, a click, tap, Enter or Space pins it (pressed state), Escape
/// releases the pin, and the arrow keys, Home and End move along its group. The card does the work; the part reports.
/// </summary>
internal abstract class MixtureOfExpertsPart : Control
{
    private bool _pressed;

    protected MixtureOfExpertsPart(MixtureOfExperts owner, int index)
    {
        Owner = owner;
        Index = index;
        Focusable = true;
        // The part draws its own keyboard ring.
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = false;
    }

    public MixtureOfExperts Owner { get; }

    public int Index { get; }

    /// <summary>True for an expert tile, false for a sequence token.</summary>
    public abstract bool IsExpert { get; }

    /// <summary>True while pinned (the pressed state screen readers report).</summary>
    public bool IsPinned { get; private set; }

    /// <summary>The time (wall seconds) until which this part animates.</summary>
    public double AnimatingUntil { get; protected set; }

    /// <summary>True while focused from the keyboard (the ring shows only then).</summary>
    public bool IsKeyboardFocused => IsFocused && Classes.Contains(":focus-visible");

    public void SetPinned(bool pinned)
    {
        if (IsPinned == pinned)
        {
            return;
        }
        IsPinned = pinned;
        if (ControlAutomationPeer.FromElement(this) is MixtureOfExpertsPartAutomationPeer peer)
        {
            peer.RaisePinnedChanged(!pinned, pinned);
        }
    }

    /// <summary>Invalidates the part when its fades still run at <paramref name="wall"/> (or just ended).</summary>
    public void Tick(double wall)
    {
        if (AnimatingUntil > 0 && wall <= AnimatingUntil + 0.05)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            Owner.OnPartHover(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            Owner.OnPartHover(this, false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        e.Handled = true;
        var point = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(point))
        {
            Owner.TogglePin(this);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            Owner.OnPartKey(this, e);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner.OnPartFocus(this, true, e.NavigationMethod);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner.OnPartFocus(this, false, NavigationMethod.Unspecified);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MixtureOfExpertsPartAutomationPeer(this);

    /// <summary>The 2 px keyboard ring, 2 px outside a rounded rect.</summary>
    protected void DrawFocusRing(DrawingContext context, Rect rect, double radius)
    {
        if (!IsKeyboardFocused || !Owner.Palette.IsReady)
        {
            return;
        }
        var pen = Owner.Palette.Pen(Owner.Palette.FocusRing, 2);
        context.DrawRectangle(null, pen, new RoundedRect(rect.Inflate(3), radius + 3));
    }

    /// <summary>
    /// How strongly to draw the old and the new color of a fade: opaque pairs keep the old one whole under the new;
    /// otherwise the old fades out as the new fades in (exact at both ends). A zero means "skip it".
    /// </summary>
    internal static (double From, double To) FadeOpacity(Color from, Color to, double mix)
    {
        if (from == to || mix >= 1)
        {
            return (0, to.A > 0 ? 1 : 0);
        }
        var opaque = from.A == 255 && to.A == 255;
        return (from.A > 0 ? (opaque ? 1 : 1 - mix) : 0, to.A > 0 ? Math.Max(0, mix) : 0);
    }
}

/// <summary>A tile or token is a toggle button to screen readers: named by the card, pressed while pinned.</summary>
internal sealed class MixtureOfExpertsPartAutomationPeer : ControlAutomationPeer, IToggleProvider
{
    public MixtureOfExpertsPartAutomationPeer(MixtureOfExpertsPart owner) : base(owner)
    {
    }

    private new MixtureOfExpertsPart Owner => (MixtureOfExpertsPart)base.Owner;

    public ToggleState ToggleState => Owner.IsPinned ? ToggleState.On : ToggleState.Off;

    public void Toggle() => Owner.Owner.TogglePin(Owner);

    public void RaisePinnedChanged(bool old, bool now)
        => RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
            old ? ToggleState.On : ToggleState.Off, now ? ToggleState.On : ToggleState.Off);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

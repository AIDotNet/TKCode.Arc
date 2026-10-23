using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// The keyboard stop of one citation. StreamMark draws citations inside a text layout, where nothing can take focus, so
/// each linked citation gets this invisible stand-in, in reading order, over the reply. When it takes focus the reply
/// moves it onto the pill (so a scroll viewer brings the pill into view), draws the focus ring on the pill and opens the
/// preview; Enter or Space invokes the source. It never takes pointer input: the pill underneath does.
/// </summary>
internal sealed class AiCitationTarget : Control
{
    static AiCitationTarget()
    {
        FocusableProperty.OverrideDefaultValue<AiCitationTarget>(true);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiCitationTarget>(false);
        FocusAdornerProperty.OverrideDefaultValue<AiCitationTarget>(null);
    }

    public AiCitationTarget(AiReplyText owner, int number, int key)
    {
        Owner = owner;
        Number = number;
        Key = key;
        // The reply draws the ring on the pill; no adorner (a local value, so app-wide focus styles do not add one).
        FocusAdorner = null;
    }

    /// <summary>The reply this citation belongs to.</summary>
    public AiReplyText Owner { get; }

    /// <summary>The cited number.</summary>
    public int Number { get; }

    /// <summary>The citation's place in the reply.</summary>
    public int Key { get; }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner.OnTargetFocused(this, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner.OnTargetBlurred(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space)
        {
            e.Handled = true;
            Owner.InvokeCitation(Number, this);
        }
        else if (e.Key == Avalonia.Input.Key.Escape && Owner.IsCardOpen)
        {
            e.Handled = true;
            Owner.CloseCard(dismissed: true);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TargetPeer(this);

    private sealed class TargetPeer(AiCitationTarget owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => owner.Owner.InvokeCitation(owner.Number, owner);
    }
}

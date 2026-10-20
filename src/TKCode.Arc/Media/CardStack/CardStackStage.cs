using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// The stage of a <see cref="CardStack"/> (Arc <c>role="group" aria-roledescription="card stack"</c>, focusable). It
/// reserves the top card plus the edges peeking out below it, so nothing reflows as cards come and go. Keyboard
/// focus outlines the top card (or the empty state) instead of the stage.
/// </summary>
internal sealed class CardStackStage : Panel
{
    public CardStackStage()
    {
        Focusable = true;
        IsTabStop = true;
        FocusAdorner = null;
        ClipToBounds = false;
    }

    internal CardStack? Owner { get; set; }

    /// <summary>True while the stage has keyboard (focus-visible) focus.</summary>
    public bool IsFocusVisible { get; private set; }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        IsFocusVisible = IsFocused && Classes.Contains(":focus-visible");
        Owner?.OnStageFocusChanged();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        IsFocusVisible = false;
        Owner?.OnStageFocusChanged();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardStackStagePeer(this);

    private sealed class CardStackStagePeer : ControlAutomationPeer
    {
        public CardStackStagePeer(CardStackStage owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "card stack";

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The More actions button of a <see cref="SwipeActionsRow"/>. It anchors the row's menu, so it never press-scales and
/// answers in color only; it reports ExpandCollapse while the menu is open.
/// </summary>
[PseudoClasses(OpenPseudoClass)]
internal sealed class SwipeActionsMoreButton : Button
{
    public const string OpenPseudoClass = ":open";

    static SwipeActionsMoreButton()
    {
        IsPressScaleEnabledProperty.OverrideDefaultValue<SwipeActionsMoreButton>(false);
    }

    internal SwipeActionsRow? Row { get; set; }

    internal bool IsMenuOpen { get; private set; }

    internal void SetMenuOpen(bool open)
    {
        if (IsMenuOpen == open)
        {
            return;
        }
        IsMenuOpen = open;
        PseudoClasses.Set(OpenPseudoClass, open);
        if (ControlAutomationPeer.FromElement(this) is SwipeActionsMoreButtonAutomationPeer peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SwipeActionsMoreButtonAutomationPeer(this);

    private sealed class SwipeActionsMoreButtonAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public SwipeActionsMoreButtonAutomationPeer(SwipeActionsMoreButton owner) : base(owner)
        {
        }

        private SwipeActionsMoreButton More => (SwipeActionsMoreButton)Owner;

        public ExpandCollapseState ExpandCollapseState => More.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand() => More.Row?.OpenMenu(MenuOpenFocus.First);

        public void Collapse() => More.Row?.CloseMenu();
    }
}

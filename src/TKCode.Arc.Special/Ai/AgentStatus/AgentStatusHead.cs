using Avalonia.VisualTree;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pill's head: a button that holds the face, opens and closes the panel and reads as expanded or collapsed to assistive
/// technology. Its hover fill and its inset keyboard focus ring (2px, following the 19px radius) come from the theme.
/// </summary>
[PseudoClasses(ExpandedPseudoClass)]
internal sealed class AgentStatusHead : AvaloniaButton
{
    public const string ExpandedPseudoClass = ":expanded";

    private bool _expanded;

    /// <summary>True while the panel is open (no hover fill then; the peer reports it).</summary>
    public bool IsExpandedState
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }
            _expanded = value;
            PseudoClasses.Set(ExpandedPseudoClass, value);
            if ((TopLevel.GetTopLevel(this) is not null) && ControlAutomationPeer.CreatePeerForElement(this) is HeadPeer peer)
            {
                peer.RaiseState(value);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeadPeer(this);

    private sealed class HeadPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        private readonly AgentStatusHead _owner;

        public HeadPeer(AgentStatusHead owner) : base(owner) => _owner = owner;

        public ExpandCollapseState ExpandCollapseState => _owner._expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => _owner.RaiseExpandRequested(true);

        public void Collapse() => _owner.RaiseExpandRequested(false);

        public void RaiseState(bool expanded) => RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            expanded ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
            expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }

    /// <summary>Raised when assistive technology asks to expand or collapse the panel.</summary>
    public event Action<bool>? ExpandRequested;

    private void RaiseExpandRequested(bool expand) => ExpandRequested?.Invoke(expand);
}

/// <summary>
/// The surface of the pill and of the open panel: one bordered rounded box that grows from the pill into the panel and
/// carries the floating shadow while the panel is open (the theme's <c>:floating</c> state).
/// </summary>
[PseudoClasses(FloatingPseudoClass)]
internal sealed class AgentStatusSurface : Border
{
    public const string FloatingPseudoClass = ":floating";

    private bool _floating;

    public bool IsFloating
    {
        get => _floating;
        set
        {
            if (_floating != value)
            {
                _floating = value;
                PseudoClasses.Set(FloatingPseudoClass, value);
            }
        }
    }
}

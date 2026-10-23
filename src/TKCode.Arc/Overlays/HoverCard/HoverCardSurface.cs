using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The surface a <see cref="HoverCard"/> opens in: a <see cref="PopoverSurface"/> (same theme) that assistive technology
/// reads as a tooltip, since the card only describes its trigger and never takes focus.
/// </summary>
internal sealed class HoverCardSurface : PopoverSurface
{
    protected override Type StyleKeyOverride => typeof(PopoverSurface);

    protected override AutomationPeer OnCreateAutomationPeer() => new HoverCardSurfaceAutomationPeer(this);

    private sealed class HoverCardSurfaceAutomationPeer : ControlAutomationPeer
    {
        public HoverCardSurfaceAutomationPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolTip;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

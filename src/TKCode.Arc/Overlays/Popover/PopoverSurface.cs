using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The anchored floating surface of <see cref="Popover"/> (and <see cref="HoverCard"/>): raised surface, 1px border,
/// panel radius, 16px padding, Arc's floating shadow (drawn by the hosting popup). Min width 12rem, max 22rem.
/// Other controls can show their own content in it through <see cref="Popover.ShowAt"/>.
/// </summary>
public class PopoverSurface : ContentControl
{
    static PopoverSurface()
    {
        FocusableProperty.OverrideDefaultValue<PopoverSurface>(true);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PopoverSurfaceAutomationPeer(this);

    private sealed class PopoverSurfaceAutomationPeer : ControlAutomationPeer
    {
        public PopoverSurfaceAutomationPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

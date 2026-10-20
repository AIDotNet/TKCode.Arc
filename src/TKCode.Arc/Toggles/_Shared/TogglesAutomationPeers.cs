using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>A radio option of a toggle group (radiogroup + radio): RadioButton control type with the selection item pattern.</summary>
internal sealed class TogglesRadioAutomationPeer : ContentControlAutomationPeer, ISelectionItemProvider
{
    public TogglesRadioAutomationPeer(TogglesItem owner) : base(owner)
    {
    }

    private TogglesItem Item => (TogglesItem)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

    public bool IsSelected => Item.IsSelected;

    public ISelectionProvider? SelectionContainer =>
        ItemsControl.ItemsControlFromItemContainer(Item) is { } owner ? CreatePeerForElement(owner).GetProvider<ISelectionProvider>() : null;

    public void AddToSelection() => Item.Select();

    public void RemoveFromSelection()
    {
        // A radio group cannot be emptied by its options.
    }

    public void Select() => Item.Select();
}

/// <summary>A segment of a segmented control (group + aria-pressed buttons): Button control type with the toggle pattern.</summary>
internal sealed class TogglesPressedAutomationPeer : ContentControlAutomationPeer, IToggleProvider
{
    public TogglesPressedAutomationPeer(TogglesItem owner) : base(owner)
    {
    }

    private TogglesItem Item => (TogglesItem)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    public ToggleState ToggleState => Item.IsSelected ? ToggleState.On : ToggleState.Off;

    public void Toggle() => Item.Select();
}

/// <summary>The group itself (radiogroup / group): a Group control type that still exposes the selection pattern.</summary>
internal sealed class TogglesGroupAutomationPeer : SelectingItemsControlAutomationPeer
{
    public TogglesGroupAutomationPeer(Avalonia.Controls.Primitives.SelectingItemsControl owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
}

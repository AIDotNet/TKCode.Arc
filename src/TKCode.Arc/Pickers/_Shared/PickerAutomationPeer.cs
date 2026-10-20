using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>What <see cref="PickerAutomationPeer"/> reads from its picker.</summary>
internal interface IPickerAutomation
{
    /// <summary>The field label (Arc's aria-label / visible label).</summary>
    string? AutomationLabel { get; }

    /// <summary>The formatted value (Arc keeps it in a visually hidden span; the visible copies are animation layers).</summary>
    string? AutomationValue { get; }

    bool AutomationIsOpen { get; set; }
}

/// <summary>
/// Automation peer of the picker triggers: a button-like field with expand / collapse (Arc's aria-expanded and
/// aria-haspopup) and a read-only value.
/// </summary>
internal sealed class PickerAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, IValueProvider
{
    private readonly IPickerAutomation _picker;
    private readonly AutomationControlType _type;

    public PickerAutomationPeer(Control owner, IPickerAutomation picker, AutomationControlType type)
        : base(owner)
    {
        _picker = picker;
        _type = type;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => _type;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }
        var label = _picker.AutomationLabel;
        var value = _picker.AutomationValue;
        return string.IsNullOrEmpty(value) ? label : string.IsNullOrEmpty(label) ? value : $"{label}: {value}";
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public ExpandCollapseState ExpandCollapseState =>
        _picker.AutomationIsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand() => _picker.AutomationIsOpen = true;

    public void Collapse() => _picker.AutomationIsOpen = false;

    public bool IsReadOnly => true;

    public string? Value => _picker.AutomationValue;

    public void SetValue(string? value)
    {
    }

    internal void RaiseExpandChanged(bool open) => RaisePropertyChangedEvent(
        ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
        open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
        open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}

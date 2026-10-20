using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>What the shared select field peer needs from its control.</summary>
internal interface ISelectFieldAutomation
{
    string? AutomationLabel { get; }
    string? AutomationDescription { get; }
    string? AutomationValue { get; }
    bool AutomationIsOpen { get; set; }
}

/// <summary>
/// Automation peer of the select fields: a ComboBox with expand / collapse and a read-only value. The visible
/// value copies are animation layers; the real value is reported here (Arc keeps it in a visually hidden span).
/// </summary>
internal sealed class SelectFieldAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, IValueProvider
{
    private readonly ISelectFieldAutomation _field;
    private readonly AutomationControlType _type;

    public SelectFieldAutomationPeer(Control owner, ISelectFieldAutomation field, AutomationControlType type = AutomationControlType.ComboBox)
        : base(owner)
    {
        _field = field;
        _type = type;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => _type;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? _field.AutomationLabel : name;
    }

    protected override string? GetHelpTextCore()
    {
        var help = base.GetHelpTextCore();
        return string.IsNullOrEmpty(help) ? _field.AutomationDescription : help;
    }

    public ExpandCollapseState ExpandCollapseState =>
        _field.AutomationIsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => true;

    public void Expand() => _field.AutomationIsOpen = true;

    public void Collapse() => _field.AutomationIsOpen = false;

    public bool IsReadOnly => true;

    public string? Value => _field.AutomationValue;

    public void SetValue(string? value)
    {
    }

    internal void RaiseExpandChanged(bool open) => RaisePropertyChangedEvent(
        ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
        open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
        open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}

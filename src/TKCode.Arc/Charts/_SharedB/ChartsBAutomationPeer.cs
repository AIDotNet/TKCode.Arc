using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>What a team B chart part tells assistive technology.</summary>
internal interface IChartsBAccessible
{
    /// <summary>Role description, such as "waffle chart".</summary>
    string ChartRoleDescription { get; }

    /// <summary>Accessible name with the key instructions, such as "Energy mix. Use arrow keys ...".</summary>
    string ChartName { get; }

    /// <summary>The live reading when an item is in focus, otherwise the summary of the whole chart.</summary>
    string ChartHelp { get; }
}

/// <summary>
/// Peer for a self-drawn chart part: a named group whose drawing is hidden (Raw) and whose data is spoken through the
/// help text, which the chart refreshes (and announces politely) whenever the reading changes.
/// </summary>
internal sealed class ChartsBAutomationPeer : ControlAutomationPeer
{
    private readonly IChartsBAccessible _chart;
    private readonly AutomationControlType _type;
    private string? _lastHelp;

    public ChartsBAutomationPeer(Control owner, IChartsBAccessible chart, AutomationControlType type = AutomationControlType.Group)
        : base(owner)
    {
        _chart = chart;
        _type = type;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => _type;

    protected override string GetLocalizedControlTypeCore() => _chart.ChartRoleDescription;

    protected override string? GetNameCore()
    {
        var name = AutomationProperties.GetName(Owner);
        return string.IsNullOrEmpty(name) ? _chart.ChartName : name;
    }

    protected override string? GetHelpTextCore() => _chart.ChartHelp;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];

    /// <summary>Raises a help text change so screen readers speak the new reading (the owner sets LiveSetting Polite).</summary>
    public void Announce()
    {
        var help = _chart.ChartHelp;
        if (help == _lastHelp)
        {
            return;
        }
        var old = _lastHelp;
        _lastHelp = help;
        RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, old, help);
    }
}

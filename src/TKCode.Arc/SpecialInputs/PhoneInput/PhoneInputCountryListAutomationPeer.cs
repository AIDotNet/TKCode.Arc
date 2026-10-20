using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The "Countries" listbox of the phone picker for assistive technology. The rows are drawn, so groups ("Suggested",
/// "All countries") and options are lightweight peers over the list's layout; an option reports its selection and can
/// be picked through the selection pattern.
/// </summary>
internal sealed class PhoneInputCountryListAutomationPeer : ControlAutomationPeer, ISelectionProvider
{
    private readonly PhoneInputCountryList _list;
    private List<AutomationPeer>? _children;

    public PhoneInputCountryListAutomationPeer(PhoneInputCountryList owner)
        : base(owner)
    {
        _list = owner;
    }

    internal PhoneInputCountryList List => _list;

    /// <summary>The list content changed: options and groups are rebuilt on the next read.</summary>
    internal void Refresh()
    {
        _children = null;
        InvalidateChildren();
        RaiseChildrenChangedEvent();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? "Countries" : name;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
    {
        if (_children is not null)
        {
            return _children;
        }
        var children = new List<AutomationPeer>();
        PhoneInputCountryGroupAutomationPeer? group = null;
        string? groupLabel = null;
        for (var row = 0; row < _list.RowCount; row++)
        {
            var label = _list.SectionLabelOf(row);
            var option = new PhoneInputCountryOptionAutomationPeer(this, row);
            if (label is null)
            {
                group = null;
                groupLabel = null;
                children.Add(option);
                continue;
            }
            if (group is null || groupLabel != label)
            {
                group = new PhoneInputCountryGroupAutomationPeer(this, label);
                groupLabel = label;
                children.Add(group);
            }
            group.Options.Add(option);
        }
        _children = children;
        return children;
    }

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        var selected = new List<AutomationPeer>();
        foreach (var child in GetChildren())
        {
            if (child is PhoneInputCountryOptionAutomationPeer { IsSelected: true } option)
            {
                selected.Add(option);
            }
            else if (child is PhoneInputCountryGroupAutomationPeer group)
            {
                selected.AddRange(group.Options.Where(o => o.IsSelected));
            }
        }
        return selected;
    }

    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => false;

    /// <summary>The rectangle of a row relative to the visual root, as control peers report their bounds.</summary>
    internal Rect RowBounds(int row)
    {
        var root = Avalonia.Controls.TopLevel.GetTopLevel(_list);
        if (root is null || _list.TransformToVisual(root) is not { } matrix)
        {
            return default;
        }
        return _list.RowRect(row).TransformToAABB(matrix);
    }
}

/// <summary>Shared plumbing of the list's lightweight child peers.</summary>
internal abstract class PhoneInputListPartAutomationPeer : AutomationPeer
{
    private AutomationPeer? _parent;

    protected PhoneInputListPartAutomationPeer(PhoneInputCountryListAutomationPeer list) => ListPeer = list;

    protected PhoneInputCountryListAutomationPeer ListPeer { get; }

    protected override void BringIntoViewCore()
    {
    }

    protected override string? GetAcceleratorKeyCore() => null;

    protected override string? GetAccessKeyCore() => null;

    protected override string? GetAutomationIdCore() => null;

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override AutomationPeer? GetParentCore() => _parent ?? ListPeer;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() => ListPeer.IsEnabled();

    protected override bool IsKeyboardFocusableCore() => false;

    protected override void SetFocusCore()
    {
    }

    protected override bool ShowContextMenuCore() => false;

    protected override bool TrySetParent(AutomationPeer? parent)
    {
        _parent = parent;
        return true;
    }
}

/// <summary>A labeled group of the country list ("Suggested", "All countries").</summary>
internal sealed class PhoneInputCountryGroupAutomationPeer : PhoneInputListPartAutomationPeer
{
    private readonly string _label;

    public PhoneInputCountryGroupAutomationPeer(PhoneInputCountryListAutomationPeer list, string label)
        : base(list)
    {
        _label = label;
    }

    public List<PhoneInputCountryOptionAutomationPeer> Options { get; } = new();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => "PhoneInputCountryGroup";

    protected override string? GetNameCore() => _label;

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore()
    {
        foreach (var option in Options)
        {
            option.SetGroup(this);
        }
        return Options;
    }

    protected override Rect GetBoundingRectangleCore()
    {
        if (Options.Count == 0)
        {
            return default;
        }
        var first = ListPeer.RowBounds(Options[0].Row);
        var last = ListPeer.RowBounds(Options[^1].Row);
        return first.Union(last);
    }
}

/// <summary>One country option: name "United Kingdom +44", selected when it is the field's country.</summary>
internal sealed class PhoneInputCountryOptionAutomationPeer : PhoneInputListPartAutomationPeer, ISelectionItemProvider
{
    private AutomationPeer? _group;

    public PhoneInputCountryOptionAutomationPeer(PhoneInputCountryListAutomationPeer list, int row)
        : base(list)
    {
        Row = row;
    }

    public int Row { get; }

    private PhoneCountry? Country => ListPeer.List.CountryAt(Row);

    internal void SetGroup(AutomationPeer group) => _group = group;

    protected override AutomationPeer? GetParentCore() => _group ?? base.GetParentCore();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => "PhoneInputCountryOption";

    protected override string? GetNameCore() => Country is { } country ? $"{country.Name} +{country.Dial}" : null;

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => Array.Empty<AutomationPeer>();

    protected override Rect GetBoundingRectangleCore() => ListPeer.RowBounds(Row);

    public bool IsSelected => Country is { } country && ReferenceEquals(country, ListPeer.List.SelectedCountry);

    public ISelectionProvider? SelectionContainer => ListPeer;

    public void Select() => ListPeer.List.InvokeRow(Row);

    public void AddToSelection() => Select();

    public void RemoveFromSelection()
    {
    }
}

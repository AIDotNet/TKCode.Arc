using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The months as a table for screen readers (Arc's visually hidden <c>&lt;table&gt;</c>, private copy of the Activity
/// terrain pattern): a zero-size control whose automation peer is a table named by its caption, with one row per month
/// (named by the month) holding a header and one cell per column ("New: $72.5k", expansion, churn, net new and closing
/// revenue), for the months of the range, named by the caption. Nothing is drawn and nothing takes input.
/// </summary>
internal sealed class RevenueDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rowHeaders = [];
    private string[][] _cells = [];

    static RevenueDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<RevenueDataTable>(false);
        FocusableProperty.OverrideDefaultValue<RevenueDataTable>(false);
    }

    /// <summary>The table's caption.</summary>
    public string Caption => _caption;

    /// <summary>The row names (the months of the range).</summary>
    public IReadOnlyList<string> RowHeaders => _rowHeaders;

    /// <summary>The cell texts, row by row.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows => _cells;

    /// <summary>Replaces the table (the peer tree is rebuilt).</summary>
    public void Update(string caption, string[] rowHeaders, string[][] cells)
    {
        _caption = caption;
        _rowHeaders = rowHeaders;
        _cells = cells;
        if (ControlAutomationPeer.FromElement(this) is RevenueDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new RevenueDataTablePeer(this);
}

/// <summary>The table's peer: a <see cref="AutomationControlType.Table"/> with a row peer per month.</summary>
internal sealed class RevenueDataTablePeer : ControlAutomationPeer
{
    private List<AutomationPeer>? _rows;

    public RevenueDataTablePeer(RevenueDataTable owner) : base(owner)
    {
    }

    private new RevenueDataTable Owner => (RevenueDataTable)base.Owner;

    internal void Rebuild()
    {
        _rows = null;
        InvalidateChildren();
        RaisePropertyChangedEvent(Avalonia.Automation.AutomationElementIdentifiers.NameProperty, null, Owner.Caption);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override string? GetNameCore() => Owner.Caption;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        if (_rows is null)
        {
            _rows = new List<AutomationPeer>(Owner.RowHeaders.Count);
            for (var r = 0; r < Owner.RowHeaders.Count; r++)
            {
                var row = new RevenueTextPeer(this, Owner, Owner.RowHeaders[r], AutomationControlType.DataItem);
                var cells = r < Owner.Rows.Count ? Owner.Rows[r] : [];
                var children = new List<AutomationPeer>(cells.Count + 1)
                {
                    new RevenueTextPeer(row, Owner, Owner.RowHeaders[r], AutomationControlType.HeaderItem)
                };
                foreach (var cell in cells)
                {
                    children.Add(new RevenueTextPeer(row, Owner, cell, AutomationControlType.Text));
                }
                row.SetChildren(children);
                _rows.Add(row);
            }
        }
        return _rows;
    }
}

/// <summary>A peer with a fixed name and type that is not backed by a control (a table row or cell).</summary>
internal sealed class RevenueTextPeer : AutomationPeer
{
    private readonly AutomationPeer _parent;
    private readonly Control _host;
    private readonly string _name;
    private readonly AutomationControlType _type;
    private IReadOnlyList<AutomationPeer> _children = [];

    public RevenueTextPeer(AutomationPeer parent, Control host, string name, AutomationControlType type)
    {
        _parent = parent;
        _host = host;
        _name = name;
        _type = type;
    }

    internal void SetChildren(IReadOnlyList<AutomationPeer> children) => _children = children;

    protected override void BringIntoViewCore()
    {
    }

    protected override string? GetAcceleratorKeyCore() => null;

    protected override string? GetAccessKeyCore() => null;

    protected override AutomationControlType GetAutomationControlTypeCore() => _type;

    protected override string? GetAutomationIdCore() => null;

    protected override Rect GetBoundingRectangleCore() => default;

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => _children;

    protected override string GetClassNameCore() => _type == AutomationControlType.DataItem ? "row" : "cell";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string? GetNameCore() => _name;

    protected override AutomationPeer? GetParentCore() => _parent;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override void SetFocusCore()
    {
    }

    protected override bool ShowContextMenuCore() => false;

    protected override bool TrySetParent(AutomationPeer? parent) => false;
}

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The bridge as a table for screen readers (Arc's visually hidden <c>&lt;table&gt;</c>): a zero-size control whose automation
/// peer is a table named by its caption (the period), with a header row and one row per bar holding the movement, the
/// amount, its share of the starting revenue and the account count. Nothing is drawn and nothing takes input.
/// </summary>
internal sealed class MrrWaterfallDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rowHeaders = [];
    private string[][] _cells = [];

    static MrrWaterfallDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<MrrWaterfallDataTable>(false);
        FocusableProperty.OverrideDefaultValue<MrrWaterfallDataTable>(false);
    }

    /// <summary>The table's caption (the period).</summary>
    public string Caption => _caption;

    /// <summary>The row names (the column headers, then one per bar).</summary>
    public IReadOnlyList<string> RowHeaders => _rowHeaders;

    /// <summary>The cell texts, row by row.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows => _cells;

    /// <summary>Replaces the table (the peer tree is rebuilt).</summary>
    public void Update(string caption, string[] rowHeaders, string[][] cells)
    {
        _caption = caption;
        _rowHeaders = rowHeaders;
        _cells = cells;
        if (ControlAutomationPeer.FromElement(this) is MrrWaterfallDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new MrrWaterfallDataTablePeer(this);
}

/// <summary>The table's peer: a <see cref="AutomationControlType.Table"/> with a row peer per bar.</summary>
internal sealed class MrrWaterfallDataTablePeer : ControlAutomationPeer
{
    private List<AutomationPeer>? _rows;

    public MrrWaterfallDataTablePeer(MrrWaterfallDataTable owner) : base(owner)
    {
    }

    private new MrrWaterfallDataTable Owner => (MrrWaterfallDataTable)base.Owner;

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
                var row = new MrrWaterfallTextPeer(this, Owner, Owner.RowHeaders[r], AutomationControlType.DataItem);
                var cells = r < Owner.Rows.Count ? Owner.Rows[r] : [];
                var children = new List<AutomationPeer>(cells.Count + 1)
                {
                    new MrrWaterfallTextPeer(row, Owner, Owner.RowHeaders[r], AutomationControlType.HeaderItem)
                };
                foreach (var cell in cells)
                {
                    children.Add(new MrrWaterfallTextPeer(row, Owner, cell, AutomationControlType.Text));
                }
                row.SetChildren(children);
                _rows.Add(row);
            }
        }
        return _rows;
    }
}

/// <summary>A peer with a fixed name and type that is not backed by a control (a table row or cell).</summary>
internal sealed class MrrWaterfallTextPeer : AutomationPeer
{
    private readonly AutomationPeer _parent;
    private readonly Control _host;
    private readonly string _name;
    private readonly AutomationControlType _type;
    private IReadOnlyList<AutomationPeer> _children = [];

    public MrrWaterfallTextPeer(AutomationPeer parent, Control host, string name, AutomationControlType type)
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

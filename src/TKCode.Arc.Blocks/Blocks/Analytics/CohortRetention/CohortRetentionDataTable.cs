using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The cohorts as a table for screen readers (Arc's visually hidden <c>&lt;table&gt;</c>, private copy of the Activity
/// terrain pattern): a zero-size control whose automation peer is a table named by its caption, with one row per cohort
/// (named by the cohort) holding a header, the size and one cell per month ("Month 3: 56.0%", or "not measured yet"), and
/// the average row last. Nothing is drawn and nothing takes input.
/// </summary>
internal sealed class CohortRetentionDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rowHeaders = [];
    private string[][] _cells = [];

    static CohortRetentionDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<CohortRetentionDataTable>(false);
        FocusableProperty.OverrideDefaultValue<CohortRetentionDataTable>(false);
    }

    /// <summary>The table's caption.</summary>
    public string Caption => _caption;

    /// <summary>The row names (cohorts, then the average).</summary>
    public IReadOnlyList<string> RowHeaders => _rowHeaders;

    /// <summary>The cell texts, row by row.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows => _cells;

    /// <summary>Replaces the table (the peer tree is rebuilt).</summary>
    public void Update(string caption, string[] rowHeaders, string[][] cells)
    {
        _caption = caption;
        _rowHeaders = rowHeaders;
        _cells = cells;
        if (ControlAutomationPeer.FromElement(this) is CohortRetentionDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new CohortRetentionDataTablePeer(this);
}

/// <summary>The table's peer: a <see cref="AutomationControlType.Table"/> with a row peer per cohort.</summary>
internal sealed class CohortRetentionDataTablePeer : ControlAutomationPeer
{
    private List<AutomationPeer>? _rows;

    public CohortRetentionDataTablePeer(CohortRetentionDataTable owner) : base(owner)
    {
    }

    private new CohortRetentionDataTable Owner => (CohortRetentionDataTable)base.Owner;

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
                var row = new CohortRetentionTextPeer(this, Owner, Owner.RowHeaders[r], AutomationControlType.DataItem);
                var cells = r < Owner.Rows.Count ? Owner.Rows[r] : [];
                var children = new List<AutomationPeer>(cells.Count + 1)
                {
                    new CohortRetentionTextPeer(row, Owner, Owner.RowHeaders[r], AutomationControlType.HeaderItem)
                };
                foreach (var cell in cells)
                {
                    children.Add(new CohortRetentionTextPeer(row, Owner, cell, AutomationControlType.Text));
                }
                row.SetChildren(children);
                _rows.Add(row);
            }
        }
        return _rows;
    }
}

/// <summary>A peer with a fixed name and type that is not backed by a control (a table row or cell).</summary>
internal sealed class CohortRetentionTextPeer : AutomationPeer
{
    private readonly AutomationPeer _parent;
    private readonly Control _host;
    private readonly string _name;
    private readonly AutomationControlType _type;
    private IReadOnlyList<AutomationPeer> _children = [];

    public CohortRetentionTextPeer(AutomationPeer parent, Control host, string name, AutomationControlType type)
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

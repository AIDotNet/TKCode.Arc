using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// Two children on one line, the first at the start and the second at the end (CSS <c>space-between</c> with
/// <c>flex-wrap</c>): when they do not fit side by side with <see cref="ColumnGap"/> they stack at the start with
/// <see cref="RowGap"/>. The first child may shrink to <see cref="LeadBasis"/> before the line breaks (the titles wrap
/// first); <see cref="IsFilled"/> makes the panel take the whole available width (the compact block's controls).
/// </summary>
internal sealed class HorizonSplitPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<HorizonSplitPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<HorizonSplitPanel, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<double> LeadBasisProperty =
        AvaloniaProperty.Register<HorizonSplitPanel, double>(nameof(LeadBasis), double.NaN);

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<HorizonSplitPanel, bool>(nameof(IsFilled));

    static HorizonSplitPanel()
    {
        AffectsMeasure<HorizonSplitPanel>(ColumnGapProperty, RowGapProperty, LeadBasisProperty, IsFilledProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>The first child's width for the break decision (NaN: its natural width).</summary>
    public double LeadBasis { get => GetValue(LeadBasisProperty); set => SetValue(LeadBasisProperty, value); }

    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    /// <summary>True when the last layout stacked the children.</summary>
    public bool IsWrapped { get; private set; }

    private (Control? Lead, Control? Trail) Parts()
    {
        Control? lead = null;
        Control? trail = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (lead is null)
            {
                lead = child;
            }
            else if (trail is null)
            {
                trail = child;
            }
        }
        return (lead, trail);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (lead, trail) = Parts();
        var width = availableSize.Width;
        var finite = double.IsFinite(width);
        if (lead is null)
        {
            IsWrapped = false;
            return default;
        }
        Size result;
        if (trail is null)
        {
            IsWrapped = false;
            lead.Measure(availableSize);
            result = lead.DesiredSize;
        }
        else
        {
            trail.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            lead.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            var natural = lead.DesiredSize.Width;
            var basis = double.IsNaN(LeadBasis) ? natural : Math.Min(natural, LeadBasis);
            IsWrapped = finite && basis + ColumnGap + trail.DesiredSize.Width > width;
            if (IsWrapped)
            {
                lead.Measure(new Size(width, double.PositiveInfinity));
                trail.Measure(new Size(width, double.PositiveInfinity));
                result = new Size(Math.Max(lead.DesiredSize.Width, trail.DesiredSize.Width),
                    lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height);
            }
            else
            {
                if (finite)
                {
                    lead.Measure(new Size(Math.Max(0, width - ColumnGap - trail.DesiredSize.Width), availableSize.Height));
                }
                result = new Size(lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width,
                    Math.Max(lead.DesiredSize.Height, trail.DesiredSize.Height));
            }
        }
        return IsFilled && finite ? new Size(width, result.Height) : result;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (lead, trail) = Parts();
        if (lead is null)
        {
            return finalSize;
        }
        if (trail is null)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), finalSize.Height));
            return finalSize;
        }
        if (IsWrapped)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), lead.DesiredSize.Height));
            trail.Arrange(new Rect(0, lead.DesiredSize.Height + RowGap, Math.Min(finalSize.Width, trail.DesiredSize.Width),
                trail.DesiredSize.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        lead.Arrange(new Rect(0, Offset(lead, height), lead.DesiredSize.Width, lead.DesiredSize.Height));
        var trailWidth = trail.DesiredSize.Width;
        trail.Arrange(new Rect(finalSize.Width - trailWidth, Offset(trail, height), trailWidth, trail.DesiredSize.Height));
        return finalSize;
    }

    private static double Offset(Control child, double height) => child.VerticalAlignment switch
    {
        VerticalAlignment.Center => (height - child.DesiredSize.Height) / 2,
        VerticalAlignment.Bottom => height - child.DesiredSize.Height,
        _ => 0
    };
}

/// <summary>
/// The rows as a table for screen readers (Arc's visually hidden <c>&lt;table&gt;</c>): a zero-size control whose
/// automation peer is a table named by its caption, with one row per series (named by the series) holding a header and a
/// cell per column ("Latest +2% on Sep 27"). Nothing is drawn and nothing takes input.
/// </summary>
internal sealed class HorizonDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rowHeaders = [];
    private string[][] _cells = [];

    static HorizonDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<HorizonDataTable>(false);
        FocusableProperty.OverrideDefaultValue<HorizonDataTable>(false);
    }

    /// <summary>The table's caption.</summary>
    public string Caption => _caption;

    /// <summary>The row names, in the current sort order.</summary>
    public IReadOnlyList<string> RowHeaders => _rowHeaders;

    /// <summary>The cell texts, row by row.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows => _cells;

    /// <summary>Replaces the table (the peer tree is rebuilt).</summary>
    public void Update(string caption, string[] rowHeaders, string[][] cells)
    {
        _caption = caption;
        _rowHeaders = rowHeaders;
        _cells = cells;
        if (ControlAutomationPeer.FromElement(this) is HorizonDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new HorizonDataTablePeer(this);
}

/// <summary>The table's peer: a <see cref="AutomationControlType.Table"/> with a row peer per series.</summary>
internal sealed class HorizonDataTablePeer : ControlAutomationPeer
{
    private List<AutomationPeer>? _rows;

    public HorizonDataTablePeer(HorizonDataTable owner) : base(owner)
    {
    }

    private new HorizonDataTable Owner => (HorizonDataTable)base.Owner;

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
                var row = new HorizonTextPeer(this, Owner, Owner.RowHeaders[r], AutomationControlType.DataItem);
                var cells = r < Owner.Rows.Count ? Owner.Rows[r] : [];
                var children = new List<AutomationPeer>(cells.Count + 1)
                {
                    new HorizonTextPeer(row, Owner, Owner.RowHeaders[r], AutomationControlType.HeaderItem)
                };
                foreach (var cell in cells)
                {
                    children.Add(new HorizonTextPeer(row, Owner, cell, AutomationControlType.Text));
                }
                row.SetChildren(children);
                _rows.Add(row);
            }
        }
        return _rows;
    }
}

/// <summary>A peer with a fixed name and type that is not backed by a control (a table row or cell).</summary>
internal sealed class HorizonTextPeer : AutomationPeer
{
    private readonly AutomationPeer _parent;
    private readonly Control _host;
    private readonly string _name;
    private readonly AutomationControlType _type;
    private IReadOnlyList<AutomationPeer> _children = [];

    public HorizonTextPeer(AutomationPeer parent, Control host, string name, AutomationControlType type)
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

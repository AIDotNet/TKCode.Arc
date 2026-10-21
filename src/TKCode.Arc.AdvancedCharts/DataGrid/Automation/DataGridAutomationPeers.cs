using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The grid as a whole: a data grid named by <see cref="DataGrid.Label"/>.</summary>
internal sealed class DataGridAutomationPeer(DataGrid owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string? GetNameCore() => ((DataGrid)Owner).AccessibleName;
}

/// <summary>
/// The focusable body of the grid. Avalonia has no grid or table automation patterns, so the body reports the active cell
/// instead: its name is "column, row: value" and its help text the cell address, both updated as the active cell moves,
/// and the value pattern reads and writes the active cell (writes go through the same commit as typing).
/// </summary>
internal sealed class DataGridBodyAutomationPeer : ControlAutomationPeer, IValueProvider
{
    private readonly DataGrid _grid;
    private string? _lastName;

    public DataGridBodyAutomationPeer(DataGrid grid, DataGridViewport viewport) : base(viewport)
    {
        _grid = grid;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;

    protected override string? GetNameCore() => CellName() ?? _grid.AccessibleName;

    protected override string? GetHelpTextCore()
    {
        if (_grid.IsShowingSkeleton)
        {
            return DataGridText.Get(DataGridLangResourceKind.Loading);
        }
        return _grid.HasCells ? DataGridText.Format(DataGridLangResourceKind.CellAddress, _grid.SelectionAddress()) : null;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    private string? CellName()
    {
        if (!_grid.HasCells)
        {
            return null;
        }
        var anchor = _grid.Anchor;
        var accessor = _grid.VisibleAccessors[anchor.Column];
        var column = _grid.ColumnOf(accessor);
        var value = _grid.CellText(anchor.Row, accessor);
        if (string.IsNullOrEmpty(value))
        {
            value = DataGridText.Get(DataGridLangResourceKind.EmptyWord);
        }
        return DataGridText.Format(DataGridLangResourceKind.CellAnnouncement, column.Header, _grid.RowName(anchor.Row), value);
    }

    /// <summary>Tells automation clients that the active cell moved or changed.</summary>
    public void NotifyActiveCellChanged()
    {
        var name = GetName();
        if (name != _lastName)
        {
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, _lastName, name);
            _lastName = name;
        }
    }

    public bool IsReadOnly
    {
        get
        {
            if (!_grid.HasCells || _grid.HasDataSource)
            {
                return true;
            }
            var column = _grid.ColumnOf(_grid.VisibleAccessors[_grid.Anchor.Column]);
            return !column.IsEditable;
        }
    }

    public string? Value => _grid.HasCells ? _grid.CellText(_grid.Anchor.Row, _grid.VisibleAccessors[_grid.Anchor.Column]) : null;

    public void SetValue(string? value)
    {
        if (IsReadOnly)
        {
            return;
        }
        _grid.BeginEdit(_grid.Anchor, value ?? string.Empty, openList: false);
        _grid.CommitEdit((0, 0), null);
    }
}

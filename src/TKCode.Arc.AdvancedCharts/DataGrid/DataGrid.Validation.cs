using Avalonia;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    /// <summary>What happens to a value that fails its column's rules: refused (default) or accepted and flagged.</summary>
    public static readonly StyledProperty<DataGridValidationMode> ValidationModeProperty =
        AvaloniaProperty.Register<DataGrid, DataGridValidationMode>(nameof(ValidationMode));

    /// <summary>
    /// Decides per cell whether it can be edited, on top of <see cref="DataGridColumn.IsEditable"/> (for example locking the rows
    /// of closed deals). Edits, pastes, fills and clears skip cells it refuses. Keep it cheap: it runs for every cell a write touches.
    /// </summary>
    public static readonly StyledProperty<Func<DataGridCellContext, bool>?> CanEditCellProperty =
        AvaloniaProperty.Register<DataGrid, Func<DataGridCellContext, bool>?>(nameof(CanEditCell));

    /// <summary>
    /// Picks a tone for a cell from its row and value, for looks the column's <see cref="DataGridColumn.FormatRules"/> cannot express.
    /// It runs for every cell in view on every frame, so keep it cheap; null from it falls through to the format rules.
    /// </summary>
    public static readonly StyledProperty<Func<DataGridCellContext, DataGridCellStyle?>?> CellStyleSelectorProperty =
        AvaloniaProperty.Register<DataGrid, Func<DataGridCellContext, DataGridCellStyle?>?>(nameof(CellStyleSelector));

    /// <summary>Tints a whole row with a tone chosen from the row object (null for no tint). Runs for every row in view on every frame.</summary>
    public static readonly StyledProperty<Func<object, DataGridTone?>?> RowStyleSelectorProperty =
        AvaloniaProperty.Register<DataGrid, Func<object, DataGridTone?>?>(nameof(RowStyleSelector));

    /// <summary>How many cells are flagged for failing their rules (see <see cref="DataGridValidationMode.Flag"/> and <see cref="ValidateAll"/>).</summary>
    public static readonly DirectProperty<DataGrid, int> ErrorCountProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, int>(nameof(ErrorCount), o => o.ErrorCount);

    private readonly Dictionary<(object Key, int Accessor), string> _errors = new();
    private int _errorCount;
    private int _refusedWrites;

    /// <inheritdoc cref="ValidationModeProperty"/>
    public DataGridValidationMode ValidationMode { get => GetValue(ValidationModeProperty); set => SetValue(ValidationModeProperty, value); }

    /// <inheritdoc cref="CanEditCellProperty"/>
    public Func<DataGridCellContext, bool>? CanEditCell { get => GetValue(CanEditCellProperty); set => SetValue(CanEditCellProperty, value); }

    /// <inheritdoc cref="CellStyleSelectorProperty"/>
    public Func<DataGridCellContext, DataGridCellStyle?>? CellStyleSelector { get => GetValue(CellStyleSelectorProperty); set => SetValue(CellStyleSelectorProperty, value); }

    /// <inheritdoc cref="RowStyleSelectorProperty"/>
    public Func<object, DataGridTone?>? RowStyleSelector { get => GetValue(RowStyleSelectorProperty); set => SetValue(RowStyleSelectorProperty, value); }

    /// <inheritdoc cref="ErrorCountProperty"/>
    public int ErrorCount { get => _errorCount; private set => SetAndRaise(ErrorCountProperty, ref _errorCount, value); }

    /// <summary>True while at least one cell is flagged.</summary>
    public bool HasErrors => _errorCount > 0;

    /// <summary>The message flagged on a cell, or null.</summary>
    internal string? ErrorOf(object? key, int accessor) =>
        key is not null && _errors.Count > 0 && _errors.TryGetValue((key, accessor), out var message) ? message : null;

    /// <summary>Runs a column's rules over a value; the first message, or null when the value passes.</summary>
    internal string? Validate(object item, object key, int accessor, object? value)
    {
        var column = ColumnOf(accessor);
        if (column.Rules.Count == 0)
        {
            return null;
        }
        var context = new DataGridCellContext(item, key, column, value);
        foreach (var rule in column.Rules)
        {
            if (rule.Validate(context) is { } message)
            {
                return message;
            }
        }
        return null;
    }

    /// <summary>Re-checks one cell after it was written and updates its flag.</summary>
    private void Revalidate(int source, int accessor)
    {
        if (ColumnOf(accessor).Rules.Count == 0)
        {
            return;
        }
        var key = _store.KeyAt(source);
        var message = Validate(_store.ItemAt(source), key, accessor, _store.Value(source, accessor));
        if (message is null)
        {
            _errors.Remove((key, accessor));
        }
        else
        {
            _errors[(key, accessor)] = message;
        }
    }

    private void PublishErrorCount()
    {
        if (ErrorCount != _errors.Count)
        {
            ErrorCount = _errors.Count;
            _toolbar?.UpdateState();
            Viewport.InvalidateLayers();
        }
    }

    /// <summary>Forgets flags of rows that are gone (after the rows changed).</summary>
    private void PruneErrors()
    {
        if (_errors.Count == 0)
        {
            return;
        }
        foreach (var key in _errors.Keys.Where(k => _store.IndexOfKey(k.Key) < 0).ToArray())
        {
            _errors.Remove(key);
        }
        PublishErrorCount();
    }

    private void ClearErrors()
    {
        if (_errors.Count == 0)
        {
            return;
        }
        _errors.Clear();
        PublishErrorCount();
    }

    /// <summary>
    /// Runs every column's rules over every row and flags the cells that fail (existing flags are replaced). Returns the number
    /// flagged. Does nothing with a <see cref="DataSource"/>, whose rows live on the server.
    /// </summary>
    public int ValidateAll()
    {
        _errors.Clear();
        if (!HasDataSource)
        {
            for (var a = 0; a < _store.Accessors.Length; a++)
            {
                if (ColumnOf(a).Rules.Count == 0)
                {
                    continue;
                }
                for (var r = 0; r < _store.Count; r++)
                {
                    Revalidate(r, a);
                }
            }
        }
        PublishErrorCount();
        Viewport.InvalidateLayers();
        return _errors.Count;
    }

    /// <summary>The flagged cells, in row order then column order.</summary>
    public IReadOnlyList<DataGridCellError> GetErrors()
    {
        var result = new List<DataGridCellError>();
        foreach (var ((key, accessor), message) in _errors.OrderBy(e => _store.IndexOfKey(e.Key.Key)).ThenBy(e => e.Key.Accessor))
        {
            var source = _store.IndexOfKey(key);
            if (source >= 0)
            {
                result.Add(new DataGridCellError(_store.ItemAt(source), key, ColumnOf(accessor), message));
            }
        }
        return result;
    }

    /// <summary>Selects the next flagged cell after the active one (wrapping round) and announces its message; false when nothing is flagged in view.</summary>
    public bool NextError()
    {
        EnsureView();
        if (_errors.Count == 0 || !HasCells)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.NoErrors));
            return false;
        }
        var visible = VisibleAccessors;
        var rows = DisplayCount;
        var total = rows * visible.Length;
        var start = Anchor.Row * visible.Length + Anchor.Column;
        for (var step = 1; step <= total; step++)
        {
            var index = (start + step) % total;
            var row = index / visible.Length;
            var column = index % visible.Length;
            if (ErrorOf(KeyOfRow(row), visible[column]) is not null)
            {
                Select(new DataGridCellAddress(row, column));
                return true;
            }
        }
        Say(DataGridText.Get(DataGridLangResourceKind.NoErrors));
        return false;
    }

    /// <summary>Whether <see cref="CanEditCell"/> lets a cell be edited (true without a predicate).</summary>
    private bool CellAllowed(int source, int accessor) =>
        CanEditCell is not { } predicate ||
        predicate(new DataGridCellContext(_store.ItemAt(source), _store.KeyAt(source), ColumnOf(accessor), _store.Value(source, accessor)));

    /// <summary>True when some column has format rules or a selector is set, so the cells layer looks for styles at all.</summary>
    internal bool HasStyling
    {
        get
        {
            if (CellStyleSelector is not null || RowStyleSelector is not null)
            {
                return true;
            }
            for (var i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].FormatRules.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The look the selector or the column's format rules give a cell (null for the plain look).</summary>
    internal DataGridCellStyle? StyleOf(object item, object key, int accessor, object? value)
    {
        var column = ColumnOf(accessor);
        if (CellStyleSelector is { } selector && selector(new DataGridCellContext(item, key, column, value)) is { } chosen)
        {
            return chosen;
        }
        for (var i = 0; i < column.FormatRules.Count; i++)
        {
            if (column.FormatRules[i].Matches(value, column, EffectiveCulture))
            {
                return column.FormatRules[i].Style;
            }
        }
        return null;
    }
}

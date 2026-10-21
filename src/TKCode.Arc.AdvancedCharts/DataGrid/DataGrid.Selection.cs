using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    private DataGridCellAddress _anchor;
    private DataGridCellAddress _focus;
    private readonly List<string> _keyboardFolded = new();

    /// <summary>The anchor of the selection (clamped to the grid).</summary>
    internal DataGridCellAddress Anchor => Clamp(_anchor);

    /// <summary>The moving corner of the selection (clamped to the grid).</summary>
    internal DataGridCellAddress Corner => Clamp(_focus);

    /// <summary>The selected range (clamped to the grid).</summary>
    internal DataGridCellRange Range => DataGridCellRange.FromCorners(Anchor, Corner);

    internal bool HasCells => DisplayCount > 0 && VisibleAccessors.Length > 0;

    private DataGridCellAddress Clamp(DataGridCellAddress address) => new(
        Math.Clamp(address.Row, 0, Math.Max(0, DisplayCount - 1)),
        Math.Clamp(address.Column, 0, Math.Max(0, VisibleAccessors.Length - 1)));

    private void ClampSelection()
    {
        PublishSelection();
    }

    /// <summary>Selects from <paramref name="anchor"/> to <paramref name="corner"/> and scrolls <paramref name="reveal"/> into view.</summary>
    internal void Select(DataGridCellAddress anchor, DataGridCellAddress? corner = null, DataGridCellAddress? reveal = null, bool scroll = true)
    {
        _anchor = Clamp(anchor);
        _focus = Clamp(corner ?? anchor);
        if (scroll)
        {
            Viewport.ScrollToCell(reveal ?? _focus);
        }
        PublishSelection();
    }

    private void PublishSelection()
    {
        if (!HasCells)
        {
            ActiveCell = null;
            SelectedRange = null;
        }
        else
        {
            ActiveCell = Anchor;
            SelectedRange = Range;
        }
        Viewport.OnSelectionChanged();
        UpdateStats();
    }

    /// <summary>Selects a range of cells (display rows, visible columns) and scrolls its corner into view.</summary>
    public void SelectRange(DataGridCellRange range)
    {
        EnsureView();
        Select(new DataGridCellAddress(range.Row0, range.Column0), new DataGridCellAddress(range.Row1, range.Column1));
    }

    /// <summary>Scrolls the row with <paramref name="rowKey"/> (and the column with <paramref name="columnKey"/>) into view.</summary>
    public void ScrollIntoView(object rowKey, string? columnKey = null)
    {
        EnsureView();
        var row = -1;
        if (!HasDataSource)
        {
            var source = _store.IndexOfKey(rowKey);
            if (source >= 0 && source < View.DisplayOfSource.Length)
            {
                row = View.DisplayOfSource[source];
            }
        }
        if (row < 0)
        {
            return;
        }
        var column = columnKey is null ? Corner.Column : Math.Max(0, VisibleIndexOf(columnKey));
        Viewport.ScrollToCell(new DataGridCellAddress(row, column));
    }

    /// <summary>Rows moved by PageUp / PageDown: one visible page minus one.</summary>
    private int PageRows => Math.Max(1, (int)Math.Floor(Viewport.BodyHeight / Math.Max(1, RowHeight)) - 1);

    /// <summary>The grid's keyboard model (the grid body has focus; the editor and inputs handle their own keys).</summary>
    internal void HandleGridKey(KeyEventArgs e)
    {
        var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        var command = (e.KeyModifiers & (hotkeys?.CommandModifiers ?? KeyModifiers.Control)) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        if (Matches(hotkeys?.Undo, e) || command && !shift && e.Key == Key.Z)
        {
            e.Handled = true;
            Undo();
            return;
        }
        if (Matches(hotkeys?.Redo, e) || command && (shift && e.Key == Key.Z || e.Key == Key.Y))
        {
            e.Handled = true;
            Redo();
            return;
        }
        if (Matches(hotkeys?.Copy, e) || command && e.Key == Key.C)
        {
            e.Handled = true;
            _ = CopySelectionAsync();
            return;
        }
        if (Matches(hotkeys?.Cut, e) || command && e.Key == Key.X)
        {
            e.Handled = true;
            _ = CutSelectionAsync();
            return;
        }
        if (Matches(hotkeys?.Paste, e) || command && e.Key == Key.V)
        {
            e.Handled = true;
            _ = PasteAsync();
            return;
        }
        if (!HasCells)
        {
            return;
        }
        var rows = DisplayCount;
        var columns = VisibleAccessors.Length;
        if (command && e.Key == Key.A)
        {
            e.Handled = true;
            Select(new DataGridCellAddress(0, 0), new DataGridCellAddress(rows - 1, columns - 1), Corner, scroll: false);
            Announce(DataGridText.Count(DataGridLangResourceKind.SelectedAllCellsOne, DataGridLangResourceKind.SelectedAllCellsOther,
                rows * columns, EffectiveCulture));
            return;
        }
        if (command && e.Key == Key.D)
        {
            e.Handled = true;
            FillDown();
            return;
        }
        if (e.Key == Key.Space && shift && !command && IsRowCheckEnabled)
        {
            e.Handled = true;
            CheckRangeRows();
            return;
        }
        if (e.Key == Key.Space && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            e.Handled = true;
            var range = Range;
            Select(new DataGridCellAddress(0, range.Column0), new DataGridCellAddress(rows - 1, range.Column1), scroll: false);
            return;
        }
        if (alt && e.Key is Key.Left or Key.Right && View.IsGrouped)
        {
            e.Handled = true;
            ToggleGroupFromKeyboard(expand: e.Key == Key.Right);
            return;
        }
        if (alt && e.Key == Key.Down && VisibleColumn(Anchor.Column).Type is DataGridColumnType.Select or DataGridColumnType.Date)
        {
            e.Handled = true;
            BeginEdit(Anchor, null, openList: true);
            return;
        }
        int? dr = e.Key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            Key.PageUp => -PageRows,
            Key.PageDown => PageRows,
            Key.Left or Key.Right or Key.Home or Key.End => 0,
            _ => null
        };
        if (dr is { } deltaRow)
        {
            e.Handled = true;
            var deltaColumn = e.Key switch
            {
                Key.Left => -1,
                Key.Right => 1,
                Key.Home => -columns,
                Key.End => columns,
                _ => 0
            };
            var from = shift ? Corner : Anchor;
            var jump = command && e.Key is Key.Up or Key.Down or Key.Left or Key.Right;
            var target = new DataGridCellAddress(
                jump ? deltaRow < 0 ? 0 : deltaRow > 0 ? rows - 1 : from.Row : from.Row + deltaRow,
                jump ? deltaColumn < 0 ? 0 : deltaColumn > 0 ? columns - 1 : from.Column : from.Column + deltaColumn);
            if (command && e.Key is Key.Home or Key.End)
            {
                target = target with { Row = e.Key == Key.Home ? 0 : rows - 1 };
            }
            target = Clamp(target);
            if (shift)
            {
                Select(Anchor, target, target);
            }
            else
            {
                Select(target);
            }
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
            case Key.F2:
                e.Handled = true;
                BeginEdit(Anchor, null, openList: false);
                return;
            case Key.Escape:
                if (!Range.IsSingleCell)
                {
                    e.Handled = true;
                    Select(Anchor, scroll: false);
                }
                else if (_checked.Count > 0)
                {
                    e.Handled = true;
                    ClearChecked();
                }
                return;
            case Key.Delete:
            case Key.Back:
                e.Handled = true;
                ClearRange(Range);
                return;
        }
    }

    private static bool Matches(List<KeyGesture>? gestures, KeyEventArgs e)
    {
        if (gestures is null)
        {
            return false;
        }
        foreach (var gesture in gestures)
        {
            if (gesture.Matches(e))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Alt+Left folds the innermost group of the active row; Alt+Right unfolds the folded group just above it. The new
    /// state is announced ("Region Europe, 12 rows. Expand").
    /// </summary>
    private void ToggleGroupFromKeyboard(bool expand)
    {
        var line = LineOfRow(Anchor.Row);
        DataGridGroupNode? target = null;
        if (expand && _keyboardFolded.Count > 0)
        {
            var last = _keyboardFolded[^1];
            _keyboardFolded.RemoveAt(_keyboardFolded.Count - 1);
            target = View.AllGroups().FirstOrDefault(g => g.Id == last && IsCollapsed(g));
        }
        if (target is null && expand)
        {
            for (var l = line - 1; l >= 0 && GroupAtLine(l) is { } group; l--)
            {
                if (IsCollapsed(group))
                {
                    target = group;
                    break;
                }
            }
            if (target is null && line + 1 < LineCount && GroupAtLine(line + 1) is { } next && IsCollapsed(next))
            {
                target = next;
            }
        }
        else if (!expand)
        {
            for (var l = line - 1; l >= 0; l--)
            {
                if (GroupAtLine(l) is { } group)
                {
                    target = group;
                    break;
                }
            }
        }
        if (target is null || IsCollapsed(target) != expand)
        {
            return;
        }
        ToggleGroup(target);
        if (!expand)
        {
            // Keep the active row next to the folded group, and remember it so Alt+Right unfolds it again.
            _keyboardFolded.Add(target.Id);
            var header = Array.FindIndex(View.Lines, l => ReferenceEquals(l.Group, target));
            if (header >= 0)
            {
                Select(new DataGridCellAddress(RowOfLine(header), Anchor.Column));
            }
        }
        var column = ColumnOf(target.Accessor);
        var value = FormatValue(target.Value, target.Accessor);
        var label = string.IsNullOrEmpty(column.Header) ? DataGridText.Get(DataGridLangResourceKind.GroupFallback) : column.Header;
        Announce(DataGridText.Format(target.Rows.Length == 1 ? DataGridLangResourceKind.GroupToggleOne : DataGridLangResourceKind.GroupToggleOther,
            label, string.IsNullOrEmpty(value) ? DataGridText.Get(DataGridLangResourceKind.EmptyValue) : value,
            target.Rows.Length.ToString("N0", EffectiveCulture),
            DataGridText.Get(IsCollapsed(target) ? DataGridLangResourceKind.Expand : DataGridLangResourceKind.Collapse)));
    }

    /// <summary>Text input on the grid body (any character) starts editing and replaces the value.</summary>
    internal void HandleGridText(TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || !HasCells || IsEditing)
        {
            return;
        }
        if (e.Text.Any(char.IsControl))
        {
            return;
        }
        e.Handled = true;
        BeginEdit(Anchor, e.Text, openList: false);
    }

    /// <summary>Shift + Space: checks the rows of the range, or unchecks them when all are checked.</summary>
    private void CheckRangeRows()
    {
        var range = Range;
        var keys = new List<object>();
        for (var r = range.Row0; r <= range.Row1; r++)
        {
            if (KeyOfRow(r) is { } key)
            {
                keys.Add(key);
            }
        }
        var check = !keys.All(_checked.Contains);
        var next = new HashSet<object>(_checked);
        foreach (var key in keys)
        {
            if (check)
            {
                next.Add(key);
            }
            else
            {
                next.Remove(key);
            }
        }
        SetChecked(next);
        Announce(DataGridText.Count(check ? DataGridLangResourceKind.CheckedRowsOne : DataGridLangResourceKind.UncheckedRowsOne,
            check ? DataGridLangResourceKind.CheckedRowsOther : DataGridLangResourceKind.UncheckedRowsOther, keys.Count, EffectiveCulture));
    }

    /// <summary>Count, sum and average of the selected cells for the status bar.</summary>
    internal (int Filled, int Numbers, decimal Sum, string Prefix, string Suffix) SelectionStats()
    {
        if (!HasCells)
        {
            return (0, 0, 0, string.Empty, string.Empty);
        }
        var range = Range;
        var accumulator = new DataGridAggregates.Accumulator();
        DataGridColumnType? kind = null;
        var mixed = false;
        var symbol = "$";
        var visible = VisibleAccessors;
        // Very large ranges (Ctrl+A on huge data) are summed row by row; values come from the cache.
        for (var r = range.Row0; r <= range.Row1; r++)
        {
            for (var c = range.Column0; c <= range.Column1; c++)
            {
                var value = ValueAt(r, visible[c]);
                accumulator.Add(value);
                if (value is decimal)
                {
                    var column = ColumnOf(visible[c]);
                    var type = column.IsNumeric ? column.Type : DataGridColumnType.Number;
                    if (kind is null)
                    {
                        kind = type;
                        symbol = column.CurrencySymbol;
                    }
                    else if (kind != type)
                    {
                        mixed = true;
                    }
                }
            }
        }
        var prefix = !mixed && kind == DataGridColumnType.Currency ? symbol : string.Empty;
        var suffix = !mixed && kind == DataGridColumnType.Percent ? EffectiveCulture.NumberFormat.PercentSymbol : string.Empty;
        return (accumulator.Filled, accumulator.Numbers, accumulator.Sum, prefix, suffix);
    }

    /// <summary>A spreadsheet address of the selection (A1 or A1:C5), in visible columns and display rows.</summary>
    internal string SelectionAddress()
    {
        if (!HasCells)
        {
            return "–";
        }
        var range = Range;
        var start = DataGridValueFormat.ColumnLetters(range.Column0) + (range.Row0 + 1).ToString(EffectiveCulture);
        if (range.IsSingleCell)
        {
            return start;
        }
        return start + ":" + DataGridValueFormat.ColumnLetters(range.Column1) + (range.Row1 + 1).ToString(EffectiveCulture);
    }

    private void UpdateStats() => _statusBar?.UpdateStats();
}

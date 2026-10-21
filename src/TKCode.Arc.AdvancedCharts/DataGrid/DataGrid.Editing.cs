using System.Collections;
using System.Globalization;
using Avalonia.Interactivity;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The cell being edited.</summary>
internal sealed class DataGridEditState
{
    public required int Row { get; init; }

    public required int Column { get; init; }

    public required int Accessor { get; init; }

    public required object RowKey { get; init; }

    /// <summary>Typing replaced the value: arrow keys commit and move (Excel's enter mode).</summary>
    public required bool EnterMode { get; init; }

    public bool Invalid { get; set; }

    /// <summary>The value being edited by a column's <see cref="DataGridColumn.EditTemplate"/>; null for the text editor.</summary>
    public DataGridEditContext? Context { get; set; }

    /// <summary>Set once the edit has been committed or cancelled (the editor's lost-focus commit is then ignored).</summary>
    public bool Done { get; set; }
}

public partial class DataGrid
{
    private readonly DataGridUndoStack _undo = new();
    private DataGridEditState? _edit;

    internal DataGridEditState? Edit => _edit;

    #region Editing

    /// <summary>Opens the editor on the active cell, keeping its value.</summary>
    public void BeginEdit()
    {
        EnsureView();
        if (HasCells)
        {
            BeginEdit(Anchor, null, openList: false);
        }
    }

    /// <summary>Commits the open editor, as Enter does without moving.</summary>
    public bool CommitEdit() => _edit is null || CommitEdit(null, null);

    /// <summary>Closes the open editor without writing.</summary>
    public void CancelEdit()
    {
        if (_edit is null)
        {
            return;
        }
        _edit.Done = true;
        CloseEditor();
    }

    /// <summary>Opens the editor; <paramref name="text"/> replaces the value (enter mode), null keeps it (edit mode).</summary>
    internal void BeginEdit(DataGridCellAddress address, string? text, bool openList)
    {
        if (_edit is not null)
        {
            if (!CommitEdit(null, null))
            {
                return;
            }
        }
        if (HasDataSource)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return;
        }
        address = Clamp(address);
        var accessor = VisibleAccessors[address.Column];
        var column = ColumnOf(accessor);
        var source = SourceOfRow(address.Row);
        if (source < 0)
        {
            return;
        }
        if (!column.IsEditable || !_store.CanWrite(source, accessor))
        {
            Say(DataGridText.Format(DataGridLangResourceKind.ColumnReadOnly, column.Header));
            return;
        }
        if (!CellAllowed(source, accessor))
        {
            Say(DataGridText.Get(DataGridLangResourceKind.CellReadOnly));
            return;
        }
        if (column.Type == DataGridColumnType.Boolean)
        {
            // No text editor: Space, Enter, F2 and double click flip the value, and a typed yes or no sets it.
            Select(address);
            var current = ValueAt(address.Row, accessor) is true;
            if (text is null || text.Trim().Length == 0)
            {
                SetCellValue(address.Row, accessor, !current);
            }
            else if (DataGridValueParser.TryParseBoolean(text, out var typed))
            {
                SetCellValue(address.Row, accessor, typed);
            }
            return;
        }
        Select(address);
        _edit = new DataGridEditState
        {
            Row = address.Row,
            Column = address.Column,
            Accessor = accessor,
            RowKey = _store.KeyAt(source),
            EnterMode = text is not null
        };
        IsEditing = true;
        UpdatePseudoClasses();
        if (column.EditTemplate is { } template)
        {
            var current = _store.Value(source, accessor);
            _edit.Context = new DataGridEditContext(this, _store.ItemAt(source), column, current, FormatValue(current, accessor));
            Viewport.OpenTemplateEditor(_edit, template, DataGridText.Format(DataGridLangResourceKind.EditorLabel, column.Header, RowName(address.Row)));
            return;
        }
        var draft = text ?? EditText(_store.Value(source, accessor));
        var list = openList || text is not null && column.Type is DataGridColumnType.Select or DataGridColumnType.Date;
        Viewport.OpenEditor(_edit, column, draft, list, OptionsOf(accessor),
            DataGridText.Format(DataGridLangResourceKind.EditorLabel, column.Header, RowName(address.Row)));
    }

    /// <summary>Whether edits to a cell are accepted: not with a data source, and not in a read-only column or cell.</summary>
    internal bool IsCellEditable(int row, int accessor)
    {
        if (HasDataSource || accessor < 0 || accessor >= _store.Accessors.Length || !ColumnOf(accessor).IsEditable)
        {
            return false;
        }
        var source = SourceOfRow(row);
        return source >= 0 && _store.CanWrite(source, accessor) && CellAllowed(source, accessor);
    }

    /// <summary>
    /// Writes one cell the way a committed edit does: <see cref="CellEditEnding"/> may refuse it, then it is one undo step and
    /// raises <see cref="CellsChanged"/>. Returns whether the cell changed.
    /// </summary>
    internal bool SetCellValue(int row, int accessor, object? value)
    {
        if (HasDataSource)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return false;
        }
        var column = ColumnOf(accessor);
        var source = SourceOfRow(row);
        if (source < 0)
        {
            return false;
        }
        if (!column.IsEditable || !_store.CanWrite(source, accessor))
        {
            Say(DataGridText.Format(DataGridLangResourceKind.ColumnReadOnly, column.Header));
            return false;
        }
        if (!CellAllowed(source, accessor))
        {
            Say(DataGridText.Get(DataGridLangResourceKind.CellReadOnly));
            return false;
        }
        if (_edit is not null && !CommitEdit(null, null))
        {
            return false;
        }
        var old = _store.Value(source, accessor);
        if (SameValue(old, value))
        {
            return false;
        }
        if (ValidationMode == DataGridValidationMode.Block && Validate(_store.ItemAt(source), _store.KeyAt(source), accessor, value) is { } invalid)
        {
            Say(invalid);
            return false;
        }
        var args = new DataGridCellEditEndingEventArgs(CellEditEndingEvent, _store.ItemAt(source), _store.KeyAt(source), column, old, value);
        RaiseEvent(args);
        if (args.Cancel)
        {
            if (!string.IsNullOrEmpty(args.ErrorMessage))
            {
                Say(args.ErrorMessage);
            }
            return false;
        }
        var visibleColumn = Array.IndexOf(Layout.Accessors, accessor);
        var range = new DataGridCellRange(row, Math.Max(0, visibleColumn), row, Math.Max(0, visibleColumn));
        var written = WriteCells([(source, accessor, value)], range, DataGridChangeReason.Edit, flash: false);
        if (written > 0)
        {
            var shown = FormatValue(_store.Value(source, accessor), accessor);
            Announce(DataGridText.Format(DataGridLangResourceKind.CellSet, column.Header, RowName(row),
                string.IsNullOrEmpty(shown) ? DataGridText.Get(DataGridLangResourceKind.EmptyWord) : shown));
        }
        return written > 0;
    }

    /// <summary>The text an editor starts with: numbers without grouping, days as ISO.</summary>
    private string EditText(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.############################", EffectiveCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        string text => text,
        _ => DataGridSorting.TextOf(value)
    };

    /// <summary>
    /// Commits the editor's text (or <paramref name="picked"/>, a chosen option or day) and moves by <paramref name="move"/>.
    /// An unreadable value keeps the editor open when the commit came from a key; a lost-focus commit drops it.
    /// </summary>
    internal bool CommitEdit((int Rows, int Columns)? move, string? picked)
    {
        var edit = _edit;
        if (edit is null || edit.Done)
        {
            return true;
        }
        var column = ColumnOf(edit.Accessor);
        var text = picked ?? Viewport.EditorText;
        var source = _store.IndexOfKey(edit.RowKey);
        if (source < 0)
        {
            edit.Done = true;
            CloseEditor();
            return true;
        }
        object? value;
        if (edit.Context is { } custom)
        {
            value = DataGridValueParser.Normalize(custom.Value, column.Type);
            if (!Fits(column, value))
            {
                RefuseEdit(edit, custom.ErrorMessage);
                return false;
            }
        }
        else if (!DataGridValueParser.TryParse(text, column.CellFormat, EffectiveCulture, column.Type == DataGridColumnType.Select ? OptionsOf(edit.Accessor) : null,
                out value))
        {
            if (move is null && picked is null && !Viewport.IsEditorFocused)
            {
                edit.Done = true;
                CloseEditor();
                return true;
            }
            RefuseEdit(edit, column.Type switch
            {
                DataGridColumnType.Select => DataGridText.Format(DataGridLangResourceKind.NeedsOption, column.Header),
                DataGridColumnType.Date => DataGridText.Format(DataGridLangResourceKind.NeedsDate, column.Header),
                _ => DataGridText.Format(DataGridLangResourceKind.NeedsNumber, column.Header)
            });
            return false;
        }
        var old = _store.Value(source, edit.Accessor);
        if (!SameValue(old, value))
        {
            if (ValidationMode == DataGridValidationMode.Block && Validate(_store.ItemAt(source), edit.RowKey, edit.Accessor, value) is { } invalid)
            {
                RefuseEdit(edit, invalid);
                return false;
            }
            var args = new DataGridCellEditEndingEventArgs(CellEditEndingEvent, _store.ItemAt(source), edit.RowKey, column, old, value);
            RaiseEvent(args);
            if (args.Cancel)
            {
                RefuseEdit(edit, args.ErrorMessage);
                return false;
            }
            var row = edit.Row;
            var written = WriteCells([(source, edit.Accessor, value)], new DataGridCellRange(row, edit.Column, row, edit.Column),
                DataGridChangeReason.Edit, flash: false);
            if (written > 0)
            {
                var shown = FormatValue(_store.Value(source, edit.Accessor), edit.Accessor);
                Announce(DataGridText.Format(DataGridLangResourceKind.CellSet, column.Header, RowName(row),
                    string.IsNullOrEmpty(shown) ? DataGridText.Get(DataGridLangResourceKind.EmptyWord) : shown));
            }
        }
        edit.Done = true;
        CloseEditor();
        if (move is { } delta && (delta.Rows != 0 || delta.Columns != 0))
        {
            Select(new DataGridCellAddress(edit.Row + delta.Rows, edit.Column + delta.Columns));
        }
        Viewport.FocusGrid();
        return true;
    }

    private void RefuseEdit(DataGridEditState edit, string? message)
    {
        edit.Invalid = true;
        Viewport.MarkEditorInvalid();
        if (!string.IsNullOrEmpty(message))
        {
            Say(message);
        }
    }

    internal void CancelEditFromEditor()
    {
        if (_edit is null)
        {
            return;
        }
        _edit.Done = true;
        CloseEditor();
        Viewport.FocusGrid();
    }

    private void CloseEditor()
    {
        _edit = null;
        Viewport.CloseEditor();
        IsEditing = false;
        UpdatePseudoClasses();
    }

    private static bool SameValue(object? a, object? b) =>
        DataGridSorting.IsEmpty(a) && DataGridSorting.IsEmpty(b) || Equals(a, b);

    #endregion

    #region Writing cells

    /// <summary>
    /// Writes cells (source index, accessor, normalized value): read-only columns, values of the wrong type and unchanged
    /// cells are skipped. Records one undo step, flashes the range and raises <see cref="CellsChanged"/>.
    /// </summary>
    internal int WriteCells(IEnumerable<(int Source, int Accessor, object? Value)> cells, DataGridCellRange? range,
                            DataGridChangeReason reason, bool flash = true)
    {
        if (HasDataSource)
        {
            return 0;
        }
        var step = new DataGridChangeSet { Reason = reason, Range = range };
        var changes = new List<DataGridCellChange>();
        _refusedWrites = 0;
        var checkRules = ValidationMode == DataGridValidationMode.Block &&
                         reason is DataGridChangeReason.Edit or DataGridChangeReason.Paste or DataGridChangeReason.Fill or DataGridChangeReason.FillDown;
        _store.Quietly(() =>
        {
            foreach (var (source, accessor, value) in cells)
            {
                if (source < 0 || source >= _store.Count)
                {
                    continue;
                }
                var column = ColumnOf(accessor);
                if (!column.IsEditable || !_store.CanWrite(source, accessor) || !Fits(column, value) || !CellAllowed(source, accessor))
                {
                    continue;
                }
                var old = _store.Value(source, accessor);
                if (SameValue(old, value))
                {
                    continue;
                }
                if (checkRules && Validate(_store.ItemAt(source), _store.KeyAt(source), accessor, value) is not null)
                {
                    _refusedWrites++;
                    continue;
                }
                var stored = _store.Write(source, accessor, value);
                Revalidate(source, accessor);
                var key = _store.KeyAt(source);
                step.Cells.Add(new DataGridCellEdit(key, accessor, old, stored));
                changes.Add(new DataGridCellChange(_store.ItemAt(source), key, column.Key, old, stored));
            }
        });
        if (changes.Count == 0)
        {
            return 0;
        }
        _undo.Push(step);
        AfterWrite(range, flash);
        PublishErrorCount();
        RaiseEvent(new DataGridCellsChangedEventArgs(CellsChangedEvent, changes, reason));
        return changes.Count;
    }

    private static bool Fits(DataGridColumn column, object? value) => value switch
    {
        null => true,
        decimal => column.IsNumeric || column.Type is DataGridColumnType.Text or DataGridColumnType.Select,
        DateOnly => column.Type is DataGridColumnType.Date or DataGridColumnType.Text,
        bool => column.Type == DataGridColumnType.Boolean,
        string => !column.IsNumeric && column.Type is not (DataGridColumnType.Date or DataGridColumnType.Boolean),
        _ => false
    };

    private void AfterWrite(DataGridCellRange? range, bool flash)
    {
        _editVersion++;
        InvalidateOptions();
        Viewport.InvalidateLayers();
        Viewport.UpdateTotals();
        UpdateUndoState();
        UpdateStats();
        if (flash && range is { } r)
        {
            Viewport.Flash(r);
        }
    }

    private void UpdateUndoState()
    {
        CanUndo = _undo.CanUndo;
        CanRedo = _undo.CanRedo;
        _toolbar?.UpdateUndo();
    }

    /// <summary>Clears the cells of a range (read-only cells are skipped).</summary>
    internal void ClearRange(DataGridCellRange range)
    {
        if (HasDataSource)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return;
        }
        var written = WriteCells(RangeCells(range, (_, _) => null), range, DataGridChangeReason.Clear);
        Say(written > 0
            ? DataGridText.Count(DataGridLangResourceKind.ClearedCellsOne, DataGridLangResourceKind.ClearedCellsOther, written, EffectiveCulture)
            : DataGridText.Get(DataGridLangResourceKind.NothingToClear));
    }

    /// <summary>The cells of a range with a value from <paramref name="valueOf"/> (display row, visible column).</summary>
    private IEnumerable<(int Source, int Accessor, object? Value)> RangeCells(DataGridCellRange range, Func<int, int, object?> valueOf)
    {
        var visible = VisibleAccessors;
        for (var r = range.Row0; r <= range.Row1; r++)
        {
            var source = SourceOfRow(r);
            if (source < 0)
            {
                continue;
            }
            for (var c = range.Column0; c <= range.Column1 && c < visible.Length; c++)
            {
                yield return (source, visible[c], valueOf(r, c));
            }
        }
    }

    /// <summary>Ctrl + D: copies the first row of the range into the rows below it.</summary>
    internal void FillDown()
    {
        if (HasDataSource)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return;
        }
        var range = Range;
        if (range.Row0 == range.Row1)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.FillDownNeedsRows));
            return;
        }
        var visible = VisibleAccessors;
        var below = range with { Row0 = range.Row0 + 1 };
        var written = WriteCells(RangeCells(below, (_, c) => ValueAt(range.Row0, visible[c])), range, DataGridChangeReason.FillDown, flash: false);
        Viewport.Flash(below);
        Say(written > 0
            ? DataGridText.Count(DataGridLangResourceKind.FilledDownOne, DataGridLangResourceKind.FilledDownOther, written, EffectiveCulture)
            : DataGridText.Get(DataGridLangResourceKind.NothingToFill));
    }

    /// <summary>Finishes a fill handle drag from <paramref name="source"/> to <paramref name="target"/> (which contains it).</summary>
    internal void CompleteFill(DataGridCellRange source, DataGridCellRange target)
    {
        if (HasDataSource || source == target)
        {
            return;
        }
        var visible = VisibleAccessors;
        var cells = new List<(int, int, object?)>();
        DataGridCellRange added;
        if (target.Row0 != source.Row0 || target.Row1 != source.Row1)
        {
            var down = target.Row1 > source.Row1;
            var count = down ? target.Row1 - source.Row1 : source.Row0 - target.Row0;
            for (var c = source.Column0; c <= source.Column1; c++)
            {
                var values = new List<object?>();
                for (var r = source.Row0; r <= source.Row1; r++)
                {
                    values.Add(ValueAt(r, visible[c]));
                }
                var series = DataGridFillSeries.Extend(values, count, down, ColumnOf(visible[c]).Type);
                for (var i = 0; i < series.Count; i++)
                {
                    var row = down ? source.Row1 + 1 + i : source.Row0 - 1 - i;
                    cells.Add((SourceOfRow(row), visible[c], series[i]));
                }
            }
            added = down ? target with { Row0 = source.Row1 + 1 } : target with { Row1 = source.Row0 - 1 };
        }
        else
        {
            var right = target.Column1 > source.Column1;
            var count = right ? target.Column1 - source.Column1 : source.Column0 - target.Column0;
            for (var r = source.Row0; r <= source.Row1; r++)
            {
                var values = new List<object?>();
                for (var c = source.Column0; c <= source.Column1; c++)
                {
                    values.Add(ValueAt(r, visible[c]));
                }
                var series = DataGridFillSeries.Extend(values, count, right);
                for (var i = 0; i < series.Count; i++)
                {
                    var column = right ? source.Column1 + 1 + i : source.Column0 - 1 - i;
                    cells.Add((SourceOfRow(r), visible[column], series[i]));
                }
            }
            added = right ? target with { Column0 = source.Column1 + 1 } : target with { Column1 = source.Column0 - 1 };
        }
        var written = WriteCells(cells, target, DataGridChangeReason.Fill, flash: false);
        Select(new DataGridCellAddress(target.Row0, target.Column0), new DataGridCellAddress(target.Row1, target.Column1), scroll: false);
        Viewport.Flash(added);
        Say(written > 0
            ? DataGridText.Count(DataGridLangResourceKind.FilledCellsOne, DataGridLangResourceKind.FilledCellsOther, written, EffectiveCulture)
            : DataGridText.Get(DataGridLangResourceKind.NothingToFill));
    }

    #endregion

    #region Undo and redo

    /// <summary>Undoes the last edit, fill, paste, clear or row delete.</summary>
    public void Undo()
    {
        if (_edit is not null)
        {
            CancelEdit();
        }
        var step = _undo.PopUndo();
        if (step is null)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.NothingToUndo));
            return;
        }
        Apply(step, undo: true);
        Say(DataGridText.Get(DataGridLangResourceKind.Undone));
    }

    /// <summary>Redoes the last undone step.</summary>
    public void Redo()
    {
        if (_edit is not null)
        {
            CancelEdit();
        }
        var step = _undo.PopRedo();
        if (step is null)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.NothingToRedo));
            return;
        }
        Apply(step, undo: false);
        Say(DataGridText.Get(DataGridLangResourceKind.Redone));
    }

    /// <summary>Forgets every undo and redo step.</summary>
    public void ClearHistory()
    {
        _undo.Clear();
        UpdateUndoState();
    }

    private void Apply(DataGridChangeSet step, bool undo)
    {
        if (step.Inserted.Count > 0)
        {
            if (undo)
            {
                RemoveRows(step.Inserted);
            }
            else
            {
                InsertRows(step.Inserted);
            }
            UpdateUndoState();
            return;
        }
        if (step.Deleted.Count > 0)
        {
            if (undo)
            {
                RestoreRows(step.Deleted);
            }
            else
            {
                RemoveRows(step.Deleted);
            }
            UpdateUndoState();
            return;
        }
        var changes = new List<DataGridCellChange>();
        _store.Quietly(() =>
        {
            var cells = undo ? Enumerable.Reverse(step.Cells) : step.Cells;
            foreach (var cell in cells)
            {
                var source = _store.IndexOfKey(cell.RowKey);
                if (source < 0)
                {
                    continue;
                }
                var value = undo ? cell.Before : cell.After;
                var old = _store.Value(source, cell.Accessor);
                var stored = _store.Write(source, cell.Accessor, value);
                Revalidate(source, cell.Accessor);
                changes.Add(new DataGridCellChange(_store.ItemAt(source), cell.RowKey, ColumnOf(cell.Accessor).Key, old, stored));
            }
        });
        AfterWrite(null, flash: false);
        PublishErrorCount();
        if (step.Range is { } range)
        {
            Select(new DataGridCellAddress(range.Row0, range.Column0), new DataGridCellAddress(range.Row1, range.Column1));
            Viewport.Flash(range);
        }
        if (changes.Count > 0)
        {
            RaiseEvent(new DataGridCellsChangedEventArgs(CellsChangedEvent, changes, undo ? DataGridChangeReason.Undo : DataGridChangeReason.Redo));
        }
    }

    #endregion

    #region Deleting rows

    /// <summary>Deletes the checked rows (undoable).</summary>
    internal void DeleteChecked()
    {
        if (HasDataSource || !CanDeleteRows || _checked.Count == 0)
        {
            return;
        }
        DeleteRowsByKeys(_checked.ToHashSet());
    }

    /// <summary>Deletes the rows with these keys as one undo step.</summary>
    internal void DeleteRowsByKeys(HashSet<object> keys)
    {
        var deleted = new List<DataGridDeletedRow>();
        for (var i = 0; i < _store.Count; i++)
        {
            if (keys.Contains(_store.KeyAt(i)))
            {
                deleted.Add(new DataGridDeletedRow(i, _store.ItemAt(i)));
            }
        }
        if (deleted.Count == 0)
        {
            return;
        }
        var step = new DataGridChangeSet { Reason = DataGridChangeReason.Edit };
        step.Deleted.AddRange(deleted);
        _undo.Push(step);
        RemoveRows(deleted);
        SetChecked(_checked.Where(k => !keys.Contains(k)).ToArray());
        UpdateUndoState();
        Say(DataGridText.Count(DataGridLangResourceKind.DeletedRowsOne, DataGridLangResourceKind.DeletedRowsOther, deleted.Count, EffectiveCulture),
            DataGridText.Get(DataGridLangResourceKind.Undo), () =>
            {
                Undo();
                Viewport.FocusGrid();
            });
    }

    private void RemoveRows(IReadOnlyList<DataGridDeletedRow> rows)
    {
        if (_store.IsWritableList && _store.Source is IList list)
        {
            _store.Batch(() =>
            {
                for (var i = rows.Count - 1; i >= 0; i--)
                {
                    var index = list.IndexOf(rows[i].Item);
                    if (index >= 0)
                    {
                        list.RemoveAt(index);
                    }
                }
            });
        }
        RaiseEvent(new DataGridRowsEventArgs(RowsDeletedEvent, rows.Select(r => r.Item).ToArray(), rows.Select(r => r.Index).ToArray()));
    }

    private void RestoreRows(IReadOnlyList<DataGridDeletedRow> rows)
    {
        if (_store.IsWritableList && _store.Source is IList list)
        {
            _store.Batch(() =>
            {
                foreach (var row in rows.OrderBy(r => r.Index))
                {
                    list.Insert(Math.Clamp(row.Index, 0, list.Count), row.Item);
                }
            });
        }
        RaiseEvent(new DataGridRowsEventArgs(RowsRestoredEvent, rows.Select(r => r.Item).ToArray(), rows.Select(r => r.Index).ToArray()));
    }

    #endregion
}

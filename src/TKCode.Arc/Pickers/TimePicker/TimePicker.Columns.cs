using System.Globalization;
using Avalonia.Input;

namespace TKCode.Arc;

// The column layout (#194): one drawn column each for the hours, the minutes (every MinuteStep), the seconds (every
// SecondStep, at second precision) and AM / PM on a 12-hour clock. A choice in a column applies that part at once and
// keeps the drop-down open; the keyboard moves within a column (Up / Down / Home / End, digits type ahead) and between
// columns (Left / Right), and Enter closes. Only the active column shows the row highlight; every column marks its
// chosen value with the dot.
public partial class TimePicker
{
    private const long TypeAheadMilliseconds = 1000;

    private TimePickerColumnKind _activeColumn = TimePickerColumnKind.Hour;
    private string _typed = string.Empty;
    private long _typedAt;

    /// <summary>Whether the drop-down shows columns (the layout, or Auto at second precision).</summary>
    internal bool UsesColumns => DropDownLayout switch
    {
        TimePickerDropDownLayout.Columns => true,
        TimePickerDropDownLayout.List => false,
        _ => Precision == TimePickerPrecision.Second
    };

    /// <summary>The column the keyboard and the row highlight are in.</summary>
    internal TimePickerColumnKind ActiveColumn => _activeColumn;

    private int ColumnCount(TimePickerColumnKind kind) => kind switch
    {
        TimePickerColumnKind.Hour => IsTwelveHour ? 12 : 24,
        TimePickerColumnKind.Minute => MinuteStep >= 60 ? 1 : (int)Math.Ceiling(60.0 / MinuteStep),
        TimePickerColumnKind.Second => (int)Math.Ceiling(60.0 / SecondStep),
        _ => 2
    };

    private string ColumnLabel(TimePickerColumnKind kind, int index) => kind switch
    {
        TimePickerColumnKind.Hour => (IsTwelveHour && index == 0 ? 12 : index).ToString("00", CultureInfo.InvariantCulture),
        TimePickerColumnKind.Minute => (index * MinuteStep).ToString("00", CultureInfo.InvariantCulture),
        TimePickerColumnKind.Second => (index * SecondStep).ToString("00", CultureInfo.InvariantCulture),
        _ => Designator(index == 1)
    };

    /// <summary>The row of <paramref name="time"/> in a column, or the step at or just before it.</summary>
    private int ColumnAnchor(TimePickerColumnKind kind, TimeOnly time) => kind switch
    {
        TimePickerColumnKind.Hour => IsTwelveHour ? time.Hour % 12 : time.Hour,
        TimePickerColumnKind.Minute => Math.Min(ColumnCount(kind) - 1, time.Minute / MinuteStep),
        TimePickerColumnKind.Second => Math.Min(ColumnCount(kind) - 1, time.Second / SecondStep),
        _ => time.Hour < 12 ? 0 : 1
    };

    /// <summary>The row that carries the chosen dot: the anchor when the part is exactly on its step, else none.</summary>
    private int ColumnExact(TimePickerColumnKind kind, TimeOnly time) => kind switch
    {
        TimePickerColumnKind.Minute => time.Minute % MinuteStep == 0 && time.Minute / MinuteStep < ColumnCount(kind) ? time.Minute / MinuteStep : -1,
        TimePickerColumnKind.Second => time.Second % SecondStep == 0 ? time.Second / SecondStep : -1,
        _ => ColumnAnchor(kind, time)
    };

    private int AnchorOf(TimePickerColumnKind kind) => SelectedTime is { } time ? ColumnAnchor(kind, time) : 0;

    private void HookColumns(TimePickerPanel panel)
    {
        foreach (var column in panel.Columns)
        {
            var kind = column.Kind;
            column.List.RowHovered += (_, index) => HoverColumnCell(kind, index);
            column.List.RowChosen += (_, index) => ChooseColumnCell(kind, index);
            column.Scroll.LayoutUpdated += OnColumnLayoutUpdated;
        }
    }

    private void ConfigureColumns()
    {
        if (_panel is null)
        {
            return;
        }
        foreach (var column in _panel.VisibleColumns)
        {
            var kind = column.Kind;
            column.List.SetOptions(ColumnCount(kind), i => ColumnLabel(kind, i));
        }
        if (_panel.ColumnOf(_activeColumn) is null)
        {
            _activeColumn = TimePickerColumnKind.Hour;
        }
        UpdateColumnSelection();
    }

    private void UpdateColumnSelection()
    {
        if (_panel is null)
        {
            return;
        }
        foreach (var column in _panel.VisibleColumns)
        {
            column.List.SetSelected(SelectedTime is { } time ? ColumnExact(column.Kind, time) : -1);
        }
    }

    /// <summary>On open: the first column is active at the chosen hour; the others show only their dots.</summary>
    private void ActivateColumns()
    {
        if (_panel is null)
        {
            return;
        }
        _activeColumn = TimePickerColumnKind.Hour;
        _typed = string.Empty;
        foreach (var column in _panel.VisibleColumns)
        {
            column.List.SetActive(column.Kind == _activeColumn ? AnchorOf(column.Kind) : -1, animate: false);
        }
    }

    private void OnColumnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_centerPending && _panel is { UsesColumns: true })
        {
            CenterColumns();
        }
    }

    /// <summary>Every column opens with its chosen value in the middle (once every column has a viewport).</summary>
    private void CenterColumns()
    {
        if (_panel is null || _panel.VisibleColumns.Count == 0)
        {
            return;
        }
        foreach (var column in _panel.VisibleColumns)
        {
            if (column.Scroll.Viewport.Height <= 0)
            {
                return;
            }
        }
        _centerPending = false;
        foreach (var column in _panel.VisibleColumns)
        {
            column.ScrollMotion?.Stop();
            CenterRow(column.Scroll, column.List, AnchorOf(column.Kind));
        }
    }

    private void StopColumnMotions()
    {
        if (_panel is null)
        {
            return;
        }
        foreach (var column in _panel.Columns)
        {
            column.ScrollMotion?.Stop();
        }
    }

    private void HoverColumnCell(TimePickerColumnKind kind, int index)
    {
        if (_panel?.ColumnOf(kind) is not { } column)
        {
            return;
        }
        ActivateColumn(kind, index, animate: true);
        column.List.SetActive(index, animate: true);
    }

    /// <summary>Applies row <paramref name="index"/> of a column to the value; the drop-down stays open.</summary>
    internal void ChooseColumnCell(TimePickerColumnKind kind, int index)
    {
        if (index < 0 || index >= ColumnCount(kind))
        {
            return;
        }
        var time = SelectedTime ?? new TimeOnly(0, 0);
        var hour = time.Hour;
        var minute = time.Minute;
        var second = Precision == TimePickerPrecision.Second ? time.Second : 0;
        switch (kind)
        {
            case TimePickerColumnKind.Hour:
                hour = IsTwelveHour ? index + (time.Hour >= 12 ? 12 : 0) : index;
                break;
            case TimePickerColumnKind.Minute:
                minute = index * MinuteStep % 60;
                break;
            case TimePickerColumnKind.Second:
                second = index * SecondStep % 60;
                break;
            default:
                hour = time.Hour % 12 + (index == 1 ? 12 : 0);
                break;
        }
        if (_panel?.ColumnOf(kind) is { } column)
        {
            ActivateColumn(kind, index, animate: true);
            column.List.SetActive(index, animate: true);
        }
        SetCurrentValue(SelectedTimeProperty, new TimeOnly(hour, minute, second));
    }

    /// <summary>Moves the highlight to column <paramref name="kind"/>; the column it leaves fades its highlight out.</summary>
    private void ActivateColumn(TimePickerColumnKind kind, int index, bool animate)
    {
        if (kind == _activeColumn || _panel is null)
        {
            return;
        }
        _panel.ColumnOf(_activeColumn)?.List.SetActive(-1, animate);
        _activeColumn = kind;
        _typed = string.Empty;
        _panel.ColumnOf(kind)?.List.SetActive(index, animate);
    }

    private void RevealColumn(TimePickerColumn column) =>
        column.ScrollMotion = Reveal(column.Scroll, column.List, column.ScrollMotion);

    /// <summary>The keys of the open column layout; false leaves the key to the shared handling (Escape).</summary>
    private bool HandleColumnKey(KeyEventArgs e)
    {
        if (_panel is null || _panel.ColumnOf(_activeColumn) is not { } column)
        {
            return false;
        }
        var list = column.List;
        var count = list.Count;
        switch (e.Key)
        {
            case Key.Enter or Key.Space:
                if (list.ActiveIndex >= 0 && list.ActiveIndex != list.SelectedIndex)
                {
                    ChooseColumnCell(column.Kind, list.ActiveIndex);
                }
                SetCurrentValue(IsDropDownOpenProperty, false);
                return true;
            case Key.Down or Key.Up when count > 0:
            {
                var from = list.ActiveIndex >= 0 ? list.ActiveIndex : AnchorOf(column.Kind);
                var next = ((from + (e.Key == Key.Down ? 1 : -1)) % count + count) % count;
                ChooseColumnCell(column.Kind, next);
                RevealColumn(column);
                return true;
            }
            case Key.Home or Key.End when count > 0:
                ChooseColumnCell(column.Kind, e.Key == Key.Home ? 0 : count - 1);
                RevealColumn(column);
                return true;
            case Key.Left or Key.Right:
            {
                var visible = _panel.VisibleColumns;
                var at = 0;
                for (var i = 0; i < visible.Count; i++)
                {
                    if (visible[i].Kind == _activeColumn)
                    {
                        at = i;
                    }
                }
                var target = visible[Math.Clamp(at + (e.Key == Key.Right ? 1 : -1), 0, visible.Count - 1)];
                if (target.Kind != _activeColumn)
                {
                    ActivateColumn(target.Kind, AnchorOf(target.Kind), animate: true);
                    RevealColumn(target);
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>Typing in the open column layout jumps the active column to the first value that starts with the typed text.</summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text) || !IsDropDownOpen || _panel is not { UsesColumns: true }
            || _panel.ColumnOf(_activeColumn) is not { } column)
        {
            return;
        }
        var now = Environment.TickCount64;
        if (now - _typedAt > TypeAheadMilliseconds)
        {
            _typed = string.Empty;
        }
        _typedAt = now;
        var text = _typed + e.Text;
        var index = FindColumnRow(column, text);
        if (index < 0)
        {
            text = e.Text;
            index = FindColumnRow(column, text);
        }
        _typed = index >= 0 ? text : string.Empty;
        if (index < 0)
        {
            return;
        }
        e.Handled = true;
        ChooseColumnCell(column.Kind, index);
        RevealColumn(column);
    }

    /// <summary>The first row whose value starts with <paramref name="text"/>, with or without its leading zero ("7" finds "07").</summary>
    private static int FindColumnRow(TimePickerColumn column, string text)
    {
        var list = column.List;
        for (var i = 0; i < list.Count; i++)
        {
            var label = list.LabelAt(i);
            if (label.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
            var trimmed = label.TrimStart('0');
            if (trimmed.Length > 0 && trimmed.Length < label.Length && trimmed.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}

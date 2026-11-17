using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class Table
{
    #region Keyboard

    /// <summary>Header: ←/→ and Home/End walk the header controls; ↓ enters the row checkboxes.</summary>
    internal bool NavigateFromHeader(TableHeaderCell cell, Key key)
    {
        if (_presenter is null)
        {
            return false;
        }
        var cells = _presenter.Header.Cells.Where(c => c.Focusable).ToList();
        var index = cells.IndexOf(cell);
        TableHeaderCell? target = key switch
        {
            Key.Right => index + 1 < cells.Count ? cells[index + 1] : null,
            Key.Left => index > 0 ? cells[index - 1] : null,
            Key.Home => cells.FirstOrDefault(),
            Key.End => cells.LastOrDefault(),
            _ => null
        };
        if (target is not null)
        {
            target.Focus(NavigationMethod.Directional);
            _presenter.Header.RevealPill(target);
            return true;
        }
        if (key == Key.Down && IsSelectable && cell.IsSelectAll && _items.Count > 0)
        {
            FocusRow(0);
            return true;
        }
        return false;
    }

    /// <summary>Rows: ↑/↓, Home/End, PageUp/PageDown move between checkboxes; Space / Enter toggles (Shift extends).</summary>
    internal bool NavigateFromRow(TableRow row, Key key, KeyModifiers modifiers)
    {
        var count = _items.Count;
        var current = row.DisplayIndex;
        if (count == 0 || current < 0)
        {
            return false;
        }
        var page = Math.Max(1, (int)((_presenter?.Viewport.Height ?? 400) / Layout.RowHeight) - 1);
        switch (key)
        {
            case Key.Down:
                if (current + 1 < count)
                {
                    FocusRow(current + 1);
                }
                return true;
            case Key.Up:
                if (current > 0)
                {
                    FocusRow(current - 1);
                }
                else
                {
                    _presenter?.Header.CellFor(-1)?.Focus(NavigationMethod.Directional);
                }
                return true;
            case Key.Home:
                FocusRow(0);
                return true;
            case Key.End:
                FocusRow(count - 1);
                return true;
            case Key.PageDown:
                FocusRow(Math.Min(count - 1, current + page));
                return true;
            case Key.PageUp:
                FocusRow(Math.Max(0, current - page));
                return true;
            case Key.Space:
            case Key.Enter:
                if (IsSelectable && row.Key is not null)
                {
                    ToggleRow(row.Key, modifiers.HasFlag(KeyModifiers.Shift));
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    /// <summary>Moves keyboard focus to the row at <paramref name="displayIndex"/>, scrolling it into view.</summary>
    internal void FocusRow(int displayIndex)
    {
        if (_presenter is null || displayIndex < 0 || displayIndex >= _items.Count)
        {
            return;
        }
        _currentRow = displayIndex;
        _presenter.RevealRow(displayIndex);
        var row = _presenter.EnsureRow(displayIndex);
        _presenter.UpdateTabStops();
        row?.Focus(NavigationMethod.Directional);
    }

    /// <summary>Scrolls the row showing <paramref name="item"/> into view.</summary>
    public void ScrollIntoView(object item)
    {
        var key = RowKeySelector?.Invoke(item) ?? item;
        if (_presenter is not null && _indexByKey.TryGetValue(key, out var source))
        {
            _presenter.RevealRow(_position[source]);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || SelectedPresentCount() == 0)
        {
            return;
        }
        e.Handled = true;
        if (_clear is not null && _clear.IsFocused)
        {
            ClearSelection();
        }
        else
        {
            Commit(new HashSet<object>());
        }
    }

    #endregion
}

using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class TreeSelect
{
    #region Pointer

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (_clearHost is { IsPresent: true } host && new Rect(host.Bounds.Size).Contains(e.GetPosition(host)))
        {
            return;
        }
        if (IsOpen && _search is { IsVisible: true } search && new Rect(search.Bounds.Size).Contains(e.GetPosition(search)))
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        PseudoClasses.Set(PressedPseudoClass, true);
        _pressedOnTrigger = true;
        e.Handled = true;
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetCurrentValue(IsOpenProperty, !IsOpen);
            _pressedOnTrigger = false;
        }
    }

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        PseudoClasses.Set(PressedPseudoClass, false);
        if (_pressedOnTrigger && _trigger is not null && new Rect(_trigger.Bounds.Size).Contains(e.GetPosition(_trigger)))
        {
            SetCurrentValue(IsOpenProperty, !IsOpen);
        }
        _pressedOnTrigger = false;
    }

    private void OnTriggerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        PseudoClasses.Set(PressedPseudoClass, false);
        _pressedOnTrigger = false;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    #endregion

    #region Keyboard

    private bool FromSearch(KeyEventArgs e) =>
        _search is not null && e.Source is Visual source && (ReferenceEquals(source, _search) || source.GetVisualAncestors().Contains(_search));

    private TreeSelectNode? ActiveNode => _activeKey is not null && _nodes.TryGetValue(_activeKey, out var node) && _panel?.IndexOfKey(_activeKey) >= 0 ? node : null;

    // Tunnel: the search input would otherwise take the arrows, Home, End and Enter for its caret.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        // Keys typed in the popup's own controls are not the field's.
        if (e.Source is Visual visual && _popup?.Child is { } child && (ReferenceEquals(visual, child) || visual.GetVisualAncestors().Contains(child)))
        {
            return;
        }
        var fromSearch = FromSearch(e);
        var multiple = SelectionMode == TreeSelectMode.Multiple;
        if (!IsOpen)
        {
            switch (e.Key)
            {
                case Key.Enter:
                case Key.Space:
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    Open();
                    return;
                case Key.Delete:
                case Key.Back:
                    if (IsClearable && !IsReadOnly && _selectedItems.Count > 0)
                    {
                        e.Handled = true;
                        Clear();
                    }
                    return;
            }
            return;
        }
        var active = ActiveNode;
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                if (_query.Length > 0)
                {
                    SetCurrentValue(SearchTextProperty, null);
                }
                else
                {
                    Close();
                }
                return;
            case Key.Tab:
                Close();
                return;
            case Key.Down:
            case Key.Up:
            case Key.Home:
            case Key.End:
            case Key.PageDown:
            case Key.PageUp:
            {
                e.Handled = true;
                var index = active is null ? -1 : _panel?.IndexOfKey(active.Item) ?? -1;
                _navigator.PageSize = Math.Max(1, (int)(ScrollHeightLimit() / Math.Max(1, RowHeight)) - 1);
                int? target = index < 0
                    ? (e.Key is Key.Up or Key.End ? _rows.Count - 1 : 0)
                    : _navigator.Navigate(index, e.Key);
                if (target is { } t && t >= 0 && t < _rows.Count)
                {
                    MoveActive((TreeSelectNode)_rows[t]);
                }
                return;
            }
            case Key.Right when active is not null:
                e.Handled = true;
                if (active.HasChildren && !active.IsExpanded)
                {
                    SetNodeExpanded(active, true);
                }
                else if (active.IsExpanded && active.Children is { Count: > 0 })
                {
                    var next = (_panel?.IndexOfKey(active.Item) ?? -1) + 1;
                    if (next > 0 && next < _rows.Count)
                    {
                        MoveActive((TreeSelectNode)_rows[next]);
                    }
                }
                return;
            case Key.Left when active is not null:
                e.Handled = true;
                if (active.IsExpanded)
                {
                    SetNodeExpanded(active, false);
                }
                else if (active.Parent is { } parent)
                {
                    MoveActive(parent);
                }
                return;
            case Key.Enter:
                e.Handled = true;
                if (active is null)
                {
                    Close();
                }
                else if (multiple)
                {
                    ToggleNode(active);
                }
                else
                {
                    PickNode(active);
                }
                return;
            case Key.Space when active is not null && (!fromSearch || _query.Length == 0):
                e.Handled = true;
                if (multiple)
                {
                    ToggleNode(active);
                }
                else
                {
                    PickNode(active);
                }
                return;
            case Key.Back when multiple && _query.Length == 0 && _selectedItems.Count > 0 && !IsReadOnly:
                e.Handled = true;
                if (_nodes.TryGetValue(_selectedItems[^1], out var last))
                {
                    if (last.HasChildren)
                    {
                        SetSubtree(last, false);
                    }
                    else
                    {
                        last.IsChecked = false;
                    }
                    Recompute();
                    UpdateReported(null);
                }
                return;
        }
    }

    private void MoveActive(TreeSelectNode node)
    {
        SetActive(node.Item, animate: true, scroll: true);
        Announce(node);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || string.IsNullOrEmpty(e.Text))
        {
            base.OnTextInput(e);
            return;
        }
        if (IsSearchable)
        {
            if (e.Text == " " && _query.Length == 0)
            {
                return;
            }
            // Typing on the field starts a search: open, and hand the text to the input.
            e.Handled = true;
            SetCurrentValue(SearchTextProperty, _query + e.Text);
            if (!IsOpen)
            {
                Open();
            }
            return;
        }
        if (!IsOpen)
        {
            base.OnTextInput(e);
            return;
        }
        var index = _activeKey is null ? -1 : _panel?.IndexOfKey(_activeKey) ?? -1;
        if (_navigator.TypeAhead(index, e.Text) is { } target && target >= 0 && target < _rows.Count)
        {
            e.Handled = true;
            MoveActive((TreeSelectNode)_rows[target]);
        }
    }

    #endregion
}

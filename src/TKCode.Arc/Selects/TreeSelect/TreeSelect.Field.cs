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
    #region Field

    private string? BuildText()
    {
        if (SelectionMode == TreeSelectMode.Multiple)
        {
            return _selectedItems.Count == 0 ? null : string.Join(", ", _selectedItems.Select(i => _nodes.TryGetValue(i, out var n) ? n.Label : TextOf(i)));
        }
        if (_selected is null)
        {
            return null;
        }
        if (!ShowFullPath)
        {
            return _selected.Label;
        }
        var path = new List<string>();
        for (var node = _selected; node is not null; node = node.Parent)
        {
            path.Add(node.Label);
        }
        path.Reverse();
        return string.Join(PathSeparator, path);
    }

    private void UpdateField(bool animate)
    {
        var text = BuildText();
        SelectedText = text;
        var empty = text is null;
        PseudoClasses.Set(PlaceholderPseudoClass, empty);
        var multiple = SelectionMode == TreeSelectMode.Multiple;
        if (_value is not null)
        {
            var shown = multiple ? null : text;
            _value.Show(shown ?? Placeholder ?? string.Empty, null, shown is null, 1, animate: animate && _rendered);
            _value.IsVisible = _query.Length == 0 && (!multiple || empty);
            _rendered = true;
        }
        if (_chips is not null)
        {
            var max = Math.Max(0, MaxVisibleChips);
            var chips = new List<(object Key, string Label)>();
            if (multiple)
            {
                foreach (var item in _selectedItems)
                {
                    if (chips.Count >= max)
                    {
                        break;
                    }
                    chips.Add((item, _nodes.TryGetValue(item, out var node) ? node.Label : TextOf(item)));
                }
            }
            _chips.Update(chips, multiple ? Math.Max(0, _selectedItems.Count - chips.Count) : 0);
        }
    }

    private void UpdateClear()
    {
        if (_clearHost is not null)
        {
            _clearHost.IsPresent = IsClearable && !IsReadOnly && IsEffectivelyEnabled && _selectedItems.Count > 0;
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Clear();
        Focus(NavigationMethod.Pointer);
    }

    #endregion
}

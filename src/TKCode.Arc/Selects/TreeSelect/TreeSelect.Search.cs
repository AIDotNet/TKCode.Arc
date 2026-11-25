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
    #region Search

    private void UpdateSearchVisibility()
    {
        var show = IsOpen && IsSearchable && IsEffectivelyEnabled;
        if (show)
        {
            EnsureSearch();
        }
        _search?.IsVisible = show;
    }

    /// <summary>The search input exists only for a searchable field, from its first open.</summary>
    private void EnsureSearch()
    {
        if (_search is not null || _searchHost is null)
        {
            return;
        }
        _search = SelectInputBox.Create();
        _search.Text = _query;
        _search.PropertyChanged += OnSearchPropertyChanged;
        _searchHost.Child = _search;
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingSearch || _search is null)
        {
            return;
        }
        SetCurrentValue(SearchTextProperty, _search.Text);
    }

    private void SetQuery(string query)
    {
        if (!IsSearchable)
        {
            query = string.Empty;
        }
        if (_search is not null && (_search.Text ?? string.Empty) != query)
        {
            _syncingSearch = true;
            try
            {
                _search.Text = query;
                _search.CaretIndex = query.Length;
            }
            finally
            {
                _syncingSearch = false;
            }
        }
        if (query == _query)
        {
            return;
        }
        _query = query;
        PseudoClasses.Set(SearchingPseudoClass, query.Length > 0);
        Refilter();
        UpdateField(animate: false);
    }

    /// <summary>
    /// Applies the search: visible rows are the matches, their ancestors (opened) and their descendants (closed);
    /// clearing the search restores what was open before it started.
    /// </summary>
    private void Refilter()
    {
        foreach (var node in _matched)
        {
            node.MatchStart = -1;
            node.MatchLength = 0;
        }
        _matched.Clear();
        _filterVisible.Clear();
        if (_query.Length == 0)
        {
            if (_expandedBeforeSearch is not null)
            {
                _expanded = _expandedBeforeSearch;
                _expandedBeforeSearch = null;
            }
            RebuildRows(animate: false);
            if (IsOpen)
            {
                SetActive(DefaultActive(), animate: false, scroll: true);
            }
            UpdateEmpty();
            return;
        }
        _expandedBeforeSearch ??= new HashSet<object>(_expanded);
        _expanded = new HashSet<object>();
        var predicate = FilterPredicate;
        foreach (var root in _roots)
        {
            Filter(root, false, predicate);
        }
        RebuildRows(animate: false);
        object? first = null;
        foreach (var row in _rows)
        {
            if (((TreeSelectNode)row).MatchStart >= 0 || predicate is not null && _matched.Contains((TreeSelectNode)row))
            {
                first = ((TreeSelectNode)row).Item;
                break;
            }
        }
        SetActive(first ?? (_rows.Count > 0 ? ((TreeSelectNode)_rows[0]).Item : null), animate: false, scroll: true);
        _scroll?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        UpdateEmpty();
    }

    /// <summary>Returns whether <paramref name="node"/> or a descendant matches.</summary>
    private bool Filter(TreeSelectNode node, bool underMatch, Func<object, string, bool>? predicate)
    {
        var index = node.Label.IndexOf(_query, StringComparison.OrdinalIgnoreCase);
        var self = predicate is not null ? predicate(node.Item, _query) : index >= 0;
        if (self)
        {
            node.MatchStart = index;
            node.MatchLength = index >= 0 ? _query.Length : 0;
            _matched.Add(node);
        }
        var below = false;
        if (node.HasChildren)
        {
            foreach (var child in node.Children!)
            {
                below |= Filter(child, underMatch || self, predicate);
            }
        }
        if (below)
        {
            _expanded.Add(node.Item);
        }
        if (underMatch || self || below)
        {
            _filterVisible.Add(node);
        }
        return self || below;
    }

    private void UpdateEmpty()
    {
        _emptyHost?.IsPresent = _query.Length > 0 && _rows.Count == 0;
    }

    #endregion
}

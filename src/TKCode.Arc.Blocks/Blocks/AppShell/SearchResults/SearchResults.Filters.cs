using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class SearchResults
{
    /// <summary>True while at least one filter narrows the results (read-only).</summary>
    public static readonly DirectProperty<SearchResults, bool> HasActiveFiltersProperty =
        AvaloniaProperty.RegisterDirect<SearchResults, bool>(nameof(HasActiveFilters), o => o.HasActiveFilters);

    /// <summary>The number of filter values ticked plus date ranges narrowed (read-only).</summary>
    public static readonly DirectProperty<SearchResults, int> ActiveFilterCountProperty =
        AvaloniaProperty.RegisterDirect<SearchResults, int>(nameof(ActiveFilterCount), o => o.ActiveFilterCount);

    private bool _hasActiveFilters;
    private int _activeFilterCount;

    /// <inheritdoc cref="HasActiveFiltersProperty"/>
    public bool HasActiveFilters => _hasActiveFilters;

    /// <inheritdoc cref="ActiveFilterCountProperty"/>
    public int ActiveFilterCount => _activeFilterCount;

    // Parts.
    private Border? _filtersColumn;
    private SearchResultsFilters? _filtersView;
    private AvaloniaButton? _filtersButton;
    private SpringSizeDecorator? _chipsHost;
    private SearchResultsChipBar? _chipBar;

    // The narrow layout's sheet (created when first opened).
    private SearchResultsFilters? _sheetFilters;
    private SearchResultsSurface? _filtersSurface;
    private ModalPresence? _filtersPresence;

    // Filter objects watched for changes.
    private readonly HashSet<SearchFilter> _watchedFilters = new();
    private readonly HashSet<SearchFilterValue> _watchedValues = new();
    private readonly HashSet<SearchValueFilter> _watchedLists = new();
    private bool _plansDirty;
    private bool _compiling;
    private readonly List<SearchChipSpec> _chipSpecs = new();

    #region 测试与诊断

    /// <summary>The filters column content (tests).</summary>
    internal SearchResultsFilters? FiltersView => _filtersView;

    /// <summary>The filters of the narrow sheet, once it was opened (tests).</summary>
    internal SearchResultsFilters? SheetFiltersView => _sheetFilters;

    /// <summary>The narrow sheet's modal (tests).</summary>
    internal ModalPresence? FiltersPresence => _filtersPresence;

    /// <summary>The active filters bar (tests).</summary>
    internal SearchResultsChipBar? ChipBar => _chipBar;

    /// <summary>The height animation around the bar (tests).</summary>
    internal SpringSizeDecorator? ChipsHost => _chipsHost;

    /// <summary>The button that opens the filters on narrow screens (tests).</summary>
    internal AvaloniaButton? FiltersButton => _filtersButton;

    #endregion

    private void AttachFilters()
    {
        if (_filtersView is not null)
        {
            _filtersView.ClearAllRequested += OnClearAllRequested;
            _filtersView.RangeChanged += OnRangeViewChanged;
        }
        if (_chipBar is not null)
        {
            _chipBar.ChipRemoved += OnChipRemoved;
            _chipBar.ClearAllRequested += OnClearAllRequested;
        }
        if (_filtersButton is not null)
        {
            _filtersButton.Click += OnFiltersButtonClick;
        }
    }

    private void DetachFilters()
    {
        if (_filtersView is not null)
        {
            _filtersView.ClearAllRequested -= OnClearAllRequested;
            _filtersView.RangeChanged -= OnRangeViewChanged;
        }
        if (_chipBar is not null)
        {
            _chipBar.ChipRemoved -= OnChipRemoved;
            _chipBar.ClearAllRequested -= OnClearAllRequested;
        }
        if (_filtersButton is not null)
        {
            _filtersButton.Click -= OnFiltersButtonClick;
        }
        _filtersPresence?.Dispose();
        _filtersPresence = null;
        _filtersSurface = null;
        _sheetFilters = null;
    }

    #region 筛选模型

    private void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_compiling)
        {
            return;
        }
        _plansDirty = true;
        _pageLimit = Math.Max(1, PageSize);
        Update(reveal: true, resetCursor: true, recompile: true);
        RaiseSearchChanged(SearchChangeReason.Filters);
    }

    /// <summary>Lets go of every filter and value (the control left the window: the filters may outlive it).</summary>
    private void UnsubscribeFilterValues()
    {
        foreach (var filter in _watchedFilters)
        {
            filter.PropertyChanged -= OnFilterPropertyChanged;
        }
        foreach (var value in _watchedValues)
        {
            value.PropertyChanged -= OnFilterValuePropertyChanged;
        }
        foreach (var list in _watchedLists)
        {
            list.Values.CollectionChanged -= OnFilterValuesChanged;
        }
        _watchedFilters.Clear();
        _watchedValues.Clear();
        _watchedLists.Clear();
    }

    /// <summary>Watches every filter and value for the changes that matter (ticks, ranges, definitions).</summary>
    private void SubscribeFilterValues()
    {
        UnsubscribeFilterValues();
        foreach (var filter in Filters)
        {
            if (_watchedFilters.Add(filter))
            {
                filter.PropertyChanged += OnFilterPropertyChanged;
            }
            if (filter is SearchValueFilter list)
            {
                if (_watchedLists.Add(list))
                {
                    list.Values.CollectionChanged += OnFilterValuesChanged;
                }
                foreach (var value in list.Values)
                {
                    if (_watchedValues.Add(value))
                    {
                        value.PropertyChanged += OnFilterValuePropertyChanged;
                    }
                }
            }
        }
    }

    private void OnFilterValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_compiling)
        {
            return;
        }
        _plansDirty = true;
        Update(reveal: false, resetCursor: false, recompile: true);
    }

    private void OnFilterValuePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_compiling)
        {
            return;
        }
        if (e.Property == SearchFilterValue.IsSelectedProperty)
        {
            OnSelectionChanged();
        }
        else if (e.Property == SearchFilterValue.ValueProperty)
        {
            _plansDirty = true;
            Update(reveal: false, resetCursor: false, recompile: true);
        }
        else if (e.Property == SearchFilterValue.LabelProperty)
        {
            UpdateChips();
        }
    }

    private void OnFilterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_compiling)
        {
            return;
        }
        if (e.Property == SearchDateFilter.StartProperty || e.Property == SearchDateFilter.EndProperty)
        {
            _filtersView?.SyncRanges();
            _sheetFilters?.SyncRanges();
            OnSelectionChanged();
        }
        else if (e.Property == SearchFilter.KeyProperty || e.Property == SearchDateFilter.MinimumProperty ||
                 e.Property == SearchDateFilter.MaximumProperty || e.Property == SearchDateFilter.AppliesToProperty)
        {
            _plansDirty = true;
            Update(reveal: false, resetCursor: false, recompile: true);
        }
        else if (e.Property == SearchFilter.TitleProperty || e.Property == SearchFilter.CaptionProperty)
        {
            RebuildFilterViews(force: true);
            UpdateChips();
        }
    }

    private void OnSelectionChanged()
    {
        _pageLimit = Math.Max(1, PageSize);
        Update(reveal: true, resetCursor: true);
        RaiseSearchChanged(SearchChangeReason.Filters);
    }

    #endregion

    #region 视图

    private void OnClearAllRequested(object? sender, EventArgs e) => ClearFilters();

    private void OnRangeViewChanged(object? sender, EventArgs e)
    {
        // The filter already holds the range; the sheet's slider follows it.
        _sheetFilters?.SyncRanges();
        if (!ReferenceEquals(sender, _filtersView))
        {
            _filtersView?.SyncRanges();
        }
    }

    /// <summary>Gives the column (and the sheet) the filters, with each date range's span and its caption.</summary>
    private void RebuildFilterViews(bool force)
    {
        var ranges = new List<(SearchFilter Filter, int MinDay, int MaxDay)>();
        foreach (var plan in _engine.Plans)
        {
            if (plan.IsDate)
            {
                ranges.Add((plan.Source, plan.MinDay, plan.MaxDay));
            }
        }
        var any = false;
        foreach (var filter in Filters)
        {
            if (filter is SearchValueFilter { Values.Count: > 0 })
            {
                any = true;
            }
            else if (filter is SearchDateFilter date)
            {
                foreach (var range in ranges)
                {
                    if (ReferenceEquals(range.Filter, date) && range.MaxDay > range.MinDay)
                    {
                        any = true;
                    }
                }
            }
        }
        PseudoClasses.Set(NoFiltersPseudoClass, !any);
        _filtersView?.SetFilters(Filters, ranges, CaptionOf, DateFormat, force);
        _sheetFilters?.SetFilters(Filters, ranges, CaptionOf, DateFormat, force);
    }

    /// <summary>The caption under a date filter: its own, else "Applies to docs and files" from <see cref="SearchDateFilter.AppliesTo"/>.</summary>
    private string? CaptionOf(SearchDateFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Caption))
        {
            return filter.Caption;
        }
        var kinds = filter.AppliesToKinds().Select(k => k.ToLower(CultureInfo.CurrentCulture)).ToList();
        if (kinds.Count == 0)
        {
            return null;
        }
        var list = kinds.Count == 1
            ? kinds[0]
            : string.Join(", ", kinds.Take(kinds.Count - 1)) + " " + AndText + " " + kinds[^1];
        return Format(AppliesToFormat, list);
    }

    private string FormatDateRange(int startDay, int endDay)
    {
        var culture = CultureInfo.CurrentCulture;
        var first = SearchResultsEngine.DateOfDay(startDay, TimeSpan.Zero).ToString(DateFormat, culture);
        var last = SearchResultsEngine.DateOfDay(endDay, TimeSpan.Zero).ToString(DateFormat, culture);
        return string.Equals(first, last, StringComparison.Ordinal) ? first : first + " – " + last;
    }

    /// <summary>Rebuilds the active filters bar (chips for ticked values and narrowed ranges) and what depends on it.</summary>
    private void UpdateChips()
    {
        _chipSpecs.Clear();
        foreach (var filter in Filters)
        {
            switch (filter)
            {
                case SearchValueFilter values:
                    foreach (var value in values.Values)
                    {
                        if (value.IsSelected)
                        {
                            _chipSpecs.Add(new SearchChipSpec(values.Title, value.DisplayLabel, value));
                        }
                    }
                    break;
                case SearchDateFilter date:
                    foreach (var plan in _engine.Plans)
                    {
                        if (ReferenceEquals(plan.Source, date) && plan.DateActive)
                        {
                            _chipSpecs.Add(new SearchChipSpec(date.Title, FormatDateRange(plan.StartDay, plan.EndDay), date));
                        }
                    }
                    break;
            }
        }
        var active = _chipSpecs.Count > 0;
        _chipBar?.SetChips(_chipSpecs);
        if (_chipsHost is not null && _chipsHost.IsExpanded != active)
        {
            _chipsHost.IsExpanded = active;
        }
        var wasActive = _hasActiveFilters;
        if (_activeFilterCount != _chipSpecs.Count)
        {
            SetAndRaise(ActiveFilterCountProperty, ref _activeFilterCount, _chipSpecs.Count);
        }
        if (wasActive != active)
        {
            SetAndRaise(HasActiveFiltersProperty, ref _hasActiveFilters, active);
            PseudoClasses.Set(FilteredPseudoClass, active);
        }
        // The views are created after the first search: they take the state whenever it is pushed.
        if (_filtersView is not null && _filtersView.HasActiveFilters != active)
        {
            _filtersView.HasActiveFilters = active;
        }
        if (_sheetFilters is not null && _sheetFilters.HasActiveFilters != active)
        {
            _sheetFilters.HasActiveFilters = active;
        }
        if (wasActive != active && ResultCount == 0)
        {
            UpdateEmpty();
        }
    }

    private void OnChipRemoved(object? sender, object token)
    {
        switch (token)
        {
            case SearchFilterValue value:
                value.IsSelected = false;
                break;
            case SearchFilter filter:
                filter.Clear();
                break;
        }
    }

    #endregion

    #region 窄屏的筛选面板

    private void OnFiltersButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenFiltersSheet();
    }

    /// <summary>Opens the filters in a bottom sheet (what the "Filters" button of the narrow layout does).</summary>
    public void OpenFiltersSheet()
    {
        if (_filtersPresence is { IsOpen: true } || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        if (_filtersPresence is null)
        {
            _sheetFilters = new SearchResultsFilters { IsTitleVisible = false, Title = FiltersTitle, ClearAllText = ClearAllText, HasActiveFilters = _hasActiveFilters };
            _sheetFilters.ClearAllRequested += OnClearAllRequested;
            _sheetFilters.RangeChanged += OnRangeViewChanged;
            _filtersSurface = new SearchResultsSurface
            {
                IsSheet = true,
                Title = FiltersTitle,
                CloseText = CloseText,
                Content = _sheetFilters,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
            };
            ((Avalonia.Controls.ISetLogicalParent)_filtersSurface).SetParent(this);
            _filtersPresence = new ModalPresence(_filtersSurface, this)
            {
                Entrance = ModalEntrance.Slide,
                Side = ModalSide.Bottom,
                InitialFocus = _filtersSurface
            };
            _filtersSurface.CloseRequested += (_, _) => _filtersPresence?.Close(DismissReason.CloseButton);
            RebuildFilterViews(force: true);
        }
        _filtersPresence.Open();
    }

    #endregion
}

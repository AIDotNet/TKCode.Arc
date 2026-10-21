using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>One ticked-value chip or range chip of the active filters bar.</summary>
internal sealed record SearchChipSpec(string Group, string Text, object Token);

/// <summary>A chip of the active filters bar: the filter title, the chosen value and a remove button.</summary>
[TemplatePart(RemovePart, typeof(AvaloniaButton))]
internal sealed class SearchResultsChip : TemplatedControl
{
    public const string RemovePart = "PART_Remove";

    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<SearchResultsChip, string?>(nameof(Group));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchResultsChip, string?>(nameof(Text));

    public static readonly StyledProperty<string> RemoveTextProperty =
        AvaloniaProperty.Register<SearchResultsChip, string>(nameof(RemoveText), "Remove filter");

    private AvaloniaButton? _remove;

    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string RemoveText { get => GetValue(RemoveTextProperty); set => SetValue(RemoveTextProperty, value); }

    /// <summary>The filter value or range the chip stands for.</summary>
    public object? Token { get; set; }

    /// <summary>Raised when the remove button is pressed.</summary>
    public event EventHandler? Removed;

    static SearchResultsChip()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsChip>(false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        base.OnApplyTemplate(e);
        _remove = e.NameScope.Find<AvaloniaButton>(RemovePart);
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
            UpdateName();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GroupProperty || change.Property == TextProperty || change.Property == RemoveTextProperty)
        {
            UpdateName();
        }
    }

    private void UpdateName()
    {
        var name = $"{Group} {Text}".Trim();
        AutomationProperties.SetName(this, name);
        if (_remove is not null)
        {
            AutomationProperties.SetName(_remove, $"{RemoveText}: {name}");
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Removed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The rounded bar under the results header that lists the active filters as chips with a "Clear all" link at the
/// right. Chips are pooled and rebuilt only when the set of filters changes.
/// </summary>
[TemplatePart(ChipsPart, typeof(WrapPanel))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
internal sealed class SearchResultsChipBar : TemplatedControl
{
    public const string ChipsPart = "PART_Chips";
    public const string ClearPart = "PART_Clear";

    public static readonly StyledProperty<string> ClearAllTextProperty =
        AvaloniaProperty.Register<SearchResultsChipBar, string>(nameof(ClearAllText), "Clear all");

    public static readonly StyledProperty<string> RemoveTextProperty =
        AvaloniaProperty.Register<SearchResultsChipBar, string>(nameof(RemoveText), "Remove filter");

    private readonly List<SearchResultsChip> _pool = new();
    private readonly List<SearchChipSpec> _specs = new();
    private WrapPanel? _chips;
    private AvaloniaButton? _clear;

    public string ClearAllText { get => GetValue(ClearAllTextProperty); set => SetValue(ClearAllTextProperty, value); }

    public string RemoveText { get => GetValue(RemoveTextProperty); set => SetValue(RemoveTextProperty, value); }

    /// <summary>Raised when a chip's remove button is pressed.</summary>
    public event EventHandler<object>? ChipRemoved;

    /// <summary>Raised when "Clear all" is pressed.</summary>
    public event EventHandler? ClearAllRequested;

    /// <summary>The chips shown (tests).</summary>
    internal IReadOnlyList<SearchResultsChip> Chips => _pool.Where(c => c.Parent == _chips).ToList();

    static SearchResultsChipBar()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsChipBar>(false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        base.OnApplyTemplate(e);
        _chips = e.NameScope.Find<WrapPanel>(ChipsPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        var specs = _specs.ToList();
        _specs.Clear();
        SetChips(specs);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RemoveTextProperty)
        {
            foreach (var chip in _pool)
            {
                chip.RemoveText = RemoveText;
            }
        }
    }

    /// <summary>Shows <paramref name="specs"/> as chips; does nothing when they are the chips already shown.</summary>
    public void SetChips(IReadOnlyList<SearchChipSpec> specs)
    {
        var same = specs.Count == _specs.Count;
        for (var i = 0; same && i < specs.Count; i++)
        {
            same = specs[i].Group == _specs[i].Group && specs[i].Text == _specs[i].Text && ReferenceEquals(specs[i].Token, _specs[i].Token);
        }
        if (same)
        {
            return;
        }
        _specs.Clear();
        _specs.AddRange(specs);
        if (_chips is null)
        {
            return;
        }
        _chips.Children.Clear();
        for (var i = 0; i < specs.Count; i++)
        {
            if (i >= _pool.Count)
            {
                var created = new SearchResultsChip { RemoveText = RemoveText };
                created.Removed += OnChipRemoved;
                _pool.Add(created);
            }
            var chip = _pool[i];
            chip.Group = specs[i].Group;
            chip.Text = specs[i].Text;
            chip.Token = specs[i].Token;
            _chips.Children.Add(chip);
        }
    }

    private void OnChipRemoved(object? sender, EventArgs e)
    {
        if (sender is SearchResultsChip { Token: { } token })
        {
            ChipRemoved?.Invoke(this, token);
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearAllRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>One checkbox of a <see cref="SearchValueFilter"/>: the label, and the count that follows the query and the other filters.</summary>
[TemplatePart(CheckPart, typeof(Checkbox))]
[PseudoClasses(DimmedPseudoClass)]
internal sealed class SearchResultsFilterRow : TemplatedControl
{
    public const string CheckPart = "PART_Check";
    public const string DimmedPseudoClass = ":dimmed";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SearchResultsFilterRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<SearchResultsFilterRow, string?>(nameof(CountText));

    private Checkbox? _check;
    private SearchFilterValue? _value;
    private bool _syncing;

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    internal Checkbox? Check => _check;

    /// <summary>The value the row edits. The row follows its label, count and tick and writes the tick.</summary>
    public SearchFilterValue? Value
    {
        get => _value;
        set
        {
            if (ReferenceEquals(_value, value))
            {
                return;
            }
            Unhook();
            _value = value;
            if (VisualRoot is not null)
            {
                Hook();
            }
            Sync();
        }
    }

    static SearchResultsFilterRow()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsFilterRow>(false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_check is not null)
        {
            _check.IsCheckedChanged -= OnCheckChanged;
        }
        base.OnApplyTemplate(e);
        _check = e.NameScope.Find<Checkbox>(CheckPart);
        if (_check is not null)
        {
            _check.IsCheckedChanged += OnCheckChanged;
        }
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Hook();
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unhook();
    }

    private void Hook()
    {
        if (_value is not null)
        {
            _value.PropertyChanged -= OnValueChanged;
            _value.PropertyChanged += OnValueChanged;
        }
    }

    private void Unhook()
    {
        if (_value is not null)
        {
            _value.PropertyChanged -= OnValueChanged;
        }
    }

    private void OnValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SearchFilterValue.IsSelectedProperty || e.Property == SearchFilterValue.CountProperty ||
            e.Property == SearchFilterValue.LabelProperty || e.Property == SearchFilterValue.ValueProperty)
        {
            Sync();
        }
    }

    private void OnCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _value is null || _check is null)
        {
            return;
        }
        _value.IsSelected = _check.IsChecked == true;
    }

    private void Sync()
    {
        if (_value is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            Label = _value.DisplayLabel;
            CountText = _value.Count.ToString("N0", CultureInfo.CurrentCulture);
            // A value with no results keeps its place but cannot be ticked (a ticked one can always be unticked).
            var dimmed = _value.Count == 0 && !_value.IsSelected;
            PseudoClasses.Set(DimmedPseudoClass, dimmed);
            if (_check is not null)
            {
                if (_check.IsChecked != _value.IsSelected)
                {
                    _check.IsChecked = _value.IsSelected;
                }
                _check.IsEnabled = !dimmed;
                AutomationProperties.SetName(_check, $"{_value.DisplayLabel}, {CountText}");
            }
        }
        finally
        {
            _syncing = false;
        }
    }
}

/// <summary>A titled group of <see cref="SearchResultsFilterRow"/>s for one <see cref="SearchValueFilter"/>.</summary>
[TemplatePart(RowsPart, typeof(Panel))]
internal sealed class SearchResultsFilterGroup : TemplatedControl
{
    public const string RowsPart = "PART_Rows";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SearchResultsFilterGroup, string?>(nameof(Title));

    private Panel? _rows;
    private SearchValueFilter? _filter;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    internal IReadOnlyList<SearchResultsFilterRow> Rows => _rows?.Children.OfType<SearchResultsFilterRow>().ToList() ?? new List<SearchResultsFilterRow>();

    static SearchResultsFilterGroup()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsFilterGroup>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultsFilterGroup>(AutomationControlType.Group);
    }

    public void SetFilter(SearchValueFilter filter)
    {
        _filter = filter;
        Title = filter.Title;
        AutomationProperties.SetName(this, filter.Title);
        Rebuild();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rows = e.NameScope.Find<Panel>(RowsPart);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_rows is null || _filter is null)
        {
            return;
        }
        _rows.Children.Clear();
        foreach (var value in _filter.Values)
        {
            _rows.Children.Add(new SearchResultsFilterRow { Value = value });
        }
    }
}

/// <summary>
/// The date range filter: the title with the chosen range ("Edited Sep 2025 – Sep 2026"), a two-thumb
/// <see cref="Slider"/> and a caption. The slider counts days; the range snaps to whole days.
/// </summary>
[TemplatePart(SliderPart, typeof(Slider))]
internal sealed class SearchResultsDateRange : TemplatedControl
{
    public const string SliderPart = "PART_Slider";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SearchResultsDateRange, string?>(nameof(Title));

    public static readonly StyledProperty<string?> RangeTextProperty =
        AvaloniaProperty.Register<SearchResultsDateRange, string?>(nameof(RangeText));

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<SearchResultsDateRange, string?>(nameof(Caption));

    public static readonly StyledProperty<string> RangeFormatProperty =
        AvaloniaProperty.Register<SearchResultsDateRange, string>(nameof(RangeFormat), "{0} – {1}");

    public static readonly StyledProperty<string> DateFormatProperty =
        AvaloniaProperty.Register<SearchResultsDateRange, string>(nameof(DateFormat), "MMM yyyy");

    private Slider? _slider;
    private SearchDateFilter? _filter;
    private int _minDay;
    private int _maxDay;
    private bool _syncing;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? RangeText { get => GetValue(RangeTextProperty); private set => SetValue(RangeTextProperty, value); }

    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    public string RangeFormat { get => GetValue(RangeFormatProperty); set => SetValue(RangeFormatProperty, value); }

    public string DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <summary>The slider (tests).</summary>
    internal Slider? SliderControl => _slider;

    /// <summary>Raised when the thumbs moved the range (the filter already holds it).</summary>
    public event EventHandler? RangeChanged;

    static SearchResultsDateRange()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsDateRange>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultsDateRange>(AutomationControlType.Group);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_slider is not null)
        {
            _slider.RemoveHandler(Slider.RangeChangedEvent, OnSliderRange);
        }
        base.OnApplyTemplate(e);
        _slider = e.NameScope.Find<Slider>(SliderPart);
        if (_slider is not null)
        {
            _slider.AddHandler(Slider.RangeChangedEvent, OnSliderRange);
            if (_filter is not null)
            {
                _slider.LowerThumbName = _filter.Title + " start";
                _slider.UpperThumbName = _filter.Title + " end";
            }
        }
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DateFormatProperty || change.Property == RangeFormatProperty)
        {
            UpdateText();
        }
    }

    /// <summary>Follows <paramref name="filter"/>, whose range spans the days <paramref name="minDay"/> to <paramref name="maxDay"/>.</summary>
    public void Configure(SearchDateFilter filter, int minDay, int maxDay, string? caption)
    {
        _filter = filter;
        _minDay = minDay;
        _maxDay = maxDay;
        Title = filter.Title;
        if (_slider is not null)
        {
            _slider.LowerThumbName = filter.Title + " start";
            _slider.UpperThumbName = filter.Title + " end";
        }
        Caption = caption;
        AutomationProperties.SetName(this, filter.Title);
        Sync();
    }

    /// <summary>Reads the range back from the filter (it was changed from the outside).</summary>
    public void Sync()
    {
        if (_filter is null || _slider is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            if (_slider.Maximum != _maxDay)
            {
                _slider.Maximum = _maxDay;
            }
            if (_slider.Minimum != _minDay)
            {
                _slider.Minimum = _minDay;
            }
            var start = _filter.Start is { } s ? Math.Clamp(SearchResultsEngine.DayOf(s), _minDay, _maxDay) : _minDay;
            var end = _filter.End is { } t ? Math.Clamp(SearchResultsEngine.DayOf(t), _minDay, _maxDay) : _maxDay;
            if (_slider.LowerValue != start)
            {
                _slider.LowerValue = start;
            }
            if (_slider.UpperValue != end)
            {
                _slider.UpperValue = end;
            }
        }
        finally
        {
            _syncing = false;
        }
        UpdateText();
    }

    private void UpdateText()
    {
        if (_slider is null)
        {
            return;
        }
        var culture = CultureInfo.CurrentCulture;
        var first = SearchResultsEngine.DateOfDay((int)_slider.LowerValue, TimeSpan.Zero).ToString(DateFormat, culture);
        var last = SearchResultsEngine.DateOfDay((int)_slider.UpperValue, TimeSpan.Zero).ToString(DateFormat, culture);
        RangeText = string.Equals(first, last, StringComparison.Ordinal) ? first : string.Format(culture, RangeFormat, first, last);
    }

    private void OnSliderRange(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _filter is null || _slider is null)
        {
            return;
        }
        var lower = (int)Math.Round(_slider.LowerValue);
        var upper = (int)Math.Round(_slider.UpperValue);
        // The ends of the range mean "no limit", so the chip and the clear link know the filter is off.
        _filter.Start = lower <= _minDay ? null : SearchResultsEngine.DateOfDay(lower, TimeSpan.Zero);
        _filter.End = upper >= _maxDay ? null : SearchResultsEngine.DateOfDay(upper, TimeSpan.Zero);
        UpdateText();
        RangeChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The filters column: a title with "Clear all", then one group per filter (checkbox values with counts, or a date range).
/// It only shows and edits the filter objects; <see cref="SearchResults"/> reacts to their changes.
/// </summary>
[TemplatePart(GroupsPart, typeof(Panel))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
internal sealed class SearchResultsFilters : TemplatedControl
{
    public const string GroupsPart = "PART_Groups";
    public const string ClearPart = "PART_Clear";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SearchResultsFilters, string>(nameof(Title), "Filters");

    public static readonly StyledProperty<string> ClearAllTextProperty =
        AvaloniaProperty.Register<SearchResultsFilters, string>(nameof(ClearAllText), "Clear all");

    public static readonly StyledProperty<bool> HasActiveFiltersProperty =
        AvaloniaProperty.Register<SearchResultsFilters, bool>(nameof(HasActiveFilters));

    public static readonly StyledProperty<bool> IsTitleVisibleProperty =
        AvaloniaProperty.Register<SearchResultsFilters, bool>(nameof(IsTitleVisible), true);

    private Panel? _groups;
    private AvaloniaButton? _clear;
    private IReadOnlyList<(SearchFilter Filter, int MinDay, int MaxDay)> _ranges = [];
    private IReadOnlyList<SearchFilter> _filters = [];
    private Func<SearchDateFilter, string?>? _caption;
    private List<object?> _signature = new();
    private string _dateFormat = "MMM yyyy";

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string ClearAllText { get => GetValue(ClearAllTextProperty); set => SetValue(ClearAllTextProperty, value); }

    /// <summary>True while a filter narrows the results (shows "Clear all").</summary>
    public bool HasActiveFilters { get => GetValue(HasActiveFiltersProperty); set => SetValue(HasActiveFiltersProperty, value); }

    /// <summary>Whether the "Filters" title is shown (the sheet has its own).</summary>
    public bool IsTitleVisible { get => GetValue(IsTitleVisibleProperty); set => SetValue(IsTitleVisibleProperty, value); }

    /// <summary>Raised when "Clear all" is pressed.</summary>
    public event EventHandler? ClearAllRequested;

    /// <summary>Raised when a date range was moved.</summary>
    public event EventHandler? RangeChanged;

    internal Panel? GroupsPanel => _groups;

    internal IEnumerable<SearchResultsDateRange> DateRanges => _groups?.Children.OfType<SearchResultsDateRange>() ?? [];

    internal IEnumerable<SearchResultsFilterGroup> Groups => _groups?.Children.OfType<SearchResultsFilterGroup>() ?? [];

    static SearchResultsFilters()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsFilters>(false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        base.OnApplyTemplate(e);
        _groups = e.NameScope.Find<Panel>(GroupsPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        Rebuild();
    }

    /// <summary>
    /// Shows <paramref name="filters"/>. <paramref name="ranges"/> gives each date filter its span of days (a filter
    /// without a usable span is left out), <paramref name="caption"/> its caption and <paramref name="dateFormat"/> the
    /// way its ends are written. Nothing is rebuilt when the groups would come out the same (unless <paramref name="force"/>).
    /// </summary>
    public void SetFilters(IReadOnlyList<SearchFilter> filters, IReadOnlyList<(SearchFilter Filter, int MinDay, int MaxDay)> ranges,
                           Func<SearchDateFilter, string?> caption, string dateFormat, bool force)
    {
        var signature = new List<object?>();
        foreach (var filter in filters)
        {
            signature.Add(filter);
            signature.Add(filter.Title);
            switch (filter)
            {
                case SearchValueFilter values:
                    signature.Add(values.Values.Count);
                    break;
                case SearchDateFilter date:
                    foreach (var range in ranges)
                    {
                        if (ReferenceEquals(range.Filter, date))
                        {
                            signature.Add(range.MinDay);
                            signature.Add(range.MaxDay);
                        }
                    }
                    signature.Add(caption(date));
                    break;
            }
        }
        signature.Add(dateFormat);
        var same = !force && signature.Count == _signature.Count;
        for (var i = 0; same && i < signature.Count; i++)
        {
            same = Equals(signature[i], _signature[i]);
        }
        _filters = filters;
        _ranges = ranges;
        _caption = caption;
        _dateFormat = dateFormat;
        if (same)
        {
            return;
        }
        _signature = signature;
        Rebuild();
    }

    /// <summary>Reads every date range back from its filter.</summary>
    public void SyncRanges()
    {
        foreach (var range in DateRanges)
        {
            range.Sync();
        }
    }

    private void Rebuild()
    {
        if (_groups is null)
        {
            return;
        }
        _groups.Children.Clear();
        foreach (var filter in _filters)
        {
            switch (filter)
            {
                case SearchValueFilter values when values.Values.Count > 0:
                    var group = new SearchResultsFilterGroup();
                    group.SetFilter(values);
                    _groups.Children.Add(group);
                    break;
                case SearchDateFilter date:
                    foreach (var range in _ranges)
                    {
                        if (ReferenceEquals(range.Filter, date) && range.MaxDay > range.MinDay)
                        {
                            var view = new SearchResultsDateRange { DateFormat = _dateFormat };
                            view.Configure(date, range.MinDay, range.MaxDay, _caption?.Invoke(date));
                            view.RangeChanged += (_, _) => RangeChanged?.Invoke(this, EventArgs.Empty);
                            _groups.Children.Add(view);
                        }
                    }
                    break;
            }
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearAllRequested?.Invoke(this, EventArgs.Empty);
    }
}

using System.Collections;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc multi-select: pick several values from a fixed list while the field stays one line. Picked values show as
/// chips in the trigger (their slots spring open, the chips grow in out of a blur); past
/// <see cref="MaxVisibleChips"/> they fold into a "+N" chip whose count rolls. Each option's check draws itself.
/// A clear button pops in while anything is selected.
/// <para>Keyboard: Enter or Space opens and closes; arrows open and move the highlight (wrapping, skipping disabled
/// options); Enter toggles the highlighted option; Escape closes. <see cref="IsSearchable"/> adds a search field to
/// the menu (an addition: Arc's menu has none).</para>
/// </summary>
[TemplatePart(TriggerPart, typeof(Border))]
[TemplatePart(ChipsPart, typeof(MultiSelectChipRow))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
[TemplatePart(ClearHostPart, typeof(PresenceHost))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(OptionsPart, typeof(SelectOptionView))]
[TemplatePart(SearchHostPart, typeof(Decorator))]
[TemplatePart(EmptyHostPart, typeof(PresenceHost))]
[PseudoClasses(OpenPseudoClass, HasSelectionPseudoClass, PressedPseudoClass, SearchablePseudoClass)]
public class MultiSelect : SelectingItemsControl, ISelectOptionHost, ISelectFieldAutomation
{
    public const string TriggerPart = "PART_Trigger";
    public const string ChipsPart = "PART_Chips";
    public const string ClearPart = "PART_Clear";
    public const string ClearHostPart = "PART_ClearHost";
    public const string PopupPart = "PART_Popup";
    public const string SurfacePart = "PART_Surface";
    public const string ScrollPart = "PART_Scroll";
    public const string OptionsPart = "PART_Options";
    public const string SearchHostPart = "PART_SearchHost";
    public const string EmptyHostPart = "PART_EmptyHost";
    public const string AnnouncerPart = "PART_Announcer";
    public const string OpenPseudoClass = ":open";
    public const string HasSelectionPseudoClass = ":has-selection";
    public const string PressedPseudoClass = ":pressed";
    public const string SearchablePseudoClass = ":searchable";

    #region 公共属性定义

    public static readonly StyledProperty<string?> LabelProperty = Select.LabelProperty.AddOwner<MultiSelect>();

    public static readonly StyledProperty<string?> DescriptionProperty = Select.DescriptionProperty.AddOwner<MultiSelect>();

    public static readonly StyledProperty<string?> PlaceholderProperty =
        Select.PlaceholderProperty.AddOwner<MultiSelect>(new StyledPropertyMetadata<string?>("Select options"));

    public static readonly StyledProperty<bool> IsOpenProperty = Select.IsOpenProperty.AddOwner<MultiSelect>();

    /// <summary>Defines the <see cref="MaxVisibleChips"/> property.</summary>
    public static readonly StyledProperty<int> MaxVisibleChipsProperty =
        AvaloniaProperty.Register<MultiSelect, int>(nameof(MaxVisibleChips), 2);

    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        Select.MaxDropDownHeightProperty.AddOwner<MultiSelect>(new StyledPropertyMetadata<double>(double.PositiveInfinity));

    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty = Select.TextSelectorProperty.AddOwner<MultiSelect>();

    public static readonly StyledProperty<Func<object?, object?>?> ValueSelectorProperty = Select.ValueSelectorProperty.AddOwner<MultiSelect>();

    public static readonly StyledProperty<Func<object?, bool>?> DisabledSelectorProperty = Select.DisabledSelectorProperty.AddOwner<MultiSelect>();

    /// <summary>Defines the <see cref="IsSearchable"/> property.</summary>
    public static readonly StyledProperty<bool> IsSearchableProperty =
        AvaloniaProperty.Register<MultiSelect, bool>(nameof(IsSearchable));

    /// <summary>Defines the <see cref="SearchPlaceholder"/> property.</summary>
    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<MultiSelect, string?>(nameof(SearchPlaceholder), "Search");

    public static readonly StyledProperty<Func<object?, string, bool>?> FilterPredicateProperty =
        Combobox.FilterPredicateProperty.AddOwner<MultiSelect>();

    public static readonly StyledProperty<string?> EmptyTextProperty = Combobox.EmptyTextProperty.AddOwner<MultiSelect>();

    /// <summary>Defines the <see cref="SelectedItems"/> property.</summary>
    public new static readonly DirectProperty<SelectingItemsControl, IList?> SelectedItemsProperty =
        SelectingItemsControl.SelectedItemsProperty;

    /// <summary>Defines the <see cref="SelectedValues"/> property.</summary>
    public static readonly DirectProperty<MultiSelect, IReadOnlyList<object?>> SelectedValuesProperty =
        AvaloniaProperty.RegisterDirect<MultiSelect, IReadOnlyList<object?>>(nameof(SelectedValues), o => o.SelectedValues);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Shown while nothing is selected (Arc: "Select options").</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Chips shown before the rest fold into "+N" (Arc: 2).</summary>
    public int MaxVisibleChips { get => GetValue(MaxVisibleChipsProperty); set => SetValue(MaxVisibleChipsProperty, value); }

    /// <summary>Tallest the menu gets before it scrolls. Arc's menu has no limit (the screen bounds it).</summary>
    public double MaxDropDownHeight { get => GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }

    /// <inheritdoc cref="Select.TextSelector"/>
    public Func<object?, string?>? TextSelector { get => GetValue(TextSelectorProperty); set => SetValue(TextSelectorProperty, value); }

    /// <inheritdoc cref="Select.ValueSelector"/>
    public Func<object?, object?>? ValueSelector { get => GetValue(ValueSelectorProperty); set => SetValue(ValueSelectorProperty, value); }

    /// <inheritdoc cref="Select.DisabledSelector"/>
    public Func<object?, bool>? DisabledSelector { get => GetValue(DisabledSelectorProperty); set => SetValue(DisabledSelectorProperty, value); }

    /// <summary>Adds a search field at the top of the menu that filters the options.</summary>
    public bool IsSearchable { get => GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }

    public string? SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <inheritdoc cref="Combobox.FilterPredicate"/>
    public Func<object?, string, bool>? FilterPredicate { get => GetValue(FilterPredicateProperty); set => SetValue(FilterPredicateProperty, value); }

    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>The selected items (bind a list to set them from a view model).</summary>
    public new IList? SelectedItems
    {
        get => base.SelectedItems;
        set => base.SelectedItems = value;
    }

    /// <summary>The selection model (multiple + toggle).</summary>
    public new ISelectionModel Selection
    {
        get => base.Selection;
        set => base.Selection = value;
    }

    /// <summary>Values of the selected options in the order they were picked.</summary>
    public IReadOnlyList<object?> SelectedValues
    {
        get => _selectedValues;
        private set => SetAndRaise(SelectedValuesProperty, ref _selectedValues, value);
    }

    #endregion

    private readonly SelectEntrySource _entries;
    private readonly List<int> _visible = new();
    private readonly List<object?> _order = new();
    private IReadOnlyList<object?> _selectedValues = Array.Empty<object?>();
    private string _query = string.Empty;
    private Border? _trigger;
    private MultiSelectChipRow? _chips;
    private AvaloniaButton? _clear;
    private PresenceHost? _clearHost;
    private ArcPopup? _popup;
    private Border? _surface;
    private ScrollViewer? _scroll;
    private SelectOptionView? _options;
    private Decorator? _searchHost;
    private TextBox? _search;
    private PresenceHost? _emptyHost;
    private TextBlock? _announcer;
    private bool _syncingSearch;

    static MultiSelect()
    {
        FocusableProperty.OverrideDefaultValue<MultiSelect>(true);
        SelectionModeProperty.OverrideDefaultValue<MultiSelect>(SelectionMode.Multiple | SelectionMode.Toggle);
    }

    public MultiSelect()
    {
        _entries = new SelectEntrySource(this, () => TextSelector, () => DisabledSelector);
        _entries.Changed += (_, _) => OnEntriesChanged();
        SelectedValueBinding = SelectValueConverter.CreateBinding(() => ValueSelector);
        SelectionChanged += OnOwnSelectionChanged;
    }

    SelectEntrySource ISelectOptionHost.Entries => _entries;
    string? ISelectFieldAutomation.AutomationLabel => Label;
    string? ISelectFieldAutomation.AutomationDescription => Description;

    string? ISelectFieldAutomation.AutomationValue =>
        _order.Count == 0 ? Placeholder : string.Join(", ", _order.Select(LabelOf));

    bool ISelectFieldAutomation.AutomationIsOpen
    {
        get => IsOpen;
        set => SetCurrentValue(IsOpenProperty, value);
    }

    /// <summary>Selects every enabled option (Arc has no select-all; offered for code).</summary>
    public void SelectAll()
    {
        using var _ = Selection.BatchUpdate();
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries.IsEnabledAt(i))
            {
                Selection.Select(i);
            }
        }
    }

    /// <summary>Clears the selection.</summary>
    public void UnselectAll() => Selection.Clear();

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectFieldAutomationPeer(this, this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
            _trigger.PointerReleased -= OnTriggerReleased;
            _trigger.PointerCaptureLost -= OnTriggerCaptureLost;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        if (_options is not null)
        {
            _options.Host = null;
        }
        if (_search is not null)
        {
            _search.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
            _search.PropertyChanged -= OnSearchPropertyChanged;
            _searchHost!.Child = null;
            _search = null;
        }

        _trigger = e.NameScope.Find<Border>(TriggerPart);
        _chips = e.NameScope.Find<MultiSelectChipRow>(ChipsPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        _clearHost = e.NameScope.Find<PresenceHost>(ClearHostPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _options = e.NameScope.Find<SelectOptionView>(OptionsPart);
        _searchHost = e.NameScope.Find<Decorator>(SearchHostPart);
        _emptyHost = e.NameScope.Find<PresenceHost>(EmptyHostPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);

        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
            _trigger.PointerReleased += OnTriggerReleased;
            _trigger.PointerCaptureLost += OnTriggerCaptureLost;
        }
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _trigger;
            // The trigger and its clear button are one control: pressing either while open is not a press outside.
            _popup.OverlayInputPassThroughElement = _trigger?.Parent as Control ?? _trigger;
            _popup.Closed += OnPopupClosed;
        }
        if (_options is not null)
        {
            _options.Host = this;
        }
        if (_searchHost is not null)
        {
            _search = SelectInputBox.Create();
            _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
            _search.PropertyChanged += OnSearchPropertyChanged;
            _search.PlaceholderText = SearchPlaceholder;
            _searchHost.Child = _search;
        }
        if (_scroll is not null)
        {
            _scroll.MaxHeight = MaxDropDownHeight;
        }
        PseudoClasses.Set(SearchablePseudoClass, IsSearchable);
        RebuildOrder();
        UpdateChips();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (open)
            {
                OpenPopup();
            }
            else
            {
                ClosePopup();
            }
            (ControlAutomationPeer.FromElement(this) as SelectFieldAutomationPeer)?.RaiseExpandChanged(open);
        }
        else if (change.Property == MaxVisibleChipsProperty)
        {
            UpdateChips();
        }
        else if (change.Property == TextSelectorProperty || change.Property == DisabledSelectorProperty ||
                 change.Property == DisplayMemberBindingProperty)
        {
            _entries.Invalidate();
        }
        else if (change.Property == IsSearchableProperty)
        {
            PseudoClasses.Set(SearchablePseudoClass, IsSearchable);
        }
        else if (change.Property == SearchPlaceholderProperty && _search is not null)
        {
            _search.PlaceholderText = SearchPlaceholder;
        }
        else if (change.Property == MaxDropDownHeightProperty && _scroll is not null)
        {
            _scroll.MaxHeight = MaxDropDownHeight;
        }
        else if (change.Property == FilterPredicateProperty && IsOpen)
        {
            Refilter();
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            UpdateClear();
        }
    }

    private void OnEntriesChanged()
    {
        if (IsOpen)
        {
            Refilter();
        }
        Dispatcher.UIThread.Post(() =>
        {
            RebuildOrder();
            UpdateChips();
        }, DispatcherPriority.Background);
    }

    // ---------------------------------------------------------------- selection order and chips

    private void OnOwnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var removed in e.RemovedItems)
        {
            _order.Remove(removed);
        }
        foreach (var added in e.AddedItems)
        {
            if (!_order.Contains(added))
            {
                _order.Add(added);
            }
        }
        // Items the model reports in a batch (bindings, select-all) that the events did not carry.
        if (_order.Count != Selection.SelectedItems.Count)
        {
            RebuildOrder();
        }
        UpdateChips();
        _options?.InvalidateRows();
    }

    private void RebuildOrder()
    {
        var selected = Selection.SelectedItems;
        _order.RemoveAll(item => !selected.Contains(item));
        foreach (var item in selected)
        {
            if (!_order.Contains(item))
            {
                _order.Add(item);
            }
        }
    }

    private string LabelOf(object? item)
    {
        var index = ItemsView.IndexOf(item);
        return index >= 0 ? _entries.TextAt(index) ?? string.Empty : SelectEntrySource.TextOf(item);
    }

    private void UpdateChips()
    {
        var max = Math.Max(0, MaxVisibleChips);
        var visible = _order.Take(max).Select(item => ((object)(item ?? s_null), LabelOf(item))).ToList();
        _chips?.Update(visible, Math.Max(0, _order.Count - visible.Count));
        PseudoClasses.Set(HasSelectionPseudoClass, _order.Count > 0);
        SelectedValues = _order.Select(item => SelectEntrySource.ValueOf(item, ValueSelector)).ToArray();
        UpdateClear();
    }

    private static readonly object s_null = new();

    private void UpdateClear()
    {
        if (_clearHost is not null)
        {
            _clearHost.IsPresent = _order.Count > 0 && IsEffectivelyEnabled;
        }
    }

    // ---------------------------------------------------------------- popup

    private void OpenPopup()
    {
        if (!IsEffectivelyEnabled || _popup is null)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            return;
        }
        if (_surface is not null && _trigger is not null && _trigger.Bounds.Width > 0)
        {
            _surface.Width = _trigger.Bounds.Width;
        }
        SetQuery(string.Empty);
        Refilter();
        if (!_popup.IsOpen)
        {
            _popup.IsOpen = true;
        }
        if (IsSearchable && _search is not null)
        {
            Dispatcher.UIThread.Post(() => _search?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    private void ClosePopup()
    {
        var restoreFocus = _search?.IsFocused == true;
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        // A closed menu forgets its highlight, so the next open starts clean.
        _options?.SetActive(-1, animate: false);
        SetQuery(string.Empty);
        if (restoreFocus)
        {
            Focus(NavigationMethod.Tab);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void SetQuery(string query)
    {
        _query = query;
        if (_search is not null && _search.Text != query)
        {
            _syncingSearch = true;
            _search.Text = query;
            _syncingSearch = false;
        }
    }

    private void Refilter()
    {
        if (_options is null)
        {
            return;
        }
        _entries.Filter(IsSearchable ? _query : null, FilterPredicate, _visible);
        _options.SetOptions(_visible);
        if (_emptyHost is not null)
        {
            _emptyHost.IsPresent = _visible.Count == 0 && IsSearchable;
        }
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingSearch || _search is null)
        {
            return;
        }
        _query = _search.Text ?? string.Empty;
        Refilter();
        _scroll?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
    }

    // ---------------------------------------------------------------- pointer

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
        {
            return;
        }
        PseudoClasses.Set(PressedPseudoClass, true);
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasPressed = PseudoClasses.Contains(PressedPseudoClass);
        PseudoClasses.Set(PressedPseudoClass, false);
        if (wasPressed && _trigger is not null && new Rect(_trigger.Bounds.Size).Contains(e.GetPosition(_trigger)))
        {
            SetCurrentValue(IsOpenProperty, !IsOpen);
        }
    }

    private void OnTriggerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => PseudoClasses.Set(PressedPseudoClass, false);

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Selection.Clear();
        SetCurrentValue(IsOpenProperty, false);
        Focus(NavigationMethod.Pointer);
    }

    // ---------------------------------------------------------------- keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || !HandleKey(e.Key, fromSearch: false))
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleKey(e.Key, fromSearch: true))
        {
            e.Handled = true;
        }
    }

    private bool HandleKey(Key key, bool fromSearch)
    {
        var active = _options?.ActiveOption ?? -1;
        switch (key)
        {
            case Key.Enter when IsOpen && active >= 0:
                Toggle(active);
                return true;
            case Key.Enter:
            case Key.Space when !fromSearch:
                SetCurrentValue(IsOpenProperty, !IsOpen);
                return true;
            case Key.Escape:
                SetCurrentValue(IsOpenProperty, false);
                return true;
            case Key.Tab when IsOpen && fromSearch:
                SetCurrentValue(IsOpenProperty, false);
                return false;
            case Key.Down:
            case Key.Up:
            {
                if (!IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, true);
                }
                var next = Step(_options?.ActiveOption ?? -1, key == Key.Down ? 1 : -1);
                if (next >= 0)
                {
                    _options!.SetActive(next, animate: false);
                    _options.ScrollIntoView(next);
                    Announce(next);
                }
                return true;
            }
        }
        return false;
    }

    private int Step(int from, int step)
    {
        var count = _visible.Count;
        if (count == 0)
        {
            return -1;
        }
        var index = from;
        for (var i = 0; i < count; i++)
        {
            index = from < 0 && i == 0 ? (step > 0 ? 0 : count - 1) : ((index + step) % count + count) % count;
            if (_entries.IsEnabledAt(_visible[index]))
            {
                return index;
            }
        }
        return -1;
    }

    private void Toggle(int position)
    {
        var entry = _options?.EntryAt(position) ?? -1;
        if (entry < 0 || !_entries.IsEnabledAt(entry) || !IsEffectivelyEnabled)
        {
            return;
        }
        var on = !Selection.IsSelected(entry);
        if (on)
        {
            Selection.Select(entry);
        }
        else
        {
            Selection.Deselect(entry);
        }
        _options?.PlayCheck(entry, on);
        Announce(position);
    }

    /// <summary>Focus stays on the trigger: the highlighted option and its state are read out.</summary>
    private void Announce(int position)
    {
        if (_announcer is null || _options is null)
        {
            return;
        }
        var entry = _options.EntryAt(position);
        var text = SelectOptionView.Describe(_entries, entry, position, _visible.Count);
        _announcer.Text = entry >= 0 && Selection.IsSelected(entry) ? text + ", selected" : text;
    }

    // ---------------------------------------------------------------- ISelectOptionHost

    bool ISelectOptionHost.IsEntrySelected(int entryIndex) => Selection.IsSelected(entryIndex);

    void ISelectOptionHost.OnOptionHovered(int position) => _options?.SetActive(position, animate: false);

    void ISelectOptionHost.OnOptionInvoked(int position) => Toggle(position);

    void ISelectOptionHost.OnOptionsPointerExited()
    {
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SetCurrentValue(IsOpenProperty, false);
    }
}

using System.Collections.Specialized;
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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc's filter toolbar: keep collection filters close and easy to reset. Applied filters are removable chips
/// ("Status · Open") whose slots open and close on a spring so neighbors slide along, a changed value morphs in
/// place, Clear all appears while any filter is applied, and the Add filter menu picks a field, then a value (one
/// chip per field). An optional search field sits with the actions. <see cref="ActiveFilters"/> is two-way and a
/// mutable list is updated in place; <see cref="FiltersChanged"/> reports every change, and changes are announced.
/// </summary>
[TemplatePart(ChipsPart, typeof(Panel))]
[TemplatePart(AddFilterPart, typeof(FilterMenu))]
[TemplatePart(ClearSlotPart, typeof(Decorator))]
[TemplatePart(ClearAllPart, typeof(AvaloniaButton))]
[TemplatePart(SearchPart, typeof(TextBox))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(EmptyPseudoClass, StackedPseudoClass)]
public class FilterToolbar : TemplatedControl
{
    public const string ChipsPart = "PART_Chips";
    public const string AddFilterPart = "PART_AddFilter";
    public const string ClearSlotPart = "PART_ClearSlot";
    public const string ClearAllPart = "PART_ClearAll";
    public const string SearchPart = "PART_Search";
    public const string AnnouncerPart = "PART_Announcer";
    public const string EmptyPseudoClass = ":empty";
    public const string StackedPseudoClass = ":stacked";

    #region Properties

    public static readonly StyledProperty<IList<FilterChip>?> ActiveFiltersProperty =
        AvaloniaProperty.Register<FilterToolbar, IList<FilterChip>?>(nameof(ActiveFilters), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FilterToolbar, string>(nameof(Label), "Active filters");

    /// <summary>Text shown in the toolbar when no filter is applied (default No filters applied).</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<FilterToolbar, string>(nameof(EmptyText), "No filters applied");

    /// <summary>Label of the button that removes every applied filter (default Clear all).</summary>
    public static readonly StyledProperty<string> ClearAllLabelProperty =
        AvaloniaProperty.Register<FilterToolbar, string>(nameof(ClearAllLabel), "Clear all");

    /// <summary>Label of the Add filter button (default Add filter).</summary>
    public static readonly StyledProperty<string> AddFilterLabelProperty =
        AvaloniaProperty.Register<FilterToolbar, string>(nameof(AddFilterLabel), "Add filter");

    public static readonly StyledProperty<FilterMenuAlignment> MenuAlignmentProperty =
        AvaloniaProperty.Register<FilterToolbar, FilterMenuAlignment>(nameof(MenuAlignment), FilterMenuAlignment.End);

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<FilterToolbar, object?>(nameof(Actions));

    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<FilterToolbar, bool>(nameof(IsSearchVisible));

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<FilterToolbar, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Placeholder text of the search field (default Search).</summary>
    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<FilterToolbar, string>(nameof(SearchPlaceholder), "Search");

    public static readonly RoutedEvent<FilterToolbarFiltersChangedEventArgs> FiltersChangedEvent =
        RoutedEvent.Register<FilterToolbar, FilterToolbarFiltersChangedEventArgs>(nameof(FiltersChanged), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<FilterToolbar, bool> HasFiltersProperty =
        AvaloniaProperty.RegisterDirect<FilterToolbar, bool>(nameof(HasFilters), o => o.HasFilters);

    internal static readonly DirectProperty<FilterToolbar, bool> HasFieldsProperty =
        AvaloniaProperty.RegisterDirect<FilterToolbar, bool>(nameof(HasFields), o => o.HasFields);

    /// <summary>Fields of the Add filter menu (the XAML content of the toolbar). The menu shows when there is at least one.</summary>
    [Content]
    public AvaloniaList<FilterField> Fields { get; } = new();

    /// <summary>The applied filters (two-way). A mutable list is updated in place by remove, Clear all and Add filter.</summary>
    public IList<FilterChip>? ActiveFilters { get => GetValue(ActiveFiltersProperty); set => SetValue(ActiveFiltersProperty, value); }

    /// <summary>Accessible name of the toolbar group.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="ClearAllLabelProperty"/>
    public string ClearAllLabel { get => GetValue(ClearAllLabelProperty); set => SetValue(ClearAllLabelProperty, value); }

    /// <inheritdoc cref="AddFilterLabelProperty"/>
    public string AddFilterLabel { get => GetValue(AddFilterLabelProperty); set => SetValue(AddFilterLabelProperty, value); }

    /// <summary>The trigger edge the Add filter panel lines up with.</summary>
    public FilterMenuAlignment MenuAlignment { get => GetValue(MenuAlignmentProperty); set => SetValue(MenuAlignmentProperty, value); }

    /// <summary>Extra actions shown beside Add filter (Arc <c>children</c>).</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>Shows a search field with the actions; its text is <see cref="SearchText"/>.</summary>
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }

    /// <summary>Text of the search field (two-way). Escape in the field clears it.</summary>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="SearchPlaceholderProperty"/>
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <summary>Raised after a filter is added, changed, removed or all are cleared from the toolbar.</summary>
    public event EventHandler<FilterToolbarFiltersChangedEventArgs>? FiltersChanged
    {
        add => AddHandler(FiltersChangedEvent, value);
        remove => RemoveHandler(FiltersChangedEvent, value);
    }

    private bool _hasFilters;
    private bool _hasFields;

    internal bool HasFilters { get => _hasFilters; private set => SetAndRaise(HasFiltersProperty, ref _hasFilters, value); }

    internal bool HasFields { get => _hasFields; private set => SetAndRaise(HasFieldsProperty, ref _hasFields, value); }

    #endregion

    /// <summary>Lucide "x" (the generated ArcIcons.X joins its two paths with a relative move).</summary>
    private static Geometry CloseGlyph => ArcIcons.X;

    private readonly Dictionary<string, FilterToolbarChipSlot> _slots = new();
    private FilterToolbarChipPanel? _chips;
    private FilterMenu? _menu;
    private FilterToolbarChipSlot? _clearSlot;
    private AvaloniaButton? _clearAll;
    private TextBox? _search;
    private TextBlock? _announcer;
    private FilterToolbarLayout? _layout;
    private INotifyCollectionChanged? _observed;
    private List<FilterChip> _seen = new();
    private int? _pendingFocus;
    private bool _templateReady;
    private bool _watching;

    public FilterToolbar()
    {
        SetCurrentValue(ActiveFiltersProperty, new AvaloniaList<FilterChip>());
        Fields.CollectionChanged += (_, _) => HasFields = Fields.Count > 0;
    }

    internal IReadOnlyDictionary<string, FilterToolbarChipSlot> Slots => _slots;

    internal FilterMenu? Menu => _menu;

    private IReadOnlyList<FilterChip> Current => ActiveFilters is { } list ? list.ToList() : [];

    #region Public operations

    /// <summary>Adds a filter, replacing the chip with the same id (one per field).</summary>
    public void AddFilter(FilterChip filter) => Apply(filter, null);

    /// <summary>Removes the filter with <paramref name="id"/>.</summary>
    public void RemoveFilter(string id)
    {
        var current = Current;
        var index = -1;
        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].Id == id)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return;
        }
        var removed = current[index];
        Mutate(list => list.RemoveAt(index), current.Where((_, i) => i != index).ToList());
        RaiseChanged(FilterChangeKind.Removed, removed, null, null);
    }

    /// <summary>Removes every filter.</summary>
    public void ClearFilters()
    {
        if (Current.Count == 0)
        {
            return;
        }
        Mutate(list => list.Clear(), new List<FilterChip>());
        RaiseChanged(FilterChangeKind.Cleared, null, null, null);
    }

    private void Apply(FilterChip filter, FilterField? field)
    {
        var current = Current;
        var index = -1;
        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].Id == filter.Id)
            {
                index = i;
                break;
            }
        }
        if (index >= 0)
        {
            var previous = current[index];
            if (previous.Equals(filter))
            {
                return;
            }
            var next = current.ToList();
            next[index] = filter;
            Mutate(list => list[index] = filter, next);
            RaiseChanged(FilterChangeKind.Changed, filter, previous, field);
        }
        else
        {
            var next = current.ToList();
            next.Add(filter);
            Mutate(list => list.Add(filter), next);
            RaiseChanged(FilterChangeKind.Added, filter, null, field);
        }
    }

    /// <summary>Changes a mutable list in place, or assigns a new list (two-way) when it is read-only.</summary>
    private void Mutate(Action<IList<FilterChip>> inPlace, List<FilterChip> replacement)
    {
        if (ActiveFilters is { IsReadOnly: false } list && list is not FilterChip[])
        {
            inPlace(list);
            if (list is not INotifyCollectionChanged)
            {
                Sync(animate: true);
            }
        }
        else
        {
            SetCurrentValue(ActiveFiltersProperty, new AvaloniaList<FilterChip>(replacement));
        }
    }

    private void RaiseChanged(FilterChangeKind kind, FilterChip? filter, FilterChip? previous, FilterField? field) =>
        RaiseEvent(new FilterToolbarFiltersChangedEventArgs(FiltersChangedEvent, kind, filter, previous, field, Current));

    #endregion

    #region Chips

    /// <summary>The filter list may outlive the toolbar (a view model): it is listened to only while the toolbar is attached.</summary>
    private void Observe(IList<FilterChip>? list)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnFiltersCollectionChanged;
            _observed = null;
        }
        if (_watching && list is INotifyCollectionChanged observable)
        {
            _observed = observable;
            observable.CollectionChanged += OnFiltersCollectionChanged;
        }
    }

    private void OnFiltersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync(animate: true);

    /// <summary>Diffs the chips against the filters: new slots open, removed ones collapse, changed values morph.</summary>
    private void Sync(bool animate)
    {
        var filters = Current;
        HasFilters = filters.Count > 0;
        PseudoClasses.Set(EmptyPseudoClass, filters.Count == 0);
        if (!_templateReady || _chips is null)
        {
            _seen = filters.ToList();
            return;
        }
        animate = animate && IsLoaded;
        var active = new HashSet<string>(filters.Select(f => f.Id));
        foreach (var (id, slot) in _slots.ToList())
        {
            if (!active.Contains(id))
            {
                _slots.Remove(id);
                slot.Exit(animate);
            }
        }
        var desired = new List<FilterToolbarChipSlot>();
        foreach (var filter in filters)
        {
            if (!_slots.TryGetValue(filter.Id, out var slot))
            {
                slot = CreateSlot(filter);
                _slots[filter.Id] = slot;
                _chips.Children.Add(slot);
                slot.Enter(animate);
            }
            else if (!Equals(slot.Chip, filter))
            {
                UpdateChip(slot, filter);
            }
            desired.Add(slot);
        }
        Reorder(desired);
        _chips.Arm();
        _clearSlot?.SetCurrentPresence(filters.Count > 0, animate);
        var message = Describe(_seen, filters);
        if (!string.IsNullOrEmpty(message))
        {
            Announce(message);
        }
        _seen = filters.ToList();
        if (_pendingFocus is { } pending)
        {
            _pendingFocus = null;
            FocusAfterRemoval(pending, filters);
        }
    }

    private void Reorder(List<FilterToolbarChipSlot> desired)
    {
        var children = _chips!.Children;
        var result = new List<Control>(children.Count);
        var next = 0;
        foreach (var child in children.ToList())
        {
            if (child is FilterToolbarChipSlot { IsLeaving: true })
            {
                result.Add(child);
                continue;
            }
            while (next < desired.Count)
            {
                var slot = desired[next++];
                if (!result.Contains(slot))
                {
                    result.Add(slot);
                }
                if (ReferenceEquals(slot, child))
                {
                    break;
                }
            }
        }
        while (next < desired.Count)
        {
            var slot = desired[next++];
            if (!result.Contains(slot))
            {
                result.Add(slot);
            }
        }
        for (var i = 0; i < result.Count; i++)
        {
            var at = children.IndexOf(result[i]);
            if (at != i && at >= 0)
            {
                children.Move(at, i);
            }
        }
    }

    private FilterToolbarChipSlot CreateSlot(FilterChip filter)
    {
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var separator = new TextBlock { Text = " · ", VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(separator, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var value = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(value, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(value, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        var valueSlot = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Spring = ArcSpring.Morph, Child = value, VerticalAlignment = VerticalAlignment.Center };
        var text = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { label, separator, valueSlot } };
        var remove = new FilterToolbarIconButton(CloseGlyph, 24, 14, 1.8, 0.96, ArcDesignTokenKind.Border)
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        var chip = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 0, 8, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { text, remove } }
        };
        ArcTokens.Bind(chip, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(chip, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(chip, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(chip, Border.MinHeightProperty, ArcDesignTokenKind.ControlSizeSM);
        ArcTokens.Bind(chip, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        var slot = new FilterToolbarChipSlot { Mode = FilterToolbarSlotMode.Chip, Gap = 8, Key = filter.Id, Child = chip };
        slot.Exited += (_, _) => _chips?.Children.Remove(slot);
        remove.Clicked = keyboard =>
        {
            var id = slot.Key;
            if (id is null)
            {
                return;
            }
            var filters = Current;
            var index = filters.ToList().FindIndex(f => f.Id == id);
            if (keyboard || remove.IsFocused)
            {
                _pendingFocus = index;
            }
            RemoveFilter(id);
        };
        UpdateChip(slot, filter, label, separator, value, valueSlot, remove);
        return slot;
    }

    private static void UpdateChip(FilterToolbarChipSlot slot, FilterChip filter)
    {
        if (slot.Child is not Border { Child: StackPanel { Children: [StackPanel text, FilterToolbarIconButton remove] } } ||
            text.Children is not [TextBlock label, TextBlock separator, SpringSizeDecorator { Child: ArcContentSwap value } valueSlot])
        {
            return;
        }
        UpdateChip(slot, filter, label, separator, value, valueSlot, remove);
    }

    private static void UpdateChip(FilterToolbarChipSlot slot, FilterChip filter, TextBlock label, TextBlock separator,
                                   ArcContentSwap value, Control valueSlot, FilterToolbarIconButton remove)
    {
        slot.Chip = filter;
        label.Text = filter.Label;
        var hasValue = !string.IsNullOrEmpty(filter.Value);
        separator.IsVisible = hasValue;
        valueSlot.IsVisible = hasValue;
        value.Content = filter.Value;
        AutomationProperties.SetName(remove, $"Remove {filter.Text}");
    }

    /// <summary>A removed chip hands focus to its neighbor, or to the Add filter trigger once the list is empty.</summary>
    private void FocusAfterRemoval(int index, IReadOnlyList<FilterChip> filters)
    {
        if (filters.Count > 0 && index >= 0)
        {
            var next = filters[Math.Min(index, filters.Count - 1)];
            if (_slots.TryGetValue(next.Id, out var slot) && FindRemove(slot) is { } button)
            {
                button.Focus(NavigationMethod.Directional);
                return;
            }
        }
        if (_menu is { IsVisible: true } menu)
        {
            menu.Focus(NavigationMethod.Directional);
        }
    }

    internal static FilterToolbarIconButton? FindRemove(FilterToolbarChipSlot slot) =>
        slot.Child is Border { Child: StackPanel { Children: [_, FilterToolbarIconButton remove] } } ? remove : null;

    /// <summary>Arc's status text: "Added Status: Open", "Status changed to Done", "Removed …", "All filters cleared".</summary>
    internal static string Describe(IReadOnlyList<FilterChip> before, IReadOnlyList<FilterChip> after)
    {
        var removed = before.Where(old => after.All(f => f.Id != old.Id)).ToList();
        if (after.Count == 0 && removed.Count > 1)
        {
            return "All filters cleared";
        }
        var added = after.Where(f => before.All(old => old.Id != f.Id));
        var changed = after.Where(f => before.Any(old => old.Id == f.Id && old.Value != f.Value));
        return string.Join(". ", added.Select(f => $"Added {f.Text}")
                                      .Concat(changed.Select(f => $"{f.Label} changed to {f.Value ?? "any"}"))
                                      .Concat(removed.Select(f => $"Removed {f.Text}")));
    }

    private void Announce(string message)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = message;
        AutomationProperties.SetName(_announcer, message);
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_menu is not null)
        {
            _menu.FilterSelected -= OnFilterSelected;
        }
        if (_clearAll is not null)
        {
            _clearAll.Click -= OnClearAll;
        }
        if (_search is not null)
        {
            _search.KeyDown -= OnSearchKeyDown;
        }
        if (_layout is not null)
        {
            _layout.StackedChanged -= OnStackedChanged;
        }
        foreach (var slot in _slots.Values)
        {
            _chips?.Children.Remove(slot);
        }
        _slots.Clear();
        _chips = e.NameScope.Find<FilterToolbarChipPanel>(ChipsPart);
        _menu = e.NameScope.Find<FilterMenu>(AddFilterPart);
        _clearSlot = e.NameScope.Find<FilterToolbarChipSlot>(ClearSlotPart);
        _clearAll = e.NameScope.Find<AvaloniaButton>(ClearAllPart);
        _search = e.NameScope.Find<TextBox>(SearchPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        _layout = e.NameScope.Find<FilterToolbarLayout>("PART_Layout");
        if (_menu is not null)
        {
            _menu.Fields = Fields;
            _menu.FilterSelected += OnFilterSelected;
        }
        if (_clearAll is not null)
        {
            _clearAll.Click += OnClearAll;
        }
        if (_search is not null)
        {
            _search.KeyDown += OnSearchKeyDown;
        }
        if (_layout is not null)
        {
            _layout.StackedChanged += OnStackedChanged;
        }
        _templateReady = true;
        _seen = Current.ToList();
        Sync(animate: false);
        _clearSlot?.SetCurrentPresence(Current.Count > 0, false);
    }

    private void OnStackedChanged(object? sender, EventArgs e) => PseudoClasses.Set(StackedPseudoClass, _layout?.IsStacked == true);

    private void OnFilterSelected(object? sender, FilterMenuSelectedEventArgs e)
    {
        e.Handled = true;
        Apply(e.Filter, e.Field);
    }

    private void OnClearAll(object? sender, RoutedEventArgs e)
    {
        _pendingFocus = -1;
        ClearFilters();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(SearchText))
        {
            e.Handled = true;
            SetCurrentValue(SearchTextProperty, string.Empty);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActiveFiltersProperty)
        {
            Observe(change.NewValue as IList<FilterChip>);
            Sync(animate: true);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        Observe(ActiveFilters);
        // Show what changed while the toolbar was away, at once and without announcing it.
        _seen = Current.ToList();
        Sync(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        Observe(null);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AutomationProperties.SetName(this, Label);
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ToolbarPeer(this);

    private sealed class ToolbarPeer : Avalonia.Automation.Peers.ControlAutomationPeer
    {
        public ToolbarPeer(FilterToolbar owner) : base(owner)
        {
        }

        protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() =>
            Avalonia.Automation.Peers.AutomationControlType.Group;
    }

    #endregion
}

internal static class FilterToolbarChipSlotExtensions
{
    /// <summary>Opens or collapses a template slot (Clear all) that stays in the tree.</summary>
    public static void SetCurrentPresence(this FilterToolbarChipSlot slot, bool present, bool animate)
    {
        if (present)
        {
            if (slot.IsLeaving || slot.Child is { IsVisible: false })
            {
                slot.Enter(animate);
            }
        }
        else if (!slot.IsLeaving)
        {
            slot.Exit(animate);
        }
    }
}

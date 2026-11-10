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

/// <summary>
/// A select field over a tree: pick one node, or check several with three-state checkboxes, in a dropdown that shows
/// the hierarchy as an indented, virtualized tree (only the visible rows are built, so thousands of nodes stay fast).
/// Typing in the field filters the tree, keeps every ancestor of a match, opens down to it and marks the matched text.
/// The field, menu surface and clear button follow <see cref="Cascader"/>; the chips of the multiple mode follow
/// <see cref="MultiSelect"/>; the rows follow <see cref="TreeView"/>; the checkboxes draw like <see cref="Checkbox"/>.
/// <para>Nodes are any objects: <see cref="ChildrenSelector"/> gives the children, <see cref="TextSelector"/> the label
/// and <see cref="ValueSelector"/> the value, which must be unique across the tree. Everything goes through delegates,
/// so nothing is reflected.</para>
/// <para>Checking a parent checks its enabled descendants; a parent whose children are partly checked shows a dash.
/// Disabled nodes keep their state when a parent changes. <see cref="CheckStrategy"/> decides which checked nodes
/// <see cref="SelectedItems"/> and <see cref="SelectedValues"/> report.</para>
/// <para>Keyboard: Enter, Space and Down open; typing searches; Up and Down move the highlight, Right opens a parent or
/// enters it, Left closes it or goes to the parent, Home and End jump; Enter picks (single) or toggles the check
/// (multiple), Space toggles the check while the search is empty; Escape clears the search, then closes; Backspace in an
/// empty search removes the last chip.</para>
/// </summary>
[TemplatePart(TriggerPart, typeof(Border))]
[TemplatePart(ValuePart, typeof(SelectValueRoll))]
[TemplatePart(ChipsPart, typeof(MultiSelectChipRow))]
[TemplatePart(SearchHostPart, typeof(Decorator))]
[TemplatePart(ClearHostPart, typeof(PresenceHost))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[PseudoClasses(OpenPseudoClass, PlaceholderPseudoClass, PressedPseudoClass, ReadOnlyPseudoClass, MultiplePseudoClass, SearchingPseudoClass)]
public class TreeSelect : TemplatedControl, ISelectFieldAutomation
{
    public const string TriggerPart = "PART_Trigger";
    public const string ValuePart = "PART_Value";
    public const string ChipsPart = "PART_Chips";
    public const string SearchHostPart = "PART_SearchHost";
    public const string ClearHostPart = "PART_ClearHost";
    public const string ClearPart = "PART_Clear";
    public const string PopupPart = "PART_Popup";
    public const string SurfacePart = "PART_Surface";
    public const string RowsPart = "PART_Rows";
    public const string AnnouncerPart = "PART_Announcer";
    public const string OpenPseudoClass = ":open";
    public const string PlaceholderPseudoClass = ":placeholder";
    public const string PressedPseudoClass = ":pressed";
    public const string ReadOnlyPseudoClass = ":readonly";
    public const string MultiplePseudoClass = ":multiple";
    public const string SearchingPseudoClass = ":searching";

    #region Public properties

    /// <summary>Visible label above the trigger; also the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TreeSelect, string?>(nameof(Label));

    /// <summary>Muted hint under the trigger; also the accessible help text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TreeSelect, string?>(nameof(Description));

    /// <summary>Muted text shown while nothing is selected.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TreeSelect, string?>(nameof(Placeholder), "Select an option");

    /// <summary>The root nodes. Inline <see cref="TreeViewItem"/> nodes need no selectors: their header, items, icon and disabled state are read.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<TreeSelect, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Children of a node; same shape as the one of <c>Cascader</c> and <c>TreeView</c>. Null or empty means a leaf.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<TreeSelect, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Label of a node; when null, <c>ToString()</c>. Searching matches this text.</summary>
    public static readonly StyledProperty<Func<object, string?>?> TextSelectorProperty =
        AvaloniaProperty.Register<TreeSelect, Func<object, string?>?>(nameof(TextSelector));

    /// <summary>Value of a node; it must be unique across the tree. When null, the node itself is the value.</summary>
    public static readonly StyledProperty<Func<object, object?>?> ValueSelectorProperty =
        AvaloniaProperty.Register<TreeSelect, Func<object, object?>?>(nameof(ValueSelector));

    /// <summary>Optional icon of a node (24x24 stroked glyph, drawn at 16px before the label); no icon when null.</summary>
    public static readonly StyledProperty<Func<object, Geometry?>?> IconSelectorProperty =
        AvaloniaProperty.Register<TreeSelect, Func<object, Geometry?>?>(nameof(IconSelector));

    /// <summary>Returns true for nodes that are shown dimmed and cannot be picked or checked; a disabled parent still opens.</summary>
    public static readonly StyledProperty<Func<object, bool>?> DisabledSelectorProperty =
        AvaloniaProperty.Register<TreeSelect, Func<object, bool>?>(nameof(DisabledSelector));

    /// <summary>Pick one node (<c>Single</c>) or check several (<c>Multiple</c>).</summary>
    public static readonly StyledProperty<TreeSelectMode> SelectionModeProperty =
        AvaloniaProperty.Register<TreeSelect, TreeSelectMode>(nameof(SelectionMode));

    /// <summary>The selected node of a single-select field (two-way), or null.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<TreeSelect, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Value of the selected node of a single-select field (two-way). A value the data does not contain (yet) is kept and resolved when the data arrives.</summary>
    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<TreeSelect, object?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected nodes in tree order: the checked nodes that <see cref="CheckStrategy"/> reports, or the single selected node.</summary>
    public static readonly DirectProperty<TreeSelect, IReadOnlyList<object>> SelectedItemsProperty =
        AvaloniaProperty.RegisterDirect<TreeSelect, IReadOnlyList<object>>(nameof(SelectedItems), o => o.SelectedItems);

    /// <summary>Values of <see cref="SelectedItems"/> (two-way). Setting it checks the nodes with these values (a parent's value checks its enabled descendants); values written before the data arrives are applied when it does.</summary>
    public static readonly DirectProperty<TreeSelect, IReadOnlyList<object?>> SelectedValuesProperty =
        AvaloniaProperty.RegisterDirect<TreeSelect, IReadOnlyList<object?>>(nameof(SelectedValues), o => o.SelectedValues,
            (o, v) => o.SelectedValues = v, Array.Empty<object?>(), BindingMode.TwoWay);

    /// <summary>The text of the field: the selected node's label or full path (single), or the checked labels joined by commas (multiple).</summary>
    public static readonly DirectProperty<TreeSelect, string?> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<TreeSelect, string?>(nameof(SelectedText), o => o.SelectedText);

    /// <summary>Which checked nodes a multi-select field reports: leaves only, fully checked parents in place of their subtree, or all.</summary>
    public static readonly StyledProperty<TreeSelectCheckStrategy> CheckStrategyProperty =
        AvaloniaProperty.Register<TreeSelect, TreeSelectCheckStrategy>(nameof(CheckStrategy));

    /// <summary>In single mode, only leaves can be picked: a click on a parent opens or closes it.</summary>
    public static readonly StyledProperty<bool> LeafOnlyProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(LeafOnly));

    /// <summary>Whether typing in the open field filters the tree.</summary>
    public static readonly StyledProperty<bool> IsSearchableProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(IsSearchable), true);

    /// <summary>The search text typed in the field (two-way); cleared when the menu closes.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<TreeSelect, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Returns true when a node matches the search text; when null, a node matches when its label contains the text, ignoring case.</summary>
    public static readonly StyledProperty<Func<object, string, bool>?> FilterPredicateProperty =
        AvaloniaProperty.Register<TreeSelect, Func<object, string, bool>?>(nameof(FilterPredicate));

    /// <summary>Levels open when the data arrives: 0 keeps every parent closed, 1 opens the roots, -1 opens everything.</summary>
    public static readonly StyledProperty<int> ExpandDepthProperty =
        AvaloniaProperty.Register<TreeSelect, int>(nameof(ExpandDepth));

    /// <summary>In single mode, show the whole path in the field (true) or only the selected node (false).</summary>
    public static readonly StyledProperty<bool> ShowFullPathProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(ShowFullPath));

    /// <summary>Text between the nodes of the full path.</summary>
    public static readonly StyledProperty<string> PathSeparatorProperty =
        AvaloniaProperty.Register<TreeSelect, string>(nameof(PathSeparator), " / ");

    /// <summary>Chips shown in a multi-select field before the rest fold into "+N".</summary>
    public static readonly StyledProperty<int> MaxVisibleChipsProperty =
        AvaloniaProperty.Register<TreeSelect, int>(nameof(MaxVisibleChips), 3);

    /// <summary>Whether a clear button shows while something is selected.</summary>
    public static readonly StyledProperty<bool> IsClearableProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(IsClearable), true);

    /// <summary>The menu opens for looking, but the selection cannot be changed or cleared.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(IsReadOnly));

    /// <summary>Whether the menu is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Tallest the menu gets before the tree scrolls.</summary>
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(MaxDropDownHeight), 320);

    /// <summary>Raised when <see cref="SelectedItems"/> changes: by the user, by a binding or by clearing.</summary>
    public static readonly RoutedEvent<TreeSelectSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<TreeSelect, TreeSelectSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="ChildrenSelectorProperty"/>
    public Func<object, IEnumerable?>? ChildrenSelector { get => GetValue(ChildrenSelectorProperty); set => SetValue(ChildrenSelectorProperty, value); }

    /// <inheritdoc cref="TextSelectorProperty"/>
    public Func<object, string?>? TextSelector { get => GetValue(TextSelectorProperty); set => SetValue(TextSelectorProperty, value); }

    /// <inheritdoc cref="ValueSelectorProperty"/>
    public Func<object, object?>? ValueSelector { get => GetValue(ValueSelectorProperty); set => SetValue(ValueSelectorProperty, value); }

    /// <inheritdoc cref="IconSelectorProperty"/>
    public Func<object, Geometry?>? IconSelector { get => GetValue(IconSelectorProperty); set => SetValue(IconSelectorProperty, value); }

    /// <inheritdoc cref="DisabledSelectorProperty"/>
    public Func<object, bool>? DisabledSelector { get => GetValue(DisabledSelectorProperty); set => SetValue(DisabledSelectorProperty, value); }

    /// <inheritdoc cref="SelectionModeProperty"/>
    public TreeSelectMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <inheritdoc cref="SelectedValueProperty"/>
    public object? SelectedValue { get => GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }

    /// <inheritdoc cref="SelectedItemsProperty"/>
    public IReadOnlyList<object> SelectedItems
    {
        get => _selectedItems;
        private set => SetAndRaise(SelectedItemsProperty, ref _selectedItems, value);
    }

    /// <inheritdoc cref="SelectedValuesProperty"/>
    public IReadOnlyList<object?> SelectedValues
    {
        get => _selectedValues;
        set => WriteSelectedValues(value ?? Array.Empty<object?>());
    }

    /// <inheritdoc cref="SelectedTextProperty"/>
    public string? SelectedText
    {
        get => _selectedText;
        private set => SetAndRaise(SelectedTextProperty, ref _selectedText, value);
    }

    /// <inheritdoc cref="CheckStrategyProperty"/>
    public TreeSelectCheckStrategy CheckStrategy { get => GetValue(CheckStrategyProperty); set => SetValue(CheckStrategyProperty, value); }

    /// <inheritdoc cref="LeafOnlyProperty"/>
    public bool LeafOnly { get => GetValue(LeafOnlyProperty); set => SetValue(LeafOnlyProperty, value); }

    /// <inheritdoc cref="IsSearchableProperty"/>
    public bool IsSearchable { get => GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="FilterPredicateProperty"/>
    public Func<object, string, bool>? FilterPredicate { get => GetValue(FilterPredicateProperty); set => SetValue(FilterPredicateProperty, value); }

    /// <inheritdoc cref="ExpandDepthProperty"/>
    public int ExpandDepth { get => GetValue(ExpandDepthProperty); set => SetValue(ExpandDepthProperty, value); }

    /// <inheritdoc cref="ShowFullPathProperty"/>
    public bool ShowFullPath { get => GetValue(ShowFullPathProperty); set => SetValue(ShowFullPathProperty, value); }

    /// <inheritdoc cref="PathSeparatorProperty"/>
    public string PathSeparator { get => GetValue(PathSeparatorProperty); set => SetValue(PathSeparatorProperty, value); }

    /// <inheritdoc cref="MaxVisibleChipsProperty"/>
    public int MaxVisibleChips { get => GetValue(MaxVisibleChipsProperty); set => SetValue(MaxVisibleChipsProperty, value); }

    /// <inheritdoc cref="IsClearableProperty"/>
    public bool IsClearable { get => GetValue(IsClearableProperty); set => SetValue(IsClearableProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="MaxDropDownHeightProperty"/>
    public double MaxDropDownHeight { get => GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }

    /// <summary>Raised when <see cref="SelectedItems"/> changes: by the user, by a binding or by clearing.</summary>
    public event EventHandler<TreeSelectSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

    #region Theme-set row colors and metrics

    internal static readonly StyledProperty<IBrush?> RowForegroundProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(RowForeground));
    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(MutedForeground));
    internal static readonly StyledProperty<IBrush?> IconForegroundProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(IconForeground));
    internal static readonly StyledProperty<IBrush?> CheckOnBrushProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(CheckOnBrush));
    internal static readonly StyledProperty<IBrush?> CheckGlyphBrushProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(CheckGlyphBrush));
    internal static readonly StyledProperty<IBrush?> CheckBorderBrushProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(CheckBorderBrush));
    internal static readonly StyledProperty<IBrush?> CheckSurfaceBrushProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(CheckSurfaceBrush));
    internal static readonly StyledProperty<IBrush?> MatchBrushProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(MatchBrush));
    internal static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<TreeSelect, IBrush?>(nameof(HighlightBrush));
    internal static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(RowHeight), 36);
    internal static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(RowGap), 2);
    internal static readonly StyledProperty<double> RowRadiusProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(RowRadius), 12);
    internal static readonly StyledProperty<double> IndentWidthProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(IndentWidth), 18);
    internal static readonly StyledProperty<double> RowInlinePaddingProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(RowInlinePadding), 8);
    internal static readonly StyledProperty<double> ContentGapProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(ContentGap), 8);
    internal static readonly StyledProperty<double> CheckboxSizeProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(CheckboxSize), 16);
    internal static readonly StyledProperty<double> CheckboxRadiusProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(CheckboxRadius), 4.5);
    internal static readonly StyledProperty<double> CheckStrokeThicknessProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(CheckStrokeThickness), 1.75);
    internal static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(IconSize), 16);
    internal static readonly StyledProperty<double> RowDisabledOpacityProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(RowDisabledOpacity), 0.45);
    internal static readonly StyledProperty<double> MatchRadiusProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(MatchRadius), 3);

    internal IBrush? RowForeground { get => GetValue(RowForegroundProperty); set => SetValue(RowForegroundProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal IBrush? IconForeground { get => GetValue(IconForegroundProperty); set => SetValue(IconForegroundProperty, value); }
    internal IBrush? CheckOnBrush { get => GetValue(CheckOnBrushProperty); set => SetValue(CheckOnBrushProperty, value); }
    internal IBrush? CheckGlyphBrush { get => GetValue(CheckGlyphBrushProperty); set => SetValue(CheckGlyphBrushProperty, value); }
    internal IBrush? CheckBorderBrush { get => GetValue(CheckBorderBrushProperty); set => SetValue(CheckBorderBrushProperty, value); }
    internal IBrush? CheckSurfaceBrush { get => GetValue(CheckSurfaceBrushProperty); set => SetValue(CheckSurfaceBrushProperty, value); }
    internal IBrush? MatchBrush { get => GetValue(MatchBrushProperty); set => SetValue(MatchBrushProperty, value); }
    internal IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }
    internal double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    internal double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }
    internal double RowRadius { get => GetValue(RowRadiusProperty); set => SetValue(RowRadiusProperty, value); }
    internal double IndentWidth { get => GetValue(IndentWidthProperty); set => SetValue(IndentWidthProperty, value); }
    internal double RowInlinePadding { get => GetValue(RowInlinePaddingProperty); set => SetValue(RowInlinePaddingProperty, value); }
    internal double ContentGap { get => GetValue(ContentGapProperty); set => SetValue(ContentGapProperty, value); }
    internal double CheckboxSize { get => GetValue(CheckboxSizeProperty); set => SetValue(CheckboxSizeProperty, value); }
    internal double CheckboxRadius { get => GetValue(CheckboxRadiusProperty); set => SetValue(CheckboxRadiusProperty, value); }
    internal double CheckStrokeThickness { get => GetValue(CheckStrokeThicknessProperty); set => SetValue(CheckStrokeThicknessProperty, value); }
    internal double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    internal double RowDisabledOpacity { get => GetValue(RowDisabledOpacityProperty); set => SetValue(RowDisabledOpacityProperty, value); }
    internal double MatchRadius { get => GetValue(MatchRadiusProperty); set => SetValue(MatchRadiusProperty, value); }

    private static readonly AvaloniaProperty[] s_paletteProperties =
    [
        RowForegroundProperty, MutedForegroundProperty, IconForegroundProperty, CheckOnBrushProperty, CheckGlyphBrushProperty,
        CheckBorderBrushProperty, CheckSurfaceBrushProperty, MatchBrushProperty, HighlightBrushProperty, RowHeightProperty, RowGapProperty,
        RowRadiusProperty, IndentWidthProperty, RowInlinePaddingProperty, ContentGapProperty, CheckboxSizeProperty, CheckboxRadiusProperty,
        CheckStrokeThicknessProperty, IconSizeProperty, RowDisabledOpacityProperty, MatchRadiusProperty, FontFamilyProperty, FontSizeProperty
    ];

    #endregion

    private static readonly IReadOnlyList<object> s_none = Array.Empty<object>();
    private static readonly IReadOnlyList<object?> s_noValues = Array.Empty<object?>();

    private readonly List<TreeSelectNode> _roots = new();
    private Dictionary<object, TreeSelectNode> _nodes = new();
    private Dictionary<object, TreeSelectNode> _byValue = new();
    private HashSet<object> _expanded = new();
    private HashSet<object>? _expandedBeforeSearch;
    private readonly HashSet<TreeSelectNode> _filterVisible = new();
    private readonly List<TreeSelectNode> _matched = new();
    private List<object> _rows = new();
    private readonly ListKeyboardNavigator _navigator;
    private IReadOnlyList<object> _selectedItems = s_none;
    private IReadOnlyList<object?> _selectedValues = s_noValues;
    private IReadOnlyList<object?>? _pendingValues;
    private TreeSelectNode? _selected;
    private INotifyCollectionChanged? _rootsNotifier;
    private string? _selectedText;
    private string _query = string.Empty;
    private object? _activeKey;
    private bool _syncing;
    private bool _syncingSearch;
    private bool _expandApplied;
    private bool _loaded;
    private bool _warnedDuplicate;
    private bool _anyIcon;
    private bool _rendered;
    private bool _pressedOnTrigger;
    private Border? _trigger;
    private SelectValueRoll? _value;
    private MultiSelectChipRow? _chips;
    private Decorator? _searchHost;
    private TextBox? _search;
    private PresenceHost? _clearHost;
    private AvaloniaButton? _clear;
    private LazyFocusRing? _focusRing;
    private ArcPopup? _popup;
    private Border? _surface;
    private ScrollViewer? _scroll;
    private TreeSelectRowsPanel? _panel;
    private PresenceHost? _emptyHost;
    private TextBlock? _announcer;

    static TreeSelect()
    {
        FocusableProperty.OverrideDefaultValue<TreeSelect>(true);
        FocusModality.Watch();
    }

    public TreeSelect()
    {
        PseudoClasses.Set(PlaceholderPseudoClass, true);
        _navigator = new ListKeyboardNavigator(() => _rows.Count, _ => true, i => ((TreeSelectNode)_rows[i]).Label) { Wrap = false };
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    internal TreeSelectPalette Palette { get; } = new();

    /// <summary>The menu's rows panel, built on the first open (tests).</summary>
    internal TreeSelectRowsPanel? RowsPanel => _panel;

    /// <summary>The visible rows in order (tests).</summary>
    internal IReadOnlyList<object> VisibleRows => _rows;

    /// <summary>The highlighted row's item (tests).</summary>
    internal object? ActiveItem => _activeKey;

    internal TextBox? SearchBox => _search;

    /// <summary>The node built for <paramref name="item"/> (tests).</summary>
    internal TreeSelectNode? NodeFor(object item) => _nodes.GetValueOrDefault(item);

    internal MultiSelectChipRow? ChipRow => _chips;

    internal TextBlock? Announcer => _announcer;

    internal PresenceHost? EmptyHost => _emptyHost;

    string? ISelectFieldAutomation.AutomationLabel => Label;
    string? ISelectFieldAutomation.AutomationDescription => Description;

    string? ISelectFieldAutomation.AutomationValue => SelectionMode == TreeSelectMode.Multiple
        ? (_selectedItems.Count == 0 ? null : TreeSelectText.Count(_selectedItems.Count))
        : SelectedText;

    bool ISelectFieldAutomation.AutomationIsOpen
    {
        get => IsOpen;
        set => SetCurrentValue(IsOpenProperty, value);
    }

    #region Public methods

    /// <summary>Opens the menu with the selection expanded.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the menu without changing the selection.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Clears the selection (every check in multiple mode); the menu does not open.</summary>
    public void Clear()
    {
        if (IsReadOnly)
        {
            return;
        }
        if (SelectionMode == TreeSelectMode.Multiple)
        {
            foreach (var node in _nodes.Values)
            {
                node.IsChecked = false;
            }
            _pendingValues = null;
            Recompute();
            UpdateReported(null);
            return;
        }
        SetSelected(null, writeValue: true);
        if (SelectedValue is not null)
        {
            SetCurrentValue(SelectedValueProperty, null);
        }
    }

    /// <summary>Opens a parent node (the menu shows it the next time it is open).</summary>
    public void Expand(object item)
    {
        if (_nodes.TryGetValue(item, out var node))
        {
            SetNodeExpanded(node, true);
        }
    }

    /// <summary>Closes a parent node.</summary>
    public void Collapse(object item)
    {
        if (_nodes.TryGetValue(item, out var node))
        {
            SetNodeExpanded(node, false);
        }
    }

    /// <summary>Whether a parent node is open.</summary>
    public bool IsItemExpanded(object item) => _expanded.Contains(item);

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectFieldAutomationPeer(this, this);

    #region Template and lifecycle

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, TreeSelectTokens.Key(TreeSelectTokenKind.FocusRingMargin), TreeSelectTokens.Key(TreeSelectTokenKind.FocusRingRadius));
        _focusRing.Attach(e.NameScope.Find<Control>(TriggerPart)?.Parent as Panel);
        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
            _trigger.PointerReleased -= OnTriggerReleased;
            _trigger.PointerCaptureLost -= OnTriggerCaptureLost;
            _trigger.SizeChanged -= OnTriggerSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Child = null;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchPropertyChanged;
            if (_searchHost is not null)
            {
                _searchHost.Child = null;
            }
            _search = null;
        }
        if (_panel is not null)
        {
            _panel.Owner = null;
        }
        _panel = null;
        _surface = null;
        _scroll = null;
        _emptyHost = null;

        _trigger = e.NameScope.Find<Border>(TriggerPart);
        _value = e.NameScope.Find<SelectValueRoll>(ValuePart);
        _chips = e.NameScope.Find<MultiSelectChipRow>(ChipsPart);
        _searchHost = e.NameScope.Find<Decorator>(SearchHostPart);
        _clearHost = e.NameScope.Find<PresenceHost>(ClearHostPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);

        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
            _trigger.PointerReleased += OnTriggerReleased;
            _trigger.PointerCaptureLost += OnTriggerCaptureLost;
            _trigger.SizeChanged += OnTriggerSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _trigger;
            _popup.OverlayInputPassThroughElement = _trigger;
            _popup.Closed += OnPopupClosed;
        }
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
            _clear.Bind(Avalonia.Automation.AutomationProperties.NameProperty,
                _clear.GetResourceObservable(TreeSelectLangResources.Key(TreeSelectLangResourceKind.Clear)));
        }
        UpdatePalette();
        UpdateModeClasses();
        _rendered = false;
        UpdateField(animate: false);
        UpdateClear();
        UpdateSearchVisibility();
        if (IsOpen)
        {
            OpenPopup();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loaded)
        {
            Reload();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SetCurrentValue(IsOpenProperty, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsOpenProperty)
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
            UpdateSearchVisibility();
            (ControlAutomationPeer.FromElement(this) as SelectFieldAutomationPeer)?.RaiseExpandChanged(open);
        }
        else if (property == ItemsSourceProperty)
        {
            _expandApplied = false;
            Reload();
        }
        else if (property == ChildrenSelectorProperty || property == ValueSelectorProperty || property == TextSelectorProperty
                 || property == DisabledSelectorProperty || property == IconSelectorProperty)
        {
            Reload();
        }
        else if (property == SelectionModeProperty)
        {
            UpdateModeClasses();
            UpdatePalette();
            UpdateReported(null);
            UpdateSearchVisibility();
        }
        else if (property == CheckStrategyProperty)
        {
            UpdateReported(null);
        }
        else if (property == SelectedItemProperty && !_syncing)
        {
            OnSelectedItemWritten(change.NewValue);
        }
        else if (property == SelectedValueProperty && !_syncing)
        {
            OnSelectedValueWritten(change.NewValue);
        }
        else if (property == ShowFullPathProperty || property == PathSeparatorProperty || property == PlaceholderProperty
                 || property == MaxVisibleChipsProperty)
        {
            UpdateField(animate: false);
        }
        else if (property == SearchTextProperty && !_syncingSearch)
        {
            SetQuery(change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (property == FilterPredicateProperty && _query.Length > 0)
        {
            Refilter();
        }
        else if (property == IsSearchableProperty)
        {
            if (!IsSearchable)
            {
                SetCurrentValue(SearchTextProperty, null);
            }
            UpdateSearchVisibility();
        }
        else if (property == ExpandDepthProperty)
        {
            _expandApplied = false;
            (_expandedBeforeSearch ?? _expanded).Clear();
            ApplyExpandDepth();
            RebuildRows(animate: false);
        }
        else if (property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
            UpdateClear();
        }
        else if (property == IsClearableProperty)
        {
            UpdateClear();
        }
        else if (property == MaxDropDownHeightProperty && _scroll is not null)
        {
            _scroll.MaxHeight = ScrollHeightLimit();
        }
        else if (property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            UpdateClear();
        }
        else if (Array.IndexOf(s_paletteProperties, property) >= 0)
        {
            UpdatePalette();
        }
    }

    private void UpdateModeClasses()
    {
        var multiple = SelectionMode == TreeSelectMode.Multiple;
        PseudoClasses.Set(MultiplePseudoClass, multiple);
        if (_chips is not null)
        {
            _chips.IsVisible = multiple;
        }
    }

    private void UpdatePalette()
    {
        var p = Palette;
        p.Foreground = RowForeground;
        p.Muted = MutedForeground;
        p.Icon = IconForeground;
        p.CheckOn = CheckOnBrush;
        p.CheckGlyph = CheckGlyphBrush;
        p.CheckBorder = CheckBorderBrush;
        p.CheckSurface = CheckSurfaceBrush;
        p.Match = MatchBrush;
        p.FontFamily = FontFamily;
        p.FontSize = FontSize;
        p.SlotHeight = RowHeight;
        p.RowGap = RowGap;
        p.Indent = IndentWidth;
        p.InlinePadding = RowInlinePadding;
        p.Gap = ContentGap;
        p.CheckboxSize = CheckboxSize;
        p.CheckboxRadius = CheckboxRadius;
        p.CheckStroke = CheckStrokeThickness;
        p.IconSize = IconSize;
        p.DisabledOpacity = RowDisabledOpacity;
        p.MatchRadius = MatchRadius;
        p.ShowCheckboxes = SelectionMode == TreeSelectMode.Multiple;
        p.ShowIcons = _anyIcon;
        p.Version++;
        if (_panel is not null)
        {
            _panel.RowHeight = RowHeight;
            _panel.Highlight.Fill = HighlightBrush;
            _panel.Highlight.CornerRadius = new CornerRadius(RowRadius);
            _panel.HighlightInset = new Thickness(0, RowGap, 0, 0);
            foreach (var row in _panel.RealizedRows)
            {
                row.Refresh();
            }
            _panel.InvalidateMeasure();
        }
    }

    #endregion

    #region Data

    // Inline TreeViewItem nodes (as TreeView takes them) need no selectors: header, children, icon and disabled come from them.
    private string TextOf(object item) =>
        (TextSelector?.Invoke(item) ?? (item as TreeViewItem)?.Header ?? item.ToString()) ?? string.Empty;

    private object? ValueOf(object item) => ValueSelector is { } selector ? selector(item) : item;

    /// <summary>Reads the whole tree again (data or a selector changed) and re-applies the selection by value.</summary>
    private void Reload()
    {
        if (_rootsNotifier is not null)
        {
            _rootsNotifier.CollectionChanged -= OnRootsChanged;
        }
        _rootsNotifier = ItemsSource as INotifyCollectionChanged;
        if (_rootsNotifier is not null)
        {
            _rootsNotifier.CollectionChanged += OnRootsChanged;
        }
        ReadTree();
    }

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ReadTree();

    private void ReadTree()
    {
        _loaded = true;
        // The values are the durable selection: checks survive a reload through them.
        var values = _pendingValues ?? (SelectionMode == TreeSelectMode.Multiple ? _selectedValues : null);
        var icon = IconSelector;
        var disabled = DisabledSelector;
        var nodes = new Dictionary<object, TreeSelectNode>(Math.Max(16, _nodes.Count));
        var byValue = new Dictionary<object, TreeSelectNode>(Math.Max(16, _byValue.Count));
        _warnedDuplicate = false;
        _anyIcon = false;
        _roots.Clear();
        Build(ItemsSource, null, 1, _roots, nodes, byValue, icon, disabled);
        _nodes = nodes;
        _byValue = byValue;
        _expanded.RemoveWhere(item => !nodes.ContainsKey(item));
        _matched.Clear();
        _filterVisible.Clear();
        ApplyExpandDepth();

        if (_roots.Count > 0)
        {
            _pendingValues = null;
            if (values is { Count: > 0 })
            {
                CheckValues(values);
            }
        }
        else if (values is { Count: > 0 } && SelectionMode == TreeSelectMode.Multiple)
        {
            // No data (yet): the values wait for it.
            _pendingValues = values;
        }
        Recompute();
        // Single mode: find the selection again (a value the data lacks is kept, as Cascader does).
        if (SelectedValue is { } value)
        {
            SetSelected(byValue.GetValueOrDefault(value), writeValue: false);
        }
        else if (SelectedItem is { } item)
        {
            SetSelected(nodes.GetValueOrDefault(item), writeValue: true);
        }
        else
        {
            SetSelected(null, writeValue: false);
        }
        UpdateReported(values);
        if (_query.Length > 0)
        {
            Refilter();
        }
        else if (IsOpen)
        {
            RebuildRows(animate: false);
        }
        UpdatePalette();
    }

    private void Build(IEnumerable? source, TreeSelectNode? parent, int depth, List<TreeSelectNode> into,
                       Dictionary<object, TreeSelectNode> nodes, Dictionary<object, TreeSelectNode> byValue,
                       Func<object, Geometry?>? icon, Func<object, bool>? disabled)
    {
        if (source is null)
        {
            return;
        }
        var start = into.Count;
        foreach (var item in source)
        {
            if (item is null || nodes.ContainsKey(item))
            {
                continue;
            }
            var node = new TreeSelectNode(item)
            {
                Parent = parent,
                Depth = depth,
                Label = TextOf(item),
                Value = ValueOf(item),
                Icon = icon is not null ? icon(item) : (item as TreeViewItem)?.Icon,
                IsDisabled = disabled is not null ? disabled(item) : item is TreeViewItem { IsDisabled: true }
            };
            _anyIcon |= node.Icon is not null;
            nodes[item] = node;
            if (node.Value is { } value && !byValue.TryAdd(value, node) && !_warnedDuplicate)
            {
                _warnedDuplicate = true;
                Debug.WriteLine($"TreeSelect: the value '{value}' belongs to more than one node; the first one wins. Values must be unique across the tree.");
            }
            into.Add(node);
            if ((ChildrenSelector is { } selector ? selector(item) : (item as TreeViewItem)?.Items) is { } children)
            {
                var list = new List<TreeSelectNode>();
                Build(children, node, depth + 1, list, nodes, byValue, icon, disabled);
                if (list.Count > 0)
                {
                    node.Children = list;
                }
            }
        }
        var size = into.Count - start;
        for (var i = start; i < into.Count; i++)
        {
            into[i].Position = i - start + 1;
            into[i].SetSize = size;
        }
    }

    private void ApplyExpandDepth()
    {
        if (_expandApplied || _roots.Count == 0)
        {
            return;
        }
        _expandApplied = true;
        var depth = ExpandDepth;
        if (depth == 0)
        {
            return;
        }
        var target = _expandedBeforeSearch ?? _expanded;
        foreach (var node in _nodes.Values)
        {
            if (node.HasChildren && (depth < 0 || node.Depth <= depth))
            {
                target.Add(node.Item);
            }
        }
    }

    #endregion

    #region Checks and selection

    /// <summary>Sets the checks from <paramref name="values"/> (multiple mode): a parent's value checks its enabled descendants.</summary>
    private void CheckValues(IReadOnlyList<object?> values)
    {
        foreach (var node in _nodes.Values)
        {
            node.IsChecked = false;
        }
        foreach (var value in values)
        {
            if (value is not null && _byValue.TryGetValue(value, out var node))
            {
                if (node.HasChildren)
                {
                    SetSubtree(node, true);
                }
                else
                {
                    node.IsChecked = true;
                }
            }
        }
    }

    /// <summary>Checks or unchecks the leaves under <paramref name="node"/>, skipping disabled nodes and their subtrees.</summary>
    private static bool SetSubtree(TreeSelectNode node, bool on)
    {
        if (!node.HasChildren)
        {
            var changed = node.IsChecked != on;
            node.IsChecked = on;
            return changed;
        }
        var any = false;
        foreach (var child in node.Children!)
        {
            if (!child.IsDisabled)
            {
                any |= SetSubtree(child, on);
            }
        }
        return any;
    }

    /// <summary>Derives every parent's three-state check from its children (disabled children do not hold a parent back).</summary>
    private void Recompute()
    {
        foreach (var root in _roots)
        {
            StateOf(root);
        }
    }

    private static CheckboxState StateOf(TreeSelectNode node)
    {
        if (!node.HasChildren)
        {
            node.State = node.IsChecked ? CheckboxState.Checked : CheckboxState.Unchecked;
            return node.State;
        }
        int enabled = 0, enabledChecked = 0, all = 0, allChecked = 0;
        var any = false;
        foreach (var child in node.Children!)
        {
            var state = StateOf(child);
            all++;
            if (state != CheckboxState.Unchecked)
            {
                any = true;
            }
            if (state == CheckboxState.Checked)
            {
                allChecked++;
            }
            if (!child.IsDisabled)
            {
                enabled++;
                if (state == CheckboxState.Checked)
                {
                    enabledChecked++;
                }
            }
        }
        node.State = (enabled > 0 ? enabledChecked == enabled : allChecked == all)
            ? CheckboxState.Checked
            : any ? CheckboxState.Indeterminate : CheckboxState.Unchecked;
        return node.State;
    }

    /// <summary>The user toggles a node's check (multiple mode).</summary>
    internal void ToggleNode(TreeSelectNode node)
    {
        if (node.IsDisabled || IsReadOnly || !IsEffectivelyEnabled)
        {
            return;
        }
        var on = node.State != CheckboxState.Checked;
        if (!SetSubtree(node, on) && on)
        {
            // Nothing left to check (the rest is disabled): the click unchecks instead.
            SetSubtree(node, false);
        }
        Recompute();
        UpdateReported(null);
        Announce(node);
    }

    /// <summary>The user picks a node (single mode).</summary>
    internal void PickNode(TreeSelectNode node)
    {
        if (node.IsDisabled)
        {
            if (node.HasChildren)
            {
                SetNodeExpanded(node, !node.IsExpanded);
            }
            return;
        }
        if (LeafOnly && node.HasChildren)
        {
            SetNodeExpanded(node, !node.IsExpanded);
            return;
        }
        if (!IsReadOnly)
        {
            SetSelected(node, writeValue: true);
        }
        Close();
        Focus(FocusModality.For(this));
    }

    internal bool IsSelectedNode(TreeSelectNode node) => SelectionMode == TreeSelectMode.Single && ReferenceEquals(_selected, node);

    private void OnSelectedItemWritten(object? item)
    {
        if (item is null)
        {
            SetSelected(null, writeValue: true);
            return;
        }
        SetSelected(_nodes.GetValueOrDefault(item), writeValue: true);
    }

    private void OnSelectedValueWritten(object? value)
    {
        // Not found: the value stays (the data may still be loading).
        SetSelected(value is null ? null : _byValue.GetValueOrDefault(value), writeValue: false);
    }

    /// <summary>Makes <paramref name="node"/> the single selection and updates the item and value without echoing.</summary>
    private void SetSelected(TreeSelectNode? node, bool writeValue)
    {
        _syncing = true;
        try
        {
            var item = node?.Item;
            if (node is not null || writeValue)
            {
                if (!Equals(SelectedItem, item))
                {
                    SetCurrentValue(SelectedItemProperty, item);
                }
            }
            else if (SelectedItem is not null && !_nodes.ContainsKey(SelectedItem))
            {
                SetCurrentValue(SelectedItemProperty, null);
            }
            if (writeValue)
            {
                var value = node?.Value;
                if (!Equals(SelectedValue, value))
                {
                    SetCurrentValue(SelectedValueProperty, value);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
        if (ReferenceEquals(_selected, node))
        {
            return;
        }
        var old = _selected;
        _selected = node;
        if (_panel is not null)
        {
            if (old is not null)
            {
                _panel.RowFor(old.Item)?.Refresh();
            }
            if (node is not null)
            {
                _panel.RowFor(node.Item)?.Refresh();
            }
        }
        UpdateReported(null);
    }

    private void WriteSelectedValues(IReadOnlyList<object?> values)
    {
        if (_syncing)
        {
            return;
        }
        if (SelectionMode == TreeSelectMode.Single)
        {
            SetCurrentValue(SelectedValueProperty, values.Count > 0 ? values[0] : null);
            UpdateReported(values);
            return;
        }
        if (_roots.Count == 0)
        {
            // No data yet: keep the values as written and apply them when the data arrives.
            _pendingValues = values.Count > 0 ? values : null;
            SetAndRaise(SelectedValuesProperty, ref _selectedValues, values);
            return;
        }
        _pendingValues = null;
        CheckValues(values);
        Recompute();
        UpdateReported(values);
    }

    /// <summary>
    /// Recomputes <see cref="SelectedItems"/> and <see cref="SelectedValues"/> from the checks or the single selection,
    /// raises <see cref="SelectionChanged"/> when the items changed and refreshes the field and the rows.
    /// <paramref name="written"/> is the list a binding just wrote: it is kept when it says the same.
    /// </summary>
    private void UpdateReported(IReadOnlyList<object?>? written)
    {
        List<TreeSelectNode> reported = new();
        if (SelectionMode == TreeSelectMode.Multiple)
        {
            Collect(_roots, CheckStrategy, reported);
        }
        else if (_selected is not null)
        {
            reported.Add(_selected);
        }
        var oldItems = _selectedItems;
        var same = oldItems.Count == reported.Count;
        for (var i = 0; same && i < reported.Count; i++)
        {
            same = ReferenceEquals(oldItems[i], reported[i].Item);
        }
        if (!same)
        {
            var items = reported.Select(n => n.Item).ToArray();
            SelectedItems = items;
            var oldSet = new HashSet<object>(oldItems);
            var newSet = new HashSet<object>(items);
            var added = items.Where(i => !oldSet.Contains(i)).ToArray();
            var removed = oldItems.Where(i => !newSet.Contains(i)).ToArray();
            RaiseEvent(new TreeSelectSelectionChangedEventArgs(SelectionChangedEvent, added, removed));
        }
        if (_pendingValues is null)
        {
            var values = reported.Select(n => n.Value).ToArray();
            IReadOnlyList<object?> next = written is not null && written.SequenceEqual(values) ? written
                : _selectedValues.SequenceEqual(values) ? _selectedValues : values;
            if (SelectionMode == TreeSelectMode.Single && _selected is null && SelectedValue is { } kept)
            {
                // A single value the data does not contain yet is reported as written.
                next = _selectedValues.Count == 1 && Equals(_selectedValues[0], kept) ? _selectedValues : new[] { kept };
            }
            if (!ReferenceEquals(next, _selectedValues))
            {
                _syncing = true;
                try
                {
                    SetAndRaise(SelectedValuesProperty, ref _selectedValues, next);
                }
                finally
                {
                    _syncing = false;
                }
            }
        }
        UpdateField(animate: true);
        UpdateClear();
        if (_panel is not null)
        {
            foreach (var row in _panel.RealizedRows)
            {
                row.Refresh();
            }
        }
    }

    private static void Collect(List<TreeSelectNode> nodes, TreeSelectCheckStrategy strategy, List<TreeSelectNode> into)
    {
        foreach (var node in nodes)
        {
            if (!node.HasChildren)
            {
                if (node.State == CheckboxState.Checked)
                {
                    into.Add(node);
                }
                continue;
            }
            if (node.State == CheckboxState.Unchecked)
            {
                continue;
            }
            if (node.State == CheckboxState.Checked && strategy != TreeSelectCheckStrategy.Leaves)
            {
                into.Add(node);
                if (strategy == TreeSelectCheckStrategy.Parents)
                {
                    continue;
                }
            }
            Collect(node.Children!, strategy, into);
        }
    }

    #endregion

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

    #region Search

    private void UpdateSearchVisibility()
    {
        var show = IsOpen && IsSearchable && IsEffectivelyEnabled;
        if (show)
        {
            EnsureSearch();
        }
        if (_search is not null)
        {
            _search.IsVisible = show;
        }
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
        if (_emptyHost is not null)
        {
            _emptyHost.IsPresent = _query.Length > 0 && _rows.Count == 0;
        }
    }

    #endregion

    #region Rows

    /// <summary>Flattens the open part of the tree (filtered while searching) into the rows panel.</summary>
    private void RebuildRows(bool animate, object? staggerAfter = null)
    {
        var rows = new List<object>(Math.Max(16, _rows.Count));
        var filtering = _query.Length > 0;
        Visit(_roots, rows, filtering);
        _rows = rows;
        if (_panel is null)
        {
            return;
        }
        _panel.SetRows(rows, animate && IsLoaded, staggerAfter);
        if (_activeKey is not null && _panel.IndexOfKey(_activeKey) < 0)
        {
            _activeKey = null;
            _panel.SetHighlight(null, animate: false);
        }
    }

    private void Visit(List<TreeSelectNode> nodes, List<object> rows, bool filtering)
    {
        foreach (var node in nodes)
        {
            if (filtering && !_filterVisible.Contains(node))
            {
                node.IsExpanded = false;
                continue;
            }
            node.IsExpanded = node.HasChildren && _expanded.Contains(node.Item);
            rows.Add(node);
            if (node.IsExpanded)
            {
                Visit(node.Children!, rows, filtering);
            }
        }
    }

    internal void SetNodeExpanded(TreeSelectNode node, bool expanded)
    {
        if (!node.HasChildren)
        {
            return;
        }
        var changed = expanded ? _expanded.Add(node.Item) : _expanded.Remove(node.Item);
        if (!changed)
        {
            return;
        }
        if (!expanded && _activeKey is not null && _nodes.TryGetValue(_activeKey, out var active) && IsDescendant(active, node))
        {
            SetActive(node.Item, animate: false, scroll: false);
        }
        RebuildRows(animate: true, expanded ? node.Item : null);
        if (_panel?.RowFor(node.Item) is { } row)
        {
            row.Refresh();
        }
    }

    private static bool IsDescendant(TreeSelectNode node, TreeSelectNode ancestor)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Opens the ancestors of the selection, so the menu opens on it.</summary>
    private void ExpandToSelection()
    {
        if (SelectionMode == TreeSelectMode.Single)
        {
            if (_selected is not null)
            {
                ExpandAncestors(_selected);
            }
            return;
        }
        foreach (var item in _selectedItems)
        {
            if (_nodes.TryGetValue(item, out var node))
            {
                ExpandAncestors(node);
            }
        }
    }

    private void ExpandAncestors(TreeSelectNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            _expanded.Add(parent.Item);
        }
    }

    private object? DefaultActive()
    {
        if (SelectionMode == TreeSelectMode.Single && _selected is not null && _panel?.IndexOfKey(_selected.Item) >= 0)
        {
            return _selected.Item;
        }
        return _rows.Count > 0 ? ((TreeSelectNode)_rows[0]).Item : null;
    }

    private void SetActive(object? key, bool animate, bool scroll)
    {
        _activeKey = key;
        if (_panel is null)
        {
            return;
        }
        _panel.SetHighlight(key, animate);
        if (scroll && key is not null)
        {
            ScrollToRow(key);
        }
    }

    private void ScrollToRow(object key)
    {
        if (_panel is null || _scroll is null)
        {
            return;
        }
        var index = _panel.IndexOfKey(key);
        if (index < 0)
        {
            return;
        }
        var top = _panel.SettledRowTop(index);
        var bottom = top + RowHeight;
        var offset = _scroll.Offset.Y;
        var viewport = _scroll.Viewport.Height;
        if (viewport <= 0)
        {
            return;
        }
        if (top < offset)
        {
            _scroll.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, top));
        }
        else if (bottom > offset + viewport)
        {
            _scroll.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, bottom - viewport));
        }
    }

    internal void OnRowHovered(TreeSelectRow row)
    {
        if (row.Node is { } node)
        {
            SetActive(node.Item, animate: true, scroll: false);
        }
    }

    internal void OnRowsPointerExited()
    {
        if (IsOpen && _activeKey is not null)
        {
            _activeKey = null;
            _panel?.SetHighlight(null, animate: false);
        }
    }

    internal void OnRowClicked(TreeSelectRow row, bool onChevron)
    {
        if (row.Node is not { } node)
        {
            return;
        }
        SetActive(node.Item, animate: false, scroll: false);
        if (onChevron)
        {
            SetNodeExpanded(node, !node.IsExpanded);
        }
        else if (SelectionMode == TreeSelectMode.Multiple)
        {
            if (node.IsDisabled)
            {
                SetNodeExpanded(node, !node.IsExpanded);
            }
            else
            {
                ToggleNode(node);
            }
        }
        else
        {
            PickNode(node);
            return;
        }
        if (IsOpen && _search is { IsVisible: true } search && !search.IsFocused)
        {
            search.Focus(NavigationMethod.Pointer);
        }
    }

    private void Announce(TreeSelectNode node)
    {
        if (_announcer is null)
        {
            return;
        }
        var text = $"{node.Label}, {TreeSelectText.Level(node.Depth)}";
        if (SelectionMode == TreeSelectMode.Multiple)
        {
            text += ", " + TreeSelectText.StateOf(node.State);
        }
        else if (ReferenceEquals(node, _selected))
        {
            text += ", " + TreeSelectLangResources.Get(TreeSelectLangResourceKind.Selected);
        }
        _announcer.Text = text;
    }

    #endregion

    #region Popup

    private double ScrollHeightLimit() => Math.Max(0, MaxDropDownHeight - 12);

    /// <summary>Builds the menu on the first open: surface, scroller, rows panel and the empty search line.</summary>
    private void EnsureMenu()
    {
        if (_panel is not null || _popup is null)
        {
            return;
        }
        _panel = new TreeSelectRowsPanel { Name = RowsPart, Owner = this };
        _scroll = new ScrollViewer
        {
            Name = "PART_Scroll",
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = ScrollHeightLimit(),
            Content = _panel
        };
        var empty = new TextBlock { TextWrapping = TextWrapping.Wrap }
            .Token(TextBlock.PaddingProperty, TreeSelectTokens.Key(TreeSelectTokenKind.EmptyPadding))
            .Token(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        empty.Bind(TextBlock.TextProperty, empty.GetResourceObservable(TreeSelectLangResources.Key(TreeSelectLangResourceKind.NoMatches)));
        _emptyHost = new PresenceHost { Name = "PART_EmptyHost", Mode = PresenceMode.Rise, Offset = 4, IsPresent = false, Child = empty };
        DockPanel.SetDock(_emptyHost, Dock.Top);
        _surface = SelectMenuParts.Surface(new DockPanel { Children = { _emptyHost, _scroll } })
            .Token(Border.PaddingProperty, TreeSelectTokens.Key(TreeSelectTokenKind.MenuPadding))
            .Token(Border.CornerRadiusProperty, TreeSelectTokens.Key(TreeSelectTokenKind.MenuCornerRadius));
        _popup.Child = _surface;
        UpdatePalette();
    }

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
        EnsureMenu();
        if (_popup.IsOpen)
        {
            return;
        }
        if (_query.Length == 0)
        {
            ExpandToSelection();
        }
        RebuildRows(animate: false);
        UpdateEmpty();
        UpdateSurfaceWidth();
        var active = DefaultActive();
        if (_query.Length > 0)
        {
            Refilter();
            active = _activeKey;
        }
        _popup.IsOpen = true;
        SetActive(active, animate: false, scroll: false);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen)
            {
                return;
            }
            if (_activeKey is { } key)
            {
                _panel?.SetHighlight(key, animate: false);
                ScrollToRow(key);
            }
            if (IsSearchable && _search is { IsVisible: true } search)
            {
                search.Focus(FocusModality.For(this) == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
                search.CaretIndex = search.Text?.Length ?? 0;
            }
        }, DispatcherPriority.Loaded);
    }

    private void ClosePopup()
    {
        if (_search?.IsKeyboardFocusWithin == true)
        {
            // Back to the field before the input hides, with the ring only after keyboard use.
            Focus(FocusModality.For(this));
        }
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        _activeKey = null;
        _panel?.SetHighlight(null, animate: false);
        if (!string.IsNullOrEmpty(SearchText))
        {
            SetCurrentValue(SearchTextProperty, null);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void OnTriggerSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateSurfaceWidth();

    private void UpdateSurfaceWidth()
    {
        if (_surface is null)
        {
            return;
        }
        var min = this.TryFindResource(TreeSelectTokens.Key(TreeSelectTokenKind.PopupMinWidth), out var value) && value is double d ? d : 240;
        var trigger = _trigger?.Bounds.Width ?? 0;
        _surface.Width = Math.Max(min, trigger);
    }

    #endregion

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

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
public partial class TreeSelect : TemplatedControl, ISelectFieldAutomation
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
        AvaloniaProperty.Register<TreeSelect, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

    /// <summary>Value of the selected node of a single-select field (two-way). A value the data does not contain (yet) is kept and resolved when the data arrives.</summary>
    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<TreeSelect, object?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

    /// <summary>The selected nodes in tree order: the checked nodes that <see cref="CheckStrategy"/> reports, or the single selected node.</summary>
    public static readonly DirectProperty<TreeSelect, IReadOnlyList<object>> SelectedItemsProperty =
        AvaloniaProperty.RegisterDirect<TreeSelect, IReadOnlyList<object>>(nameof(SelectedItems), o => o.SelectedItems);

    /// <summary>Values of <see cref="SelectedItems"/> (two-way). Setting it checks the nodes with these values (a parent's value checks its enabled descendants); values written before the data arrives are applied when it does.</summary>
    public static readonly DirectProperty<TreeSelect, IReadOnlyList<object?>> SelectedValuesProperty =
        AvaloniaProperty.RegisterDirect<TreeSelect, IReadOnlyList<object?>>(nameof(SelectedValues), o => o.SelectedValues,
            (o, v) => o.SelectedValues = v, Array.Empty<object?>(), BindingMode.TwoWay, enableDataValidation: true);

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
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(IsReadOnly),
            coerce: (o, v) => v || o.GetValue(Form.IsReadOnlyProperty));

    /// <summary>Whether the menu is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<TreeSelect, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Tallest the menu gets before the tree scrolls.</summary>
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<TreeSelect, double>(nameof(MaxDropDownHeight), 320);

    /// <summary>Defines the <see cref="MenuMinWidth"/> property.</summary>
    public static readonly StyledProperty<double> MenuMinWidthProperty = Select.MenuMinWidthProperty.AddOwner<TreeSelect>();

    /// <summary>Defines the <see cref="MenuMaxWidth"/> property.</summary>
    public static readonly StyledProperty<double> MenuMaxWidthProperty = Select.MenuMaxWidthProperty.AddOwner<TreeSelect>();

    /// <summary>Defines the <see cref="ShowTrimmedOptionToolTip"/> property.</summary>
    public static readonly StyledProperty<bool> ShowTrimmedOptionToolTipProperty = Select.ShowTrimmedOptionToolTipProperty.AddOwner<TreeSelect>();

    /// <summary>Raised when <see cref="SelectedItems"/> changes: by the user, by a binding or by clearing.</summary>
    public static readonly RoutedEvent<TreeSelectSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<TreeSelect, TreeSelectSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="Field.LabelPlacementProperty"/>
    public static readonly AttachedProperty<FormLabelPlacement> LabelPlacementProperty =
        Field.LabelPlacementProperty.AddOwner<TreeSelect>();

    /// <inheritdoc cref="Field.LabelWidthProperty"/>
    public static readonly AttachedProperty<double> LabelWidthProperty =
        Field.LabelWidthProperty.AddOwner<TreeSelect>();

    /// <inheritdoc cref="Field.LabelFontWeightProperty"/>
    public static readonly AttachedProperty<FontWeight?> LabelFontWeightProperty =
        Field.LabelFontWeightProperty.AddOwner<TreeSelect>();

    /// <summary>Where the label sits: Top (default) or Left of the editor. Inherited from <c>arc:Field.LabelPlacement</c> on a container.</summary>
    public FormLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>Width of the left label column; NaN (default) takes the widest left label of the scope, up to 240 px.</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>
    /// Font weight of the visible field label. Set it on a container to apply it to the Arc field labels inside,
    /// or on a single field. Null uses the label's theme weight. This property only styles the label text.
    /// </summary>
    public FontWeight? LabelFontWeight { get => GetValue(LabelFontWeightProperty); set => SetValue(LabelFontWeightProperty, value); }

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

    /// <summary>The narrowest the menu gets (default 0; the theme's popup minimum applies as well). The menu is as wide as its widest row needs, never narrower than the field or this value, and never wider than <see cref="MenuMaxWidth"/> or the window.</summary>
    public double MenuMinWidth { get => GetValue(MenuMinWidthProperty); set => SetValue(MenuMinWidthProperty, value); }

    /// <summary>The widest the menu gets (default infinity: bounded only by the window). Rows wider than this are trimmed.</summary>
    public double MenuMaxWidth { get => GetValue(MenuMaxWidthProperty); set => SetValue(MenuMaxWidthProperty, value); }

    /// <summary>Whether a row whose text is trimmed shows its full text in a tooltip on hover (default true).</summary>
    public bool ShowTrimmedOptionToolTip { get => GetValue(ShowTrimmedOptionToolTipProperty); set => SetValue(ShowTrimmedOptionToolTipProperty, value); }

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

    /// <summary>Trigger height: Small 36, Medium 44 (default), Large 50, as for the text fields.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        Field.SizeProperty.AddOwner<TreeSelect>();

    /// <summary>Trigger height: Small 36, Medium 44 (default), Large 50, as for the text fields.</summary>
    public ControlSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

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
        _clear?.Click -= OnClearClick;
        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchPropertyChanged;
            _searchHost?.Child = null;
            _search = null;
        }
        _panel?.Owner = null;
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
        if (change.Property == Form.IsReadOnlyProperty)
        {
            // Form.IsReadOnly (inherited) makes the editor read-only with its form or section.
            CoerceValue(IsReadOnlyProperty);
        }
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
        else if (property == MenuMinWidthProperty || property == MenuMaxWidthProperty)
        {
            UpdateSurfaceWidth();
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
            UpdateSurfaceWidth();
        }
    }

    private void UpdateModeClasses()
    {
        var multiple = SelectionMode == TreeSelectMode.Multiple;
        PseudoClasses.Set(MultiplePseudoClass, multiple);
        _chips?.IsVisible = multiple;
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

}

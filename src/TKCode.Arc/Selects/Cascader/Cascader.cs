using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc cascader: a select field that picks one node of a hierarchy, level by level (province, city, district).
/// Opening the menu shows the first level; choosing a parent opens its children in a column to the right, and the
/// menu grows with a spring. Look and motion follow <see cref="Select"/>: the same trigger, menu surface, gliding
/// highlight and clear button.
/// <para>Nodes are any objects: <see cref="ChildrenSelector"/> gives the children, <see cref="TextSelector"/> the
/// label and <see cref="ValueSelector"/> the value, which must be unique across the whole tree. Everything goes
/// through delegates, so nothing is reflected. <see cref="SelectedItem"/>, <see cref="SelectedValue"/> and
/// <see cref="SelectedPath"/> always agree; a value written before the data arrives is kept and resolved when it
/// does.</para>
/// <para>Keyboard: Enter, Space and the arrows open; Up and Down move in the column, Right opens the highlighted
/// parent, Left goes back a column; Enter picks a leaf (or opens a parent); Escape closes without a change; typing
/// jumps to the next matching node; Delete clears.</para>
/// </summary>
[TemplatePart(TriggerPart, typeof(Border))]
[TemplatePart(ValuePart, typeof(SelectValueRoll))]
[TemplatePart(ClearHostPart, typeof(PresenceHost))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(ColumnsPart, typeof(CascaderColumnsPanel))]
[PseudoClasses(OpenPseudoClass, PlaceholderPseudoClass, PressedPseudoClass, ReadOnlyPseudoClass)]
public class Cascader : TemplatedControl, ISelectFieldAutomation, ICascaderColumnOwner
{
    public const string TriggerPart = "PART_Trigger";
    public const string ValuePart = "PART_Value";
    public const string ClearHostPart = "PART_ClearHost";
    public const string ClearPart = "PART_Clear";
    public const string PopupPart = "PART_Popup";
    public const string SurfacePart = "PART_Surface";
    public const string ColumnsPart = "PART_Columns";
    public const string AnnouncerPart = "PART_Announcer";
    public const string OpenPseudoClass = ":open";
    public const string PlaceholderPseudoClass = ":placeholder";
    public const string PressedPseudoClass = ":pressed";
    public const string ReadOnlyPseudoClass = ":readonly";

    #region 公共属性定义

    /// <summary>Visible label above the trigger; also the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Cascader, string?>(nameof(Label));

    /// <summary>Muted hint under the trigger; also the accessible help text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Cascader, string?>(nameof(Description));

    /// <summary>Muted text shown while nothing is selected.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<Cascader, string?>(nameof(Placeholder), "Select an option");

    /// <summary>The root nodes (first column).</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Cascader, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Children of a node; same shape as the one of <c>TreeView</c>. Null or empty means a leaf.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<Cascader, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Decides whether a node is a leaf; when null, a node without children is one.</summary>
    public static readonly StyledProperty<Func<object, bool>?> IsLeafSelectorProperty =
        AvaloniaProperty.Register<Cascader, Func<object, bool>?>(nameof(IsLeafSelector));

    /// <summary>Label of a node; when null, <c>ToString()</c>.</summary>
    public static readonly StyledProperty<Func<object, string?>?> TextSelectorProperty =
        AvaloniaProperty.Register<Cascader, Func<object, string?>?>(nameof(TextSelector));

    /// <summary>Value of a node; it must be unique across the tree. When null, the node itself is the value.</summary>
    public static readonly StyledProperty<Func<object, object?>?> ValueSelectorProperty =
        AvaloniaProperty.Register<Cascader, Func<object, object?>?>(nameof(ValueSelector));

    /// <summary>Returns true for nodes that are shown but cannot be picked or opened.</summary>
    public static readonly StyledProperty<Func<object, bool>?> DisabledSelectorProperty =
        AvaloniaProperty.Register<Cascader, Func<object, bool>?>(nameof(DisabledSelector));

    /// <summary>The selected node (two-way), or null.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<Cascader, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Value of the selected node (two-way). A value that the data does not contain (yet) is kept: the field shows the placeholder until the data arrives, then resolves it.</summary>
    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<Cascader, object?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The nodes from the root to the selected node; empty when nothing is selected.</summary>
    public static readonly DirectProperty<Cascader, IReadOnlyList<object>> SelectedPathProperty =
        AvaloniaProperty.RegisterDirect<Cascader, IReadOnlyList<object>>(nameof(SelectedPath), o => o.SelectedPath);

    /// <summary>The text of the field: the full path or the selected node alone, see <see cref="ShowFullPath"/>.</summary>
    public static readonly DirectProperty<Cascader, string?> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<Cascader, string?>(nameof(SelectedText), o => o.SelectedText);

    /// <summary>When true (the default) only leaves can be picked; parents just open their children.</summary>
    public static readonly StyledProperty<bool> LeafOnlyProperty =
        AvaloniaProperty.Register<Cascader, bool>(nameof(LeafOnly), true);

    /// <summary>Whether a parent opens on click only, or also while the pointer rests on it.</summary>
    public static readonly StyledProperty<CascaderExpandTrigger> ExpandTriggerProperty =
        AvaloniaProperty.Register<Cascader, CascaderExpandTrigger>(nameof(ExpandTrigger));

    /// <summary>Show the whole path in the field (true), or only the selected node (false).</summary>
    public static readonly StyledProperty<bool> ShowFullPathProperty =
        AvaloniaProperty.Register<Cascader, bool>(nameof(ShowFullPath), true);

    /// <summary>Text between the nodes of the full path.</summary>
    public static readonly StyledProperty<string> PathSeparatorProperty =
        AvaloniaProperty.Register<Cascader, string>(nameof(PathSeparator), " / ");

    /// <summary>Whether a clear button shows while a value is selected.</summary>
    public static readonly StyledProperty<bool> IsClearableProperty =
        AvaloniaProperty.Register<Cascader, bool>(nameof(IsClearable), true);

    /// <summary>The menu opens for looking, but the value cannot be changed or cleared.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<Cascader, bool>(nameof(IsReadOnly));

    /// <summary>Whether the menu is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Cascader, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Tallest a column gets before it scrolls.</summary>
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<Cascader, double>(nameof(MaxDropDownHeight), 320);

    /// <summary>Width of every column; NaN sizes each column to its labels (160 to 280).</summary>
    public static readonly StyledProperty<double> ColumnWidthProperty =
        AvaloniaProperty.Register<Cascader, double>(nameof(ColumnWidth), double.NaN);

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="ChildrenSelectorProperty"/>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => GetValue(ChildrenSelectorProperty);
        set => SetValue(ChildrenSelectorProperty, value);
    }

    /// <inheritdoc cref="IsLeafSelectorProperty"/>
    public Func<object, bool>? IsLeafSelector
    {
        get => GetValue(IsLeafSelectorProperty);
        set => SetValue(IsLeafSelectorProperty, value);
    }

    /// <inheritdoc cref="TextSelectorProperty"/>
    public Func<object, string?>? TextSelector
    {
        get => GetValue(TextSelectorProperty);
        set => SetValue(TextSelectorProperty, value);
    }

    /// <inheritdoc cref="ValueSelectorProperty"/>
    public Func<object, object?>? ValueSelector
    {
        get => GetValue(ValueSelectorProperty);
        set => SetValue(ValueSelectorProperty, value);
    }

    /// <inheritdoc cref="DisabledSelectorProperty"/>
    public Func<object, bool>? DisabledSelector
    {
        get => GetValue(DisabledSelectorProperty);
        set => SetValue(DisabledSelectorProperty, value);
    }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="SelectedValueProperty"/>
    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <inheritdoc cref="SelectedPathProperty"/>
    public IReadOnlyList<object> SelectedPath => _path;

    /// <inheritdoc cref="SelectedTextProperty"/>
    public string? SelectedText
    {
        get => _selectedText;
        private set => SetAndRaise(SelectedTextProperty, ref _selectedText, value);
    }

    /// <inheritdoc cref="LeafOnlyProperty"/>
    public bool LeafOnly
    {
        get => GetValue(LeafOnlyProperty);
        set => SetValue(LeafOnlyProperty, value);
    }

    /// <inheritdoc cref="ExpandTriggerProperty"/>
    public CascaderExpandTrigger ExpandTrigger
    {
        get => GetValue(ExpandTriggerProperty);
        set => SetValue(ExpandTriggerProperty, value);
    }

    /// <inheritdoc cref="ShowFullPathProperty"/>
    public bool ShowFullPath
    {
        get => GetValue(ShowFullPathProperty);
        set => SetValue(ShowFullPathProperty, value);
    }

    /// <inheritdoc cref="PathSeparatorProperty"/>
    public string PathSeparator
    {
        get => GetValue(PathSeparatorProperty);
        set => SetValue(PathSeparatorProperty, value);
    }

    /// <inheritdoc cref="IsClearableProperty"/>
    public bool IsClearable
    {
        get => GetValue(IsClearableProperty);
        set => SetValue(IsClearableProperty, value);
    }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="MaxDropDownHeightProperty"/>
    public double MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    /// <inheritdoc cref="ColumnWidthProperty"/>
    public double ColumnWidth
    {
        get => GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, value);
    }

    /// <summary>Raised when the selected node changes: by the user, by a binding or by clearing.</summary>
    public event EventHandler<CascaderSelectionChangedEventArgs>? SelectionChanged;

    #endregion

    private static readonly IReadOnlyList<object> s_noPath = Array.Empty<object>();

    private readonly SelectTypeahead _typeahead = new(1000);
    private readonly List<object> _expanded = new();
    private readonly List<CascaderColumn> _columns = new();
    private IReadOnlyList<object> _path = s_noPath;
    private IReadOnlyList<object> _roots = s_noPath;
    private Dictionary<object, IReadOnlyList<object>>? _index;
    private INotifyCollectionChanged? _rootsNotifier;
    private string? _selectedText;
    private bool _syncing;
    private bool _warnedDuplicate;
    private Border? _trigger;
    private SelectValueRoll? _value;
    private PresenceHost? _clearHost;
    private AvaloniaButton? _clear;
    private ArcPopup? _popup;
    private Border? _surface;
    private CascaderColumnsPanel? _panel;
    private TextBlock? _announcer;
    private int _activeLevel;
    private bool _pressedOnTrigger;
    private bool _rendered;

    static Cascader()
    {
        FocusableProperty.OverrideDefaultValue<Cascader>(true);
    }

    public Cascader()
    {
        PseudoClasses.Set(PlaceholderPseudoClass, true);
    }

    internal CascaderColumnsPanel? ColumnsPanel => _panel;

    internal int ActiveLevel => _activeLevel;

    string? ISelectFieldAutomation.AutomationLabel => Label;
    string? ISelectFieldAutomation.AutomationDescription => Description;
    string? ISelectFieldAutomation.AutomationValue => SelectedText;

    bool ISelectFieldAutomation.AutomationIsOpen
    {
        get => IsOpen;
        set => SetCurrentValue(IsOpenProperty, value);
    }

    /// <summary>Opens the menu with the selected path expanded.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the menu without changing the value.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Clears the selection (the menu does not open).</summary>
    public void Clear()
    {
        if (IsReadOnly)
        {
            return;
        }
        ApplyPath(s_noPath, writeValue: true);
        if (SelectedValue is not null)
        {
            SetCurrentValue(SelectedValueProperty, null);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectFieldAutomationPeer(this, this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
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
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }

        _trigger = e.NameScope.Find<Border>(TriggerPart);
        _value = e.NameScope.Find<SelectValueRoll>(ValuePart);
        _clearHost = e.NameScope.Find<PresenceHost>(ClearHostPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _panel = e.NameScope.Find<CascaderColumnsPanel>(ColumnsPart);
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
        }
        if (_panel is not null)
        {
            _panel.ColumnWidth = ColumnWidth;
            _panel.MaxColumnHeight = ColumnHeightLimit();
        }
        _rendered = false;
        UpdateValue(animate: false);
        UpdateClear();
        if (IsOpen)
        {
            OpenPopup();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_rootsNotifier is null)
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
            (ControlAutomationPeer.FromElement(this) as SelectFieldAutomationPeer)?.RaiseExpandChanged(open);
        }
        else if (property == ItemsSourceProperty)
        {
            Reload();
        }
        else if (property == ChildrenSelectorProperty || property == IsLeafSelectorProperty || property == ValueSelectorProperty)
        {
            Reload();
        }
        else if (property == TextSelectorProperty || property == DisabledSelectorProperty)
        {
            UpdateValue(animate: false);
            if (IsOpen)
            {
                RebuildColumns(animate: false, forceNew: true);
            }
        }
        else if (property == SelectedItemProperty && !_syncing)
        {
            OnSelectedItemWritten(change.NewValue);
        }
        else if (property == SelectedValueProperty && !_syncing)
        {
            OnSelectedValueWritten(change.NewValue);
        }
        else if (property == ShowFullPathProperty || property == PathSeparatorProperty)
        {
            UpdateValue(animate: false);
        }
        else if (property == PlaceholderProperty)
        {
            UpdateValue(animate: false);
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
        else if (property == ColumnWidthProperty)
        {
            if (_panel is not null)
            {
                _panel.ColumnWidth = ColumnWidth;
            }
        }
        else if (property == MaxDropDownHeightProperty && _panel is not null)
        {
            _panel.MaxColumnHeight = ColumnHeightLimit();
        }
        else if (property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            UpdateClear();
        }
    }

    // ---------------------------------------------------------------- data

    private IReadOnlyList<object> ChildrenOf(object node)
    {
        if (IsLeafSelector is { } isLeaf && isLeaf(node))
        {
            return s_noPath;
        }
        return ToList(ChildrenSelector?.Invoke(node));
    }

    private static IReadOnlyList<object> ToList(IEnumerable? source)
    {
        if (source is null)
        {
            return s_noPath;
        }
        var list = new List<object>();
        foreach (var item in source)
        {
            if (item is not null)
            {
                list.Add(item);
            }
        }
        return list;
    }

    private bool IsLeaf(object node) => ChildrenOf(node).Count == 0;

    private string TextOf(object node) => (TextSelector?.Invoke(node) ?? node.ToString()) ?? string.Empty;

    private object? ValueOf(object node) => ValueSelector is { } selector ? selector(node) : node;

    private bool IsDisabled(object node) => DisabledSelector?.Invoke(node) == true;

    /// <summary>Re-reads the roots and re-resolves the selection (data, children or value selector changed).</summary>
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
        ReadRoots();
    }

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ReadRoots();

    private void ReadRoots()
    {
        _roots = ToList(ItemsSource);
        _index = null;
        _warnedDuplicate = false;
        Resolve();
        if (IsOpen)
        {
            RebuildColumns(animate: false, forceNew: true);
        }
    }

    /// <summary>Finds the selection again in the current data (rules 4 and 5: an unknown value is kept).</summary>
    private void Resolve()
    {
        if (SelectedValue is { } value)
        {
            var found = FindByValue(value);
            ApplyPath(found ?? s_noPath, writeValue: false);
        }
        else if (SelectedItem is { } item)
        {
            var found = FindByItem(item);
            ApplyPath(found ?? new[] { item }, writeValue: true);
        }
    }

    private void EnsureIndex()
    {
        if (_index is not null)
        {
            return;
        }
        _index = new Dictionary<object, IReadOnlyList<object>>();
        var stack = new List<object>();
        Visit(_roots, stack);
    }

    private void Visit(IReadOnlyList<object> nodes, List<object> stack)
    {
        foreach (var node in nodes)
        {
            stack.Add(node);
            if (ValueOf(node) is { } value)
            {
                if (!_index!.TryAdd(value, stack.ToArray()) && !_warnedDuplicate)
                {
                    _warnedDuplicate = true;
                    Debug.WriteLine($"Cascader: the value '{value}' belongs to more than one node; the first one wins. Values must be unique across the tree.");
                }
            }
            Visit(ChildrenOf(node), stack);
            stack.RemoveAt(stack.Count - 1);
        }
    }

    private IReadOnlyList<object>? FindByValue(object value)
    {
        EnsureIndex();
        return _index!.TryGetValue(value, out var path) ? path : null;
    }

    private IReadOnlyList<object>? FindByItem(object item)
    {
        var stack = new List<object>();
        return Search(_roots, item, stack) ? stack.ToArray() : null;
    }

    private bool Search(IReadOnlyList<object> nodes, object target, List<object> stack)
    {
        foreach (var node in nodes)
        {
            stack.Add(node);
            if (ReferenceEquals(node, target) || Equals(node, target))
            {
                return true;
            }
            if (Search(ChildrenOf(node), target, stack))
            {
                return true;
            }
            stack.RemoveAt(stack.Count - 1);
        }
        return false;
    }

    private void OnSelectedItemWritten(object? item)
    {
        if (item is null)
        {
            ApplyPath(s_noPath, writeValue: true);
            return;
        }
        ApplyPath(FindByItem(item) ?? new[] { item }, writeValue: true);
    }

    private void OnSelectedValueWritten(object? value)
    {
        if (value is null)
        {
            ApplyPath(s_noPath, writeValue: false);
            return;
        }
        // Not found: the value stays (the data may still be loading), the item and the path are empty.
        ApplyPath(FindByValue(value) ?? s_noPath, writeValue: false);
    }

    /// <summary>
    /// Makes <paramref name="path"/> the selection and updates the other two properties without echoing.
    /// <paramref name="writeValue"/> is false when the value came first and must stay as written.
    /// </summary>
    private void ApplyPath(IReadOnlyList<object> path, bool writeValue)
    {
        var old = _path;
        var changed = old.Count != path.Count;
        for (var i = 0; !changed && i < old.Count; i++)
        {
            changed = !ReferenceEquals(old[i], path[i]);
        }
        var last = path.Count > 0 ? path[^1] : null;
        _syncing = true;
        try
        {
            if (!Equals(SelectedItem, last))
            {
                SetCurrentValue(SelectedItemProperty, last);
            }
            if (writeValue)
            {
                var value = last is null ? null : ValueOf(last);
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
        if (!changed)
        {
            UpdateValue(animate: false);
            return;
        }
        var previous = old;
        _path = path;
        RaisePropertyChanged(SelectedPathProperty, previous, path);
        UpdateValue(animate: true);
        UpdateClear();
        SelectionChanged?.Invoke(this, new CascaderSelectionChangedEventArgs(previous, path));
        if (IsOpen)
        {
            foreach (var column in _columns)
            {
                column.Refresh();
            }
        }
    }

    // ---------------------------------------------------------------- field

    private string? BuildText()
    {
        if (_path.Count == 0)
        {
            return null;
        }
        if (!ShowFullPath)
        {
            return TextOf(_path[^1]);
        }
        return string.Join(PathSeparator, _path.Select(TextOf));
    }

    private void UpdateValue(bool animate)
    {
        var text = BuildText();
        SelectedText = text;
        PseudoClasses.Set(PlaceholderPseudoClass, text is null);
        _value?.Show(text ?? Placeholder ?? string.Empty, null, text is null, 1, animate: animate && _rendered);
        _rendered = _value is not null;
    }

    private void UpdateClear()
    {
        if (_clearHost is not null)
        {
            _clearHost.IsPresent = IsClearable && !IsReadOnly && _path.Count > 0 && IsEffectivelyEnabled;
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Clear();
        Focus(NavigationMethod.Pointer);
    }

    // ---------------------------------------------------------------- popup

    private double ColumnHeightLimit() => Math.Max(0, MaxDropDownHeight - 14);

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
        _typeahead.Reset();
        if (_popup.IsOpen)
        {
            return;
        }
        // Open on the selected path, every column laid out at once (no column-by-column animation).
        _expanded.Clear();
        foreach (var node in _path)
        {
            if (!IsLeaf(node))
            {
                _expanded.Add(node);
            }
        }
        RebuildColumns(animate: false, forceNew: true);
        _activeLevel = Math.Max(0, Math.Min(_path.Count, _columns.Count) - 1);
        var column = _columns.Count > 0 ? _columns[Math.Min(_activeLevel, _columns.Count - 1)] : null;
        if (column is not null)
        {
            var target = _path.Count > _activeLevel ? column.IndexOf(_path[_activeLevel]) : -1;
            column.Options.SetActive(target >= 0 && column.IsEnabledAt(target) ? target : FirstEnabled(column, 0, 1), animate: false);
        }
        UpdateSurfaceWidth();
        _popup.IsOpen = true;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var c in _columns)
            {
                var active = c.Options.ActiveOption;
                if (active >= 0)
                {
                    c.Options.SetActive(active, animate: false);
                    c.Options.ScrollIntoView(active);
                }
            }
        }, DispatcherPriority.Loaded);
    }

    private void ClosePopup()
    {
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        foreach (var column in _columns)
        {
            column.Options.SetActive(-1, animate: false);
        }
        _typeahead.Reset();
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
        if (_surface is not null && _trigger is not null && _trigger.Bounds.Width > 0)
        {
            _surface.MinWidth = _trigger.Bounds.Width;
        }
    }

    /// <summary>
    /// Brings the columns in line with <see cref="_expanded"/>: column n shows the children of the n-th expanded
    /// node. Columns whose parent did not change are kept (and keep their scroll position).
    /// </summary>
    private void RebuildColumns(bool animate, bool forceNew = false)
    {
        if (_panel is null)
        {
            return;
        }
        var wanted = new List<CascaderColumn>();
        var created = new List<CascaderColumn>();
        var text = (Func<object?, string?>)(node => node is null ? null : TextOf(node));
        var disabled = (Func<object?, bool>)(node => node is not null && IsDisabled(node));
        for (var level = 0; level <= _expanded.Count; level++)
        {
            var parent = level == 0 ? null : _expanded[level - 1];
            var existing = !forceNew && level < _columns.Count && ReferenceEquals(_columns[level].ParentNode, parent) ? _columns[level] : null;
            if (existing is null)
            {
                var nodes = level == 0 ? _roots : ChildrenOf(parent!);
                existing = new CascaderColumn(this, level, parent, nodes, text, disabled);
                created.Add(existing);
            }
            wanted.Add(existing);
        }
        _columns.Clear();
        _columns.AddRange(wanted);
        _panel.SetColumns(_columns, animate);
        if (animate)
        {
            var slide = ArcMotion.IsFull(_panel);
            foreach (var column in created)
            {
                if (column.Level > 0)
                {
                    column.PlayReveal(slide);
                }
            }
        }
        foreach (var column in _columns)
        {
            column.Refresh();
        }
    }

    // ---------------------------------------------------------------- expanding and picking

    private void ExpandTo(int level, object? node)
    {
        while (_expanded.Count > level)
        {
            _expanded.RemoveAt(_expanded.Count - 1);
        }
        if (node is not null && !IsLeaf(node))
        {
            _expanded.Add(node);
        }
        RebuildColumns(animate: true);
    }

    private IReadOnlyList<object> PathTo(int level, object node)
    {
        var path = new List<object>(level + 1);
        for (var i = 0; i < level && i < _expanded.Count; i++)
        {
            path.Add(_expanded[i]);
        }
        path.Add(node);
        return path;
    }

    private void Activate(CascaderColumn column, int position, bool fromKeyboardRight)
    {
        if (position < 0 || position >= column.Count || !column.IsEnabledAt(position))
        {
            return;
        }
        var node = column.Nodes[position];
        _activeLevel = column.Level;
        var leaf = IsLeaf(node);
        if (leaf)
        {
            if (!IsReadOnly)
            {
                ApplyPath(PathTo(column.Level, node), writeValue: true);
            }
            Close();
            Focus(NavigationMethod.Pointer);
            return;
        }
        if (!LeafOnly && !IsReadOnly && !fromKeyboardRight)
        {
            ApplyPath(PathTo(column.Level, node), writeValue: true);
        }
        ExpandTo(column.Level, node);
        foreach (var c in _columns)
        {
            c.Refresh();
        }
    }

    private void Describe(CascaderColumn column, int position)
    {
        if (_announcer is null || position < 0 || position >= column.Count)
        {
            return;
        }
        _announcer.Text = $"{TextOf(column.Nodes[position])}, level {column.Level + 1}, {column.Count} items";
    }

    // ---------------------------------------------------------------- ICascaderColumnOwner

    bool ICascaderColumnOwner.IsBranchNode(object node) => !IsLeaf(node);

    bool ICascaderColumnOwner.IsPathNode(int level, object node) =>
        level < _expanded.Count && ReferenceEquals(_expanded[level], node);

    bool ICascaderColumnOwner.IsSelectedNode(object node) => _path.Count > 0 && ReferenceEquals(_path[^1], node);

    void ICascaderColumnOwner.OnColumnHovered(CascaderColumn column, int position)
    {
        _activeLevel = column.Level;
        foreach (var c in _columns)
        {
            if (!ReferenceEquals(c, column))
            {
                c.Options.SetActive(-1, animate: false);
            }
        }
        column.Options.SetActive(position, animate: true);
        if (ExpandTrigger == CascaderExpandTrigger.Hover && position >= 0 && position < column.Count && column.IsEnabledAt(position))
        {
            var node = column.Nodes[position];
            if (!IsLeaf(node) && !(column.Level < _expanded.Count && ReferenceEquals(_expanded[column.Level], node)))
            {
                ExpandTo(column.Level, node);
            }
        }
    }

    void ICascaderColumnOwner.OnColumnInvoked(CascaderColumn column, int position) => Activate(column, position, fromKeyboardRight: false);

    void ICascaderColumnOwner.OnColumnPointerExited(CascaderColumn column)
    {
        if (IsOpen)
        {
            column.Options.SetActive(-1, animate: true);
        }
    }

    // ---------------------------------------------------------------- pointer

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

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    // ---------------------------------------------------------------- keyboard

    private CascaderColumn? ActiveColumn =>
        _columns.Count == 0 ? null : _columns[Math.Clamp(_activeLevel, 0, _columns.Count - 1)];

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        if (!IsOpen)
        {
            switch (e.Key)
            {
                case Key.Space when _typeahead.IsTyping:
                    return;
                case Key.Enter:
                case Key.Space:
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    Open();
                    return;
                case Key.Delete:
                case Key.Back:
                    if (IsClearable && !IsReadOnly && _path.Count > 0)
                    {
                        e.Handled = true;
                        Clear();
                    }
                    return;
            }
            base.OnKeyDown(e);
            return;
        }
        var column = ActiveColumn;
        var active = column?.Options.ActiveOption ?? -1;
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Close();
                return;
            case Key.Tab:
                Close();
                return;
            case Key.Space when _typeahead.IsTyping:
                return;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (column is null || active < 0)
                {
                    Close();
                }
                else if (LeafOnly && !IsLeaf(column.Nodes[active]))
                {
                    EnterChildren(column, active);
                }
                else
                {
                    Activate(column, active, fromKeyboardRight: false);
                }
                return;
            case Key.Down when column is not null:
                e.Handled = true;
                MoveActive(column, active < 0 ? FirstEnabled(column, 0, 1) : NextEnabled(column, active, 1));
                return;
            case Key.Up when column is not null:
                e.Handled = true;
                MoveActive(column, active < 0 ? FirstEnabled(column, column.Count - 1, -1) : NextEnabled(column, active, -1));
                return;
            case Key.Home when column is not null:
                e.Handled = true;
                MoveActive(column, FirstEnabled(column, 0, 1));
                return;
            case Key.End when column is not null:
                e.Handled = true;
                MoveActive(column, FirstEnabled(column, column.Count - 1, -1));
                return;
            case Key.PageDown when column is not null:
                e.Handled = true;
                MoveActive(column, FirstEnabled(column, Math.Min(column.Count - 1, Math.Max(active, 0) + 8), -1));
                return;
            case Key.PageUp when column is not null:
                e.Handled = true;
                MoveActive(column, FirstEnabled(column, Math.Max(0, active - 8), 1));
                return;
            case Key.Right when column is not null:
                e.Handled = true;
                if (active >= 0)
                {
                    EnterChildren(column, active);
                }
                return;
            case Key.Left:
                e.Handled = true;
                if (_activeLevel > 0 && column is not null)
                {
                    LeaveColumn(column);
                }
                return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || !IsOpen || string.IsNullOrEmpty(e.Text))
        {
            base.OnTextInput(e);
            return;
        }
        if (e.Text == " " && !_typeahead.IsTyping)
        {
            return;
        }
        var column = ActiveColumn;
        if (column is null)
        {
            return;
        }
        e.Handled = true;
        var next = _typeahead.Next(e.Text, column.Options.ActiveOption, column.Count, column.Entries.TextAt, column.Entries.IsEnabledAt);
        if (next >= 0)
        {
            MoveActive(column, next);
        }
    }

    private void MoveActive(CascaderColumn column, int position)
    {
        if (position < 0)
        {
            return;
        }
        column.Options.SetActive(position, animate: true);
        column.Options.ScrollIntoView(position);
        if (ExpandTrigger == CascaderExpandTrigger.Hover && position < column.Count)
        {
            var node = column.Nodes[position];
            if (!IsLeaf(node))
            {
                ExpandTo(column.Level, node);
            }
            else if (column.Level < _expanded.Count)
            {
                ExpandTo(column.Level, null);
            }
        }
        Describe(column, position);
    }

    /// <summary>Right arrow: open the highlighted parent and move the highlight into its first child.</summary>
    private void EnterChildren(CascaderColumn column, int position)
    {
        if (position < 0 || position >= column.Count || !column.IsEnabledAt(position) || IsLeaf(column.Nodes[position]))
        {
            return;
        }
        var node = column.Nodes[position];
        if (!(column.Level < _expanded.Count && ReferenceEquals(_expanded[column.Level], node)))
        {
            ExpandTo(column.Level, node);
        }
        if (column.Level + 1 < _columns.Count)
        {
            var child = _columns[column.Level + 1];
            _activeLevel = child.Level;
            var first = FirstEnabled(child, 0, 1);
            if (first >= 0)
            {
                child.Options.SetActive(first, animate: false);
                child.Options.ScrollIntoView(first);
                Describe(child, first);
            }
        }
    }

    /// <summary>Left arrow: back to the previous column with the parent highlighted.</summary>
    private void LeaveColumn(CascaderColumn column)
    {
        var parentColumn = _columns[column.Level - 1];
        column.Options.SetActive(-1, animate: false);
        _activeLevel = parentColumn.Level;
        var index = parentColumn.IndexOf(_expanded[column.Level - 1]);
        if (index >= 0)
        {
            parentColumn.Options.SetActive(index, animate: true);
            parentColumn.Options.ScrollIntoView(index);
            Describe(parentColumn, index);
        }
    }

    private static int NextEnabled(CascaderColumn column, int from, int step)
    {
        for (var i = from + step; i >= 0 && i < column.Count; i += step)
        {
            if (column.IsEnabledAt(i))
            {
                return i;
            }
        }
        return from;
    }

    private static int FirstEnabled(CascaderColumn column, int start, int step)
    {
        for (var i = start; i >= 0 && i < column.Count; i += step)
        {
            if (column.IsEnabledAt(i))
            {
                return i;
            }
        }
        return -1;
    }
}

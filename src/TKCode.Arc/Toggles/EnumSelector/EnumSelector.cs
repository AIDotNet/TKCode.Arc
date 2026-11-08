using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How an <see cref="EnumSelector"/> presents its options.</summary>
public enum EnumSelectorDisplayMode
{
    /// <summary>Radio options for a plain enum, checkboxes for a <c>[Flags]</c> enum.</summary>
    Auto,

    /// <summary>Radio options: one member is chosen, also for a <c>[Flags]</c> enum (composite members are choices too).</summary>
    Radio,

    /// <summary>Checkboxes. A <c>[Flags]</c> enum combines the checked members; a plain enum still holds one member, so
    /// checking a box unchecks the others.</summary>
    Check
}

/// <summary>Arguments of <see cref="EnumSelector.SelectedEnumChanged"/>.</summary>
public sealed class EnumSelectorChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnumSelectorChangedEventArgs(RoutedEvent routedEvent, object? oldValue, object? newValue)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The value before the change.</summary>
    public object? OldValue { get; }

    /// <summary>The value after the change.</summary>
    public object? NewValue { get; }
}

/// <summary>
/// Lists the members of an enum as choices: a <see cref="RadioGroup"/> for a plain enum, and <see cref="Checkbox"/>es
/// for a <c>[Flags]</c> enum, whose checked members are combined bit by bit (checking a composite member checks its
/// parts, a member whose bits are only partly set shows the dash, and a zero member such as <c>None</c> clears the
/// rest). The members, their <c>[Description]</c> texts and the <c>[Flags]</c> marker come from code the generator writes
/// for enums marked <see cref="EnumMetadataAttribute"/>, so nothing is reflected (AOT and trimming safe).
/// <see cref="SelectedEnum"/> (two-way) is the value; the options are laid out in <see cref="Rows"/> and
/// <see cref="Columns"/>, or in a panel of your own (<see cref="ItemsPanel"/>).
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(FlagsPseudoClass, RadioPseudoClass, CheckPseudoClass)]
public class EnumSelector : TemplatedControl
{
    public const string HostPart = "PART_Host";
    public const string FlagsPseudoClass = ":flags";
    public const string RadioPseudoClass = ":radio";
    public const string CheckPseudoClass = ":check";

    #region 公共属性定义

    /// <summary>The enum to list. When null, the type of <see cref="SelectedEnum"/> is used. The enum must be marked
    /// <see cref="EnumMetadataAttribute"/> (or registered with <see cref="EnumTypes"/>); otherwise no option is shown.</summary>
    public static readonly StyledProperty<Type?> EnumTypeProperty =
        AvaloniaProperty.Register<EnumSelector, Type?>(nameof(EnumType));

    /// <summary>The selected value (two-way): a member, a combination of members of a <c>[Flags]</c> enum, or null for none.</summary>
    public static readonly StyledProperty<object?> SelectedEnumProperty =
        AvaloniaProperty.Register<EnumSelector, object?>(nameof(SelectedEnum), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Radio options or checkboxes; <see cref="EnumSelectorDisplayMode.Auto"/> picks by <see cref="HasFlags"/>.</summary>
    public static readonly StyledProperty<EnumSelectorDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<EnumSelector, EnumSelectorDisplayMode>(nameof(DisplayMode));

    /// <summary>Show each member's localized text or <c>[Description]</c> (true, the default), or its name in code.</summary>
    public static readonly StyledProperty<bool> UseDisplayNamesProperty =
        AvaloniaProperty.Register<EnumSelector, bool>(nameof(UseDisplayNames), true);

    /// <summary>Rows of the default grid; 0 computes them from <see cref="Columns"/>.</summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<EnumSelector, int>(nameof(Rows), validate: v => v >= 0);

    /// <summary>Columns of the default grid; 0 computes them from <see cref="Rows"/>. With both 0 the options are one column.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<EnumSelector, int>(nameof(Columns), validate: v => v >= 0);

    /// <summary>Gap between rows of the default grid; 8 for radio options and 0 for checkboxes by default.</summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<EnumSelector, double>(nameof(RowSpacing));

    /// <summary>Gap between columns of the default grid (16 by default).</summary>
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<EnumSelector, double>(nameof(ColumnSpacing));

    /// <summary>A panel of your own for the options (for example a <c>WrapPanel</c>); null uses a uniform grid of
    /// <see cref="Rows"/> by <see cref="Columns"/>.</summary>
    public static readonly StyledProperty<ITemplate<Panel?>?> ItemsPanelProperty =
        AvaloniaProperty.Register<EnumSelector, ITemplate<Panel?>?>(nameof(ItemsPanel));

    /// <summary>The label of each option; it receives an <see cref="EnumSelectorItem"/>. Null shows its <see cref="EnumSelectorItem.Text"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<EnumSelector, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>The legend above the options; a string is also the group's accessible name.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<EnumSelector, object?>(nameof(Header));

    /// <summary>Whether the listed enum is marked <c>[Flags]</c>.</summary>
    public static readonly DirectProperty<EnumSelector, bool> HasFlagsProperty =
        AvaloniaProperty.RegisterDirect<EnumSelector, bool>(nameof(HasFlags), o => o.HasFlags);

    /// <summary>The mode in use: <see cref="DisplayMode"/> with <see cref="EnumSelectorDisplayMode.Auto"/> resolved.</summary>
    public static readonly DirectProperty<EnumSelector, EnumSelectorDisplayMode> EffectiveDisplayModeProperty =
        AvaloniaProperty.RegisterDirect<EnumSelector, EnumSelectorDisplayMode>(nameof(EffectiveDisplayMode), o => o.EffectiveDisplayMode);

    /// <summary>The options shown, one per listed member, in declaration order.</summary>
    public static readonly DirectProperty<EnumSelector, IReadOnlyList<EnumSelectorItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<EnumSelector, IReadOnlyList<EnumSelectorItem>>(nameof(Items), o => o.Items);

    /// <summary>Raised when <see cref="SelectedEnum"/> changes, by the person or by code.</summary>
    public static readonly RoutedEvent<EnumSelectorChangedEventArgs> SelectedEnumChangedEvent =
        RoutedEvent.Register<EnumSelector, EnumSelectorChangedEventArgs>(nameof(SelectedEnumChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="EnumTypeProperty"/>
    public Type? EnumType
    {
        get => GetValue(EnumTypeProperty);
        set => SetValue(EnumTypeProperty, value);
    }

    /// <inheritdoc cref="SelectedEnumProperty"/>
    public object? SelectedEnum
    {
        get => GetValue(SelectedEnumProperty);
        set => SetValue(SelectedEnumProperty, value);
    }

    /// <inheritdoc cref="DisplayModeProperty"/>
    public EnumSelectorDisplayMode DisplayMode
    {
        get => GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    /// <inheritdoc cref="UseDisplayNamesProperty"/>
    public bool UseDisplayNames
    {
        get => GetValue(UseDisplayNamesProperty);
        set => SetValue(UseDisplayNamesProperty, value);
    }

    /// <inheritdoc cref="RowsProperty"/>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="RowSpacingProperty"/>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <inheritdoc cref="ColumnSpacingProperty"/>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <inheritdoc cref="ItemsPanelProperty"/>
    public ITemplate<Panel?>? ItemsPanel
    {
        get => GetValue(ItemsPanelProperty);
        set => SetValue(ItemsPanelProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="HasFlagsProperty"/>
    public bool HasFlags
    {
        get => _hasFlags;
        private set => SetAndRaise(HasFlagsProperty, ref _hasFlags, value);
    }

    /// <inheritdoc cref="EffectiveDisplayModeProperty"/>
    public EnumSelectorDisplayMode EffectiveDisplayMode
    {
        get => _effectiveDisplayMode;
        private set => SetAndRaise(EffectiveDisplayModeProperty, ref _effectiveDisplayMode, value);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<EnumSelectorItem> Items
    {
        get => _items;
        private set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    /// <summary>Raised when <see cref="SelectedEnum"/> changes, with the old and new values.</summary>
    public event EventHandler<EnumSelectorChangedEventArgs>? SelectedEnumChanged
    {
        add => AddHandler(SelectedEnumChangedEvent, value);
        remove => RemoveHandler(SelectedEnumChangedEvent, value);
    }

    #endregion

    /// <summary>The label of an option when <see cref="ItemTemplate"/> is null: its text, following later changes.</summary>
    private static readonly IDataTemplate s_textTemplate = new FuncDataTemplate<EnumSelectorItem>((item, _) =>
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (item is not null)
        {
            text.Bind(TextBlock.TextProperty, item.GetObservable(EnumSelectorItem.TextProperty));
        }
        return text;
    });

    private bool _hasFlags;
    private EnumSelectorDisplayMode _effectiveDisplayMode = EnumSelectorDisplayMode.Radio;
    private IReadOnlyList<EnumSelectorItem> _items = Array.Empty<EnumSelectorItem>();
    private readonly List<IDisposable> _textSubscriptions = [];
    private EnumTypeInfo? _info;
    private Decorator? _host;
    private RadioGroup? _radio;
    private Panel? _checkPanel;
    private readonly List<Checkbox> _checks = [];
    private UniformGrid? _grid;
    private bool _syncing;
    private bool _usesHeaderName;
    private bool _isAttached;

    static EnumSelector()
    {
        FocusableProperty.OverrideDefaultValue<EnumSelector>(false);
    }

    public EnumSelector()
    {
        UpdatePseudoClasses();
    }

    /// <summary>The description of the listed enum; null when there is none (no type, or not registered).</summary>
    public EnumTypeInfo? EnumInfo => _info;

    private Type? EffectiveType => EnumType ?? SelectedEnum?.GetType();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_host is not null)
        {
            _host.Child = null;
        }
        _host = e.NameScope.Find<Decorator>(HostPart);
        BuildVisual();
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _isAttached = true;
        // An enum may have registered after the type was set (its assembly loaded later).
        if (_info is null && EffectiveType is not null && EnumTypes.Find(EffectiveType) is not null)
        {
            Rebuild();
        }
        else
        {
            ObserveTexts();
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _isAttached = false;
        StopObservingTexts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EnumTypeProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedEnumProperty)
        {
            // Without EnumType the value's own type is listed, so a value of another enum changes the options.
            if (EnumType is null && SelectedEnum is { } value && _info?.Type != value.GetType())
            {
                Rebuild();
            }
            SyncFromValue();
            RaiseEvent(new EnumSelectorChangedEventArgs(SelectedEnumChangedEvent, change.OldValue, change.NewValue));
        }
        else if (change.Property == DisplayModeProperty)
        {
            UpdateMode();
        }
        else if (change.Property == UseDisplayNamesProperty)
        {
            foreach (var item in _items)
            {
                UpdateText(item);
            }
        }
        else if (change.Property == RowsProperty || change.Property == ColumnsProperty
                 || change.Property == RowSpacingProperty || change.Property == ColumnSpacingProperty)
        {
            UpdateGrid();
        }
        else if (change.Property == ItemsPanelProperty)
        {
            BuildVisual();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            ApplyItemTemplate();
        }
        else if (change.Property == HeaderProperty)
        {
            UpdateAccessibleName();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EnumSelectorAutomationPeer(this);

    /// <summary>Reads the enum again and rebuilds the options (the selection is kept).</summary>
    private void Rebuild()
    {
        StopObservingTexts();
        _info = EnumTypes.Find(EffectiveType);
        HasFlags = _info?.IsFlags ?? false;
        var items = new List<EnumSelectorItem>();
        if (_info is not null)
        {
            foreach (var member in _info.Members)
            {
                var item = new EnumSelectorItem(member);
                UpdateText(item);
                items.Add(item);
            }
        }
        Items = items;
        ObserveTexts();
        UpdateMode(force: true);
    }

    private void UpdateMode(bool force = false)
    {
        var mode = DisplayMode == EnumSelectorDisplayMode.Auto
            ? (HasFlags ? EnumSelectorDisplayMode.Check : EnumSelectorDisplayMode.Radio)
            : DisplayMode;
        var changed = mode != EffectiveDisplayMode;
        EffectiveDisplayMode = mode;
        UpdatePseudoClasses();
        if (changed || force)
        {
            BuildVisual();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(FlagsPseudoClass, HasFlags);
        PseudoClasses.Set(RadioPseudoClass, EffectiveDisplayMode == EnumSelectorDisplayMode.Radio);
        PseudoClasses.Set(CheckPseudoClass, EffectiveDisplayMode == EnumSelectorDisplayMode.Check);
    }

    private IDataTemplate EffectiveItemTemplate => ItemTemplate ?? s_textTemplate;

    /// <summary>Puts the options into the template: a radio group, or a panel of checkboxes.</summary>
    private void BuildVisual()
    {
        ClearChecks();
        _grid = null;
        if (_radio is not null)
        {
            _radio.ItemsSource = null;
        }
        if (_host is null)
        {
            return;
        }
        if (EffectiveDisplayMode == EnumSelectorDisplayMode.Radio)
        {
            if (_radio is null)
            {
                _radio = new RadioGroup();
                _radio.SelectionChanged += OnRadioSelectionChanged;
            }
            _radio.ItemsPanel = new FuncTemplate<Panel?>(CreatePanel);
            _radio.ItemTemplate = EffectiveItemTemplate;
            _syncing = true;
            try
            {
                _radio.ItemsSource = _items;
            }
            finally
            {
                _syncing = false;
            }
            _host.Child = _radio;
        }
        else
        {
            var panel = CreatePanel() ?? new StackPanel();
            foreach (var item in _items)
            {
                // Content first, then its template: a template is never built without its item.
                var box = new Checkbox { Content = item };
                box.ContentTemplate = EffectiveItemTemplate;
                box.CheckedStateChanged += OnCheckChanged;
                _checks.Add(box);
                panel.Children.Add(box);
            }
            _checkPanel = panel;
            _host.Child = panel;
        }
        UpdateAccessibleName();
        SyncFromValue();
    }

    private void ClearChecks()
    {
        foreach (var box in _checks)
        {
            box.CheckedStateChanged -= OnCheckChanged;
            box.ContentTemplate = null;
            box.Content = null;
        }
        _checks.Clear();
        _checkPanel?.Children.Clear();
        _checkPanel = null;
    }

    private Panel? CreatePanel()
    {
        if (ItemsPanel is { } template)
        {
            return template.Build();
        }
        _grid = new UniformGrid { HorizontalAlignment = HorizontalAlignment.Stretch };
        UpdateGrid();
        return _grid;
    }

    private void UpdateGrid()
    {
        if (_grid is null)
        {
            return;
        }
        var rows = Rows;
        var columns = Columns;
        _grid.Rows = rows;
        // Neither given: one column, like a radio group.
        _grid.Columns = rows == 0 && columns == 0 ? 1 : columns;
        _grid.RowSpacing = RowSpacing;
        _grid.ColumnSpacing = ColumnSpacing;
    }

    private void ApplyItemTemplate()
    {
        var template = EffectiveItemTemplate;
        if (_radio is not null && ReferenceEquals(_host?.Child, _radio))
        {
            // Rebuild the containers so every label is built from the new template.
            _syncing = true;
            try
            {
                _radio.ItemsSource = null;
                _radio.ItemTemplate = template;
                _radio.ItemsSource = _items;
            }
            finally
            {
                _syncing = false;
            }
            SyncFromValue();
        }
        foreach (var box in _checks)
        {
            box.ContentTemplate = template;
        }
    }

    private void UpdateAccessibleName()
    {
        var name = AutomationProperties.GetName(this);
        if (Header is string header && (string.IsNullOrEmpty(name) || _usesHeaderName))
        {
            AutomationProperties.SetName(this, header);
            _usesHeaderName = true;
        }
        else if (_usesHeaderName && Header is not string)
        {
            ClearValue(AutomationProperties.NameProperty);
            _usesHeaderName = false;
        }
        if (_radio is not null)
        {
            AutomationProperties.SetName(_radio, AutomationProperties.GetName(this));
        }
    }

    private void UpdateText(EnumSelectorItem item)
    {
        item.Text = UseDisplayNames ? item.ResourceText ?? item.Member.DisplayName ?? item.Name : item.Name;
    }

    /// <summary>Follows the language catalog texts while the selector is in a tree (they change with the language).</summary>
    private void ObserveTexts()
    {
        StopObservingTexts();
        if (!_isAttached)
        {
            return;
        }
        foreach (var item in _items)
        {
            if (item.Member.TextKey is not { } key)
            {
                continue;
            }
            var target = item;
            _textSubscriptions.Add(this.GetResourceObservable(key).Subscribe(new TextObserver(value =>
            {
                target.ResourceText = value as string;
                UpdateText(target);
            })));
        }
    }

    private void StopObservingTexts()
    {
        foreach (var subscription in _textSubscriptions)
        {
            subscription.Dispose();
        }
        _textSubscriptions.Clear();
    }

    /// <summary>Shows <see cref="SelectedEnum"/> on the options.</summary>
    private void SyncFromValue()
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        try
        {
            var value = SelectedEnum;
            var has = _info is not null && _info.IsValue(value);
            var bits = has ? _info!.ToBits(value!) : 0UL;
            if (_radio is not null && ReferenceEquals(_host?.Child, _radio))
            {
                var index = -1;
                if (has)
                {
                    for (var i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].Bits == bits)
                        {
                            index = i;
                            break;
                        }
                    }
                }
                _radio.SelectedIndex = index;
            }
            foreach (var box in _checks)
            {
                var item = (EnumSelectorItem)box.Content!;
                box.SetCurrentValue(Checkbox.CheckedStateProperty, has ? StateOf(item.Bits, bits) : CheckboxState.Unchecked);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private CheckboxState StateOf(ulong member, ulong value)
    {
        if (!HasFlags)
        {
            return member == value ? CheckboxState.Checked : CheckboxState.Unchecked;
        }
        // A zero member (None) is checked only when nothing is set.
        if (member == 0)
        {
            return value == 0 ? CheckboxState.Checked : CheckboxState.Unchecked;
        }
        var overlap = value & member;
        if (overlap == member)
        {
            return CheckboxState.Checked;
        }
        // A composite member (ReadWrite) with only some of its bits set shows the dash.
        return overlap == 0 ? CheckboxState.Unchecked : CheckboxState.Indeterminate;
    }

    private void OnRadioSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
        if (_syncing || _radio?.SelectedItem is not EnumSelectorItem item)
        {
            return;
        }
        SetCurrentValue(SelectedEnumProperty, item.Value);
    }

    // CheckedStateChanged comes after the box has settled both of its state properties, so the box can be set back here.
    private void OnCheckChanged(object? sender, CheckboxStateChangedEventArgs e)
    {
        e.Handled = true;
        if (_syncing || _info is null || sender is not Checkbox { Content: EnumSelectorItem item } box)
        {
            return;
        }
        var isChecked = e.NewState == CheckboxState.Checked;
        if (HasFlags)
        {
            var value = SelectedEnum;
            var current = _info.IsValue(value) ? _info.ToBits(value!) : 0UL;
            ulong next;
            if (item.Bits == 0)
            {
                // Checking None clears every flag; unchecking it changes nothing (it is checked again below).
                next = isChecked ? 0UL : current;
            }
            else
            {
                next = isChecked ? current | item.Bits : current & ~item.Bits;
            }
            if (!_info.IsValue(value) || next != current)
            {
                SetCurrentValue(SelectedEnumProperty, _info.FromBits(next));
            }
        }
        else if (isChecked)
        {
            SetCurrentValue(SelectedEnumProperty, item.Value);
        }
        // Boxes the change did not move (an unchecked current choice, an unchecked None) go back to the value.
        SyncFromValue();
    }

    private sealed class TextObserver(Action<object?> next) : IObserver<object?>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(object? value) => next(value);
    }

    private sealed class EnumSelectorAutomationPeer(EnumSelector owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

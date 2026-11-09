using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Base of the filter boxes (<see cref="NumberFilterBox"/>, <see cref="TextFilterBox"/>): one filter condition laid out
/// as an enable checkbox with the label, an operator <see cref="Select"/> that shows symbols, the value editor and an
/// optional <see cref="ExtraContent"/>. The box only holds the condition; it never filters data. Your view model reads
/// <see cref="IsActive"/>, the operator and the value (or calls the derived <c>Matches</c>) and filters on its own terms.
/// <para>Unchecking the box disables the operator, the value and the extra content but keeps them, so checking it again
/// restores the condition.</para>
/// </summary>
[TemplatePart(LayoutPart, typeof(Grid))]
[TemplatePart(TogglePart, typeof(Checkbox))]
[TemplatePart(OperatorPart, typeof(Select))]
[TemplatePart(ExtraPart, typeof(Control))]
[PseudoClasses(InactivePseudoClass, ExtraPseudoClass, StackedPseudoClass, SmallPseudoClass, LargePseudoClass)]
public abstract class FilterBox : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string TogglePart = "PART_Toggle";
    public const string OperatorPart = "PART_Operator";
    public const string ExtraPart = "PART_Extra";

    /// <summary>The filter is switched off (<see cref="IsActive"/> is false).</summary>
    public const string InactivePseudoClass = ":inactive";

    /// <summary><see cref="ExtraContent"/> is set.</summary>
    public const string ExtraPseudoClass = ":extra";

    /// <summary>Too narrow for one row: the label sits above the fields.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Below this much room for the label the box stacks, instead of wrapping the label a word at a time.</summary>
    private const double LabelMinWidth = 120;

    /// <summary>The operator menu shows names as well as symbols, so it is wider than the field.</summary>
    private const double OperatorMenuMinWidth = 200;

    /// <summary>Set while <see cref="Size"/> is <see cref="ControlSize.Small"/> (the theme selects on it instead of a property selector).</summary>
    public const string SmallPseudoClass = ":small";

    /// <summary>Set while <see cref="Size"/> is <see cref="ControlSize.Large"/>.</summary>
    public const string LargePseudoClass = ":large";

    #region 公共属性定义

    /// <summary>The field name shown beside the checkbox (text or any content); clicking it toggles <see cref="IsActive"/>.</summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<FilterBox, object?>(nameof(Label));

    /// <summary>Muted hint under the label; collapses when empty.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FilterBox, string?>(nameof(Description));

    /// <summary>Whether the condition takes part in filtering (two-way, default true). Off keeps the operator and the value.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<FilterBox, bool>(nameof(IsActive), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Shares the width of the label column with every box (or <c>Grid</c> column) of the same group name inside an
    /// element with <c>Grid.IsSharedSizeScope="True"</c>, so stacked filters line up. A name is letters, digits and
    /// underscores and does not start with a digit.
    /// </summary>
    public static readonly StyledProperty<string?> LabelSizeGroupProperty =
        AvaloniaProperty.Register<FilterBox, string?>(nameof(LabelSizeGroup));

    /// <summary>A fixed width for the label column (default NaN: as wide as the label, or the shared width of <see cref="LabelSizeGroup"/>).</summary>
    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<FilterBox, double>(nameof(LabelWidth), double.NaN);

    /// <summary>Height of the operator and value fields: Small 36, Medium 44 (default), Large 50.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        Field.SizeProperty.AddOwner<FilterBox>();

    /// <summary>Content placed after the value editor (a checkbox, a button); disabled with the filter, collapsed when null.</summary>
    public static readonly StyledProperty<object?> ExtraContentProperty =
        AvaloniaProperty.Register<FilterBox, object?>(nameof(ExtraContent));

    /// <summary>Accessible name of the operator field (default "Operator"; read as "Price operator").</summary>
    public static readonly StyledProperty<string> OperatorTextProperty =
        AvaloniaProperty.Register<FilterBox, string>(nameof(OperatorText), "Operator");

    /// <summary>Raised after the condition changes: <see cref="IsActive"/>, the operator, the value or an option that changes matching.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FilterChangedEvent =
        RoutedEvent.Register<FilterBox, RoutedEventArgs>(nameof(FilterChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="LabelProperty"/>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <inheritdoc cref="LabelSizeGroupProperty"/>
    public string? LabelSizeGroup { get => GetValue(LabelSizeGroupProperty); set => SetValue(LabelSizeGroupProperty, value); }

    /// <inheritdoc cref="LabelWidthProperty"/>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ExtraContentProperty"/>
    public object? ExtraContent { get => GetValue(ExtraContentProperty); set => SetValue(ExtraContentProperty, value); }

    /// <inheritdoc cref="OperatorTextProperty"/>
    public string OperatorText { get => GetValue(OperatorTextProperty); set => SetValue(OperatorTextProperty, value); }

    /// <inheritdoc cref="FilterChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? FilterChanged
    {
        add => AddHandler(FilterChangedEvent, value);
        remove => RemoveHandler(FilterChangedEvent, value);
    }

    #endregion

    private Grid? _layout;
    private Checkbox? _toggle;
    private Select? _operator;
    private bool _syncingOperator;
    private bool _stacked;
    private Control? _extra;

    /// <summary>The operator field of the applied template.</summary>
    protected Select? OperatorField => _operator;

    protected FilterBox()
    {
        PseudoClasses.Set(InactivePseudoClass, false);
        UpdateSizePseudoClasses();
    }

    private void UpdateSizePseudoClasses()
    {
        var size = Size;
        PseudoClasses.Set(SmallPseudoClass, size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, size == ControlSize.Large);
    }

    /// <summary>The operators the menu offers, in order (boxed enum values).</summary>
    protected abstract IReadOnlyList<object> OperatorChoices();

    /// <summary>The current operator (boxed).</summary>
    protected abstract object CurrentOperator();

    /// <summary>Writes the operator the person picked.</summary>
    protected abstract void PickOperator(object choice);

    /// <summary>The symbol shown for an operator.</summary>
    protected abstract string SymbolOf(object choice);

    /// <summary>The spoken name of an operator.</summary>
    protected abstract string NameOf(object choice);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_toggle is not null)
        {
            _toggle.IsCheckedChanged -= OnToggleChanged;
        }
        if (_operator is not null)
        {
            _operator.SelectionChanged -= OnOperatorPicked;
        }

        _layout = e.NameScope.Find<Grid>(LayoutPart);
        _toggle = e.NameScope.Find<Checkbox>(TogglePart);
        _operator = e.NameScope.Find<Select>(OperatorPart);

        if (_toggle is not null)
        {
            _toggle.IsChecked = IsActive;
            _toggle.IsCheckedChanged += OnToggleChanged;
        }
        if (_operator is not null)
        {
            // The field keeps the compact symbol; the menu spells each one out, because few people can read a symbol like ∋ alone.
            _operator.TextSelector = item => item is null ? null : $"{SymbolOf(item)}   {NameOf(item)}";
            _operator.FieldTextSelector = item => item is null ? null : SymbolOf(item);
            _operator.MenuMinWidth = OperatorMenuMinWidth;
            RefreshOperatorChoices();
            _operator.SelectionChanged += OnOperatorPicked;
        }
        _extra = e.NameScope.Find<Control>(ExtraPart);
        _stacked = false;
        PseudoClasses.Set(StackedPseudoClass, false);
        UpdateLabelColumn();
        UpdateValueSpan();
        UpdateAutomation();
    }

    /// <summary>
    /// Fits the row to the width before the grid measures it. A grid's Auto column offers its content unlimited width, so a long
    /// description would push the fields out of the box instead of wrapping: the label gets what the fields leave, and when
    /// that is too little the label moves above them.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        FitToWidth(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private void FitToWidth(double available)
    {
        if (_layout is null || _toggle is null)
        {
            return;
        }
        if (double.IsInfinity(available))
        {
            SetStacked(false);
            _toggle.MaxWidth = double.PositiveInfinity;
            return;
        }
        var fields = FieldsWidth();
        var fixedLabel = !double.IsNaN(LabelWidth) && LabelWidth >= 0;
        var stacked = available < (fixedLabel ? LabelWidth : LabelMinWidth) + fields;
        SetStacked(stacked);
        _toggle.MaxWidth = stacked || fixedLabel ? double.PositiveInfinity : available - fields;
    }

    /// <summary>
    /// A grid keeps the gap of a column whose content is collapsed, so with no extra content the value editor would stop one gap
    /// short of the edge, and that gap would count against the room for the fields. It takes the empty column over instead.
    /// </summary>
    private void UpdateValueSpan()
    {
        if (_layout is not { } layout || _extra is null)
        {
            return;
        }
        foreach (var child in layout.Children)
        {
            if (child != _toggle && child != _operator && child != _extra)
            {
                Grid.SetColumnSpan(child, ExtraContent is null ? 2 : 1);
            }
        }
    }

    /// <summary>The width of everything after the label, with the gap before each part.</summary>
    private double FieldsWidth()
    {
        var total = 0.0;
        foreach (var child in _layout!.Children)
        {
            if (child == _toggle || !child.IsVisible)
            {
                continue;
            }
            var width = child.Width;
            if (double.IsNaN(width))
            {
                width = child.MinWidth;
            }
            if (width <= 0)
            {
                // Content sized by itself (the extra content): ask it.
                child.Measure(new Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
                width = child.DesiredSize.Width;
            }
            total += _layout.ColumnSpacing + width;
        }
        return total;
    }

    private void SetStacked(bool stacked)
    {
        if (_stacked == stacked || _layout is not { } layout || _toggle is not { } toggle)
        {
            return;
        }
        _stacked = stacked;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        var fields = layout.Children.Where(child => child != toggle).ToList();
        if (stacked)
        {
            // The label column goes: the fields shift into its place on a second row, and the label spans them.
            layout.ColumnDefinitions.RemoveAt(0);
            layout.RowDefinitions = new RowDefinitions("Auto,Auto");
            layout.RowSpacing = layout.ColumnSpacing;
            Grid.SetColumnSpan(toggle, layout.ColumnDefinitions.Count);
            foreach (var field in fields)
            {
                Grid.SetRow(field, 1);
                Grid.SetColumn(field, Grid.GetColumn(field) - 1);
            }
        }
        else
        {
            layout.ColumnDefinitions.Insert(0, new ColumnDefinition(GridLength.Auto));
            layout.RowDefinitions = new RowDefinitions();
            layout.RowSpacing = 0;
            Grid.SetColumnSpan(toggle, 1);
            foreach (var field in fields)
            {
                Grid.SetRow(field, 0);
                Grid.SetColumn(field, Grid.GetColumn(field) + 1);
            }
            UpdateLabelColumn();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(InactivePseudoClass, !IsActive);
            if (_toggle is not null && _toggle.IsChecked != IsActive)
            {
                _toggle.IsChecked = IsActive;
            }
            RaiseFilterChanged();
        }
        else if (change.Property == ExtraContentProperty)
        {
            PseudoClasses.Set(ExtraPseudoClass, change.NewValue is not null);
            UpdateValueSpan();
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizePseudoClasses();
        }
        else if (change.Property == LabelSizeGroupProperty || change.Property == LabelWidthProperty)
        {
            UpdateLabelColumn();
        }
        else if (change.Property == LabelProperty || change.Property == OperatorTextProperty)
        {
            UpdateAutomation();
        }
    }

    /// <summary>Raises <see cref="FilterChanged"/>.</summary>
    protected void RaiseFilterChanged() => RaiseEvent(new RoutedEventArgs(FilterChangedEvent, this));

    /// <summary>Shows the current operator in the operator field; call when the operator property changes.</summary>
    protected void SyncOperator()
    {
        if (_operator is null)
        {
            return;
        }
        var current = CurrentOperator();
        _syncingOperator = true;
        try
        {
            _operator.SelectedItem = current;
        }
        finally
        {
            _syncingOperator = false;
        }
        Tooltip.SetTip(_operator, NameOf(current));
        UpdateAutomation();
    }

    /// <summary>Rebuilds the operator menu; call when the allowed operators change.</summary>
    protected void RefreshOperatorChoices()
    {
        if (_operator is null)
        {
            return;
        }
        _syncingOperator = true;
        try
        {
            _operator.ItemsSource = OperatorChoices();
        }
        finally
        {
            _syncingOperator = false;
        }
        SyncOperator();
    }

    /// <summary>The label as plain text (for accessible names), or null when it is not text.</summary>
    protected string? LabelText => Label switch
    {
        string text => text,
        TextBlock block => block.Text,
        _ => null
    };

    /// <summary>Names the parts after the label; derived boxes extend it for their value editor.</summary>
    protected virtual void UpdateAutomation()
    {
        if (_operator is null)
        {
            return;
        }
        var label = LabelText;
        var name = string.IsNullOrEmpty(label) ? OperatorText : $"{label} {OperatorText.ToLowerInvariant()}";
        AutomationProperties.SetName(_operator, name);
        AutomationProperties.SetHelpText(_operator, NameOf(CurrentOperator()));
    }

    private void UpdateLabelColumn()
    {
        if (_layout is null || _layout.ColumnDefinitions.Count == 0 || _stacked)
        {
            return;
        }
        var width = LabelWidth;
        var group = string.IsNullOrWhiteSpace(LabelSizeGroup) ? null : LabelSizeGroup;
        // A fresh definition: the grid joins a shared size group when a definition is added, not when its group changes.
        _layout.ColumnDefinitions[0] = new ColumnDefinition
        {
            Width = double.IsNaN(width) || width < 0 ? GridLength.Auto : new GridLength(width),
            SharedSizeGroup = group
        };
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_toggle is { } toggle)
        {
            SetCurrentValue(IsActiveProperty, toggle.IsChecked == true);
        }
    }

    private void OnOperatorPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingOperator || _operator?.SelectedItem is not { } picked)
        {
            return;
        }
        PickOperator(picked);
    }
}

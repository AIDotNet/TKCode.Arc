using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="PropertyGrid"/>: the label (indented by depth, with a chevron when the value expands) and the
/// editor, plus the add and remove buttons of lists. Created by the grid; style it with its pseudo-classes or by
/// <see cref="PropertyName"/>, for example <c>arc|PropertyGridItem[PropertyName=Radius]</c>.
/// </summary>
[TemplatePart(EditorPart, typeof(ContentPresenter))]
[TemplatePart(ExpanderPart, typeof(Control))]
[TemplatePart(SplitterPart, typeof(Control))]
[TemplatePart(AddPart, typeof(Button))]
[TemplatePart(RemovePart, typeof(Button))]
[PseudoClasses(SelectedPseudoClass, ReadOnlyPseudoClass, MixedPseudoClass, ExpandablePseudoClass, ExpandedPseudoClass,
    NestedPseudoClass, CollectionPseudoClass, ListItemPseudoClass, InvalidPseudoClass)]
public class PropertyGridItem : TemplatedControl
{
    public const string EditorPart = "PART_Editor";
    public const string ExpanderPart = "PART_Expander";
    public const string SplitterPart = "PART_Splitter";
    public const string AddPart = "PART_Add";
    public const string RemovePart = "PART_Remove";

    /// <summary>The row is the grid's selected row.</summary>
    public const string SelectedPseudoClass = ":selected";

    /// <summary>The value cannot be written.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>The selected objects hold different values.</summary>
    public const string MixedPseudoClass = ":mixed";

    /// <summary>The value has rows of its own.</summary>
    public const string ExpandablePseudoClass = ":expandable";

    /// <summary>The value's rows are shown.</summary>
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>The row is below the top level.</summary>
    public const string NestedPseudoClass = ":nested";

    /// <summary>The value is a list.</summary>
    public const string CollectionPseudoClass = ":collection";

    /// <summary>The row is an item of a list.</summary>
    public const string ListItemPseudoClass = ":list-item";

    /// <summary>The last write failed (<see cref="PropertyGridRow.ErrorMessage"/>).</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>The name of the row's property (for style selectors).</summary>
    public static readonly StyledProperty<string?> PropertyNameProperty =
        AvaloniaProperty.Register<PropertyGridItem, string?>(nameof(PropertyName));

    /// <summary>Width of the label column, inherited from the grid.</summary>
    public static readonly StyledProperty<double> LabelWidthProperty =
        PropertyGrid.LabelWidthProperty.AddOwner<PropertyGridItem>();

    /// <summary>Indent of one nesting level.</summary>
    public static readonly StyledProperty<double> IndentStepProperty =
        AvaloniaProperty.Register<PropertyGridItem, double>(nameof(IndentStep), 14);

    /// <summary>The row shown.</summary>
    public static readonly DirectProperty<PropertyGridItem, PropertyGridRow?> RowProperty =
        AvaloniaProperty.RegisterDirect<PropertyGridItem, PropertyGridRow?>(nameof(Row), o => o.Row);

    /// <summary>The label text.</summary>
    public static readonly DirectProperty<PropertyGridItem, string?> LabelProperty =
        AvaloniaProperty.RegisterDirect<PropertyGridItem, string?>(nameof(Label), o => o.Label);

    /// <summary>The label's left indent (nesting level × <see cref="IndentStep"/>).</summary>
    public static readonly DirectProperty<PropertyGridItem, Thickness> IndentProperty =
        AvaloniaProperty.RegisterDirect<PropertyGridItem, Thickness>(nameof(Indent), o => o.Indent);

    private PropertyGrid? _grid;
    private PropertyGridRow? _row;
    private string? _label;
    private Thickness _indent;
    private ContentPresenter? _editor;
    private Control? _expander;
    private Control? _splitter;
    private Button? _add;
    private Button? _remove;
    private string[] _rowClasses = [];
    private bool _resizing;
    private double _resizeStart;
    private double _resizeOrigin;

    static PropertyGridItem()
    {
        FocusableProperty.OverrideDefaultValue<PropertyGridItem>(true);
        IndentStepProperty.Changed.AddClassHandler<PropertyGridItem>((item, _) => item.UpdateIndent());
    }

    /// <summary>Creates a row container (the grid does this).</summary>
    public PropertyGridItem()
    {
        AddHandler(GotFocusEvent, (_, _) =>
        {
            if (_grid is not null && _row is not null)
            {
                _grid.SelectedRow = _row;
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Bubble);
    }

    /// <summary>The property name.</summary>
    public string? PropertyName { get => GetValue(PropertyNameProperty); private set => SetValue(PropertyNameProperty, value); }

    /// <summary>Width of the label column.</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>Indent of one nesting level.</summary>
    public double IndentStep { get => GetValue(IndentStepProperty); set => SetValue(IndentStepProperty, value); }

    /// <summary>The row shown.</summary>
    public PropertyGridRow? Row
    {
        get => _row;
        private set => SetAndRaise(RowProperty, ref _row, value);
    }

    /// <summary>The label text.</summary>
    public string? Label
    {
        get => _label;
        private set => SetAndRaise(LabelProperty, ref _label, value);
    }

    /// <summary>The label's left indent.</summary>
    public Thickness Indent
    {
        get => _indent;
        private set => SetAndRaise(IndentProperty, ref _indent, value);
    }

    internal void Attach(PropertyGrid grid, PropertyGridRow row)
    {
        if (_row is not null)
        {
            _row.PropertyChanged -= OnRowChanged;
        }
        _grid = grid;
        Row = row;
        row.PropertyChanged += OnRowChanged;
        PropertyName = row.Name;
        Label = row.DisplayName;
        AutomationProperties.SetName(this, row.DisplayName);
        AutomationProperties.SetHelpText(this, row.Description);
        UpdateIndent();
        UpdateRowClasses(row.Property.Classes);
        UpdateEditor();
        UpdateState();
        UpdateSelection();
    }

    internal void Detach()
    {
        if (_row is not null)
        {
            _row.PropertyChanged -= OnRowChanged;
        }
        _resizing = false;
    }

    /// <summary>Moves keyboard focus into the editor; false when it has nothing focusable.</summary>
    internal bool FocusEditor()
    {
        if (_editor?.Child is not { } editor)
        {
            return false;
        }
        if (editor.Focusable && editor.IsEffectivelyEnabled && editor.Focus(NavigationMethod.Tab))
        {
            return true;
        }
        foreach (var descendant in editor.GetVisualDescendants())
        {
            if (descendant is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } input
                && input.Focus(NavigationMethod.Tab))
            {
                return true;
            }
        }
        return false;
    }

    internal void RefreshEditor() => UpdateEditor();

    internal void UpdateSelection() => PseudoClasses.Set(SelectedPseudoClass, _row is not null && ReferenceEquals(_grid?.SelectedRow, _row));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_expander is not null)
        {
            _expander.PointerPressed -= OnExpanderPressed;
        }
        if (_splitter is not null)
        {
            _splitter.PointerPressed -= OnSplitterPressed;
            _splitter.PointerMoved -= OnSplitterMoved;
            _splitter.PointerReleased -= OnSplitterReleased;
            _splitter.PointerCaptureLost -= OnSplitterCaptureLost;
        }
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        if (_editor is not null)
        {
            _editor.ContentTemplate = null;
            _editor.Content = null;
        }
        _editor = e.NameScope.Find<ContentPresenter>(EditorPart);
        _expander = e.NameScope.Find<Control>(ExpanderPart);
        _splitter = e.NameScope.Find<Control>(SplitterPart);
        _add = e.NameScope.Find<Button>(AddPart);
        _remove = e.NameScope.Find<Button>(RemovePart);
        if (_expander is not null)
        {
            _expander.PointerPressed += OnExpanderPressed;
        }
        if (_splitter is not null)
        {
            _splitter.PointerPressed += OnSplitterPressed;
            _splitter.PointerMoved += OnSplitterMoved;
            _splitter.PointerReleased += OnSplitterReleased;
            _splitter.PointerCaptureLost += OnSplitterCaptureLost;
        }
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        UpdateEditor();
        UpdateState();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _row is null || _grid is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _grid.SelectedRow = _row;
        if (e.ClickCount == 2 && _row.IsExpandable)
        {
            _row.IsExpanded = !_row.IsExpanded;
        }
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // Escape in an editor (that the editor did not use) gives focus back to the row, for the arrow keys.
        if (e.Handled || e.Key != Key.Escape || ReferenceEquals(e.Source, this))
        {
            return;
        }
        Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnExpanderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_row is { IsExpandable: true } row && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (_grid is not null)
            {
                _grid.SelectedRow = row;
            }
            row.IsExpanded = !row.IsExpanded;
            Focus(NavigationMethod.Pointer);
            e.Handled = true;
        }
    }

    private void OnSplitterPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_grid is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _resizing = true;
        _resizeStart = e.GetPosition(_grid).X;
        _resizeOrigin = _grid.LabelWidth;
        e.Pointer.Capture(_splitter);
        e.Handled = true;
    }

    private void OnSplitterMoved(object? sender, PointerEventArgs e)
    {
        if (_resizing && _grid is not null)
        {
            _grid.ResizeLabel(_resizeOrigin + e.GetPosition(_grid).X - _resizeStart);
            e.Handled = true;
        }
    }

    private void OnSplitterReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizing)
        {
            _resizing = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnSplitterCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _resizing = false;

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        _row?.AddItem();
        e.Handled = true;
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        var grid = _grid;
        var parent = _row?.Parent;
        _row?.Remove();
        e.Handled = true;
        // The row is gone: keep the keyboard in the list, on the list's row.
        if (grid is not null && parent is not null)
        {
            var index = grid.VisibleItems is IList<object> items ? items.IndexOf(parent) : -1;
            grid.FocusItem(index);
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PropertyGridRow.IsReadOnly) or nameof(PropertyGridRow.ItemCount):
                UpdateEditor();
                UpdateState();
                break;
            case nameof(PropertyGridRow.IsMixed) or nameof(PropertyGridRow.IsExpandable) or nameof(PropertyGridRow.IsExpanded)
                or nameof(PropertyGridRow.ErrorMessage) or nameof(PropertyGridRow.CanAddItem) or nameof(PropertyGridRow.CanRemove):
                UpdateState();
                break;
        }
    }

    private void UpdateIndent() => Indent = new Thickness((_row?.Depth ?? 0) * IndentStep, 0, 0, 0);

    /// <summary>Shows the row in the editor slot. Same template: only the content changes and the editor is reused; a new
    /// template is set after the content (null-safe order, implementation guide section 13).</summary>
    private void UpdateEditor()
    {
        if (_editor is null || _row is null || _grid is null)
        {
            return;
        }
        var template = _grid.ResolveEditor(_row);
        if (ReferenceEquals(_editor.ContentTemplate, template))
        {
            _editor.Content = _row;
            return;
        }
        _editor.ContentTemplate = null;
        _editor.Content = _row;
        _editor.ContentTemplate = template;
    }

    private void UpdateState()
    {
        var row = _row;
        PseudoClasses.Set(ReadOnlyPseudoClass, row?.IsReadOnly == true);
        PseudoClasses.Set(MixedPseudoClass, row?.IsMixed == true);
        PseudoClasses.Set(ExpandablePseudoClass, row?.IsExpandable == true);
        PseudoClasses.Set(ExpandedPseudoClass, row is { IsExpandable: true, IsExpanded: true });
        PseudoClasses.Set(NestedPseudoClass, row?.Depth > 0);
        PseudoClasses.Set(CollectionPseudoClass, row?.IsCollection == true);
        PseudoClasses.Set(ListItemPseudoClass, row?.Index >= 0);
        PseudoClasses.Set(InvalidPseudoClass, row?.ErrorMessage is not null);
        ToolTip.SetTip(this, row?.ErrorMessage);
        if (_add is not null)
        {
            _add.IsVisible = row?.CanAddItem == true;
            AutomationProperties.SetName(_add, _grid?.AddItemText);
        }
        if (_remove is not null)
        {
            _remove.IsVisible = row?.CanRemove == true;
            AutomationProperties.SetName(_remove, _grid?.RemoveItemText);
        }
        if (row is not null)
        {
            AutomationProperties.SetItemStatus(this, row.IsExpandable ? (row.IsExpanded ? "Expanded" : "Collapsed") : null);
        }
    }

    private void UpdateRowClasses(string? classes)
    {
        foreach (var name in _rowClasses)
        {
            Classes.Remove(name);
        }
        _rowClasses = string.IsNullOrWhiteSpace(classes) ? [] : classes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var name in _rowClasses)
        {
            Classes.Add(name);
        }
    }
}

/// <summary>A category header of a <see cref="PropertyGrid"/>: a click, Enter or Space collapses or expands its rows.</summary>
[PseudoClasses(ExpandedPseudoClass)]
public class PropertyGridCategoryItem : TemplatedControl
{
    /// <summary>The category's rows are shown.</summary>
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>The category shown.</summary>
    public static readonly DirectProperty<PropertyGridCategoryItem, PropertyGridCategory?> CategoryProperty =
        AvaloniaProperty.RegisterDirect<PropertyGridCategoryItem, PropertyGridCategory?>(nameof(Category), o => o.Category);

    /// <summary>The header text.</summary>
    public static readonly DirectProperty<PropertyGridCategoryItem, string?> HeaderProperty =
        AvaloniaProperty.RegisterDirect<PropertyGridCategoryItem, string?>(nameof(Header), o => o.Header);

    private PropertyGridCategory? _category;
    private string? _header;

    static PropertyGridCategoryItem()
    {
        FocusableProperty.OverrideDefaultValue<PropertyGridCategoryItem>(true);
    }

    /// <summary>The category shown.</summary>
    public PropertyGridCategory? Category
    {
        get => _category;
        private set => SetAndRaise(CategoryProperty, ref _category, value);
    }

    /// <summary>The header text.</summary>
    public string? Header
    {
        get => _header;
        private set => SetAndRaise(HeaderProperty, ref _header, value);
    }

    internal void Attach(PropertyGridCategory category)
    {
        if (_category is not null)
        {
            _category.PropertyChanged -= OnCategoryChanged;
        }
        Category = category;
        Header = category.Name;
        category.PropertyChanged += OnCategoryChanged;
        AutomationProperties.SetName(this, category.Name);
        Update();
    }

    internal void Detach()
    {
        if (_category is not null)
        {
            _category.PropertyChanged -= OnCategoryChanged;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && _category is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Focus(NavigationMethod.Pointer);
            _category.IsExpanded = !_category.IsExpanded;
            e.Handled = true;
        }
    }

    private void OnCategoryChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        var expanded = _category?.IsExpanded == true;
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        AutomationProperties.SetItemStatus(this, expanded ? "Expanded" : "Collapsed");
    }
}

/// <summary>The virtualized list inside a <see cref="PropertyGrid"/>: a <see cref="PropertyGridCategoryItem"/> for each
/// category and a <see cref="PropertyGridItem"/> for each row, each kind recycled on its own.</summary>
internal sealed class PropertyGridItemsHost : ItemsControl
{
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = item is PropertyGridCategory ? typeof(PropertyGridCategoryItem) : typeof(PropertyGridItem);
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => (Type?)recycleKey == typeof(PropertyGridCategoryItem) ? new PropertyGridCategoryItem() : new PropertyGridItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (TemplatedParent is not PropertyGrid grid)
        {
            return;
        }
        switch (container)
        {
            case PropertyGridItem rowItem when item is PropertyGridRow row:
                rowItem.Attach(grid, row);
                break;
            case PropertyGridCategoryItem header when item is PropertyGridCategory category:
                header.Attach(category);
                break;
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        switch (container)
        {
            case PropertyGridItem rowItem:
                rowItem.Detach();
                break;
            case PropertyGridCategoryItem header:
                header.Detach();
                break;
        }
    }
}

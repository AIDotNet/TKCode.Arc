using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A transfer list (shuttle box): two lists side by side and the actions that move items between them. Moving edits
/// the bound <see cref="SourceItems"/> and <see cref="TargetItems"/> collections themselves (the same object references
/// leave one and join the other), so a view model reads the result directly. Each list selects like a list box (click,
/// Ctrl and Shift, Ctrl+A), has a select-all box and an optional search filter; Enter or a double click moves the
/// selection, and the move commands are public for buttons of your own.
/// </summary>
[TemplatePart(SourceListPart, typeof(Control))]
[TemplatePart(TargetListPart, typeof(Control))]
[TemplatePart(SourceSelectAllPart, typeof(Checkbox))]
[TemplatePart(TargetSelectAllPart, typeof(Checkbox))]
[TemplatePart(SourceCountPart, typeof(TextBlock))]
[TemplatePart(TargetCountPart, typeof(TextBlock))]
[TemplatePart(SourceEmptyPart, typeof(ContentPresenter))]
[TemplatePart(TargetEmptyPart, typeof(ContentPresenter))]
[TemplatePart(MoveToTargetPart, typeof(AvaloniaButton))]
[TemplatePart(MoveToSourcePart, typeof(AvaloniaButton))]
[TemplatePart(MoveAllToTargetPart, typeof(AvaloniaButton))]
[TemplatePart(MoveAllToSourcePart, typeof(AvaloniaButton))]
public class TransferBox : TemplatedControl
{
    public const string SourceListPart = "PART_SourceList";
    public const string TargetListPart = "PART_TargetList";
    public const string SourceSelectAllPart = "PART_SourceSelectAll";
    public const string TargetSelectAllPart = "PART_TargetSelectAll";
    public const string SourceCountPart = "PART_SourceCount";
    public const string TargetCountPart = "PART_TargetCount";
    public const string SourceEmptyPart = "PART_SourceEmpty";
    public const string TargetEmptyPart = "PART_TargetEmpty";
    public const string MoveToTargetPart = "PART_MoveToTarget";
    public const string MoveToSourcePart = "PART_MoveToSource";
    public const string MoveAllToTargetPart = "PART_MoveAllToTarget";
    public const string MoveAllToSourcePart = "PART_MoveAllToSource";

    #region 公共属性定义

    /// <summary>The items of the left list. Moving removes items from it and adds items to it, so it must be a modifiable <see cref="IList"/> (for example an <c>ObservableCollection&lt;T&gt;</c>); a read-only or fixed-size list disables moving.</summary>
    public static readonly StyledProperty<IList?> SourceItemsProperty =
        AvaloniaProperty.Register<TransferBox, IList?>(nameof(SourceItems));

    /// <summary>The items of the right list; the same rules as <see cref="SourceItems"/>. Moved items are added at its end.</summary>
    public static readonly StyledProperty<IList?> TargetItemsProperty =
        AvaloniaProperty.Register<TransferBox, IList?>(nameof(TargetItems));

    /// <summary>Title of the left list: text or any content.</summary>
    public static readonly StyledProperty<object?> SourceHeaderProperty =
        AvaloniaProperty.Register<TransferBox, object?>(nameof(SourceHeader), "Available");

    /// <summary>Title of the right list: text or any content.</summary>
    public static readonly StyledProperty<object?> TargetHeaderProperty =
        AvaloniaProperty.Register<TransferBox, object?>(nameof(TargetHeader), "Selected");

    /// <summary>Template of a row in both lists. Without one a row shows <see cref="ItemTextSelector"/> or the item's text.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<TransferBox, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>Text of an item, used by the default row, the search filter and the row's accessible name. Typed as a delegate, so it needs no reflection; null uses <c>ToString()</c>.</summary>
    public static readonly StyledProperty<Func<object?, string>?> ItemTextSelectorProperty =
        AvaloniaProperty.Register<TransferBox, Func<object?, string>?>(nameof(ItemTextSelector));

    /// <summary>How rows are selected in both lists: <c>Multiple</c> (default; Ctrl and Shift extend the selection, Ctrl+A selects all) or <c>Single</c>.</summary>
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<TransferBox, SelectionMode>(nameof(SelectionMode), SelectionMode.Multiple);

    /// <summary>Shows a select-all box in each list header (only with multiple selection).</summary>
    public static readonly StyledProperty<bool> IsSelectAllVisibleProperty =
        AvaloniaProperty.Register<TransferBox, bool>(nameof(IsSelectAllVisible), true);

    /// <summary>Shows the "move all" buttons above and below the move buttons.</summary>
    public static readonly StyledProperty<bool> IsMoveAllVisibleProperty =
        AvaloniaProperty.Register<TransferBox, bool>(nameof(IsMoveAllVisible));

    /// <summary>Shows a search field under each list header; it filters the rows by <see cref="ItemTextSelector"/>.</summary>
    public static readonly StyledProperty<bool> IsSearchableProperty =
        AvaloniaProperty.Register<TransferBox, bool>(nameof(IsSearchable));

    /// <summary>Search text of the left list (case-insensitive, matches anywhere in the item text).</summary>
    public static readonly StyledProperty<string?> SourceSearchTextProperty =
        AvaloniaProperty.Register<TransferBox, string?>(nameof(SourceSearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Search text of the right list (case-insensitive, matches anywhere in the item text).</summary>
    public static readonly StyledProperty<string?> TargetSearchTextProperty =
        AvaloniaProperty.Register<TransferBox, string?>(nameof(TargetSearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Placeholder and accessible name of the search fields.</summary>
    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(SearchPlaceholder), "Search");

    /// <summary>Shown in the left list when it has no items: text or any content.</summary>
    public static readonly StyledProperty<object?> SourceEmptyContentProperty =
        AvaloniaProperty.Register<TransferBox, object?>(nameof(SourceEmptyContent), "No items");

    /// <summary>Shown in the right list when it has no items: text or any content.</summary>
    public static readonly StyledProperty<object?> TargetEmptyContentProperty =
        AvaloniaProperty.Register<TransferBox, object?>(nameof(TargetEmptyContent), "No items");

    /// <summary>Shown in a list whose items are all hidden by its search text.</summary>
    public static readonly StyledProperty<string> NoMatchesTextProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(NoMatchesText), "No matches");

    /// <summary>Accessible name and tooltip of the button that moves the selection to the right list.</summary>
    public static readonly StyledProperty<string> MoveToTargetLabelProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(MoveToTargetLabel), "Move to selected");

    /// <summary>Accessible name and tooltip of the button that moves the selection back to the left list.</summary>
    public static readonly StyledProperty<string> MoveToSourceLabelProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(MoveToSourceLabel), "Move to available");

    /// <summary>Accessible name and tooltip of the button that moves every shown item to the right list.</summary>
    public static readonly StyledProperty<string> MoveAllToTargetLabelProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(MoveAllToTargetLabel), "Move all to selected");

    /// <summary>Accessible name and tooltip of the button that moves every shown item back to the left list.</summary>
    public static readonly StyledProperty<string> MoveAllToSourceLabelProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(MoveAllToSourceLabel), "Move all to available");

    /// <summary>Accessible name of the select-all boxes (followed by the list title).</summary>
    public static readonly StyledProperty<string> SelectAllLabelProperty =
        AvaloniaProperty.Register<TransferBox, string>(nameof(SelectAllLabel), "Select all");

    /// <summary>Number of selected rows in the left list.</summary>
    public static readonly DirectProperty<TransferBox, int> SourceSelectionCountProperty =
        AvaloniaProperty.RegisterDirect<TransferBox, int>(nameof(SourceSelectionCount), o => o.SourceSelectionCount);

    /// <summary>Number of selected rows in the right list.</summary>
    public static readonly DirectProperty<TransferBox, int> TargetSelectionCountProperty =
        AvaloniaProperty.RegisterDirect<TransferBox, int>(nameof(TargetSelectionCount), o => o.TargetSelectionCount);

    /// <summary>True when the left list has a selection and both collections can be modified.</summary>
    public static readonly DirectProperty<TransferBox, bool> CanMoveToTargetProperty =
        AvaloniaProperty.RegisterDirect<TransferBox, bool>(nameof(CanMoveToTarget), o => o.CanMoveToTarget);

    /// <summary>True when the right list has a selection and both collections can be modified.</summary>
    public static readonly DirectProperty<TransferBox, bool> CanMoveToSourceProperty =
        AvaloniaProperty.RegisterDirect<TransferBox, bool>(nameof(CanMoveToSource), o => o.CanMoveToSource);

    /// <summary>Raised after items moved, once per move with all the moved items.</summary>
    public static readonly RoutedEvent<TransferBoxTransferredEventArgs> ItemsTransferredEvent =
        RoutedEvent.Register<TransferBox, TransferBoxTransferredEventArgs>(nameof(ItemsTransferred), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SourceItemsProperty"/>
    public IList? SourceItems { get => GetValue(SourceItemsProperty); set => SetValue(SourceItemsProperty, value); }

    /// <inheritdoc cref="TargetItemsProperty"/>
    public IList? TargetItems { get => GetValue(TargetItemsProperty); set => SetValue(TargetItemsProperty, value); }

    /// <inheritdoc cref="SourceHeaderProperty"/>
    public object? SourceHeader { get => GetValue(SourceHeaderProperty); set => SetValue(SourceHeaderProperty, value); }

    /// <inheritdoc cref="TargetHeaderProperty"/>
    public object? TargetHeader { get => GetValue(TargetHeaderProperty); set => SetValue(TargetHeaderProperty, value); }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <inheritdoc cref="ItemTextSelectorProperty"/>
    public Func<object?, string>? ItemTextSelector { get => GetValue(ItemTextSelectorProperty); set => SetValue(ItemTextSelectorProperty, value); }

    /// <inheritdoc cref="SelectionModeProperty"/>
    public SelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <inheritdoc cref="IsSelectAllVisibleProperty"/>
    public bool IsSelectAllVisible { get => GetValue(IsSelectAllVisibleProperty); set => SetValue(IsSelectAllVisibleProperty, value); }

    /// <inheritdoc cref="IsMoveAllVisibleProperty"/>
    public bool IsMoveAllVisible { get => GetValue(IsMoveAllVisibleProperty); set => SetValue(IsMoveAllVisibleProperty, value); }

    /// <inheritdoc cref="IsSearchableProperty"/>
    public bool IsSearchable { get => GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }

    /// <inheritdoc cref="SourceSearchTextProperty"/>
    public string? SourceSearchText { get => GetValue(SourceSearchTextProperty); set => SetValue(SourceSearchTextProperty, value); }

    /// <inheritdoc cref="TargetSearchTextProperty"/>
    public string? TargetSearchText { get => GetValue(TargetSearchTextProperty); set => SetValue(TargetSearchTextProperty, value); }

    /// <inheritdoc cref="SearchPlaceholderProperty"/>
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <inheritdoc cref="SourceEmptyContentProperty"/>
    public object? SourceEmptyContent { get => GetValue(SourceEmptyContentProperty); set => SetValue(SourceEmptyContentProperty, value); }

    /// <inheritdoc cref="TargetEmptyContentProperty"/>
    public object? TargetEmptyContent { get => GetValue(TargetEmptyContentProperty); set => SetValue(TargetEmptyContentProperty, value); }

    /// <inheritdoc cref="NoMatchesTextProperty"/>
    public string NoMatchesText { get => GetValue(NoMatchesTextProperty); set => SetValue(NoMatchesTextProperty, value); }

    /// <inheritdoc cref="MoveToTargetLabelProperty"/>
    public string MoveToTargetLabel { get => GetValue(MoveToTargetLabelProperty); set => SetValue(MoveToTargetLabelProperty, value); }

    /// <inheritdoc cref="MoveToSourceLabelProperty"/>
    public string MoveToSourceLabel { get => GetValue(MoveToSourceLabelProperty); set => SetValue(MoveToSourceLabelProperty, value); }

    /// <inheritdoc cref="MoveAllToTargetLabelProperty"/>
    public string MoveAllToTargetLabel { get => GetValue(MoveAllToTargetLabelProperty); set => SetValue(MoveAllToTargetLabelProperty, value); }

    /// <inheritdoc cref="MoveAllToSourceLabelProperty"/>
    public string MoveAllToSourceLabel { get => GetValue(MoveAllToSourceLabelProperty); set => SetValue(MoveAllToSourceLabelProperty, value); }

    /// <inheritdoc cref="SelectAllLabelProperty"/>
    public string SelectAllLabel { get => GetValue(SelectAllLabelProperty); set => SetValue(SelectAllLabelProperty, value); }

    /// <inheritdoc cref="SourceSelectionCountProperty"/>
    public int SourceSelectionCount { get => _sourceSelectionCount; private set => SetAndRaise(SourceSelectionCountProperty, ref _sourceSelectionCount, value); }

    /// <inheritdoc cref="TargetSelectionCountProperty"/>
    public int TargetSelectionCount { get => _targetSelectionCount; private set => SetAndRaise(TargetSelectionCountProperty, ref _targetSelectionCount, value); }

    /// <inheritdoc cref="CanMoveToTargetProperty"/>
    public bool CanMoveToTarget { get => _canMoveToTarget; private set => SetAndRaise(CanMoveToTargetProperty, ref _canMoveToTarget, value); }

    /// <inheritdoc cref="CanMoveToSourceProperty"/>
    public bool CanMoveToSource { get => _canMoveToSource; private set => SetAndRaise(CanMoveToSourceProperty, ref _canMoveToSource, value); }

    /// <summary>Moves the selected rows of the left list to the right list (<see cref="MoveToTarget"/>).</summary>
    public ICommand MoveToTargetCommand => _moveToTarget;

    /// <summary>Moves the selected rows of the right list back to the left list (<see cref="MoveToSource"/>).</summary>
    public ICommand MoveToSourceCommand => _moveToSource;

    /// <summary>Moves every shown row of the left list to the right list (<see cref="MoveAllToTarget"/>).</summary>
    public ICommand MoveAllToTargetCommand => _moveAllToTarget;

    /// <summary>Moves every shown row of the right list back to the left list (<see cref="MoveAllToSource"/>).</summary>
    public ICommand MoveAllToSourceCommand => _moveAllToSource;

    /// <inheritdoc cref="ItemsTransferredEvent"/>
    public event EventHandler<TransferBoxTransferredEventArgs>? ItemsTransferred
    {
        add => AddHandler(ItemsTransferredEvent, value);
        remove => RemoveHandler(ItemsTransferredEvent, value);
    }

    #endregion

    private readonly TransferBoxView _sourceView = new();
    private readonly TransferBoxView _targetView = new();
    private readonly TransferBoxCommand _moveToTarget;
    private readonly TransferBoxCommand _moveToSource;
    private readonly TransferBoxCommand _moveAllToTarget;
    private readonly TransferBoxCommand _moveAllToSource;
    private TransferBoxList? _sourceList;
    private TransferBoxList? _targetList;
    private Checkbox? _sourceSelectAll;
    private Checkbox? _targetSelectAll;
    private TextBlock? _sourceCount;
    private TextBlock? _targetCount;
    private ContentPresenter? _sourceEmpty;
    private ContentPresenter? _targetEmpty;
    private AvaloniaButton? _moveToTargetButton;
    private AvaloniaButton? _moveToSourceButton;
    private AvaloniaButton? _moveAllToTargetButton;
    private AvaloniaButton? _moveAllToSourceButton;
    private int _sourceSelectionCount;
    private int _targetSelectionCount;
    private bool _canMoveToTarget;
    private bool _canMoveToSource;
    private bool _updatingSelectAll;

    public TransferBox()
    {
        _moveToTarget = new TransferBoxCommand(() => CanMoveToTarget, () => MoveToTarget());
        _moveToSource = new TransferBoxCommand(() => CanMoveToSource, () => MoveToSource());
        _moveAllToTarget = new TransferBoxCommand(() => CanMoveAll(TransferDirection.ToTarget), () => MoveAllToTarget());
        _moveAllToSource = new TransferBoxCommand(() => CanMoveAll(TransferDirection.ToSource), () => MoveAllToSource());
        _sourceView.Changed += UpdateState;
        _targetView.Changed += UpdateState;
    }

    // ---------------------------------------------------------------- public operations

    /// <summary>Moves the selected rows of the left list to the end of <see cref="TargetItems"/>; returns how many moved.</summary>
    /// <exception cref="InvalidOperationException">A collection is missing, read-only or fixed-size, or both sides are the same collection.</exception>
    public int MoveToTarget() => Move(TransferDirection.ToTarget, SelectedRows(TransferBoxSide.Source));

    /// <summary>Moves the selected rows of the right list to the end of <see cref="SourceItems"/>; returns how many moved.</summary>
    /// <exception cref="InvalidOperationException">A collection is missing, read-only or fixed-size, or both sides are the same collection.</exception>
    public int MoveToSource() => Move(TransferDirection.ToSource, SelectedRows(TransferBoxSide.Target));

    /// <summary>Moves every row the left list shows (all items, or the ones its search text matches) to the right list.</summary>
    /// <exception cref="InvalidOperationException">A collection is missing, read-only or fixed-size, or both sides are the same collection.</exception>
    public int MoveAllToTarget() => Move(TransferDirection.ToTarget, AllRows(_sourceView));

    /// <summary>Moves every row the right list shows (all items, or the ones its search text matches) back to the left list.</summary>
    /// <exception cref="InvalidOperationException">A collection is missing, read-only or fixed-size, or both sides are the same collection.</exception>
    public int MoveAllToSource() => Move(TransferDirection.ToSource, AllRows(_targetView));

    /// <summary>The selected items of one list, in list order.</summary>
    public IReadOnlyList<object?> GetSelectedItems(TransferBoxSide side)
    {
        var view = ViewOf(side);
        return SelectedRows(side).Select(row => view.Items[row]).ToArray();
    }

    /// <summary>Selects every row of one list (only with multiple selection).</summary>
    public void SelectAll(TransferBoxSide side)
    {
        if (ListOf(side) is { } list && SelectionMode.HasFlag(SelectionMode.Multiple))
        {
            list.SelectAll();
        }
    }

    /// <summary>Clears the selection of one list.</summary>
    public void ClearSelection(TransferBoxSide side) => ListOf(side)?.UnselectAll();

    // ---------------------------------------------------------------- moving

    private TransferBoxView ViewOf(TransferBoxSide side) => side == TransferBoxSide.Source ? _sourceView : _targetView;

    private TransferBoxList? ListOf(TransferBoxSide side) => side == TransferBoxSide.Source ? _sourceList : _targetList;

    private List<int> SelectedRows(TransferBoxSide side)
    {
        var rows = new List<int>();
        if (ListOf(side) is { } list)
        {
            var count = ViewOf(side).Items.Count;
            foreach (var index in list.Selection.SelectedIndexes)
            {
                if (index >= 0 && index < count)
                {
                    rows.Add(index);
                }
            }
            rows.Sort();
        }
        return rows;
    }

    private static List<int> AllRows(TransferBoxView view) => Enumerable.Range(0, view.Items.Count).ToList();

    private static bool IsWritable(IList? list) => list is { IsReadOnly: false, IsFixedSize: false };

    private bool CanWrite() =>
        IsWritable(SourceItems) && IsWritable(TargetItems) && !ReferenceEquals(SourceItems, TargetItems);

    private bool CanMoveAll(TransferDirection direction) =>
        IsEnabled && CanWrite() && (direction == TransferDirection.ToTarget ? _sourceView : _targetView).Items.Count > 0;

    private int Move(TransferDirection direction, List<int> rows)
    {
        if (!CanWrite())
        {
            throw new InvalidOperationException(
                "TransferBox moves items by editing SourceItems and TargetItems: both must be set to modifiable lists " +
                "(not read-only, not fixed-size, such as ObservableCollection<T>) and must not be the same collection.");
        }
        if (rows.Count == 0)
        {
            return 0;
        }

        var fromView = direction == TransferDirection.ToTarget ? _sourceView : _targetView;
        var from = fromView.Source!;
        var to = (direction == TransferDirection.ToTarget ? TargetItems : SourceItems)!;

        // Snapshot first: the selection and the rows change while the collections are edited.
        var indexes = rows.Select(fromView.SourceIndexOf).Distinct().Order().ToArray();
        var items = indexes.Select(i => from[i]).ToArray();

        // Remove by index, last first, so equal values elsewhere in the list are never taken by mistake.
        var removed = 0;
        try
        {
            for (var k = indexes.Length - 1; k >= 0; k--)
            {
                from.RemoveAt(indexes[k]);
                removed++;
            }
        }
        catch
        {
            Restore(from, indexes, items, indexes.Length - removed);
            throw;
        }

        var added = 0;
        try
        {
            foreach (var item in items)
            {
                to.Add(item);
                added++;
            }
        }
        catch
        {
            // Nothing is lost: take back what the target took and put every item back where it was.
            for (var k = 0; k < added; k++)
            {
                to.RemoveAt(to.Count - 1);
            }
            Restore(from, indexes, items, 0);
            throw;
        }
        finally
        {
            // Lists without change notifications (List<T>) are followed here.
            _sourceView.Sync();
            _targetView.Sync();
        }

        RaiseEvent(new TransferBoxTransferredEventArgs(ItemsTransferredEvent, direction, items));
        return items.Length;
    }

    /// <summary>Reinserts the removed items (those at <paramref name="indexes"/> from <paramref name="firstRemoved"/> on) at their indexes.</summary>
    private static void Restore(IList list, int[] indexes, object?[] items, int firstRemoved)
    {
        for (var k = firstRemoved; k < indexes.Length; k++)
        {
            list.Insert(Math.Min(indexes[k], list.Count), items[k]);
        }
    }

    // ---------------------------------------------------------------- template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachList(_sourceList);
        DetachList(_targetList);
        if (_sourceSelectAll is not null)
        {
            _sourceSelectAll.Click -= OnSelectAllClick;
        }
        if (_targetSelectAll is not null)
        {
            _targetSelectAll.Click -= OnSelectAllClick;
        }

        _sourceList = e.NameScope.Find<TransferBoxList>(SourceListPart);
        _targetList = e.NameScope.Find<TransferBoxList>(TargetListPart);
        _sourceSelectAll = e.NameScope.Find<Checkbox>(SourceSelectAllPart);
        _targetSelectAll = e.NameScope.Find<Checkbox>(TargetSelectAllPart);
        _sourceCount = e.NameScope.Find<TextBlock>(SourceCountPart);
        _targetCount = e.NameScope.Find<TextBlock>(TargetCountPart);
        _sourceEmpty = e.NameScope.Find<ContentPresenter>(SourceEmptyPart);
        _targetEmpty = e.NameScope.Find<ContentPresenter>(TargetEmptyPart);
        _moveToTargetButton = e.NameScope.Find<AvaloniaButton>(MoveToTargetPart);
        _moveToSourceButton = e.NameScope.Find<AvaloniaButton>(MoveToSourcePart);
        _moveAllToTargetButton = e.NameScope.Find<AvaloniaButton>(MoveAllToTargetPart);
        _moveAllToSourceButton = e.NameScope.Find<AvaloniaButton>(MoveAllToSourcePart);

        AttachList(_sourceList, _sourceView);
        AttachList(_targetList, _targetView);
        if (_sourceSelectAll is not null)
        {
            _sourceSelectAll.Click += OnSelectAllClick;
        }
        if (_targetSelectAll is not null)
        {
            _targetSelectAll.Click += OnSelectAllClick;
        }
        SetCommand(_moveToTargetButton, _moveToTarget);
        SetCommand(_moveToSourceButton, _moveToSource);
        SetCommand(_moveAllToTargetButton, _moveAllToTarget);
        SetCommand(_moveAllToSourceButton, _moveAllToSource);
        UpdateNames();
        UpdateState();
    }

    private static void SetCommand(AvaloniaButton? button, ICommand command)
    {
        if (button is not null)
        {
            button.Command = command;
        }
    }

    private void AttachList(TransferBoxList? list, TransferBoxView view)
    {
        if (list is null)
        {
            return;
        }
        list.TextOf = ItemTextSelector;
        list.SelectionMode = SelectionMode;
        list.ItemsSource = view.Items;
        list.SelectionChanged += OnListSelectionChanged;
        // Tunnel: the list box answers Enter by selecting only the focused row, so the move must see the key first.
        list.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        list.DoubleTapped += OnListDoubleTapped;
    }

    private void DetachList(TransferBoxList? list)
    {
        if (list is null)
        {
            return;
        }
        list.SelectionChanged -= OnListSelectionChanged;
        list.RemoveHandler(KeyDownEvent, OnListKeyDown);
        list.DoubleTapped -= OnListDoubleTapped;
        list.ItemsSource = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _sourceView.Attach();
        _targetView.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _sourceView.Detach();
        _targetView.Detach();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceItemsProperty)
        {
            _sourceView.SetSource(SourceItems);
        }
        else if (change.Property == TargetItemsProperty)
        {
            _targetView.SetSource(TargetItems);
        }
        else if (change.Property == SourceSearchTextProperty || change.Property == TargetSearchTextProperty
                 || change.Property == IsSearchableProperty || change.Property == ItemTextSelectorProperty)
        {
            if (change.Property == ItemTextSelectorProperty)
            {
                SetTextOf(_sourceList);
                SetTextOf(_targetList);
            }
            _sourceView.SetFilter(FilterOf(SourceSearchText));
            _targetView.SetFilter(FilterOf(TargetSearchText));
        }
        else if (change.Property == SourceHeaderProperty || change.Property == TargetHeaderProperty
                 || change.Property == SelectAllLabelProperty)
        {
            UpdateNames();
        }
        else if (change.Property == SelectionModeProperty)
        {
            // Set from here: a template binding does not reach the list box's selection mode.
            if (_sourceList is not null)
            {
                _sourceList.SelectionMode = SelectionMode;
            }
            if (_targetList is not null)
            {
                _targetList.SelectionMode = SelectionMode;
            }
            UpdateState();
        }
        else if (change.Property == IsEnabledProperty || change.Property == IsSelectAllVisibleProperty || change.Property == SourceEmptyContentProperty
                 || change.Property == TargetEmptyContentProperty || change.Property == NoMatchesTextProperty)
        {
            UpdateState();
        }
    }

    private void SetTextOf(TransferBoxList? list)
    {
        if (list is null)
        {
            return;
        }
        list.TextOf = ItemTextSelector;
        // Rebuild the rows so the new text shows.
        list.ItemsSource = null;
        list.ItemsSource = ViewOf(list == _sourceList ? TransferBoxSide.Source : TransferBoxSide.Target).Items;
    }

    private Func<object?, bool>? FilterOf(string? query)
    {
        if (!IsSearchable || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }
        var text = query.Trim();
        var selector = ItemTextSelector;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        return item =>
        {
            var value = selector is not null ? selector(item) : item?.ToString();
            return value is not null && compare.IndexOf(value, text, CompareOptions.IgnoreCase) >= 0;
        };
    }

    // ---------------------------------------------------------------- interaction

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateState();

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (sender == _sourceList && CanMoveToTarget)
        {
            MoveToTarget();
            e.Handled = true;
        }
        else if (sender == _targetList && CanMoveToSource)
        {
            MoveToSource();
            e.Handled = true;
        }
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TransferBoxList list || (e.Source as Visual)?.FindAncestorOfType<TransferBoxListItem>(includeSelf: true) is not { } row)
        {
            return;
        }
        var index = list.IndexFromContainer(row);
        if (index < 0)
        {
            return;
        }
        if (!list.Selection.IsSelected(index))
        {
            list.Selection.Select(index);
        }
        if (list == _sourceList && CanMoveToTarget)
        {
            MoveToTarget();
            e.Handled = true;
        }
        else if (list == _targetList && CanMoveToSource)
        {
            MoveToSource();
            e.Handled = true;
        }
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        if (_updatingSelectAll)
        {
            return;
        }
        var side = sender == _sourceSelectAll ? TransferBoxSide.Source : TransferBoxSide.Target;
        var list = ListOf(side);
        if (list is null)
        {
            return;
        }
        var count = ViewOf(side).Items.Count;
        if (count > 0 && list.Selection.SelectedIndexes.Count >= count)
        {
            list.UnselectAll();
        }
        else
        {
            SelectAll(side);
        }
        UpdateState();
    }

    // ---------------------------------------------------------------- state

    private void UpdateState()
    {
        SourceSelectionCount = SelectedRows(TransferBoxSide.Source).Count;
        TargetSelectionCount = SelectedRows(TransferBoxSide.Target).Count;
        var writable = IsEnabled && CanWrite();
        CanMoveToTarget = writable && SourceSelectionCount > 0;
        CanMoveToSource = writable && TargetSelectionCount > 0;
        _moveToTarget.RaiseCanExecuteChanged();
        _moveToSource.RaiseCanExecuteChanged();
        _moveAllToTarget.RaiseCanExecuteChanged();
        _moveAllToSource.RaiseCanExecuteChanged();

        UpdateSide(_sourceView, SourceSelectionCount, _sourceSelectAll, _sourceCount, _sourceEmpty, SourceEmptyContent);
        UpdateSide(_targetView, TargetSelectionCount, _targetSelectAll, _targetCount, _targetEmpty, TargetEmptyContent);
    }

    private void UpdateSide(TransferBoxView view, int selected, Checkbox? selectAll, TextBlock? count, ContentPresenter? empty, object? emptyContent)
    {
        var shown = view.Items.Count;
        if (selectAll is not null)
        {
            _updatingSelectAll = true;
            try
            {
                selectAll.IsVisible = IsSelectAllVisible && SelectionMode.HasFlag(SelectionMode.Multiple);
                selectAll.IsEnabled = shown > 0;
                selectAll.IsChecked = selected == 0 ? false : selected >= shown ? true : null;
            }
            finally
            {
                _updatingSelectAll = false;
            }
        }
        if (count is not null)
        {
            count.Text = selected > 0
                ? string.Create(CultureInfo.CurrentCulture, $"{selected}/{shown}")
                : shown.ToString(CultureInfo.CurrentCulture);
        }
        if (empty is not null)
        {
            var isEmpty = shown == 0;
            empty.IsVisible = isEmpty;
            if (isEmpty)
            {
                // A list that has items but shows none is filtered out by its search text.
                empty.Content = view.Source is { Count: > 0 } ? NoMatchesText : emptyContent;
            }
        }
    }

    private void UpdateNames()
    {
        var sourceName = SourceHeader as string ?? "Available";
        var targetName = TargetHeader as string ?? "Selected";
        if (_sourceList is not null)
        {
            AutomationProperties.SetName(_sourceList, sourceName);
        }
        if (_targetList is not null)
        {
            AutomationProperties.SetName(_targetList, targetName);
        }
        if (_sourceSelectAll is not null)
        {
            AutomationProperties.SetName(_sourceSelectAll, $"{SelectAllLabel}: {sourceName}");
        }
        if (_targetSelectAll is not null)
        {
            AutomationProperties.SetName(_targetSelectAll, $"{SelectAllLabel}: {targetName}");
        }
    }
}

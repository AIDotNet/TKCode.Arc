using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A list whose rows reveal actions on a horizontal swipe, the way a mail inbox does (Arc swipe actions): short, touch-first
/// triage lists such as mail, notifications or tasks. Rows are <see cref="SwipeActionsRow"/>s declared inline or generated
/// from <c>ItemsSource</c> (with <see cref="LeadingActions"/> / <see cref="TrailingActions"/> shared by every row that
/// declares none, and <see cref="RowLabelBinding"/> naming each row). Only one row stays open at a time; pressing anywhere
/// else or Escape puts it away. Every row also has a More actions menu with the same actions, so keyboard and screen reader
/// users never need the gesture.
/// <para>
/// Remove the item in <see cref="ActionInvoked"/> (or the action's own handler or command): removing actions run once their
/// row has slid away and collapsed. The surface fades with the last row and hides when the list is empty. Rows are not
/// virtualized; paginate long lists.
/// </para>
/// </summary>
[PseudoClasses(EmptyPseudoClass, AllLeavingPseudoClass)]
public class SwipeActions : ItemsControl
{
    public const string EmptyPseudoClass = ":empty";
    public const string AllLeavingPseudoClass = ":all-leaving";

    #region 公共属性定义

    /// <summary>Accessible name of the list (Arc: required), such as "Inbox".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SwipeActions, string>(nameof(Label), "List");

    /// <summary>Lets a long swipe commit the outermost action of rows that do not decide for themselves (default true).</summary>
    public static readonly StyledProperty<bool> IsFullSwipeEnabledProperty =
        AvaloniaProperty.Register<SwipeActions, bool>(nameof(IsFullSwipeEnabled), true);

    /// <summary>Accessible name of each row's More actions button; {0} is the row's label.</summary>
    public static readonly StyledProperty<string> MoreActionsFormatProperty =
        AvaloniaProperty.Register<SwipeActions, string>(nameof(MoreActionsFormat), SwipeActionsRow.DefaultMoreActionsFormat);

    /// <summary>Binding that names generated rows (their <see cref="SwipeActionsRow.Label"/>), such as the message subject.</summary>
    public static readonly StyledProperty<BindingBase?> RowLabelBindingProperty =
        AvaloniaProperty.Register<SwipeActions, BindingBase?>(nameof(RowLabelBinding));

    /// <summary>Raised (bubbling from the row) after an action ran.</summary>
    public static readonly RoutedEvent<SwipeActionInvokedEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<SwipeActions, SwipeActionInvokedEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool IsFullSwipeEnabled { get => GetValue(IsFullSwipeEnabledProperty); set => SetValue(IsFullSwipeEnabledProperty, value); }

    public string MoreActionsFormat { get => GetValue(MoreActionsFormatProperty); set => SetValue(MoreActionsFormatProperty, value); }

    [AssignBinding]
    public BindingBase? RowLabelBinding { get => GetValue(RowLabelBindingProperty); set => SetValue(RowLabelBindingProperty, value); }

    /// <summary>Leading actions of every row that declares no actions of its own.</summary>
    public AvaloniaList<SwipeAction> LeadingActions { get; } = new();

    /// <summary>Trailing actions of every row that declares no actions of its own.</summary>
    public AvaloniaList<SwipeAction> TrailingActions { get; } = new();

    public event EventHandler<SwipeActionInvokedEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    #endregion

    private SwipeActionsRow? _openRow;
    private bool _settled;

    static SwipeActions()
    {
        // Focus lands on the list when the last row leaves (Arc: tabIndex -1).
        FocusableProperty.OverrideDefaultValue<SwipeActions>(true);
        IsTabStopProperty.OverrideDefaultValue<SwipeActions>(false);
    }

    public SwipeActions()
    {
        LeadingActions.CollectionChanged += (_, _) => OnDefaultsChanged();
        TrailingActions.CollectionChanged += (_, _) => OnDefaultsChanged();
    }

    /// <summary>The row that is open (or opening), or null.</summary>
    public SwipeActionsRow? OpenRow => _openRow;

    /// <summary>Puts the open row away.</summary>
    public void CloseRows() => _openRow?.Close();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        AutomationProperties.SetName(this, Label);
        UpdateFrame();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Rows present at first render do not animate in; rows added later do.
        _settled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == MoreActionsFormatProperty)
        {
            ForEachRow(row => row.OnActionsChanged());
        }
    }

    private void OnDefaultsChanged() => ForEachRow(row => row.OnActionsChanged());

    private void ForEachRow(Action<SwipeActionsRow> action)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is SwipeActionsRow row)
            {
                action(row);
            }
        }
    }

    #region 容器

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is SwipeActionsRow)
        {
            recycleKey = null;
            return false;
        }
        recycleKey = DefaultRecycleKey;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new SwipeActionsRow();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not SwipeActionsRow row)
        {
            return;
        }
        row.List = this;
        if (!ReferenceEquals(item, row))
        {
            row.IsGenerated = true;
            if (RowLabelBinding is { } binding)
            {
                row.Bind(SwipeActionsRow.LabelProperty, binding);
            }
            else if (string.IsNullOrEmpty(row.Label))
            {
                row.Label = item?.ToString() ?? string.Empty;
            }
        }
        row.OnActionsChanged();
        if (_settled)
        {
            row.PlayEntrance();
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateFrame();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is SwipeActionsRow row)
        {
            if (ReferenceEquals(_openRow, row))
            {
                _openRow = null;
            }
            row.List = null;
            if (row.IsGenerated)
            {
                row.IsGenerated = false;
                row.ClearValue(SwipeActionsRow.LabelProperty);
            }
            // Let go of the list's shared actions (a cleared row that stays in the panel follows its own again).
            row.OnActionsChanged();
        }
        UpdateFrame();
    }

    #endregion

    #region 行协调

    /// <summary>Only one row is open at a time: a row that starts opening puts the previous one away.</summary>
    internal void OnRowOpened(SwipeActionsRow row)
    {
        if (ReferenceEquals(_openRow, row))
        {
            return;
        }
        var previous = _openRow;
        _openRow = row;
        previous?.CloseForOthers();
    }

    internal void OnRowClosed(SwipeActionsRow row)
    {
        if (ReferenceEquals(_openRow, row))
        {
            _openRow = null;
        }
    }

    internal void OnRowLeavingChanged() => UpdateFrame();

    /// <summary>Where focus goes when a row is removed from its menu: the next staying row's More button, else the previous one's, else the list.</summary>
    internal IInputElement? NeighbourFocusTarget(SwipeActionsRow row)
    {
        var index = IndexFromContainer(row);
        if (index >= 0)
        {
            for (var i = index + 1; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is SwipeActionsRow { IsLeaving: false, MoreButton: { IsEffectivelyVisible: true } more })
                {
                    return more;
                }
            }
            for (var i = index - 1; i >= 0; i--)
            {
                if (ContainerFromIndex(i) is SwipeActionsRow { IsLeaving: false, MoreButton: { IsEffectivelyVisible: true } more })
                {
                    return more;
                }
            }
        }
        return this;
    }

    /// <summary>The frame fades once the last row is on its way out and hides when the list is empty.</summary>
    private void UpdateFrame()
    {
        var count = 0;
        var leaving = 0;
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is SwipeActionsRow row)
            {
                count++;
                if (row.IsLeaving)
                {
                    leaving++;
                }
            }
        }
        PseudoClasses.Set(EmptyPseudoClass, ItemCount == 0);
        PseudoClasses.Set(AllLeavingPseudoClass, count > 0 && leaving == count);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new SwipeActionsAutomationPeer(this);

    private sealed class SwipeActionsAutomationPeer : ItemsControlAutomationPeer
    {
        public SwipeActionsAutomationPeer(SwipeActions owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A floating toolbar for an editor's frequently swapped tools and modes, such as select, pan and draw on a canvas. The
/// tool in use sits on a soft accent tint that moves when the choice changes; a small label above the dock names the
/// slot under the mouse or keyboard focus and follows it along; and variants of one tool can share a slot that opens a
/// tray of them above the dock. The whole dock is drawn with a single thin outline. With <see cref="CanReorder"/>,
/// people can rearrange the slots by dragging or with Alt and the arrow keys.
/// </summary>
/// <remarks>
/// Entries are <see cref="ToolDockItem"/> elements written inline (members of a group as their children), or any data
/// through <see cref="ItemsControl.ItemsSource"/> with an <see cref="ItemsControl.ItemContainerTheme"/> that binds the
/// containers' <see cref="ToolDockItem.Value"/>, <see cref="ToolDockItem.Label"/> and <see cref="ToolDockItem.Glyph"/>;
/// an <c>ItemsSource</c> of <see cref="ToolDockItem"/> objects may hold groups. <see cref="SelectedValue"/> is the
/// <see cref="ToolDockItem.Value"/> of the chosen entry or member. Key hints are display only: handle the keys on the
/// page. Keep free space above the dock: the tray and the floating label appear there.
/// </remarks>
[TemplatePart(StagePart, typeof(ToolDockStage))]
[TemplatePart(SurfacePart, typeof(ToolDockSurface))]
[PseudoClasses(OpenPseudoClass)]
public partial class ToolDock : ItemsControl
{
    internal const string StagePart = "PART_Stage";
    internal const string SurfacePart = "PART_Surface";
    internal const string OpenPseudoClass = ":open";

    /// <summary>The <see cref="ToolDockItem.Value"/> of the chosen entry or group member; null chooses nothing.</summary>
    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<ToolDock, object?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Whether people may reorder the slots by dragging them sideways or with Alt and the left and right arrow keys. A
    /// changeable source (the inline items, or an <c>ItemsSource</c> that is an <c>IList</c>) is reordered in place and
    /// <see cref="Reordered"/> reports each move.
    /// </summary>
    public static readonly StyledProperty<bool> CanReorderProperty =
        AvaloniaProperty.Register<ToolDock, bool>(nameof(CanReorder));

    /// <summary>The accessible name of the dock (a toolbar), "Tools" by default.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ToolDock, string>(nameof(Label), "Tools");

    /// <summary>Raised when someone chooses an entry or a group member (by pointer, keyboard or assistive technology).</summary>
    public static readonly RoutedEvent<ToolDockItemEventArgs> ItemChosenEvent =
        RoutedEvent.Register<ToolDock, ToolDockItemEventArgs>(nameof(ItemChosen), RoutingStrategies.Bubble);

    /// <summary>Raised after a slot moved: once for every neighbour it passes while dragged, and on Alt + an arrow key.</summary>
    public static readonly RoutedEvent<ToolDockReorderedEventArgs> ReorderedEvent =
        RoutedEvent.Register<ToolDock, ToolDockReorderedEventArgs>(nameof(Reordered), RoutingStrategies.Bubble);

    private static readonly FuncTemplate<Panel?> s_defaultPanel = new(() => new ToolDockPanel());

    private ToolDockStage? _stage;
    private ToolDockSurface? _surface;
    private ToolDockItem? _selectedSlot;
    private ToolDockItem? _currentSlot;

    static ToolDock()
    {
        ItemsPanelProperty.OverrideDefaultValue<ToolDock>(s_defaultPanel);
        ClipToBoundsProperty.OverrideDefaultValue<ToolDock>(false);
        FocusableProperty.OverrideDefaultValue<ToolDock>(false);
    }

    /// <inheritdoc cref="SelectedValueProperty"/>
    public object? SelectedValue { get => GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }

    /// <inheritdoc cref="CanReorderProperty"/>
    public bool CanReorder { get => GetValue(CanReorderProperty); set => SetValue(CanReorderProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ItemChosenEvent"/>
    public event EventHandler<ToolDockItemEventArgs>? ItemChosen
    {
        add => AddHandler(ItemChosenEvent, value);
        remove => RemoveHandler(ItemChosenEvent, value);
    }

    /// <inheritdoc cref="ReorderedEvent"/>
    public event EventHandler<ToolDockReorderedEventArgs>? Reordered
    {
        add => AddHandler(ReorderedEvent, value);
        remove => RemoveHandler(ReorderedEvent, value);
    }

    /// <summary>The slots, in order (containers of the items).</summary>
    internal IEnumerable<ToolDockItem> Slots
    {
        get
        {
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is ToolDockItem slot)
                {
                    yield return slot;
                }
            }
        }
    }

    /// <summary>The material part (null before the template is applied).</summary>
    internal ToolDockSurface? Surface => _surface;

    /// <summary>The stage (null before the template is applied).</summary>
    internal ToolDockStage? Stage => _stage;

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new ToolDockItem();

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<ToolDockItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is ToolDockItem slot)
        {
            slot.Sync(SelectedValue, animate: false);
            if (slot.IsSelected)
            {
                _selectedSlot = slot;
            }
            AttachDrag(slot);
        }
        UpdateTabStops();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is ToolDockItem slot)
        {
            DetachDrag(slot);
            if (ReferenceEquals(slot, _openGroup))
            {
                CloseTray();
            }
            if (ReferenceEquals(slot, _selectedSlot))
            {
                _selectedSlot = null;
            }
            if (ReferenceEquals(slot, _currentSlot))
            {
                _currentSlot = null;
            }
            ForgetLabelTarget(slot);
        }
        base.ClearContainerForItemOverride(container);
        UpdateTabStops();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<ToolDockStage>(StagePart);
        _surface = e.NameScope.Find<ToolDockSurface>(SurfacePart);
        _tray = null;
        _label = null;
        _openGroup?.SetTrayOpen(false);
        _openGroup = null;
        _tab = null;
        PseudoClasses.Set(OpenPseudoClass, false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedValueProperty)
        {
            SyncSelection(animate: true);
        }
        else if (change.Property == CanReorderProperty)
        {
            foreach (var slot in Slots)
            {
                DetachDrag(slot);
                AttachDrag(slot);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeOutside();
        StopLabelTimers();
    }

    // ---- Selection -----------------------------------------------------------------------------------------------

    /// <summary>Brings every slot in line with <see cref="SelectedValue"/> and slides the tint to the new slot.</summary>
    private void SyncSelection(bool animate)
    {
        var old = _selectedSlot;
        var oldTintX = old is not null && old.IsVisible ? old.Bounds.X + old.SlotOffset + old.TintOffset : double.NaN;
        var velocity = old?.TintVelocity ?? 0;
        ToolDockItem? now = null;
        foreach (var slot in Slots)
        {
            slot.Sync(SelectedValue, animate);
            if (slot.IsSelected && now is null)
            {
                now = slot;
            }
        }
        _selectedSlot = now;
        if (now is not null && old is not null && !ReferenceEquals(now, old) && !double.IsNaN(oldTintX) && animate)
        {
            now.GlideTintFrom(oldTintX - (now.Bounds.X + now.SlotOffset), velocity);
        }
        if (_tray is { } tray)
        {
            foreach (var button in tray.Buttons)
            {
                button.SetSelected(SelectedValue is not null && Equals(button.Member.Value, SelectedValue));
            }
        }
        if (_currentSlot is null || !_currentSlot.IsKeyboardFocusWithin)
        {
            _currentSlot = null;
        }
        UpdateTabStops();
    }

    /// <summary>An entry's value or members changed: the selection and an open tray follow.</summary>
    internal void OnItemsStateChanged()
    {
        SyncSelection(animate: false);
        if (_openGroup is { } group && _tray is { } tray)
        {
            if (!group.IsGroup)
            {
                CloseTray();
            }
            else if (!tray.Buttons.Select(b => b.Member).SequenceEqual(group.Items))
            {
                BuildTrayButtons(tray, group, animate: false);
                PlaceTray(group, tray);
            }
        }
    }

    /// <summary>A member's glyph or text changed: its tray button follows.</summary>
    internal void OnMemberChanged(ToolDockItem group, ToolDockItem member)
    {
        if (_tray is { } tray && ReferenceEquals(tray.Group, group))
        {
            foreach (var button in tray.Buttons)
            {
                if (ReferenceEquals(button.Member, member))
                {
                    button.Glyph = member.Glyph;
                }
            }
        }
    }

    /// <summary>Acts on a press of <paramref name="item"/>: a single tool becomes the value; a group makes its visible member the value and toggles its tray.</summary>
    internal void Choose(ToolDockItem item, bool keyboard)
    {
        if (!item.IsEffectivelyEnabled)
        {
            return;
        }
        if (item.IsGroup)
        {
            var wasOpen = item.IsTrayOpen;
            if (item.ShownItem is { } shown)
            {
                Select(shown, item);
            }
            if (wasOpen)
            {
                CloseTray();
            }
            else
            {
                OpenTray(item, focusMember: false);
            }
            return;
        }
        CloseTray();
        Select(item, null);
    }

    /// <summary>Chooses a member in the open tray: selects it, closes the tray and gives the focus back to the group.</summary>
    internal void ChooseMember(ToolDockItem member, bool keyboard)
    {
        var group = member.Group;
        Select(member, group);
        CloseTray();
        if (group is not null && (keyboard || IsKeyboardFocusWithin))
        {
            group.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void Select(ToolDockItem chosen, ToolDockItem? group)
    {
        group?.Remember(chosen);
        SetCurrentValue(SelectedValueProperty, chosen.Value);
        // The value may not have changed (choosing the selected tool again); the group still shows the chosen member.
        group?.Sync(SelectedValue, animate: true);
        RaiseEvent(new ToolDockItemEventArgs(ItemChosenEvent, chosen));
    }

    // ---- Focus ---------------------------------------------------------------------------------------------------

    /// <summary>Only one slot is a tab stop: the one last focused, else the selected one, else the first.</summary>
    private void UpdateTabStops()
    {
        var current = _currentSlot ?? _selectedSlot ?? Slots.FirstOrDefault(s => s.IsEffectivelyEnabled);
        foreach (var slot in Slots)
        {
            slot.IsTabStop = ReferenceEquals(slot, current);
        }
    }

    private static bool IsKeyboard(NavigationMethod method) => method is NavigationMethod.Tab or NavigationMethod.Directional;

    internal void OnItemFocused(ToolDockItem slot, NavigationMethod method)
    {
        _currentSlot = slot;
        UpdateTabStops();
        if (IsKeyboard(method))
        {
            OnKeyboardTarget(slot);
        }
    }

    internal void OnMemberFocused(ToolDockTrayButton button, NavigationMethod method)
    {
        if (_tray is { } tray)
        {
            foreach (var other in tray.Buttons)
            {
                other.IsTabStop = ReferenceEquals(other, button);
            }
        }
        if (IsKeyboard(method))
        {
            OnKeyboardTarget(button);
        }
    }

    private bool FocusSlot(ToolDockItem? slot)
    {
        if (slot is null)
        {
            return false;
        }
        _currentSlot = slot;
        UpdateTabStops();
        return slot.Focus(NavigationMethod.Directional);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolDockAutomationPeer(this);

    /// <summary>A toolbar named by <see cref="Label"/>.</summary>
    private sealed class ToolDockAutomationPeer(ToolDock owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

        protected override string? GetNameCore()
            => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : ((ToolDock)Owner).Label;
    }
}

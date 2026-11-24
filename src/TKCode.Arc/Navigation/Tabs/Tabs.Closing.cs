using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// Closable tabs, the add button and where the selection goes when the selected tab is removed.
public partial class Tabs
{
    private TabGlyphButton? _addButton;
    private int _removedSelection = -1;
    private IDisposable? _alwaysSelectedHold;
    private NavigationMethod? _refocusAfterRemoval;

    #region Closing

    /// <summary>
    /// Asks to close <paramref name="tab"/>: raises <see cref="TabCloseRequested"/>, then runs <see cref="CloseTabCommand"/>
    /// unless a handler handled the request. When the app removed the tab and it was closed from the keyboard, focus moves to
    /// the tab that is selected now.
    /// </summary>
    internal void RequestClose(TabItem tab, NavigationMethod method)
    {
        var index = IndexFromContainer(tab);
        if (index < 0 || !tab.IsEffectivelyClosable || !tab.IsEffectivelyEnabled)
        {
            return;
        }
        var item = ItemFromContainer(tab) ?? tab;
        var args = new TabCloseRequestedEventArgs(TabCloseRequestedEvent, item, index, tab);
        RaiseEvent(args);
        if (!args.Handled && CloseTabCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
        if (method != NavigationMethod.Pointer && IndexFromContainer(tab) < 0)
        {
            // Removed: keyboard focus goes to the selected tab, once the selection has moved on (FinishRemoval).
            if (_removedSelection >= 0)
            {
                _refocusAfterRemoval = method;
            }
            else
            {
                FocusSelectedTab(method);
            }
        }
    }

    private void FocusSelectedTab(NavigationMethod method)
    {
        if (SelectedIndex >= 0 && ContainerFromIndex(SelectedIndex) is TabItem tab)
        {
            _focusIndex = SelectedIndex;
            UpdateTabStops();
            tab.Focus(method);
        }
    }

    private void ApplyOwnerState(TabItem tab) => tab.ApplyOwnerState(AreTabsClosable, Variant == TabsVariant.Card, CloseTabLabel);

    private void UpdateOwnerStates()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is TabItem tab)
            {
                ApplyOwnerState(tab);
            }
        }
    }

    /// <summary>
    /// Runs before the selection model learns of a change to the items. When the selected tab is being removed, the stock
    /// AlwaysSelected rule would jump to the first tab; it is held back until the model has dropped the selection, and once
    /// the list has caught up (same dispatcher turn, before the next frame) the tab that took the removed one's place is
    /// selected, or the one before it at the end. Disabled and hidden tabs are skipped.
    /// </summary>
    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Remove || e.OldItems is null || _removedSelection >= 0)
        {
            return;
        }
        var selected = SelectedIndex;
        if (selected < e.OldStartingIndex || selected >= e.OldStartingIndex + e.OldItems.Count
            || !SelectionMode.HasFlag(SelectionMode.AlwaysSelected) || ItemCount == 0)
        {
            return;
        }
        _removedSelection = e.OldStartingIndex;
        // Held until FinishRemoval: both the model's lost-selection handler and the items control's own collection
        // handler (which run after this one) would otherwise select the first tab.
        _alwaysSelectedHold = SetValue(SelectionModeProperty, SelectionMode & ~SelectionMode.AlwaysSelected, BindingPriority.Animation);
        // The containers still describe the old list here (they follow the change after the model); choose afterwards.
        Dispatcher.UIThread.Post(FinishRemoval, DispatcherPriority.Send);
    }

    private void FinishRemoval()
    {
        if (_removedSelection < 0)
        {
            return;
        }
        var at = _removedSelection;
        _removedSelection = -1;
        _alwaysSelectedHold?.Dispose();
        _alwaysSelectedHold = null;
        var refocus = _refocusAfterRemoval;
        _refocusAfterRemoval = null;
        if (SelectedIndex < 0 && ItemCount > 0)
        {
            var target = NearestSelectable(Math.Min(at, ItemCount - 1));
            SelectedIndex = target >= 0 ? target : 0;
        }
        if (refocus is { } method)
        {
            FocusSelectedTab(method);
        }
    }

    // The first selectable tab at or after start, else the nearest one before it.
    private int NearestSelectable(int start)
    {
        for (var i = start; i < ItemCount; i++)
        {
            if (IsTabEnabled(i))
            {
                return i;
            }
        }
        for (var i = start - 1; i >= 0; i--)
        {
            if (IsTabEnabled(i))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Add button

    private void UpdateAddButton()
    {
        if (IsAddButtonVisible && _addButton is null && _listRow is not null)
        {
            _addButton = new TabGlyphButton { Name = AddButtonPart, Glyph = ArcIcons.Plus };
            _addButton.Classes.Add(TabGlyphButton.AddClass);
            _addButton.Click += OnAddButtonClick;
            _addButton.Bind(Avalonia.Automation.AutomationProperties.NameProperty, this.GetObservable(AddTabLabelProperty));
            DockPanel.SetDock(_addButton, Dock.Right);
            // Before the list's panel: the row's last child fills, the button docks at its end.
            _listRow.Children.Insert(0, _addButton);
        }
        if (_addButton is not null)
        {
            _addButton.IsVisible = IsAddButtonVisible;
            _addButton.Classes.Set(TabGlyphButton.CardClass, Variant == TabsVariant.Card);
        }
    }

    private void DetachAddButton()
    {
        if (_addButton is null)
        {
            return;
        }
        _addButton.Click -= OnAddButtonClick;
        _listRow?.Children.Remove(_addButton);
        _addButton = null;
    }

    private void OnAddButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RequestAdd();
    }

    /// <summary>Raises <see cref="TabAddRequested"/> and runs <see cref="AddTabCommand"/> unless a handler handled it.</summary>
    private void RequestAdd()
    {
        var args = new RoutedEventArgs(TabAddRequestedEvent, this);
        RaiseEvent(args);
        if (!args.Handled && AddTabCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    #endregion

    // Card variant: the scroll buttons stop above the strip's baseline.
    private void UpdateScrollButtonVariant(Avalonia.Controls.Button? button)
    {
        if (button is null)
        {
            return;
        }
        if (Variant == TabsVariant.Card)
        {
            button.Bind(MarginProperty, button.GetResourceObservable(TabsTokens.Key(TabsTokenKind.CardScrollButtonMargin)), BindingPriority.LocalValue);
        }
        else
        {
            button.ClearValue(MarginProperty);
        }
    }
}

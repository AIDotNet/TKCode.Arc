using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class Ribbon
{
    private ArcPopup? _popup;
    private RibbonPopupSurface? _popupSurface;
    private bool _popupOpen;

    /// <summary>
    /// Whether a collapsed ribbon shows the selected tab's groups in its pop-up. (The pop-up's own IsOpen stays true while it
    /// plays its exit, so the ribbon keeps its own flag.)
    /// </summary>
    internal bool IsPopupOpen => _popupOpen && IsMinimized;

    /// <summary>The pop-up of a collapsed ribbon (null until it first opens).</summary>
    internal ArcPopup? Popup => _popup;

    /// <summary>Collapses the ribbon to its tab row, or pins it open again (double-click on a tab, Ctrl+F1, the end button).</summary>
    internal void ToggleMinimized() => SetCurrentValue(IsMinimizedProperty, !IsMinimized);

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleMinimized();
    }

    private ArcLanguageManager? _languages;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        ApplyText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
        ClosePopup();
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => ApplyText();

    // The built-in text in the current language: the collapse button and the tab list's name.
    private void ApplyText()
    {
        if (_tabStrip is not null)
        {
            AutomationProperties.SetName(_tabStrip, RibbonText.Get(RibbonLangResourceKind.RibbonTabs));
        }
        if (_minimizeButton is not null)
        {
            var text = RibbonText.Get(IsMinimized ? RibbonLangResourceKind.ExpandRibbon : RibbonLangResourceKind.CollapseRibbon);
            _minimizeButton.Label = text;
            AutomationProperties.SetName(_minimizeButton, text);
        }
    }

    private void UpdateMinimizedState()
    {
        var minimized = IsMinimized;
        PseudoClasses.Set(MinimizedPseudoClass, minimized);
        if (_contentArea is not null)
        {
            _contentArea.IsVisible = !minimized;
        }
        if (!minimized)
        {
            ClosePopup();
        }
        var tab = _shownTab;
        _shownTab = null;
        ShowTab(tab ?? SelectedTab, 0);
        if (_minimizeButton is not null)
        {
            _minimizeButton.Icon = minimized ? ArcIcons.ChevronDown : ArcIcons.ChevronUp;
            _minimizeButton.Shortcut = "Ctrl+F1";
        }
        ApplyText();
        _indicatorPlaced = false;
        InvalidateArrange();
    }

    /// <summary>A click on a tab header: select it, and on a collapsed ribbon open (or close) its pop-up.</summary>
    internal void OnHeaderPressed(RibbonTabHeader header)
    {
        var index = Tabs.IndexOf(header.Tab);
        if (index < 0 || !CanSelect(header.Tab))
        {
            return;
        }
        var same = ReferenceEquals(SelectedTab, header.Tab);
        Select(index);
        header.Focus(NavigationMethod.Pointer);
        if (IsMinimized)
        {
            if (same && IsPopupOpen)
            {
                ClosePopup();
            }
            else
            {
                OpenPopup();
            }
        }
    }

    /// <summary>Shows the selected tab's groups under the tab row of a collapsed ribbon.</summary>
    internal void OpenPopup()
    {
        if (!IsMinimized || _tabRow is null || SelectedTab is null)
        {
            return;
        }
        if (_popup is null)
        {
            _popupSurface = new RibbonPopupSurface();
            _popupSurface.AddHandler(KeyDownEvent, OnPopupKeyDown, RoutingStrategies.Tunnel);
            _popup = new ArcPopup
            {
                Child = _popupSurface,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                EnterOffset = 4,
                MarginToAnchor = 2,
                IsLightDismissEnabled = true
            };
            _popup.Closed += OnPopupClosed;
        }
        _popupSurface!.Width = Math.Max(0, Bounds.Width);
        _popup.PlacementTarget = _tabRow;
        if (_popup.Parent is null)
        {
            ((ISetLogicalParent)_popup).SetParent(this);
        }
        _popupOpen = true;
        _popup.IsOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        var tab = _shownTab;
        _shownTab = null;
        ShowTab(tab ?? SelectedTab, 0);
        _indicatorPlaced = false;
        InvalidateArrange();
    }

    /// <summary>Closes the pop-up of a collapsed ribbon.</summary>
    internal void ClosePopup()
    {
        if (!_popupOpen)
        {
            return;
        }
        _popupOpen = false;
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        AfterPopupClosed();
    }

    // Light dismiss closes the pop-up without ClosePopup.
    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_popupOpen)
        {
            _popupOpen = false;
            AfterPopupClosed();
        }
        if (!_popupOpen)
        {
            // The tab stayed in the pop-up while it faded out.
            _popupSurface?.Slot.SetChild(null);
        }
        if (_popup is { IsOpen: false, Parent: not null })
        {
            ((ISetLogicalParent)_popup).SetParent(null);
        }
    }

    private void AfterPopupClosed()
    {
        PseudoClasses.Set(OpenPseudoClass, false);
        if (SelectedTab is { } selected)
        {
            foreach (var group in selected.Groups)
            {
                group.CloseDropDown();
            }
        }
        var tab = _shownTab;
        _shownTab = null;
        ShowTab(tab ?? SelectedTab, 0);
        _indicatorPlaced = false;
        InvalidateArrange();
    }

    private void OnPopupKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ClosePopup();
            HeaderOf(SelectedTab)?.Focus(NavigationMethod.Tab);
        }
    }
}

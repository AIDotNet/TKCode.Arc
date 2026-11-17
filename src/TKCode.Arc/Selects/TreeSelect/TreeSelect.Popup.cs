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

public partial class TreeSelect
{
    #region Popup

    private double ScrollHeightLimit() => Math.Max(0, MaxDropDownHeight - 12);

    /// <summary>Builds the menu on the first open: surface, scroller, rows panel and the empty search line.</summary>
    private void EnsureMenu()
    {
        if (_panel is not null || _popup is null)
        {
            return;
        }
        _panel = new TreeSelectRowsPanel { Name = RowsPart, Owner = this };
        _scroll = new ScrollViewer
        {
            Name = "PART_Scroll",
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = ScrollHeightLimit(),
            Content = _panel
        };
        var empty = new TextBlock { TextWrapping = TextWrapping.Wrap }
            .Token(TextBlock.PaddingProperty, TreeSelectTokens.Key(TreeSelectTokenKind.EmptyPadding))
            .Token(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        empty.Bind(TextBlock.TextProperty, empty.GetResourceObservable(TreeSelectLangResources.Key(TreeSelectLangResourceKind.NoMatches)));
        _emptyHost = new PresenceHost { Name = "PART_EmptyHost", Mode = PresenceMode.Rise, Offset = 4, IsPresent = false, Child = empty };
        DockPanel.SetDock(_emptyHost, Dock.Top);
        _surface = SelectMenuParts.Surface(new DockPanel { Children = { _emptyHost, _scroll } })
            .Token(Border.PaddingProperty, TreeSelectTokens.Key(TreeSelectTokenKind.MenuPadding))
            .Token(Border.CornerRadiusProperty, TreeSelectTokens.Key(TreeSelectTokenKind.MenuCornerRadius));
        _popup.Child = _surface;
        UpdatePalette();
    }

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
        EnsureMenu();
        if (_popup.IsOpen)
        {
            return;
        }
        if (_query.Length == 0)
        {
            ExpandToSelection();
        }
        RebuildRows(animate: false);
        UpdateEmpty();
        UpdateSurfaceWidth();
        var active = DefaultActive();
        if (_query.Length > 0)
        {
            Refilter();
            active = _activeKey;
        }
        _popup.IsOpen = true;
        SetActive(active, animate: false, scroll: false);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen)
            {
                return;
            }
            if (_activeKey is { } key)
            {
                _panel?.SetHighlight(key, animate: false);
                ScrollToRow(key);
            }
            if (IsSearchable && _search is { IsVisible: true } search)
            {
                search.Focus(FocusModality.For(this) == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
                search.CaretIndex = search.Text?.Length ?? 0;
            }
        }, DispatcherPriority.Loaded);
    }

    private void ClosePopup()
    {
        if (_search?.IsKeyboardFocusWithin == true)
        {
            // Back to the field before the input hides, with the ring only after keyboard use.
            Focus(FocusModality.For(this));
        }
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        _activeKey = null;
        _panel?.SetHighlight(null, animate: false);
        if (!string.IsNullOrEmpty(SearchText))
        {
            SetCurrentValue(SearchTextProperty, null);
        }
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
        if (_surface is null)
        {
            return;
        }
        var min = this.TryFindResource(TreeSelectTokens.Key(TreeSelectTokenKind.PopupMinWidth), out var value) && value is double d ? d : 240;
        var trigger = _trigger?.Bounds.Width ?? 0;
        _surface.Width = Math.Max(min, trigger);
    }

    #endregion
}

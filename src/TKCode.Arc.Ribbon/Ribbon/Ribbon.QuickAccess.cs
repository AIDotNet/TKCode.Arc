using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class Ribbon
{
    private QuickAccessToolBar? _quickAccess;
    private MenuSurface? _contextMenu;
    private MenuEntry? _contextEntry;
    private RibbonButton? _contextTarget;
    private bool _catalogPending;

    /// <summary>The quick access toolbar linked to this ribbon (set through <see cref="QuickAccessToolBar.Ribbon"/>).</summary>
    internal QuickAccessToolBar? QuickAccess
    {
        get => _quickAccess;
        set => _quickAccess = value;
    }

    /// <summary>The context menu of a command (tests).</summary>
    internal MenuSurface? CommandContextMenu => _contextMenu;

    /// <summary>Raises <see cref="ItemInvoked"/> for <paramref name="item"/> and closes the ribbon's pop-ups.</summary>
    internal void RaiseItemInvoked(Control item, MenuEntry? entry)
    {
        CloseTransientPopups();
        RaiseEvent(new RibbonItemInvokedEventArgs(ItemInvokedEvent, item, entry));
    }

    private void CloseTransientPopups()
    {
        if (SelectedTab is { } tab)
        {
            foreach (var group in tab.Groups)
            {
                group.CloseDropDown();
            }
        }
        ClosePopup();
    }

    /// <summary>Every command of the ribbon that has a <see cref="RibbonButton.QuickAccessKey"/>, in tab, group and item order.</summary>
    internal IEnumerable<RibbonButton> QuickAccessCandidates()
    {
        foreach (var tab in Tabs)
        {
            foreach (var group in tab.Groups)
            {
                foreach (var item in group.Items)
                {
                    if (item is RibbonButton { QuickAccessKey: { Length: > 0 } } button)
                    {
                        yield return button;
                    }
                }
            }
        }
    }

    /// <summary>The command with <paramref name="key"/> as its <see cref="RibbonButton.QuickAccessKey"/>, or null.</summary>
    internal RibbonButton? FindQuickAccessItem(string key)
    {
        foreach (var button in QuickAccessCandidates())
        {
            if (string.Equals(button.QuickAccessKey, key, StringComparison.Ordinal))
            {
                return button;
            }
        }
        return null;
    }

    /// <summary>Tabs, groups, commands or keys changed: the linked toolbar finds its commands again (once per batch).</summary>
    internal void OnQuickAccessCatalogChanged()
    {
        if (_quickAccess is null || _catalogPending)
        {
            return;
        }
        _catalogPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _catalogPending = false;
            _quickAccess?.Rebuild();
        }, DispatcherPriority.Background);
    }

    // Right-click on a command offers to add it to the linked quick access toolbar, or remove it.
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            return;
        }
        var button = source as RibbonButton ?? source.FindAncestorOfType<RibbonButton>(includeSelf: true);
        if (button is not null && e.TryGetPosition(button, out var point))
        {
            e.Handled = OpenCommandMenu(button, point);
        }
        else if (button is not null)
        {
            e.Handled = OpenCommandMenu(button, null);
        }
    }

    /// <summary>
    /// Opens the add / remove menu of <paramref name="button"/> at <paramref name="point"/> (or against the button, from the
    /// keyboard). False when the button cannot be pinned or no toolbar is linked.
    /// </summary>
    internal bool OpenCommandMenu(RibbonButton button, Point? point)
    {
        if (_quickAccess is null || button is RibbonChromeButton or RibbonGroupCollapsedButton or QuickAccessProxyButton
            || string.IsNullOrEmpty(button.QuickAccessKey))
        {
            return false;
        }
        EnsureContextMenu();
        _contextTarget = button;
        var pinned = _quickAccess.Keys.Contains(button.QuickAccessKey!);
        _contextEntry!.Header = RibbonText.Get(pinned
            ? RibbonLangResourceKind.RemoveFromQuickAccess
            : RibbonLangResourceKind.AddToQuickAccess);
        _contextEntry.Icon = pinned ? ArcIcons.Trash2 : ArcIcons.Pin;
        if (point is { } at)
        {
            _contextMenu!.OpenAt(button, at);
        }
        else
        {
            _contextMenu!.Open(button, MenuOpenFocus.First);
        }
        return true;
    }

    private void EnsureContextMenu()
    {
        if (_contextMenu is not null)
        {
            return;
        }
        _contextEntry = new MenuEntry();
        _contextMenu = new MenuSurface { Variant = MenuSurfaceVariant.Context };
        _contextMenu.Items.Add(_contextEntry);
        _contextMenu.ItemClicked += (_, _) =>
        {
            if (_contextTarget?.QuickAccessKey is { } key && _quickAccess is { } toolbar)
            {
                toolbar.TogglePinned(key);
            }
            _contextTarget = null;
        };
    }
}

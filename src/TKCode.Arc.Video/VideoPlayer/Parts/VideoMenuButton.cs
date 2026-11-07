using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A control bar button that opens a menu above itself (the speed and captions menus, spec section 5): the library's
/// <see cref="MenuSurface"/> (Dropdown variant, end-aligned, opening upwards). The rows are rebuilt from
/// <see cref="BuildItems"/> on every open so they always show the current state. A pointer click opens it with no
/// highlighted row, Enter / Space / ArrowUp with the first row highlighted.
/// </summary>
internal sealed class VideoMenuButton : VideoButton
{
    private MenuSurface? _menu;
    private bool _keyboardOpen;

    /// <summary>Builds the rows each time the menu opens.</summary>
    public Func<IEnumerable<Control>>? BuildItems { get; set; }

    /// <summary>The menu, once it has been opened.</summary>
    public MenuSurface? Menu => _menu;

    /// <summary>Whether the menu is open.</summary>
    public bool IsMenuOpen => _menu?.IsOpen == true;

    /// <summary>Raised when the menu opens (true) or closes (false).</summary>
    public event Action<bool>? MenuOpenChanged;

    /// <summary>Opens the menu (or closes it when open).</summary>
    public void ToggleMenu(MenuOpenFocus focus)
    {
        if (IsMenuOpen)
        {
            _menu!.Close(returnFocus: true);
            return;
        }
        var menu = EnsureMenu();
        menu.Items.Clear();
        if (BuildItems is { } build)
        {
            foreach (var item in build())
            {
                menu.Items.Add(item);
            }
        }
        menu.Open(this, focus);
    }

    /// <summary>Closes the menu without moving focus.</summary>
    public void CloseMenu() => _menu?.Close(returnFocus: false);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _keyboardOpen = e.Key is Key.Enter or Key.Space;
        if (e.Key == Key.Up && e.KeyModifiers == KeyModifiers.None)
        {
            ToggleMenu(MenuOpenFocus.First);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnClick()
    {
        base.OnClick();
        ToggleMenu(_keyboardOpen ? MenuOpenFocus.First : MenuOpenFocus.Surface);
        _keyboardOpen = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MenuButtonPeer(this);

    private MenuSurface EnsureMenu()
    {
        if (_menu is not null)
        {
            return _menu;
        }
        _menu = new MenuSurface
        {
            Variant = MenuSurfaceVariant.Dropdown,
            Side = MenuSide.Top,
            Alignment = MenuAlignment.End,
            AnchorGap = 8,
        };
        _menu.Opened += (_, _) => MenuOpenChanged?.Invoke(true);
        _menu.Closed += (_, _) => MenuOpenChanged?.Invoke(false);
        return _menu;
    }

    private sealed class MenuButtonPeer(VideoMenuButton owner) : ButtonAutomationPeer(owner), Avalonia.Automation.Provider.IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => owner.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Collapse() => owner.CloseMenu();

        public void Expand()
        {
            if (!owner.IsMenuOpen)
            {
                owner.ToggleMenu(MenuOpenFocus.First);
            }
        }
    }
}

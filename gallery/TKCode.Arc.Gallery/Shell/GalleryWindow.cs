using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The desktop window around a <see cref="GalleryShell"/>; the browser host shows the shell as its single view. It is an
/// <see cref="ArcWindow"/>: an Arc <see cref="Arc.TitleBar"/> with the logo, the window title and the keep-on-top pin sits
/// above the shell, with minimize, maximize and close drawn on Windows and Linux and the traffic lights kept on macOS.
/// </summary>
public sealed class GalleryWindow : ArcWindow
{
    private readonly GalleryShell _shell = new();
    private IDisposable? _pinLabel;

    /// <summary>A window for this system: caption buttons drawn on Windows and Linux, the traffic lights kept on macOS.</summary>
    public GalleryWindow()
        : this(!OperatingSystem.IsMacOS())
    {
    }

    /// <param name="drawsCaptionButtons">
    /// Whether the title bar draws minimize, maximize and close (<see cref="TitleBarCaptionButtons.Drawn"/>); otherwise it
    /// leaves room for the system's (<see cref="TitleBarCaptionButtons.System"/>).
    /// </param>
    public GalleryWindow(bool drawsCaptionButtons)
    {
        Title = "TKCode Gallery";
        Icon = GalleryBrand.CreateWindowIcon();
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 600;

        PinButton = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            IsPressScaleEnabled = false,
            Padding = new Thickness(0),
            Width = 30,
            Height = 30,
            MinWidth = 30,
            MinHeight = 30,
            Focusable = false
        };
        PinButton.Click += (_, _) => Topmost = !Topmost;
        TitleBar = new TitleBar
        {
            Icon = new Image { Source = GalleryBrand.CreateLogo(), Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center },
            RightContent = PinButton,
            CaptionButtons = drawsCaptionButtons ? TitleBarCaptionButtons.Drawn : TitleBarCaptionButtons.System
        };
        SyncPin();
        Content = _shell;
    }

    public GalleryTheme Appearance => _shell.Appearance;

    public GallerySidebar Sidebar => _shell.Sidebar;

    public GalleryTopBar TopBar => _shell.TopBar;

    public GalleryShell Shell => _shell;

    /// <summary>Keeps the window above other windows (<see cref="Window.Topmost"/>) or lets it go; in the title bar's right area.</summary>
    public Button PinButton { get; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && GalleryShell.IsSearchShortcut(e))
        {
            _shell.FocusSearch();
            e.Handled = true;
        }
    }

    /// <summary>Opens <paramref name="entry"/>'s page and reveals it in the sidebar.</summary>
    public void Select(GalleryEntry entry) => _shell.Select(entry);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TopmostProperty)
        {
            SyncPin();
        }
    }

    /// <summary>A pinned window shows the crossed-out pin on a secondary (filled) button; the tooltip says what a press does next.</summary>
    private void SyncPin()
    {
        var button = PinButton;
        if (button is null)
        {
            return;
        }
        var pinned = Topmost;
        button.Content = new ArcGlyph { Data = pinned ? GalleryIcons.PinOff : GalleryIcons.Pin, Size = 15 };
        button.Variant = pinned ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        _pinLabel?.Dispose();
        var text = pinned ? GalleryShellLangResourceKind.WindowUnpin : GalleryShellLangResourceKind.WindowPin;
        var tip = GalleryText.Bind(button, Tooltip.TipProperty, text);
        var name = GalleryText.Bind(button, Avalonia.Automation.AutomationProperties.NameProperty, text);
        _pinLabel = new Labels(tip, name);
    }

    private sealed class Labels(IDisposable tip, IDisposable name) : IDisposable
    {
        public void Dispose()
        {
            tip.Dispose();
            name.Dispose();
        }
    }
}

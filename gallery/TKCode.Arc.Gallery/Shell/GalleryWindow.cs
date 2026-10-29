using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The desktop window around a <see cref="GalleryShell"/>; the browser host shows the shell as its single view. The client
/// area extends into the decorations and the window draws its own <see cref="GalleryTitleBar"/> above the shell (on macOS
/// the system's traffic lights stay in it). Its template hosts bar and shell in a named visual layer manager, so popups,
/// toasts, tooltips and focus adorners have their overlay and adorner layers, and its text defaults follow ArcDesign.
/// </summary>
public sealed class GalleryWindow : Window
{
    private readonly GalleryShell _shell = new();
    private Control? _frame;

    /// <summary>A window for this system: caption buttons drawn on Windows and Linux, the traffic lights kept on macOS.</summary>
    public GalleryWindow()
        : this(!OperatingSystem.IsMacOS())
    {
    }

    /// <param name="drawsCaptionButtons">Whether the title bar draws minimize, maximize and close (see <see cref="GalleryTitleBar"/>).</param>
    public GalleryWindow(bool drawsCaptionButtons)
    {
        Title = "TKCode.Arc Gallery";
        Icon = GalleryBrand.CreateWindowIcon();
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 600;
        TitleBar = new GalleryTitleBar(this, drawsCaptionButtons);
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = GalleryTitleBar.BarHeight;
        // Where the system leaves the decorations to the app (Windows, Linux), Avalonia draws a title bar from the
        // decorations theme; an empty theme draws nothing, so the gallery's bar is the only one. Resize grips stay.
        WindowDecorationsTheme = new ControlTheme(typeof(WindowDrawnDecorations));
        Template = new FuncControlTemplate<GalleryWindow>(BuildTemplate);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(this, FontSizeProperty, ArcDesignTokenKind.TypeSM);
        Content = _shell;
    }

    public GalleryTheme Appearance => _shell.Appearance;

    public GallerySidebar Sidebar => _shell.Sidebar;

    public GalleryTopBar TopBar => _shell.TopBar;

    /// <summary>The window's own title bar: logo, title, the keep-on-top pin and (not on macOS) the caption buttons.</summary>
    public GalleryTitleBar TitleBar { get; }

    public GalleryShell Shell => _shell;

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
        if (change.Property == WindowStateProperty)
        {
            // Windows keeps the frame of a maximized window off screen; the next layout knows the margin.
            Dispatcher.UIThread.Post(SyncOffScreenMargin);
        }
    }

    private void SyncOffScreenMargin()
    {
        if (_frame is not null)
        {
            _frame.Margin = WindowState == WindowState.Maximized ? OffScreenMargin : default;
        }
    }

    /// <summary>
    /// Background, then the title bar over the content inside a visual layer manager. Avalonia 12 gives the manager named
    /// <c>PART_VisualLayerManager</c> its overlay layer, which hosts Arc's popups, tooltips and toasts.
    /// </summary>
    private static Control BuildTemplate(GalleryWindow window, INameScope scope)
    {
        var presenter = new ContentPresenter();
        presenter.Bind(ContentPresenter.ContentProperty, window.GetObservable(ContentProperty));
        presenter.Bind(ContentPresenter.ContentTemplateProperty, window.GetObservable(ContentTemplateProperty));
        presenter.Bind(Layoutable.MarginProperty, window.GetObservable(PaddingProperty));
        scope.Register("PART_ContentPresenter", presenter);

        var frame = new DockPanel();
        DockPanel.SetDock(window.TitleBar, Dock.Top);
        frame.Children.Add(window.TitleBar);
        frame.Children.Add(presenter);

        var layers = new VisualLayerManager { Child = frame };
        scope.Register("PART_VisualLayerManager", layers);

        var background = new Border { Child = layers };
        background.Bind(Border.BackgroundProperty, window.GetObservable(BackgroundProperty));
        window._frame = background;
        window.SyncOffScreenMargin();
        return background;
    }
}

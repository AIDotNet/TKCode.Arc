using AtomWindow = AtomUI.Desktop.Controls.Window;
using TKCode.Arc.Gallery.Catalog;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>The desktop window around a <see cref="GalleryShell"/>; the browser host shows the shell as its single view.</summary>
public sealed class GalleryWindow : AtomWindow
{
    private readonly GalleryShell _shell = new();

    public GalleryWindow()
    {
        Title = "TKCode.Arc Gallery";
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 600;
        Content = _shell;
    }

    public GalleryTheme Appearance => _shell.Appearance;

    public GallerySidebar Sidebar => _shell.Sidebar;

    public GalleryTopBar TopBar => _shell.TopBar;

    /// <summary>Opens <paramref name="entry"/>'s page and reveals it in the sidebar.</summary>
    public void Select(GalleryEntry entry) => _shell.Select(entry);
}

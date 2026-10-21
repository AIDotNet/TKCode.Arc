using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

/// <summary>
/// A Docs page (<see cref="GalleryGuides"/>): its Markdown rendered on its own, with no page chrome and no Preview or
/// Code tabs, in the current gallery language and with its screenshots following the theme.
/// </summary>
public static class DocumentPage
{
    /// <summary>Docs pages read at a comfortable line length, like uiarc.dev's documentation column.</summary>
    private const double ReadingWidth = 760;

    public static Control Create(string page)
    {
        var document = new Markdown { Services = GalleryDemoPlugin.Services };
        var scroller = new ScrollViewer
        {
            Padding = new Thickness(40, 32),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                MaxWidth = ReadingWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = document,
            },
        };
        GalleryText.Track(scroller, () =>
        {
            if (GalleryGuides.Get(page, GalleryText.CurrentLanguage) is { } guide && document.Text != guide.Markdown)
            {
                document.Text = guide.Markdown;
            }
        });
        return scroller;
    }
}

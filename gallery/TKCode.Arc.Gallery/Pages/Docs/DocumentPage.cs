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
        // The language whose guide is shown or on its way: attaching again does not fetch it again.
        string? shown = null;
        GalleryText.Track(scroller, () =>
        {
            var language = GalleryText.CurrentLanguage;
            if (language != shown)
            {
                shown = language;
                _ = ShowAsync(language);
            }
        });
        return scroller;

        // Shows the guide in language once its text is read (at once from the gallery's resources, later when the
        // WebAssembly host fetches it). A guide that arrives after the language changed again is dropped, and one that
        // cannot be read says so and is asked for again on the next refresh.
        async Task ShowAsync(string language)
        {
            var guide = await GalleryGuides.GetAsync(page, language);
            if (language != shown)
            {
                return;
            }
            if (guide is null)
            {
                shown = null;
            }
            var text = guide?.Markdown ?? "> " + GalleryText.Get(GalleryShellLangResourceKind.PageLoadFailed);
            if (document.Text != text)
            {
                document.Text = text;
            }
        }
    }
}

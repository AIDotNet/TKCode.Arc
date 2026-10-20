using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class PaginationPage
{
    public static Control Create()
    {
        var status = GalleryUi.Text("Page 1 of 12", ArcDesignTokenKind.TextMuted, 12);
        var basic = new Pagination { PageCount = 12, CurrentPage = 1 };
        basic.PageChanged += (_, e) => status.Text = $"Page {e.NewPage} of {basic.PageCount}";

        var few = new Pagination { PageCount = 3, CurrentPage = 2 };

        var boundaries = new Pagination { PageCount = 48, CurrentPage = 20, ShowBoundaryPages = true };

        var empty = new Pagination { PageCount = 0 };

        return GalleryUi.Column(20,
            GalleryUi.Card("Pagination", "Five pages slide past like a belt while one mark glides to the current page. Tab between buttons; Enter or Space jumps.",
                GalleryUi.Column(12, basic, status)),
            GalleryUi.Card("Few pages", "Fewer than five pages show them all; the arrows disable at the ends.", few),
            GalleryUi.Card("Boundary pages", "ShowBoundaryPages keeps the first and last page one click away behind an ellipsis (an addition to Arc).", boundaries),
            GalleryUi.Card("No pages", "A page count of 0 renders an empty navigation landmark.", GalleryUi.Row(empty, GalleryUi.Text("(nothing to page)", ArcDesignTokenKind.TextMuted, 12))));
    }
}

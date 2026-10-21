using TKCode.Arc.Gallery.Docs;

namespace TKCode.Arc.Gallery.Pages;

// The Docs section: each page is Guides/<Page>/<language>.md rendered by DocumentPage (see GalleryGuides).
public static partial class GalleryPages
{
    static partial void RegisterDocs()
    {
        foreach (var page in GalleryGuides.Pages)
        {
            Register($"Docs{page}", () => DocumentPage.Create(page));
        }
    }
}

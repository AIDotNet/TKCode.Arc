namespace TKCode.Arc.Gallery.Pages;

// The Docs section: each page is its Markdown rendered by DocumentPage (see DocsMarkdown for the text).
public static partial class GalleryPages
{
    static partial void RegisterDocs()
    {
        Register("DocsIntroduction", () => DocumentPage.Create(DocsMarkdown.Introduction));
        Register("DocsInstallation", () => DocumentPage.Create(DocsMarkdown.Installation));
        Register("DocsTheming", () => DocumentPage.Create(DocsMarkdown.Theming));
        Register("DocsMotion", () => DocumentPage.Create(DocsMarkdown.Motion));
        Register("DocsAiAndMcp", () => DocumentPage.Create(DocsMarkdown.AiAndMcp));
        Register("DocsChangelog", () => DocumentPage.Create(DocsMarkdown.Changelog));
    }
}

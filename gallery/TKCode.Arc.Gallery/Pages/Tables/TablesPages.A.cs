namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterTablesA()
    {
        Register("CodeBlock", CodeBlockPage.Create);
        Register("TreeView", TreeViewPage.Create);
        Register("JsonViewer", JsonViewerPage.Create);
    }
}

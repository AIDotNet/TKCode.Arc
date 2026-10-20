namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterTablesB()
    {
        Register("SortableDataTable", SortableDataTablePage.Create);
        Register("ReorderableList", ReorderableListPage.Create);
        Register("FilterToolbar", FilterToolbarPage.Create);
    }
}

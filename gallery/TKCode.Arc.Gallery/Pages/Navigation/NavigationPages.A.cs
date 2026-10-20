namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterNavigationA()
    {
        Register("Tabs", TabsPage.Create);
        Register("Pagination", PaginationPage.Create);
        Register("Breadcrumb", BreadcrumbPage.Create);
    }
}

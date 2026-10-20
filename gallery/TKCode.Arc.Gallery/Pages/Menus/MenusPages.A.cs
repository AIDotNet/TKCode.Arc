namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterMenusA()
    {
        Register("DropdownMenu", DropdownMenuPage.Create);
        Register("ContextMenu", ContextMenuPage.Create);
        Register("UserMenu", UserMenuPage.Create);
    }
}

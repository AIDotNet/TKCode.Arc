namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterOverlaysA()
    {
        Register("Dialog", DialogPage.Create);
        Register("Drawer", DrawerPage.Create);
        Register("BottomSheet", BottomSheetPage.Create);
        Register("Popover", PopoverPage.Create);
        Register("HoverCard", HoverCardPage.Create);
    }
}

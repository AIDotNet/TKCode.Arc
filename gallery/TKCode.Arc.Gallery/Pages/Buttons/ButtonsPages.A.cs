namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterButtonsA()
    {
        Register("ActionButton", ActionButtonPage.Create);
        Register("FloatingButtonGroup", FloatingButtonGroupPage.Create);
        Register("ExpandingButtonGroup", ExpandingButtonGroupPage.Create);
        Register("IconMorph", IconMorphPage.Create);
        Register("ConfirmMorph", ConfirmMorphPage.Create);
        Register("ThemeSwitcher", ThemeSwitcherPage.Create);
    }
}

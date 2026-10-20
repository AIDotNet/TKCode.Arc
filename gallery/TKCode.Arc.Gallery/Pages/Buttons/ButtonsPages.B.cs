namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterButtonsB()
    {
        Register("SplitButton", SplitButtonPage.Create);
        Register("ButtonGroup", ButtonGroupPage.Create);
    }
}

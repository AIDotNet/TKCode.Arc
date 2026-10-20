namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// ButtonsPages.A.cs / ButtonsPages.B.cs / ButtonsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterButtons()
    {
        Register("Button", ButtonPage.Create);
        Register("CopyButton", CopyButtonPage.Create);
        RegisterButtonsA();
        RegisterButtonsB();
        RegisterButtonsC();
    }

    static partial void RegisterButtonsA();

    static partial void RegisterButtonsB();

    static partial void RegisterButtonsC();
}

namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// MenusPages.A.cs / MenusPages.B.cs / MenusPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterMenus()
    {
        RegisterMenusA();
        RegisterMenusB();
        RegisterMenusC();
    }

    static partial void RegisterMenusA();

    static partial void RegisterMenusB();

    static partial void RegisterMenusC();
}

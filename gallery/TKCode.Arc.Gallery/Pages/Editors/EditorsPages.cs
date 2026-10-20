namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// EditorsPages.A.cs / EditorsPages.B.cs / EditorsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterEditors()
    {
        RegisterEditorsA();
        RegisterEditorsB();
        RegisterEditorsC();
    }

    static partial void RegisterEditorsA();

    static partial void RegisterEditorsB();

    static partial void RegisterEditorsC();
}

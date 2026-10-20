namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// SelectsPages.A.cs / SelectsPages.B.cs / SelectsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterSelects()
    {
        RegisterSelectsA();
        RegisterSelectsB();
        RegisterSelectsC();
    }

    static partial void RegisterSelectsA();

    static partial void RegisterSelectsB();

    static partial void RegisterSelectsC();
}

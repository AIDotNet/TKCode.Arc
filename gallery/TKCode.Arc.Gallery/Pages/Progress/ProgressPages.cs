namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// ProgressPages.A.cs / ProgressPages.B.cs / ProgressPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterProgress()
    {
        RegisterProgressA();
        RegisterProgressB();
        RegisterProgressC();
    }

    static partial void RegisterProgressA();

    static partial void RegisterProgressB();

    static partial void RegisterProgressC();
}

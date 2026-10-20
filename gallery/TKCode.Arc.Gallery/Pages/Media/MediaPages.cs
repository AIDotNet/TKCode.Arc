namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// MediaPages.A.cs / MediaPages.B.cs / MediaPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterMedia()
    {
        RegisterMediaA();
        RegisterMediaB();
        RegisterMediaC();
    }

    static partial void RegisterMediaA();

    static partial void RegisterMediaB();

    static partial void RegisterMediaC();
}

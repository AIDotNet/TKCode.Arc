namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// ActivityPages.A.cs / ActivityPages.B.cs / ActivityPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterActivity()
    {
        RegisterActivityA();
        RegisterActivityB();
        RegisterActivityC();
    }

    static partial void RegisterActivityA();

    static partial void RegisterActivityB();

    static partial void RegisterActivityC();
}

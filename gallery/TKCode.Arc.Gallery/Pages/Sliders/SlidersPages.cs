namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// SlidersPages.A.cs / SlidersPages.B.cs / SlidersPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterSliders()
    {
        RegisterSlidersA();
        RegisterSlidersB();
        RegisterSlidersC();
    }

    static partial void RegisterSlidersA();

    static partial void RegisterSlidersB();

    static partial void RegisterSlidersC();
}

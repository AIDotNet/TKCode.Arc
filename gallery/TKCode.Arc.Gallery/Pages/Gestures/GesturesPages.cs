namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// GesturesPages.A.cs / GesturesPages.B.cs / GesturesPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterGestures()
    {
        RegisterGesturesA();
        RegisterGesturesB();
        RegisterGesturesC();
    }

    static partial void RegisterGesturesA();

    static partial void RegisterGesturesB();

    static partial void RegisterGesturesC();
}

namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// ExpandPages.A.cs / ExpandPages.B.cs / ExpandPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterExpand()
    {
        RegisterExpandA();
        RegisterExpandB();
        RegisterExpandC();
    }

    static partial void RegisterExpandA();

    static partial void RegisterExpandB();

    static partial void RegisterExpandC();
}

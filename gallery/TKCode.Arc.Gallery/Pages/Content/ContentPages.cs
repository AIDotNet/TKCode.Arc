namespace TKCode.Arc.Gallery.Pages;

// Pages of the Content category (TKCode extras with no Arc original, listed under "Extras"). Teams working in
// parallel each own one hook and implement it in ContentPages.A.cs / ContentPages.B.cs / ContentPages.C.cs
// (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterContent()
    {
        RegisterContentA();
        RegisterContentB();
        RegisterContentC();
    }

    static partial void RegisterContentA();

    static partial void RegisterContentB();

    static partial void RegisterContentC();
}

namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// OverlaysPages.A.cs / OverlaysPages.B.cs / OverlaysPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterOverlays()
    {
        Register("Tooltip", TooltipPage.Create);
        RegisterOverlaysA();
        RegisterOverlaysB();
        RegisterOverlaysC();
    }

    static partial void RegisterOverlaysA();

    static partial void RegisterOverlaysB();

    static partial void RegisterOverlaysC();
}

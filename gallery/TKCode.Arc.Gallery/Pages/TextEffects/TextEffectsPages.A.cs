namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterTextEffectsA()
    {
        Register("TextReveal", TextRevealPage.Create);
        Register("InViewTitle", InViewTitlePage.Create);
        Register("TextShimmer", TextShimmerPage.Create);
    }
}

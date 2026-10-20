namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// TextEffectsPages.A.cs / TextEffectsPages.B.cs / TextEffectsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterTextEffects()
    {
        Register("TextMorph", TextMorphPage.Create);
        RegisterTextEffectsA();
        RegisterTextEffectsB();
        RegisterTextEffectsC();
    }

    static partial void RegisterTextEffectsA();

    static partial void RegisterTextEffectsB();

    static partial void RegisterTextEffectsC();
}

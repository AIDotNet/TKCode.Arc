namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// CardsPages.A.cs / CardsPages.B.cs / CardsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterCards()
    {
        RegisterCardsA();
        RegisterCardsB();
        RegisterCardsC();
    }

    static partial void RegisterCardsA();

    static partial void RegisterCardsB();

    static partial void RegisterCardsC();
}

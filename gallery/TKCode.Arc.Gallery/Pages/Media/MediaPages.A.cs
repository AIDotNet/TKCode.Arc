namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterMediaA()
    {
        Register("ImageCompare", ImageComparePage.Create);
        Register("Carousel", CarouselPage.Create);
        Register("CardStack", CardStackPage.Create);
    }
}

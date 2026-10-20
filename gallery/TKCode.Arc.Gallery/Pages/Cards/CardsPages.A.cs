namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterCardsA()
    {
        Register("MetricCard", MetricCardPage.Create);
        Register("EmptyState", EmptyStatePage.Create);
    }
}

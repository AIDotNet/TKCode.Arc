namespace TKCode.Arc.Gallery.Pages;

// Pages of the Illustrations category (TKCode originals; Arc's own Illustrations group is Pro and not reproduced).
public static partial class GalleryPages
{
    static partial void RegisterIllustrations()
    {
        Register("DataFlow", DataFlowPage.Create);
        Register("PaymentFlow", PaymentFlowPage.Create);
        Register("MultiRegionFailover", MultiRegionFailoverPage.Create);
        Register("VectorSearch", VectorSearchPage.Create);
    }
}

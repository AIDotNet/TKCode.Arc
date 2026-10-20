namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterChartsB()
    {
        Register("WaffleChart", WaffleChartPage.Create);
        Register("SlopeChart", SlopeChartPage.Create);
        Register("Gauge", GaugePage.Create);
        Register("ActivityHeatmap", ActivityHeatmapPage.Create);
        Register("Ridgeline", RidgelinePage.Create);
        Register("Treemap", TreemapPage.Create);
    }
}

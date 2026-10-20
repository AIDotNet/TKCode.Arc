namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterChartsA()
    {
        Register("LineChart", LineChartPage.Create);
        Register("BarChart", BarChartPage.Create);
        Register("DonutChart", DonutChartPage.Create);
        Register("Streamgraph", StreamgraphPage.Create);
        Register("BrushChart", BrushChartPage.Create);
        Register("Sparkline", SparklinePage.Create);
    }
}

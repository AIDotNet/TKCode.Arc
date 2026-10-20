namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// ChartsPages.A.cs / ChartsPages.B.cs / ChartsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterCharts()
    {
        Register("AnimatedCounter", AnimatedCounterPage.Create);
        RegisterChartsA();
        RegisterChartsB();
        RegisterChartsC();
    }

    static partial void RegisterChartsA();

    static partial void RegisterChartsB();

    static partial void RegisterChartsC();
}

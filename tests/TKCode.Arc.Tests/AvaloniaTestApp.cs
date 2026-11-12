using TKCode.Arc;
using TKCode.Arc.Mobile;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Localization;

[assembly: AvaloniaTestApplication(typeof(TKCode.Arc.Tests.TestAppBuilder))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TKCode.Arc.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        // The runtime XAML loader reads the xmlns definitions of the assemblies loaded when it first runs; tests that parse Lucide XAML need this one.
        _ = typeof(TKCode.Arc.Icons.Lucide.LucideIcon).Assembly;
        return Build();
    }

    private static AppBuilder Build() =>
        AppBuilder.Configure<TestApplication>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseArc(arc => arc.UseArcControls()
                                    .UseArcBlocks()
                                    .UseArcAdvancedCharts()
                                    .UseArcWorkflow()
                                    .UseArcDock()
                                    .UseArcVideo()
                                    .UseArcPdf()
                                    .UseArcMobile()
                                    .UseArcPixelTheme()
                                    .UseArcCorners()
                                    .UseArcSpecial()
                                    .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]));
}

public class TestApplication : Application
{
}

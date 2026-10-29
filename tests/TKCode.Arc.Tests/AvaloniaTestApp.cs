using TKCode.Arc;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Shell;
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
        // The top bar's GitHub button asks GitHub for the star count; tests have no network and no use for it.
        GalleryGitHub.StarsProvider = static () => Task.FromResult<int?>(null);
        // The runtime XAML loader reads the xmlns definitions of the assemblies loaded when it first runs; the guide tests parse Lucide XAML.
        _ = typeof(TKCode.Arc.Icons.Lucide.LucideIcon).Assembly;
        return Build();
    }

    private static AppBuilder Build() =>
        AppBuilder.Configure<TestApplication>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseArc(arc => arc.UseArcControls()
                                    .UseArcGallery()
                                    .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]));
}

public class TestApplication : Application
{
}

using AtomUI;
using TKCode.Arc;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Shell;
using AtomUI.Desktop.Controls;
using AtomUI.Localization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

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
        return Build();
    }

    private static AppBuilder Build() =>
        AppBuilder.Configure<TestApplication>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseAtomUI(builder => builder.UseDesktopControls()
                                               .UseArc()
                                               .UseArcGallery()
                                               .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]));
}

public partial class TestApplication : Application
{
    public override void RegisterServices()
    {
        base.RegisterServices();
        ApplicationStartup.Attach(this);
    }
}

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Mobile;
using TKCode.Arc.Localization;

[assembly: AvaloniaTestApplication(typeof(TKCode.Arc.Mobile.Tests.TestAppBuilder))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TKCode.Arc.Mobile.Tests;

public static class TestAppBuilder
{
    // Note: no UseArcControls(): the mobile package must stand on Core alone.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseArc(arc => arc.UseArcMobile()
                                    .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS]));
}

public class TestApplication : Application
{
}

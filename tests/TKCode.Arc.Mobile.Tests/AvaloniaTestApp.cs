using AtomUI;
using AtomUI.Localization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Mobile;

[assembly: AvaloniaTestApplication(typeof(TKCode.Arc.Mobile.Tests.TestAppBuilder))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TKCode.Arc.Mobile.Tests;

public static class TestAppBuilder
{
    // Note: no UseDesktopControls() and no UseArc(): the mobile package must stand on Core alone.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseAtomUI(builder => builder.UseArcMobile()
                                               .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS]));
}

public partial class TestApplication : Application
{
    public override void RegisterServices()
    {
        base.RegisterServices();
        ApplicationStartup.Attach(this);
    }
}

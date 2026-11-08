using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using TKCode.Arc;
using Xunit;

// Every test gets a fresh app on the headless platform, and the tests run one after another (Avalonia has one UI thread).
[assembly: AvaloniaTestApplication(typeof(ArcLibrary.Tests.TestApp))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ArcLibrary.Tests;

public static class TestApp
{
    // An app with Arc and the library registered, the way an app that uses the library sets it up.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
                  .UseArc(arc => arc.UseArcLibraryIdentifier()
                                    .WithTheme(t => t.Appearance(ArcAppearance.Light).Accent(ArcAccentPreset.Neutral)))
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

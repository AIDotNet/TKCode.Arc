using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using TKCode.Arc.Foundation.Samples;
using TKCode.Arc.Localization;

[assembly: AvaloniaTestApplication(typeof(TKCode.Arc.Foundation.Tests.TestAppBuilder))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TKCode.Arc.Foundation.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseArc(arc => arc.UseFoundationSamples()
                                    .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW])
                                    .WithTheme(t => t.Appearance(ArcAppearance.Light)));
}

/// <summary>
/// Test host. There is no Fluent or Simple theme (Foundation must work on bare Avalonia), so the window gets a minimal
/// template here; Arc's base theme for built-in controls is phase F2's ArcBaseTheme, not part of the spike.
/// </summary>
public class TestApplication : Application
{
    public override void Initialize()
    {
        var windowTheme = new ControlTheme(typeof(Window));
        windowTheme.Setters.Add(new Setter(TemplatedControl.TemplateProperty,
            new FuncControlTemplate<Window>((window, _) => new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = window[!ContentControl.ContentProperty]
            })));
        Resources[typeof(Window)] = windowTheme;
    }
}

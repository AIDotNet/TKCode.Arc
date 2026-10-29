using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

[assembly: AvaloniaTestApplication(typeof(TKCode.Arc.BaseTheme.Tests.TestAppBuilder))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TKCode.Arc.BaseTheme.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// A bare application: no Fluent or Simple theme and no Arc setup. It gives its windows a minimal theme of its own, as an
/// application with its own Window theme would (the base theme then leaves Window out); the base theme is registered by
/// the tests themselves.
/// </summary>
public partial class TestApplication : Application
{
    public override void Initialize()
    {
        var windowTheme = new ControlTheme(typeof(Window));
        windowTheme.Setters.Add(new Setter(TemplatedControl.TemplateProperty,
            new FuncControlTemplate<Window>((window, _) => new VisualLayerManager
            {
                Child = new ContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    [!ContentPresenter.ContentProperty] = window[!ContentControl.ContentProperty]
                }
            })));
        Resources[typeof(Window)] = windowTheme;
    }
}

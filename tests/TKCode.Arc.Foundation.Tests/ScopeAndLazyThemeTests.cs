using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using TKCode.Arc.Foundation.Samples;
using static TKCode.Arc.Foundation.Tests.TestHelpers;

namespace TKCode.Arc.Foundation.Tests;

public class ScopeTests
{
    private static readonly Color Violet = Color.Parse("#7747FF");
    private static Color DarkAccent(Color seed) => Mix(seed, Colors.White, 0.2);

    [AvaloniaFact]
    public void Dark_subtree_inside_a_light_app()
    {
        var outer = new SampleButton();
        var inner = new SampleButton();
        var innerSwitch = new SampleSwitch();
        var scope = new ThemeVariantScope { Child = new StackPanel { Children = { inner, innerSwitch } } };
        ArcThemeScope.SetAppearance(scope, ArcAppearance.Dark);
        Show(new StackPanel { Children = { outer, scope } });

        BrushColor(outer.Background).ShouldBe(ArcSeedToken.DefaultAccent);
        BrushColor(inner.Background).ShouldBe(DarkAccent(ArcSeedToken.DefaultAccent));
        BrushColor(innerSwitch.Background).ShouldBe(Color.Parse("#383838"));
        scope.ActualThemeVariant.ShouldBe(ThemeVariant.Dark);

        // The scope follows the application's accent, and stays dark.
        ArcTheme.Apply(t => t.Accent(Violet));
        BrushColor(outer.Background).ShouldBe(Violet);
        BrushColor(inner.Background).ShouldBe(DarkAccent(Violet));

        // And it stays dark when the application turns dark and back.
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Light));
        BrushColor(inner.Background).ShouldBe(DarkAccent(Violet));

        ArcThemeScope.SetAppearance(scope, null);
        Dispatcher.UIThread.RunJobs();
        BrushColor(inner.Background).ShouldBe(Violet);
        scope.Resources.MergedDictionaries.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Light_and_dark_side_by_side_on_any_panel()
    {
        var light = new SampleButton();
        var dark = new SampleButton();
        var lightPanel = new Border { Child = light };
        var darkPanel = new Border { Child = dark };
        ArcThemeScope.SetAppearance(lightPanel, ArcAppearance.Light);
        ArcThemeScope.SetAppearance(darkPanel, ArcAppearance.Dark);
        Show(new StackPanel { Children = { lightPanel, darkPanel } });

        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        BrushColor(light.Background).ShouldBe(ArcSeedToken.DefaultAccent);
        BrushColor(dark.Background).ShouldBe(DarkAccent(ArcSeedToken.DefaultAccent));
    }

    [AvaloniaFact]
    public void Fixed_state_scope_ignores_application_changes()
    {
        var inner = new SampleButton();
        var scope = new Border { Child = inner };
        ArcThemeScope.SetState(scope, ArcThemeState.Default.With(t => t.Accent(Violet)));
        Show(scope);

        BrushColor(inner.Background).ShouldBe(Violet);
        ArcTheme.Apply(t => t.Accent(Colors.Orange).Appearance(ArcAppearance.Dark));
        BrushColor(inner.Background).ShouldBe(Violet);
    }
}

public class LazyThemeTests
{
    private const string ButtonAsset = "TKCode.Arc.Foundation.Tests:Samples/SampleButton/Themes/SampleButtonTheme.axaml";
    private const string SwitchAsset = "TKCode.Arc.Foundation.Tests:Samples/SampleSwitch/Themes/SampleSwitchTheme.axaml";
    private const string BadgeAsset = "TKCode.Arc.Foundation.Tests:Samples/SampleBadge/Themes/SampleBadgeTheme.axaml";

    [AvaloniaFact]
    public void Theme_files_load_on_first_use_only()
    {
        var themes = ArcRuntime.Current!.Themes;
        var loads = new List<(string Asset, object? Trigger)>();
        themes.AssetLoaded += (_, e) => loads.Add((e.Asset.Id, e.Trigger));
        themes.AssetCount.ShouldBe(3);
        themes.LoadedCount.ShouldBe(0);
        SampleArcBuilderExtensions.FirstUseCount.ShouldBe(0);

        var window = Show(new SampleButton());

        loads.ShouldBe(new[] { (ButtonAsset, (object?)typeof(SampleButton)) });
        SampleArcBuilderExtensions.FirstUseCount.ShouldBe(1);

        // More buttons, and a theme switch, load nothing more.
        window.Content = new StackPanel { Children = { new SampleButton(), new SampleButton() } };
        Dispatcher.UIThread.RunJobs();
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        loads.Count.ShouldBe(1);

        window.Content = new SampleSwitch();
        Dispatcher.UIThread.RunJobs();
        loads.Select(l => l.Asset).ShouldBe(new[] { ButtonAsset, SwitchAsset });
        themes.IsLoaded(BadgeAsset).ShouldBeFalse();
        SampleArcBuilderExtensions.FirstUseCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Named_keys_load_their_file_and_static_resources_inside_it_resolve()
    {
        var themes = ArcRuntime.Current!.Themes;
        var ghost = Application.Current!.FindResource("SampleGhostButtonTheme").ShouldBeOfType<ControlTheme>();
        themes.IsLoaded(ButtonAsset).ShouldBeTrue();
        ghost.BasedOn.ShouldNotBeNull();
        ghost.BasedOn.TargetType.ShouldBe(typeof(SampleButton));

        var button = new SampleButton { Theme = ghost };
        Show(button);
        BrushColor(button.Background).ShouldBe(Color.Parse("#F1F1F1"));
        button.Padding.ShouldBe(new Thickness(16, 0));
    }

    [AvaloniaFact]
    public void Unrelated_lookups_load_nothing()
    {
        var themes = ArcRuntime.Current!.Themes;
        Show(new StackPanel { Children = { new TextBlock(), new Border() } });
        Application.Current!.TryFindResource("NoSuchKey", out _).ShouldBeFalse();
        themes.LoadedCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Eager_mode_loads_every_file_up_front()
    {
        var provider = new ArcThemeProvider();
        foreach (var asset in TKCode.Arc.Generated.TKCodeArcFoundationTests.ArcGeneratedAssets.Themes)
        {
            provider.Add(asset, null);
        }
        provider.LoadedCount.ShouldBe(0);
        provider.LoadAll();
        provider.LoadedCount.ShouldBe(3);
        provider.TryGetResource(typeof(SampleBadge), null, out var badge).ShouldBeTrue();
        badge.ShouldBeOfType<ControlTheme>();
    }

    [AvaloniaFact]
    public void Duplicate_keys_across_files_are_rejected()
    {
        var provider = new ArcThemeProvider();
        provider.Add(new ArcThemeAsset("a", new object[] { typeof(SampleBadge) }, static () => new ResourceDictionary()), null);
        Should.Throw<InvalidOperationException>(() =>
                provider.Add(new ArcThemeAsset("b", new object[] { typeof(SampleBadge) }, static () => new ResourceDictionary()), null))
              .Message.ShouldContain("defined by both 'a' and 'b'");
    }
}

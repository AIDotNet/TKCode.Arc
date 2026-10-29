using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using TKCode.Arc.Foundation.Samples;
using TKCode.Arc.Foundation.Samples.DesignTokens;
using TKCode.Arc.Motion;
using static TKCode.Arc.Foundation.Tests.TestHelpers;

namespace TKCode.Arc.Foundation.Tests;

public class ThemeRuntimeTests
{
    private static readonly Color Violet = Color.Parse("#7747FF");

    [AvaloniaFact]
    public void Tokens_are_published_under_their_boxed_kinds()
    {
        // A fresh boxing of the kind finds the value: this is what ArcTokens.Bind(target, property, ArcDesignTokenKind.X) does.
        BrushColor(AppResource(SampleDesignTokenKind.Accent) as IBrush).ShouldBe(ArcSeedToken.DefaultAccent);
        AppResource(SampleSwitchTokenKind.TrackWidth).ShouldBe(44d);
        AppResource(SampleButtonTokenKind.InlinePadding).ShouldBe(new Thickness(16, 0));
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(18));
        AppResource(SampleDesignTokenKind.WeightMedium).ShouldBe(FontWeight.Medium);
        AppResource(SampleDesignTokenKind.DurationFast).ShouldBe(TimeSpan.FromMilliseconds(160));
        AppResource(SampleDesignTokenKind.EaseStandard).ShouldBeOfType<SplineEasing>();
        AppResource(SampleDesignTokenKind.Density).ShouldBe(SampleDensity.Comfortable);
        AppResource(SampleDesignTokenKind.MotionMode).ShouldBe("full");
        // Same enum value, different identity: different keys.
        ((object)SampleBadgeTokenKind.DotSize).ShouldNotBe((object)SampleSwitchTokenKind.TrackWidth);
    }

    [AvaloniaFact]
    public void Control_themes_bind_tokens_in_setters_and_templates()
    {
        var button = new SampleButton { Content = "Save" };
        var toggle = new SampleSwitch();
        Show(new StackPanel { Children = { button, toggle } });

        BrushColor(button.Background).ShouldBe(ArcSeedToken.DefaultAccent);
        BrushColor(button.Foreground).ShouldBe(Colors.White);
        button.Padding.ShouldBe(new Thickness(16, 0));
        button.CornerRadius.ShouldBe(new CornerRadius(18));
        button.MinHeight.ShouldBe(44);
        button.FontWeight.ShouldBe(FontWeight.Medium);
        Part<Border>(button, "PART_Frame").BoxShadow.ShouldBe(BoxShadows.Parse("0 2 8 0 #1A000000"));

        toggle.Width.ShouldBe(44);
        toggle.Height.ShouldBe(26);
        Part<Border>(toggle, "PART_Thumb").Width.ShouldBe(22);
        BrushColor(toggle.Background).ShouldBe(Color.Parse("#E3E3E3"));
        toggle.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        BrushColor(toggle.Background).ShouldBe(ArcSeedToken.DefaultAccent);
    }

    [AvaloniaFact]
    public void Light_to_dark_updates_dynamic_resources()
    {
        var button = new SampleButton { Content = "Save" };
        var toggle = new SampleSwitch();
        var surface = new Border();
        SampleArcBuilderExtensions.BindToken(surface, Border.BackgroundProperty, SampleDesignTokenKind.Surface);
        Show(new StackPanel { Children = { button, toggle, surface } });
        ArcThemeChangedEventArgs? changed = null;
        ArcTheme.Manager.StateChanged += (_, e) => changed = e;

        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));

        ArcTheme.Manager.IsDark.ShouldBeTrue();
        Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Dark);
        BrushColor(button.Background).ShouldBe(Mix(ArcSeedToken.DefaultAccent, Colors.White, 0.2));
        BrushColor(button.Foreground).ShouldBe(Color.Parse("#0B0B0B"));
        BrushColor(toggle.Background).ShouldBe(Color.Parse("#383838"));
        BrushColor(surface.Background).ShouldBe(Color.Parse("#1D1D1D"));
        Part<Border>(button, "PART_Frame").BoxShadow.ShouldBe(BoxShadows.Parse("0 2 8 0 #66000000"));
        changed.ShouldNotBeNull();
        changed.WasDark.ShouldBeFalse();
        changed.IsDark.ShouldBeTrue();

        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Light));
        BrushColor(button.Background).ShouldBe(ArcSeedToken.DefaultAccent);
        BrushColor(surface.Background).ShouldBe(Colors.White);
    }

    [AvaloniaFact]
    public void Accent_seed_change_recomputes_derived_tokens()
    {
        var button = new SampleButton();
        Show(button);

        ArcTheme.Apply(t => t.Accent(Violet));
        BrushColor(button.Background).ShouldBe(Violet);
        BrushColor(AppResource(SampleDesignTokenKind.AccentStrong) as IBrush).ShouldBe(Mix(Violet, Colors.Black, 0.15));

        // The accent is kept when only the appearance changes.
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        ArcTheme.State.Accent.ShouldBe(Violet);
        BrushColor(button.Background).ShouldBe(Mix(Violet, Colors.White, 0.2));

        ArcTheme.Apply(t => t.DefaultAccent());
        BrushColor(button.Background).ShouldBe(Mix(ArcSeedToken.DefaultAccent, Colors.White, 0.2));
    }

    [AvaloniaFact]
    public void Publishing_rewrites_only_changed_keys()
    {
        var manager = ArcTheme.Manager;
        var surface = AppResource(SampleDesignTokenKind.Surface);
        var accent = AppResource(SampleDesignTokenKind.Accent);

        manager.Apply(t => t.Accent(Violet));

        // Accent and AccentStrong changed; nothing else did, so those keys keep their instances.
        manager.LastPublishedCount.ShouldBe(2);
        AppResource(SampleDesignTokenKind.Surface).ShouldBeSameAs(surface);
        AppResource(SampleDesignTokenKind.Accent).ShouldNotBeSameAs(accent);

        manager.Apply(manager.State);
        manager.LastPublishedCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Motion_level_reaches_token_calculation()
    {
        ArcTheme.Apply(t => t.MotionLevel(ArcMotionLevel.Reduced));
        AppResource(SampleDesignTokenKind.MotionMode).ShouldBe("reduced");
        ArcTheme.Apply(t => t.MotionLevel(ArcMotionLevel.Off));
        AppResource(SampleDesignTokenKind.MotionMode).ShouldBe("off");
    }

    [AvaloniaFact]
    public async Task ApplyAsync_computes_off_thread_and_publishes_on_ui_thread()
    {
        var button = new SampleButton();
        Show(button);
        var task = ArcTheme.Manager.ApplyAsync(t => t.Appearance(ArcAppearance.Dark));
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
        }
        await task;
        BrushColor(button.Background).ShouldBe(Mix(ArcSeedToken.DefaultAccent, Colors.White, 0.2));
    }

    [AvaloniaFact]
    public void Runtime_registers_packages_in_dependency_order()
    {
        var runtime = ArcRuntime.Current.ShouldNotBeNull();
        runtime.Packages.ShouldBe(new[] { "Samples.Core", "TKCode.Arc.Foundation.Tests" });
        SampleArcBuilderExtensions.CommitOrder.ShouldBe(new[] { "Samples.Core", "TKCode.Arc.Foundation.Tests" });
        runtime.Theme.Descriptors[0].Identity.ShouldBe("SampleDesign");
        runtime.Theme.Descriptors[0].IsGlobal.ShouldBeTrue();
        runtime.Theme.Descriptors.Count.ShouldBe(4);
        Application.Current!.Resources.MergedDictionaries.ShouldContain(runtime.Theme.Resources);
        Application.Current.Resources.MergedDictionaries.ShouldContain(runtime.Themes);
    }

    [AvaloniaFact]
    public void Package_builder_is_idempotent_and_rejects_cycles()
    {
        var builder = new ArcBuilder();
        builder.UsePackage("A", _ => { }).UsePackage("A", _ => throw new InvalidOperationException("defined twice"));
        builder.Packages.Count.ShouldBe(1);

        var cyclic = new ArcBuilder();
        var error = Should.Throw<InvalidOperationException>(() =>
            cyclic.UsePackage("A", a => a.DependsOn(b => b.UsePackage("B", p => p.DependsOn(c => c.UsePackage("A", _ => { }))))));
        error.Message.ShouldContain("A → B → A");
    }
}

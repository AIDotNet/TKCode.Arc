using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Foundation.Samples;
using TKCode.Arc.Foundation.Samples.DesignTokens;
using static TKCode.Arc.Foundation.Tests.TestHelpers;

namespace TKCode.Arc.Foundation.Tests;

public class DensityTests
{
    [AvaloniaFact]
    public void Compact_applies_at_runtime_and_reverts_to_default()
    {
        var button = new SampleButton { Content = "Save" };
        Show(button);

        ArcTheme.State.Density.ShouldBe(ArcDensity.Default);
        button.MinHeight.ShouldBe(44);
        button.CornerRadius.ShouldBe(new CornerRadius(18));
        AppResource(SampleDesignTokenKind.Density).ShouldBe(SampleDensity.Comfortable);

        ArcTheme.Apply(t => t.Compact());
        ArcTheme.State.Density.ShouldBe(ArcDensity.Compact);
        button.MinHeight.ShouldBe(34);
        button.CornerRadius.ShouldBe(new CornerRadius(10));
        AppResource(SampleDesignTokenKind.SpaceInset).ShouldBe(new Thickness(12, 6));
        AppResource(SampleDesignTokenKind.Density).ShouldBe(SampleDensity.Compact);

        // Changing appearance preserves compact density.
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        ArcTheme.State.Density.ShouldBe(ArcDensity.Compact);
        button.MinHeight.ShouldBe(34);

        ArcTheme.Apply(t => t.Density(ArcDensity.Default));
        ArcTheme.State.Density.ShouldBe(ArcDensity.Default);
        button.MinHeight.ShouldBe(44);
        button.CornerRadius.ShouldBe(new CornerRadius(18));
        AppResource(SampleDesignTokenKind.Density).ShouldBe(SampleDensity.Comfortable);
    }

    [AvaloniaFact]
    public void Compact_runs_before_presets_and_explicit_overrides_regardless_of_call_order()
    {
        var square = ArcThemePreset.Create("SquareCorners", p => p.Token("RadiusControl", "0"), slot: "Corners");

        // Compact then Preset
        ArcTheme.Apply(t => t.Compact().Preset(square));
        AppResource(SampleDesignTokenKind.ControlSizeMD).ShouldBe(34d);
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(0));

        // Preset then Compact -> identical result
        ArcTheme.Apply(t => t.Density(ArcDensity.Default).ClearPresets());
        ArcTheme.Apply(t => t.Preset(square).Compact());
        AppResource(SampleDesignTokenKind.ControlSizeMD).ShouldBe(34d);
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(0));

        // Explicit token override wins over both Compact and Preset
        ArcTheme.Apply(t => t.Token("ControlSizeMD", "30").Token("RadiusControl", "6"));
        AppResource(SampleDesignTokenKind.ControlSizeMD).ShouldBe(30d);
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(6));
    }

    [AvaloniaFact]
    public void Density_scope_overrides_only_density_for_a_subtree()
    {
        var outer = new SampleButton { Content = "Outer" };
        var inner = new SampleButton { Content = "Inner" };
        var scope = new Border { Child = inner };
        ArcThemeScope.SetDensity(scope, ArcDensity.Compact);
        Show(new StackPanel { Children = { outer, scope } });

        outer.MinHeight.ShouldBe(44);
        inner.MinHeight.ShouldBe(34);
        inner.CornerRadius.ShouldBe(new CornerRadius(10));

        // Scope follows appearance changes while keeping Compact density.
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        outer.MinHeight.ShouldBe(44);
        inner.MinHeight.ShouldBe(34);

        // Removing the scope density falls back to the application density.
        ArcThemeScope.SetDensity(scope, null);
        Dispatcher.UIThread.RunJobs();
        inner.MinHeight.ShouldBe(44);
        scope.Resources.MergedDictionaries.Count.ShouldBe(0);
    }
}

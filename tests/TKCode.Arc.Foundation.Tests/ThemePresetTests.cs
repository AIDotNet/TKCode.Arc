using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Foundation.Samples.DesignTokens;
using static TKCode.Arc.Foundation.Tests.TestHelpers;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>Theme presets: named override bundles per appearance, applied between the calculation and the app's overrides.</summary>
public class ThemePresetTests
{
    private static readonly ArcThemePreset s_retro = ArcThemePreset.Create("Retro", p => p
        .Token("RadiusControl", "0")
        .Token("SampleSwitch", "TrackWidth", "60")
        .ForLight(l => l.Token("Surface", "#F4F4F4"))
        .ForDark(d => d.Token("Surface", "#1A1C2C").Token("SampleBadge", "DotSize", "12")), slot: "Style");

    private static ArcThemePreset Corners(string name, string radius)
        => ArcThemePreset.Create(name, p => p.Token("RadiusControl", radius).Token("RadiusPill", radius), slot: "Corners");

    [AvaloniaFact]
    public void A_preset_applies_shared_and_per_appearance_overrides()
    {
        ArcTheme.Apply(t => t.Preset(s_retro));

        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(0));
        AppResource(SampleSwitchTokenKind.TrackWidth).ShouldBe(60d);
        BrushColor(AppResource(SampleDesignTokenKind.Surface) as IBrush).ShouldBe(Color.Parse("#F4F4F4"));
        AppResource(SampleBadgeTokenKind.DotSize).ShouldNotBe(12d);

        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));

        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(0));
        BrushColor(AppResource(SampleDesignTokenKind.Surface) as IBrush).ShouldBe(Color.Parse("#1A1C2C"));
        AppResource(SampleBadgeTokenKind.DotSize).ShouldBe(12d);
    }

    [AvaloniaFact]
    public void App_overrides_win_over_presets_and_later_presets_win_over_earlier_ones()
    {
        ArcTheme.Apply(t => t.Preset(s_retro).Preset(Corners("Round", "6")));
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(6));

        ArcTheme.Apply(t => t.Token("RadiusControl", "3"));
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(3));
        AppResource(SampleDesignTokenKind.RadiusPill).ShouldBe(new CornerRadius(6));
    }

    [AvaloniaFact]
    public void A_preset_replaces_the_one_in_its_slot_in_place_and_can_be_removed()
    {
        ArcTheme.Apply(t => t.Preset(Corners("Round", "6")).Preset(s_retro).Preset(Corners("Rounder", "9")));

        ArcTheme.State.Presets.Select(p => p.Name).ShouldBe(new[] { "Rounder", "Retro" });
        // Retro comes after the corner slot, so its RadiusControl wins; RadiusPill only comes from the corners.
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(0));
        AppResource(SampleDesignTokenKind.RadiusPill).ShouldBe(new CornerRadius(9));

        ArcTheme.Apply(t => t.RemovePreset("Style"));
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(9));

        ArcTheme.Apply(t => t.ClearPresets());
        ArcTheme.State.Presets.ShouldBeEmpty();
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(18));
        AppResource(SampleDesignTokenKind.RadiusPill).ShouldBe(new CornerRadius(999));
        AppResource(SampleSwitchTokenKind.TrackWidth).ShouldNotBe(60d);
    }

    [AvaloniaFact]
    public void Unregistered_identities_are_skipped_but_unknown_tokens_fail_the_change()
    {
        var wide = ArcThemePreset.Create("Wide", p => p
            .Token("SampleSwitch", "TrackWidth", "70")
            .Token("PackageNotEnabled", "Width", "1"));
        ArcTheme.Apply(t => t.Preset(wide));
        AppResource(SampleSwitchTokenKind.TrackWidth).ShouldBe(70d);

        var before = ArcTheme.State;
        var broken = ArcThemePreset.Create("Broken", p => p.Token("SampleSwitch", "Nope", "1").Token("RadiusControl", "round"));
        var error = Should.Throw<ArcThemeException>(() => ArcTheme.Apply(t => t.Preset(broken)));
        error.Errors.ShouldBe(new[]
        {
            "Preset 'Broken': 'round' is not a valid corner radius (1 or 4 numbers) for token 'SampleDesign.RadiusControl'.",
            "Preset 'Broken': 'SampleSwitch' has no token named 'Nope'."
        }, ignoreOrder: true);
        ArcTheme.State.ShouldBeSameAs(before);
    }

    [AvaloniaFact]
    public void Include_copies_each_appearance_and_later_writes_win()
    {
        var pixel = ArcThemePreset.Create("Pixel", p => p
            .Token("Surface", "#000000")
            .Include(s_retro)
            .ForDark(d => d.Token("RadiusControl", "2")));

        pixel.LightTokens[ArcThemePreset.GlobalIdentity]["Surface"].ShouldBe("#F4F4F4");
        pixel.LightTokens[ArcThemePreset.GlobalIdentity]["RadiusControl"].ShouldBe("0");
        pixel.DarkTokens[ArcThemePreset.GlobalIdentity]["RadiusControl"].ShouldBe("2");
        pixel.DarkTokens["SampleBadge"]["DotSize"].ShouldBe("12");
        pixel.LightTokens.ContainsKey("SampleBadge").ShouldBeFalse();
        pixel.Slot.ShouldBe("Pixel");
        ArcThemePreset.Create("Empty", _ => { }).IsEmpty.ShouldBeTrue();
        Should.Throw<ArgumentException>(() => ArcThemePreset.Create("", _ => { }));
    }

    [AvaloniaFact]
    public void Packages_register_presets_by_unique_name()
    {
        var builder = new ArcBuilder()
            .UsePackage("Retro.Theme", p => p.AddPresets([s_retro]))
            .UsePackage("Corners.Theme", p => p.AddPresets([Corners("Round", "6"), Corners("Square", "0")]));
        builder.Presets.Keys.Order(StringComparer.Ordinal).ShouldBe(new[] { "Retro", "Round", "Square" });
        builder.Presets["Retro"].ShouldBeSameAs(s_retro);

        // The same instance from two packages is fine; a different preset with a taken name is not.
        builder.UsePackage("Again", p => p.AddPresets([s_retro]));
        var error = Should.Throw<InvalidOperationException>(() =>
            builder.UsePackage("Clash", p => p.AddPresets([Corners("Round", "1")])));
        error.Message.ShouldBe("Theme preset 'Round' is registered by both 'Corners.Theme' and 'Clash'.");

        // The running app's runtime exposes what its packages registered (the samples register none).
        ArcRuntime.Current!.Presets.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Compute_uses_the_presets_of_the_requested_appearance()
    {
        ArcTheme.Apply(t => t.Preset(s_retro));
        var dark = ArcTheme.Manager.Compute(ArcTheme.State, isDark: true);
        BrushColor(dark[SampleDesignTokenKind.Surface] as IBrush).ShouldBe(Color.Parse("#1A1C2C"));
        dark[SampleBadgeTokenKind.DotSize].ShouldBe(12d);
    }
}

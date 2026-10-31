using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using TKCode.Arc.Foundation.Samples;
using TKCode.Arc.Localization;
using static TKCode.Arc.Foundation.Tests.TestHelpers;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// <see cref="ArcRuntime.UsePackages"/>: a package enabled after startup (the package of an assembly a browser app loads on
/// demand) gets its tokens, theme files, text and commit callbacks as if it had been enabled at startup.
/// </summary>
public class LatePackageTests
{
    public LatePackageTests()
    {
        Late.CommitCount = 0;
        Late.FirstUseCount = 0;
    }

    [AvaloniaFact]
    public void A_package_enabled_later_publishes_its_tokens_themes_and_text()
    {
        var runtime = ArcRuntime.Current!;
        AppResource(LateTokenKind.Gap).ShouldBeNull();
        AppResource(LateTextKind.Title).ShouldBeNull();
        var stateChanges = 0;
        runtime.Theme.StateChanged += (_, _) => stateChanges++;

        var enabled = runtime.UsePackages(b => b.UseLate());

        enabled.ShouldBe(["Late"]);
        runtime.Packages[^1].ShouldBe("Late");
        runtime.Theme.Descriptors[^1].Identity.ShouldBe("Late");
        runtime.Presets.ShouldContainKey("Late wide");
        AppResource(LateTokenKind.Gap).ShouldBe(8d);
        AppResource(LateTextKind.Title).ShouldBe("Later");
        stateChanges.ShouldBe(0);
        Late.CommitCount.ShouldBe(1);
        Late.FirstUseCount.ShouldBe(0);

        var control = new LateControl();
        Show(control);
        control.Opacity.ShouldBe(0.5);
        Late.FirstUseCount.ShouldBe(1);

        // The new tokens and text follow later theme and language changes like the ones registered at startup.
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        AppResource(LateTokenKind.Gap).ShouldBe(12d);
        ArcTheme.Apply(t => t.Token("Late", "Gap", "20"));
        AppResource(LateTokenKind.Gap).ShouldBe(20d);
        ArcLanguage.Set(LanguageTags.ZhCN);
        AppResource(LateTextKind.Title).ShouldBe("稍后");
    }

    [AvaloniaFact]
    public void Enabled_packages_and_their_dependencies_are_skipped()
    {
        var runtime = ArcRuntime.Current!;
        SampleArcBuilderExtensions.CommitOrder.Clear();

        // Late depends on the samples, which the application enabled at startup: only Late is new.
        runtime.UsePackages(b => b.UseLate(dependsOnSamples: true)).ShouldBe(["Late"]);
        runtime.UsePackages(b => b.UseLate()).ShouldBeEmpty();
        runtime.UsePackages(b => b.UseFoundationSamples()).ShouldBeEmpty();

        SampleArcBuilderExtensions.CommitOrder.ShouldBeEmpty();
        Late.CommitCount.ShouldBe(1);
        runtime.Packages.Count(p => p == "Late").ShouldBe(1);
    }

    [AvaloniaFact]
    public void Theme_scopes_publish_the_new_tokens_for_their_own_state()
    {
        var dark = new Border();
        ArcThemeScope.SetAppearance(dark, ArcAppearance.Dark);
        var fixedState = new Border();
        // A preset, not an override: an override naming a token identity no package registered is an error.
        var scoped = ArcThemePreset.Create("Scoped gaps", p => p.Token("Late", "Gap", "30"), slot: "Gaps");
        ArcThemeScope.SetState(fixedState, ArcThemeState.Default.With(t => t.Preset(scoped)));
        Show(new StackPanel { Children = { dark, fixedState } });

        ArcRuntime.Current!.UsePackages(b => b.UseLate());

        AppResource(LateTokenKind.Gap).ShouldBe(8d);
        Scoped(dark, LateTokenKind.Gap).ShouldBe(12d);
        Scoped(fixedState, LateTokenKind.Gap).ShouldBe(30d);
    }

    [AvaloniaFact]
    public void A_preset_applied_before_the_package_was_enabled_covers_its_tokens()
    {
        var wide = ArcThemePreset.Create("Wide gaps", p => p.Token("Late", "Gap", "16"), slot: "Gaps");
        ArcTheme.Apply(t => t.Preset(wide));

        ArcRuntime.Current!.UsePackages(b => b.UseLate());

        AppResource(LateTokenKind.Gap).ShouldBe(16d);
    }

    [AvaloniaFact]
    public void A_conflict_or_a_language_gap_changes_nothing()
    {
        var runtime = ArcRuntime.Current!;
        var packages = runtime.Packages.ToArray();
        var descriptors = runtime.Theme.Descriptors.Count;

        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.UseLate(tokens: [new LateDescriptor("SampleButton")])))
              .Message.ShouldBe("Token identity 'SampleButton' is registered by both 'TKCode.Arc.Foundation.Tests' and 'Late'.");
        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.UseLate(themeKey: typeof(SampleButton))))
              .Message.ShouldBe("Theme key 'TKCode.Arc.Foundation.Samples.SampleButton' is defined by both "
                                + "'TKCode.Arc.Foundation.Tests:Samples/SampleButton/Themes/SampleButtonTheme.axaml' and 'Late:Themes/LateTheme.axaml'.");
        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.UseLate(chinese: false)))
              .Message.ShouldBe("""
                  The application's languages are not covered by every enabled package:
                  - Package 'Late' has no 'zh-CN' text for 'Tests.LateTextKind' (add Localization/zh-CN.xlf).
                  """.ReplaceLineEndings("\n"));
        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.UseLate(tokens: [new LateDescriptor("LateGlobal", isGlobal: true)])))
              .Message.ShouldBe("Package 'Late' registers the global token identity 'LateGlobal'; that is only possible at startup.");
        // A preset value the new token rejects: computing the tokens fails before anything is registered.
        var bad = ArcThemePreset.Create("Bad gaps", p => p.Token("Late", "Gap", "wide"), slot: "Gaps");
        ArcTheme.Apply(t => t.Preset(bad));
        Should.Throw<ArcThemeException>(() => runtime.UsePackages(b => b.UseLate()));

        runtime.Packages.ShouldBe(packages);
        runtime.Theme.Descriptors.Count.ShouldBe(descriptors);
        runtime.Themes.Defines(typeof(LateControl)).ShouldBeFalse();
        AppResource(LateTokenKind.Gap).ShouldBeNull();
        AppResource(LateTextKind.Title).ShouldBeNull();
        Late.CommitCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Startup_settings_throw_in_a_running_application()
    {
        var runtime = ArcRuntime.Current!;
        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.WithTheme(t => t.Appearance(ArcAppearance.Dark))))
              .Message.ShouldStartWith("WithTheme configures startup");
        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS])));
        Should.Throw<InvalidOperationException>(() => runtime.UsePackages(b => b.WithThemeLoading(ArcThemeLoading.Eager)));
    }

    private static object? Scoped(StyledElement element, object key)
        => element.TryGetResource(key, element.ActualThemeVariant, out var value) ? value : null;
}

internal enum LateTokenKind
{
    Gap
}

internal enum LateTextKind
{
    Title
}

internal sealed class LateControl : TemplatedControl
{
}

internal sealed class LateToken : AbstractControlDesignToken
{
    public double Gap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Gap = isDarkMode ? 12 : 8;
    }
}

/// <summary>What the generator emits for a token class, written out for one token.</summary>
internal sealed class LateDescriptor(string identity, bool isGlobal = false)
    : ArcTokenDescriptor(identity, isGlobal, ["Gap"], [LateTokenKind.Gap])
{
    public override AbstractControlDesignToken CreateToken() => new LateToken();

    public override void Write(AbstractControlDesignToken token, object?[] values) => values[0] = ((LateToken)token).Gap;

    public override bool TrySetToken(AbstractControlDesignToken token, string name, string value, out string? error)
    {
        if (name != "Gap")
        {
            error = UnknownToken(name);
            return false;
        }
        if (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var gap))
        {
            error = InvalidValue(name, "number", value);
            return false;
        }
        ((LateToken)token).Gap = gap;
        error = null;
        return true;
    }
}

/// <summary>A package with one of everything, the way a package's generated <c>UseXxx()</c> declares it.</summary>
internal static class Late
{
    public static int CommitCount { get; set; }

    public static int FirstUseCount { get; set; }

    public static ArcBuilder UseLate(this ArcBuilder builder, bool dependsOnSamples = false, ArcTokenDescriptor[]? tokens = null,
                                     Type? themeKey = null, bool chinese = true)
    {
        return builder.UsePackage("Late", p =>
        {
            if (dependsOnSamples)
            {
                p.DependsOn(b => b.UseFoundationSamples());
            }
            p.AddTokens(tokens ?? [new LateDescriptor("Late")]);
            p.AddThemes([new ArcThemeAsset("Late:Themes/LateTheme.axaml", [themeKey ?? typeof(LateControl)], () =>
            {
                var theme = new ControlTheme(typeof(LateControl));
                theme.Setters.Add(new Setter(Visual.OpacityProperty, 0.5));
                return new ResourceDictionary { [typeof(LateControl)] = theme };
            })]);
            var tables = new List<ArcLanguageTable> { new(LanguageTags.EnUS, ["Later"]) };
            if (chinese)
            {
                tables.Add(new ArcLanguageTable(LanguageTags.ZhCN, ["稍后"]));
            }
            tables.Add(new ArcLanguageTable(LanguageTags.ZhTW, ["稍後"]));
            p.AddLanguages([new ArcLanguageCatalog("Tests.LateTextKind", LanguageTags.EnUS, [LateTextKind.Title], ["Title"], tables)]);
            p.AddPresets([ArcThemePreset.Create("Late wide", q => q.Token("Late", "Gap", "24"), slot: "Gaps")]);
            p.OnCommit(_ => Late.CommitCount++);
            p.OnFirstUse(_ => Late.FirstUseCount++);
        });
    }
}

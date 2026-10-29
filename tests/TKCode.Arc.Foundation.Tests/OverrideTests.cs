using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Foundation.Samples;
using TKCode.Arc.Foundation.Samples.DesignTokens;
using static TKCode.Arc.Foundation.Tests.TestHelpers;

namespace TKCode.Arc.Foundation.Tests;

public class OverrideTests
{
    [AvaloniaFact]
    public void String_overrides_parse_every_supported_type()
    {
        ArcTheme.Apply(t => t
            .Token("Surface", "#102030")
            .Token("RadiusControl", "12")
            .Token("RadiusPill", "1,2,3,4")
            .Token("ShadowRaised", "0 4 12 0 #33000000, inset 0 1 0 0 #10FFFFFF")
            .Token("SpaceInset", "4,2")
            .Token("ControlSizeMD", "40.5")
            .Token("StaggerCount", "6")
            .Token("ReduceTransparency", "TRUE")
            .Token("BodyFontFamily", "Fira Code, monospace")
            .Token("WeightMedium", "bold")
            .Token("DurationFast", "120ms")
            .Token("EaseStandard", "0.4,0,0.2,1")
            .Token("Density", "compact")
            .Token("MotionMode", "custom")
            .Token("SampleSwitch", "TrackWidth", "50")
            .Token("SampleSwitch", "ThumbTravel", "0.3s"));

        BrushColor(AppResource(SampleDesignTokenKind.Surface) as IBrush).ShouldBe(Color.Parse("#102030"));
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(12));
        AppResource(SampleDesignTokenKind.RadiusPill).ShouldBe(new CornerRadius(1, 2, 3, 4));
        AppResource(SampleDesignTokenKind.ShadowRaised).ShouldBe(BoxShadows.Parse("0 4 12 0 #33000000, inset 0 1 0 0 #10FFFFFF"));
        AppResource(SampleDesignTokenKind.SpaceInset).ShouldBe(new Thickness(4, 2));
        AppResource(SampleDesignTokenKind.ControlSizeMD).ShouldBe(40.5);
        AppResource(SampleDesignTokenKind.StaggerCount).ShouldBe(6);
        AppResource(SampleDesignTokenKind.ReduceTransparency).ShouldBe(true);
        AppResource(SampleDesignTokenKind.BodyFontFamily).ShouldBe(new FontFamily("Fira Code, monospace"));
        AppResource(SampleDesignTokenKind.WeightMedium).ShouldBe(FontWeight.Bold);
        AppResource(SampleDesignTokenKind.DurationFast).ShouldBe(TimeSpan.FromMilliseconds(120));
        AppResource(SampleDesignTokenKind.EaseStandard).ShouldBeOfType<SplineEasing>();
        AppResource(SampleDesignTokenKind.Density).ShouldBe(SampleDensity.Compact);
        AppResource(SampleDesignTokenKind.MotionMode).ShouldBe("custom");
        AppResource(SampleSwitchTokenKind.TrackWidth).ShouldBe(50d);
        AppResource(SampleSwitchTokenKind.ThumbTravel).ShouldBe(TimeSpan.FromMilliseconds(300));
    }

    [AvaloniaFact]
    public void Overrides_are_final_values_applied_after_the_calculation()
    {
        var button = new SampleButton();
        Show(button);
        var strong = AppResource(SampleDesignTokenKind.AccentStrong);

        ArcTheme.Apply(t => t.Token("Accent", "#FF0000"));

        BrushColor(button.Background).ShouldBe(Color.Parse("#FF0000"));
        // Not a seed: AccentStrong is still derived from the default accent.
        AppResource(SampleDesignTokenKind.AccentStrong).ShouldBeSameAs(strong);
        // Overrides persist across appearance changes; the identity can also be named explicitly.
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark).Token("SampleDesign", "Foreground", "#00FF00"));
        BrushColor(button.Background).ShouldBe(Color.Parse("#FF0000"));
        BrushColor(AppResource(SampleDesignTokenKind.Foreground) as IBrush).ShouldBe(Color.Parse("#00FF00"));
    }

    [AvaloniaFact]
    public void Bad_overrides_fail_the_whole_change_with_clear_messages()
    {
        var button = new SampleButton();
        Show(button);
        var before = ArcTheme.State;

        var error = Should.Throw<ArcThemeException>(() => ArcTheme.Apply(t => t
            .Appearance(ArcAppearance.Dark)
            .Token("RadiusControl", "twelve")
            .Token("Density", "roomy")
            .Token("DurationFast", "fast")
            .Token("SampleSwitch", "Nope", "1")
            .Token("Missing", "Width", "1")));

        error.Errors.ShouldBe(new[]
        {
            "'twelve' is not a valid corner radius (1 or 4 numbers) for token 'SampleDesign.RadiusControl'.",
            "'roomy' is not a valid SampleDensity (Comfortable, Compact) for token 'SampleDesign.Density'.",
            "'fast' is not a valid duration (120ms, 0.2s or 00:00:00.120) for token 'SampleDesign.DurationFast'.",
            "'SampleSwitch' has no token named 'Nope'.",
            "No token identity named 'Missing' is registered (is its package enabled in UseArc?)."
        }, ignoreOrder: true);
        error.Message.ShouldStartWith("The theme was not applied:");

        // All or nothing: still light, still the old values.
        ArcTheme.State.ShouldBeSameAs(before);
        ArcTheme.Manager.IsDark.ShouldBeFalse();
        BrushColor(button.Background).ShouldBe(ArcSeedToken.DefaultAccent);
        AppResource(SampleDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(18));
    }

    [AvaloniaFact]
    public void Value_parsers_accept_documented_forms_and_reject_garbage()
    {
        ArcTokenValueParser.TryParseTimeSpan("120", out var bare).ShouldBeTrue();
        bare.ShouldBe(TimeSpan.FromMilliseconds(120));
        ArcTokenValueParser.TryParseTimeSpan("00:00:00.250", out var span).ShouldBeTrue();
        span.ShouldBe(TimeSpan.FromMilliseconds(250));
        ArcTokenValueParser.TryParseTimeSpan("-5ms", out _).ShouldBeFalse();
        ArcTokenValueParser.TryParseColor("rebeccapurple", out _).ShouldBeFalse();
        ArcTokenValueParser.TryParseColor("Red", out var red).ShouldBeTrue();
        red.ShouldBe(Colors.Red);
        ArcTokenValueParser.TryParseThickness("1,2,3", out _).ShouldBeFalse();
        ArcTokenValueParser.TryParseBoxShadows("none", out var none).ShouldBeTrue();
        none.Count.ShouldBe(0);
        ArcTokenValueParser.TryParseBoxShadows("shadow", out _).ShouldBeFalse();
        ArcTokenValueParser.TryParseEasing("CubicEaseOut", out var cubic).ShouldBeTrue();
        cubic.ShouldBeOfType<CubicEaseOut>();
        ArcTokenValueParser.TryParseEasing("1.5,0,0,1", out _).ShouldBeFalse();
        ArcTokenValueParser.TryParseDouble("1,5", out _).ShouldBeFalse();
    }
}

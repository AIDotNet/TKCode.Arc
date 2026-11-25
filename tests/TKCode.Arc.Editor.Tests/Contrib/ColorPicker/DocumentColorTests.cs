// Tests of the color picker's engine part, ported from vscode (commit b57d5c9e), MIT license:
//  - src/vs/editor/test/common/languages/defaultDocumentColorsComputer.test.ts: all 13 cases (the TestDocumentModel is the
//    document's lines);
//  - src/vs/editor/contrib/colorPicker/test/browser/defaultDocumentColorProvider.test.ts: all 5 cases.
// Own tests of getColors (color.ts) and the HSLA conversions follow.

using Shouldly;
using TKCode.Arc.Editor.Contrib.ColorPicker;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.ColorPicker;

public sealed class DocumentColorTests
{
    private static IReadOnlyList<ColorInformation> Compute(string content) =>
        DefaultDocumentColorsComputer.ComputeDefaultDocumentColors(content.Split('\n'));

    private static ColorInformation Info(double r, double g, double b, double a) => new(new TextRange(1, 1, 1, 10), new LanguageColor(r, g, b, a));

    // ---- defaultDocumentColorsComputer.test.ts

    [Fact]
    public void Hex_colors_in_strings_should_be_detected()
    {
        var colors = Compute("const color = '#ff0000';");
        colors.Count.ShouldBe(1);
        colors[0].Color.ShouldBe(new LanguageColor(1, 0, 0, 1));
    }

    [Fact]
    public void Hex_colors_in_double_quotes_should_be_detected()
    {
        var colors = Compute("const color = \"#00ff00\";");
        colors.Count.ShouldBe(1);
        colors[0].Color.ShouldBe(new LanguageColor(0, 1, 0, 1));
    }

    [Fact]
    public void Multiple_hex_colors_in_array_should_be_detected()
    {
        var colors = Compute("const colors = ['#ff0000', '#00ff00', '#0000ff'];");
        colors.Count.ShouldBe(3);
        colors[0].Color.ShouldBe(new LanguageColor(1, 0, 0, 1));
        colors[1].Color.ShouldBe(new LanguageColor(0, 1, 0, 1));
        colors[2].Color.ShouldBe(new LanguageColor(0, 0, 1, 1));
    }

    [Fact]
    public void Existing_functionality_should_still_work()
    {
        foreach (var content in new[] { "const color = ' #ff0000';", "#ff0000", "  #ff0000" })
        {
            Compute(content).Count.ShouldBe(1, content);
        }
    }

    [Fact]
    public void Issue_261765_hex_like_identifiers_are_not_detected_as_colors()
    {
        string[] contents = ["class MyClass {\n\t#add() {}\n}", "#add-mailbox", "const color = ' #add(';", "  #ffffff-suffix", "#add.call()", "#add:"];
        contents.Select(c => Compute(c).Count).ShouldBe([0, 0, 0, 0, 0, 0]);
        string[] colors = ["#add", "#add;", "#add)", "#add,", "#add }", "'#add'", "\"#add\"", "#add\nnext"];
        colors.Select(c => Compute(c).Count).ShouldBe([1, 1, 1, 1, 1, 1, 1, 1]);
    }

    [Fact]
    public void Eight_digit_hex_colors_should_also_work()
    {
        var colors = Compute("const color = '#ff0000ff';");
        colors.Count.ShouldBe(1);
        colors[0].Color.ShouldBe(new LanguageColor(1, 0, 0, 1));
    }

    [Fact]
    public void Hsl_100_percent_saturation_works_with_decimals() => Compute("const color = hsl(253, 100.00%, 47.10%);").Count.ShouldBe(1);

    [Fact]
    public void Hsl_100_percent_saturation_works_without_decimals() => Compute("const color = hsl(253, 100%, 47.10%);").Count.ShouldBe(1);

    [Fact]
    public void Hsl_not_100_percent_saturation_should_also_work() => Compute("const color = hsl(0, 83.60%, 47.80%);").Count.ShouldBe(1);

    [Theory]
    [InlineData("hsl(253.5, 100%, 50%)")]
    [InlineData("hsl(360.0, 50%, 50%)")]
    [InlineData("hsl(100.5, 50.5%, 50.5%)")]
    [InlineData("hsl(0.5, 50%, 50%)")]
    [InlineData("hsl(359.9, 100%, 50%)")]
    public void Hsl_with_decimal_hue_values_should_work(string content) => Compute($"const color = {content};").Count.ShouldBe(1);

    [Theory]
    [InlineData("hsla(253.5, 100%, 50%, 0.5)")]
    [InlineData("hsla(360.0, 50.5%, 50.5%, 1)")]
    [InlineData("hsla(0.5, 50%, 50%, 0.25)")]
    public void Hsla_with_decimal_values_should_work(string content) => Compute($"const color = {content};").Count.ShouldBe(1);

    [Theory]
    [InlineData("hsl(253 100% 50%)")]
    [InlineData("hsl(253.5 100% 50%)")]
    [InlineData("hsla(253 100% 50% / 0.5)")]
    [InlineData("hsla(253.5 100% 50% / 0.5)")]
    [InlineData("hsla(253 100% 50% / 1)")]
    public void Hsl_with_space_separator_CSS_level_4_syntax_should_work(string content) => Compute($"const color = {content};").Count.ShouldBe(1);

    [Theory]
    [InlineData("rgb(255 0 0)")]
    [InlineData("rgb(128 128 128)")]
    [InlineData("rgba(255 0 0 / 0.5)")]
    [InlineData("rgba(128 128 128 / 0.8)")]
    [InlineData("rgba(255 0 0 / 1)")]
    [InlineData("rgb(255, 0, 0)")]
    [InlineData("rgba(255, 0, 0, 0.5)")]
    public void Rgb_and_rgba_with_CSS_level_4_space_separated_syntax_should_work(string content) => Compute($"const color = {content};").Count.ShouldBe(1);

    // ---- defaultDocumentColorProvider.test.ts

    [Fact]
    public void Color_presentations_should_not_include_alpha_channel_when_alpha_is_1()
    {
        var presentations = DefaultDocumentColorProvider.ProvideColorPresentations(Info(1, 0, 0, 1));
        presentations.Select(p => p.Label).ShouldBe(["rgb(255, 0, 0)", "hsl(0, 100%, 50%)", "#ff0000"]);
    }

    [Fact]
    public void Color_presentations_should_include_alpha_channel_when_alpha_is_not_1()
    {
        var presentations = DefaultDocumentColorProvider.ProvideColorPresentations(Info(0, 0, 0, 0));
        presentations.Select(p => p.Label).ShouldBe(["rgba(0, 0, 0, 0)", "hsla(0, 0%, 0%, 0.00)", "#00000000"]);
    }

    [Fact]
    public void Color_presentations_should_include_alpha_channel_when_alpha_is_between_0_and_1()
    {
        var presentations = DefaultDocumentColorProvider.ProvideColorPresentations(Info(0.67, 0, 0, 0.67));
        presentations.Select(p => p.Label).ShouldBe(["rgba(171, 0, 0, 0.67)", "hsla(0, 100%, 34%, 0.67)", "#ab0000ab"]);
    }

    [Fact]
    public void Regression_issue_243746_opacity_should_be_preserved_when_switching_to_hex_format() =>
        DefaultDocumentColorProvider.ProvideColorPresentations(Info(0.5, 0.5, 0.5, 0.5))[2].Label.ShouldBe("#80808080");

    [Fact]
    public void Regression_issue_256853_fully_opaque_colors_should_not_add_unnecessary_alpha_suffix() =>
        DefaultDocumentColorProvider.ProvideColorPresentations(Info(0.58, 0.36, 0.65, 1))[2].Label.ShouldBe("#945ca6");

    // ---- own tests

    [Fact]
    public void Ranges_cover_the_color_text_and_hsl_converts_to_rgb()
    {
        var colors = Compute("a: hsl(120, 100%, 50%);\nb: rgba(0, 0, 255, .5)");
        colors.Select(c => c.Range).ShouldBe([new TextRange(1, 4, 1, 23), new TextRange(2, 4, 2, 23)]);
        colors[0].Color.ShouldBe(new LanguageColor(0, 1, 0, 1));
        colors[1].Color.ShouldBe(new LanguageColor(0, 0, 1, 0.5));
    }

    [Fact]
    public void Each_presentation_replaces_the_color_range()
    {
        var info = new ColorInformation(new TextRange(3, 5, 3, 12), new LanguageColor(0, 0, 1, 1));
        foreach (var presentation in DefaultDocumentColorProvider.ProvideColorPresentations(info))
        {
            presentation.TextEdit.ShouldBe(new TextEdit(info.Range, presentation.Label));
        }
    }

    [Fact]
    public async Task Get_colors_uses_the_default_provider_only_when_no_other_gives_colors()
    {
        using var model = TestTextModel.Create("x = '#ff0000'", "css", uri: new Uri("file:///a.css"));
        var registry = new LanguageFeatureRegistry<IDocumentColorProvider>();
        var defaultProvider = new DefaultDocumentColorProvider();
        registry.Register("*", defaultProvider);

        (await DocumentColors.GetColors(registry, model, default)).Single().Provider.ShouldBe(defaultProvider);
        (await DocumentColors.GetColors(registry, model, default, DefaultColorDecoratorsEnablement.Never)).ShouldBeEmpty();

        var own = new FixedColors([new ColorInformation(new TextRange(1, 1, 1, 2), new LanguageColor(0, 0, 0, 1))]);
        using var registration = registry.Register("css", own);
        (await DocumentColors.GetColors(registry, model, default)).Select(c => c.Provider).ShouldBe([own]);
        (await DocumentColors.GetColors(registry, model, default, DefaultColorDecoratorsEnablement.Always)).Select(c => c.Provider).ShouldBe([defaultProvider, own]);

        // a provider that answers null does not count as having found colors
        registration.Dispose();
        using var none = registry.Register("css", new FixedColors(null));
        (await DocumentColors.GetColors(registry, model, default)).Single().Provider.ShouldBe(defaultProvider);
    }

    [Fact]
    public void Hsla_round_trips_through_rgba()
    {
        var rgba = new CssRgba(51, 102, 153, 0.5);
        var hsla = CssHsla.FromRgba(rgba);
        hsla.H.ShouldBe(210);
        hsla.S.ShouldBe(0.5);
        hsla.L.ShouldBe(0.4);
        CssHsla.ToRgba(hsla).ShouldBe(rgba);
        CssColor.FormatHsl(rgba).ShouldBe("hsla(210, 50%, 40%, 0.50)");
        CssColor.FormatRgb(new CssRgba(300, -4, 12.7)).ShouldBe("rgb(255, 0, 12)");
    }

    private sealed class FixedColors(IReadOnlyList<ColorInformation>? colors) : IDocumentColorProvider
    {
        public Task<IReadOnlyList<ColorInformation>?> ProvideDocumentColors(TextModel model, CancellationToken token) => Task.FromResult(colors);

        public Task<IReadOnlyList<ColorPresentation>?> ProvideColorPresentations(TextModel model, ColorInformation colorInfo, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ColorPresentation>?>([]);
    }
}

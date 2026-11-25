// Own tests of the port of vscode's src/vs/editor/contrib/colorPicker/browser/colorPickerModel.ts and the
// createColorHover / updateColorPresentations helpers of colorPickerParticipantUtils.ts (commit b57d5c9e, MIT license);
// upstream tests the model only through the widget.

using Shouldly;
using TKCode.Arc.Editor.Contrib.ColorPicker;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.ColorPicker;

public sealed class ColorPickerModelTests
{
    [Fact]
    public async Task Create_selects_the_presentation_the_document_uses()
    {
        using var model = TestTextModel.Create("a { color: hsl(0, 100%, 50%); }");
        var info = DefaultDocumentColorsComputer.ComputeDefaultDocumentColors(model).Single();
        var picker = await ColorPickerModel.Create(model, info, new DefaultDocumentColorProvider(), default);

        picker.OriginalColor.ShouldBe(new CssRgba(255, 0, 0));
        picker.ColorPresentations.Select(p => p.Label).ShouldBe(["rgb(255, 0, 0)", "hsl(0, 100%, 50%)", "#ff0000"]);
        picker.PresentationIndex.ShouldBe(1);
    }

    [Fact]
    public async Task Create_falls_back_to_the_function_name_and_then_to_the_first()
    {
        using var model = TestTextModel.Create("#FF0000 rgba(255,0,0,0.5)");
        var colors = DefaultDocumentColorsComputer.ComputeDefaultDocumentColors(model);
        var hex = await ColorPickerModel.Create(model, colors[0], new DefaultDocumentColorProvider(), default);
        hex.Presentation!.Label.ShouldBe("#ff0000");

        var rgba = await ColorPickerModel.Create(model, colors[1], new DefaultDocumentColorProvider(), default);
        // "rgba(255, 0, 0, 0.5)" is written differently, but its function name matches the first presentation
        rgba.PresentationIndex.ShouldBe(0);
        rgba.Presentation!.Label.ShouldBe("rgba(255, 0, 0, 0.5)");
    }

    [Fact]
    public async Task Next_presentation_cycles_and_flushes_and_new_presentations_follow_the_color()
    {
        using var model = TestTextModel.Create("x = '#00ff00'");
        var info = DefaultDocumentColorsComputer.ComputeDefaultDocumentColors(model).Single();
        var provider = new DefaultDocumentColorProvider();
        var picker = await ColorPickerModel.Create(model, info, provider, default);
        var flushed = new List<CssRgba>();
        var changed = new List<CssRgba>();
        picker.ColorFlushed += flushed.Add;
        picker.DidChangeColor += changed.Add;

        picker.PresentationIndex.ShouldBe(2);
        picker.SelectNextColorPresentation();
        picker.PresentationIndex.ShouldBe(0);
        flushed.Count.ShouldBe(1);

        picker.Color = new CssRgba(0, 0, 255);
        picker.Color = new CssRgba(0, 0, 255);
        changed.ShouldBe([new CssRgba(0, 0, 255)]);
        await picker.UpdateColorPresentations(model, picker.Color, info.Range, provider, default);
        picker.Presentation!.Label.ShouldBe("rgb(0, 0, 255)");
        picker.Presentation.TextEdit!.Range.ShouldBe(info.Range);
        ColorPickerModel.ToLanguageColor(new CssRgba(255, 0, 51, 0.5)).ShouldBe(new LanguageColor(1, 0, 0.2, 0.5));
    }
}

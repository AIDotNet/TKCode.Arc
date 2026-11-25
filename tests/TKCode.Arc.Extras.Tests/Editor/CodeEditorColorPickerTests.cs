// Tests of the color decorators and the color picker of the code editor (the port of vscode's
// src/vs/editor/contrib/colorPicker: colorDetector.ts, colorContribution / hover participant; commit b57d5c9e). Upstream's
// colorDetector.test.ts checks CSS custom properties of the DOM; its subject here is the same: no swatches without
// colors, swatches with them, and the swatches gone when the detector is disposed. The picker tests are own.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorColorPickerTests
{
    private static (CodeEditor Editor, Avalonia.Controls.Window Window, ColorDetector Detector) Show(string text, string languageId = "plaintext")
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = 300, LanguageId = languageId, StickyScroll = false };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        var detector = ColorDetector.Get(editor).ShouldNotBeNull();
        Pump(detector.LastCompute);
        window.CaptureRenderedFrame()?.Dispose();
        return (editor, window, detector);
    }

    private static void Pump(Task task)
    {
        TestHelpers.PumpUntil(() => task.IsCompleted, 5000).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
    }

    private static Point ToWindow(CodeEditor editor, Avalonia.Controls.Window window, Point viewPoint) =>
        editor.View!.TranslatePoint(viewPoint, window)!.Value;

    [AvaloniaFact]
    public void No_swatches_without_colors()
    {
        var (editor, window, detector) = Show("plain text\nno colors here");
        detector.LimitReporter.Computed.ShouldBe(0);
        editor.View!.GetInlineBackgroundRects(1, 2).ShouldBeEmpty();
        window.Close();
    }

    [AvaloniaFact]
    public void Each_color_gets_a_swatch_before_it_and_disposing_removes_them()
    {
        var (editor, window, detector) = Show("a: #ff0000;\nb: rgba(0, 0, 255, 0.5);");
        detector.LimitReporter.Computed.ShouldBe(2);
        detector.ColorDatas.Count.ShouldBe(2);

        var view = editor.View!;
        var rects = view.GetInlineBackgroundRects(1, 2);
        rects.Count.ShouldBe(2);
        ((ISolidColorBrush)rects[0].Fill!).Color.ShouldBe(Color.FromRgb(255, 0, 0));
        ((ISolidColorBrush)rects[1].Fill!).Color.ShouldBe(Color.FromArgb(128, 0, 0, 255));
        rects[0].Stroke.ShouldNotBeNull();

        // a square box, smaller than the line, inside the two injected characters before the color (column 4)
        var box = rects[0].Bounds;
        box.Width.ShouldBe(box.Height);
        box.Height.ShouldBeLessThan(view.LineHeight);
        box.X.ShouldBeGreaterThanOrEqualTo(view.GetX(1, 4) - 0.5);
        box.Right.ShouldBeLessThanOrEqualTo(view.GetX(1, 4) + (2 * view.CharWidth) + 0.5);

        // the text after the swatch moved right by the injected text
        var viewModel = editor.ViewModel!;
        viewModel.GetLineContent(1).ShouldBe("a:   #ff0000;");

        detector.Dispose();
        viewModel.GetLineContent(1).ShouldBe("a: #ff0000;");
        window.Close();
    }

    [AvaloniaFact]
    public void Turning_color_decorators_off_and_on()
    {
        var (editor, window, detector) = Show("x = '#00ff00'");
        editor.ViewModel!.GetLineContent(1).ShouldContain(" ");
        editor.ColorDecorators = false;
        editor.ViewModel!.GetLineContent(1).ShouldBe("x = '#00ff00'");
        editor.ColorDecorators = true;
        Pump(detector.LastCompute);
        editor.ViewModel!.GetLineContent(1).ShouldContain(" ");
        window.Close();
    }

    [AvaloniaFact]
    public void A_language_color_provider_replaces_the_default_colors()
    {
        var editor = new CodeEditor { Text = "red and #ff0000", Width = 500, Height = 300, LanguageId = "colorlang", StickyScroll = false };
        using var registration = editor.LanguageFeatures.ColorProvider.Register("colorlang", new NamedColors());
        var window = TestHelpers.Show(editor);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        var detector = ColorDetector.Get(editor).ShouldNotBeNull();
        Pump(detector.LastCompute);
        detector.ColorDatas.Select(c => c.ColorInfo.Range).ShouldBe([new TextRange(1, 1, 1, 4)]);
        window.Close();
    }

    [AvaloniaFact]
    public void A_click_on_a_swatch_opens_the_picker_which_writes_the_color_back()
    {
        var (editor, window, detector) = Show("color: #ff0000;");
        var view = editor.View!;
        var controller = ColorPickerController.Get(editor);
        editor.OnBeforeInteractionEvent();
        controller = ColorPickerController.Get(editor).ShouldNotBeNull();
        var (range, _) = detector.GetColorRanges().Single();
        var swatch = controller.GetSwatchBounds(view, range).ShouldNotBeNull();
        var point = ToWindow(editor, window, swatch.Center);

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        TestHelpers.PumpUntil(() => controller.Widget is not null, 5000).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
        var widget = controller.Widget!;
        widget.PresentationLabel.ShouldBe("#ff0000");
        editor.GetContentWidgetPlacement(widget.Id).ShouldNotBeNull();
        editor.ContextKeyService.GetContextKeyValue("standaloneColorPickerVisible").ShouldBe(true);
        // the cursor stayed where it was
        editor.GetPosition().ShouldBe(new TextPosition(1, 1));

        // picking a color writes it in the presentation the document uses
        widget.Pick(Color.FromRgb(0, 255, 0));
        TestHelpers.PumpUntil(() => editor.Text == "color: #00ff00;", 5000).ShouldBeTrue();

        // a click on the presentation selects the next one (hex -> rgb)
        controller.Model!.SelectNextColorPresentation();
        TestHelpers.PumpUntil(() => editor.Text == "color: rgb(0, 255, 0);", 5000).ShouldBeTrue();
        widget.PresentationLabel.ShouldBe("rgb(0, 255, 0)");
        controller.Range.ShouldBe(new TextRange(1, 8, 1, 22));

        // the original color comes back in the current presentation
        Pump(controller.ApplyColor(controller.Model!.OriginalColor));
        editor.Text.ShouldBe("color: rgb(255, 0, 0);");

        // Escape closes the picker
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        controller.Widget.ShouldBeNull();
        editor.ContextKeyService.GetContextKeyValue("standaloneColorPickerVisible").ShouldBe(false);
        window.Close();
    }

    [AvaloniaFact]
    public void An_edit_elsewhere_closes_the_picker_and_the_action_opens_it_at_the_cursor()
    {
        var (editor, window, _) = Show("a #123456 b");
        editor.OnBeforeInteractionEvent();
        var controller = ColorPickerController.Get(editor).ShouldNotBeNull();
        editor.SetPosition(new TextPosition(1, 5));
        Pump(editor.GetAction("editor.action.showOrFocusStandaloneColorPicker")!.Run());
        controller.Widget.ShouldNotBeNull();
        controller.Model!.OriginalColor.ShouldBe(new TKCode.Arc.Editor.Contrib.ColorPicker.CssRgba(0x12, 0x34, 0x56));

        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("z"));
        Dispatcher.UIThread.RunJobs();
        controller.Widget.ShouldBeNull();
        window.Close();
    }

    private sealed class NamedColors : IDocumentColorProvider
    {
        public Task<IReadOnlyList<ColorInformation>?> ProvideDocumentColors(TextModel model, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ColorInformation>?>([new ColorInformation(new TextRange(1, 1, 1, 4), new LanguageColor(1, 0, 0, 1))]);

        public Task<IReadOnlyList<ColorPresentation>?> ProvideColorPresentations(TextModel model, ColorInformation colorInfo, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ColorPresentation>?>([new ColorPresentation("red")]);
    }
}

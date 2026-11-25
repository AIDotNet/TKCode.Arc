using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Tests.Editor;

/// <summary>The minimap and the overview ruler of the code editor (phase 4.8).</summary>
public class CodeEditorMinimapTests
{
    private static (CodeEditor Editor, CodeEditorView View, Avalonia.Controls.Window Window) Show(string text, double width = 600, double height = 200, bool showMinimap = true)
    {
        var editor = new CodeEditor { Text = text, Width = width, Height = height, ShowMinimap = showMinimap };
        var window = TestHelpers.Show(editor);
        // room for the whole editor (the helper's window is 400 x 300)
        window.Width = width + 40;
        window.Height = height + 40;
        editor.Focus();
        Render(window);
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    private static void Render(Avalonia.Controls.Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
    }

    private static string Lines(int count) => string.Join("\n", Enumerable.Range(1, count).Select(i => "line number " + i));

    private static Point ToWindow(CodeEditorView view, Avalonia.Controls.Window window, Point point) =>
        view.TranslatePoint(point, window)!.Value;

    [AvaloniaFact]
    public void The_Minimap_Shows_By_Default_Left_Of_The_Overview_Ruler()
    {
        var (editor, view, _) = Show("hello\nworld");
        editor.ShowMinimap.ShouldBeTrue();
        view.IsMinimapVisible.ShouldBeTrue();
        var layout = view.MinimapLayout;
        layout.RenderMinimap.ShouldBe(RenderMinimap.Text);
        layout.MinimapWidth.ShouldBeGreaterThan(EditorLayoutInfoComputer.MinimapGutterWidth);
        layout.MinimapWidth.ShouldBeLessThanOrEqualTo(120);
        layout.MinimapLeft.ShouldBe(Math.Truncate(view.Bounds.Width) - layout.MinimapWidth - view.OverviewRulerWidth);

        // the text ends where the minimap starts
        view.TextAreaRight.ShouldBe(layout.MinimapLeft);
        view.MinimapView.ShouldNotBeNull().MinimapBounds.X.ShouldBe(layout.MinimapLeft);
        view.OverviewRulerView.ShouldNotBeNull().RulerBounds.ShouldBe(new Rect(view.Bounds.Width - 14, 0, 14, view.Bounds.Height));
    }

    [AvaloniaFact]
    public void Without_The_Minimap_The_Text_Spans_The_View_And_The_Ruler_Lies_Over_It()
    {
        var (editor, view, window) = Show("hello\nworld", showMinimap: false);
        view.IsMinimapVisible.ShouldBeFalse();
        view.TextAreaRight.ShouldBe(view.Bounds.Width);
        view.MinimapView!.MinimapBounds.ShouldBe(default);
        // a transparent ruler over the text: the cursor mark only
        var frame = view.OverviewRulerView!.Frame.ShouldNotBeNull();
        frame.Background.ShouldBeNull();
        frame.Rects.ShouldNotBeEmpty();

        editor.ShowMinimap = true;
        Render(window);
        view.IsMinimapVisible.ShouldBeTrue();
        view.TextAreaRight.ShouldBeLessThan(view.Bounds.Width);
        view.OverviewRulerView.Frame!.Background.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void The_Lines_Are_Drawn_Into_A_Bitmap_And_An_Edit_Copies_Only_Its_Rows()
    {
        var (editor, view, window) = Show(Lines(20));
        var minimap = view.MinimapView!;
        var bitmap = minimap.Bitmap.ShouldNotBeNull();
        var options = view.MinimapPart!.Options;
        bitmap.PixelSize.ShouldBe(new PixelSize(options.CanvasInnerWidth, options.CanvasInnerHeight));
        minimap.LastCopiedRows.ShouldBe(options.CanvasInnerHeight);

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(3, 1, 3, 1), "changed ")]);
        Render(window);
        minimap.Bitmap.ShouldBeSameAs(bitmap);
        minimap.LastCopiedRows.ShouldBe((int)options.MinimapLineHeight);
    }

    [AvaloniaFact]
    public void A_Cursor_Move_Redraws_The_Minimap_Without_Copying_Lines()
    {
        var (editor, view, window) = Show(Lines(20));
        var minimap = view.MinimapView!;
        var before = minimap.RenderCount;
        editor.SetPosition(new TextPosition(5, 3));
        Render(window);
        minimap.RenderCount.ShouldBeGreaterThan(before);
        minimap.LastCopiedRows.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Selection_Shows_In_The_Minimap()
    {
        var (editor, view, window) = Show(Lines(20));
        editor.SetSelection(new TextSelection(2, 1, 4, 5));
        Render(window);
        var expected = CodeEditorView.ToRgba(view.MinimapSelectionBrush)!.Value;
        view.MinimapView!.Frame!.Decorations.ShouldContain(r => r.Color == expected);
    }

    [AvaloniaFact]
    public void Clicking_The_Minimap_Reveals_The_Line_And_Leaves_The_Cursor()
    {
        var (editor, view, window) = Show(Lines(300));
        var area = view.MinimapView!.MinimapBounds;
        // line 200 at two pixels a line (the slider is at the top)
        var y = 199 * view.MinimapPart!.Options.MinimapLineHeight + 1;
        window.MouseDown(ToWindow(view, window, new Point(area.X + 20, Math.Min(y, area.Height - 2))), MouseButton.Left);
        window.MouseUp(ToWindow(view, window, new Point(area.X + 20, Math.Min(y, area.Height - 2))), MouseButton.Left);
        Render(window);
        view.ScrollTop.ShouldBeGreaterThan(0);
        editor.GetPosition().ShouldBe(new TextPosition(1, 1));
    }

    [AvaloniaFact]
    public void The_Slider_Shows_On_Hover_And_Dragging_It_Scrolls()
    {
        var (_, view, window) = Show(Lines(500));
        var minimap = view.MinimapView!;
        minimap.IsSliderVisible.ShouldBeFalse();

        var slider = minimap.SliderBounds;
        slider.Height.ShouldBeGreaterThan(0);
        var start = ToWindow(view, window, slider.Center);
        window.MouseMove(start);
        Render(window);
        minimap.IsSliderVisible.ShouldBeTrue();

        window.MouseDown(start, MouseButton.Left);
        minimap.IsDragging.ShouldBeTrue();
        window.MouseMove(start + new Point(0, 40), RawInputModifiers.LeftMouseButton);
        Render(window);
        view.ScrollTop.ShouldBeGreaterThan(0);
        minimap.SliderBounds.Y.ShouldBeGreaterThan(slider.Y);
        window.MouseUp(start + new Point(0, 40), MouseButton.Left);
        minimap.IsDragging.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Wheel_Over_The_Minimap_Scrolls_The_Editor()
    {
        var (_, view, window) = Show(Lines(300));
        var area = view.MinimapView!.MinimapBounds;
        window.MouseWheel(ToWindow(view, window, area.Center), new Vector(0, -3));
        Render(window);
        view.ScrollTop.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void The_Overview_Ruler_Marks_The_Cursor_And_The_Markers()
    {
        var (editor, view, window) = Show(Lines(100));
        var ruler = view.OverviewRulerView!;
        var cursor = CodeEditorView.ToRgba(view.OverviewRulerCursorBrush, 0.7)!.Value;
        ruler.Frame!.Rects.ShouldContain(r => r.Color == cursor);

        editor.MarkerService.ChangeOne("test", editor.Model.Uri,
        [
            new MarkerData { Severity = MarkerSeverity.Error, Message = "e", StartLineNumber = 50, StartColumn = 1, EndLineNumber = 50, EndColumn = 5 },
        ]);
        Render(window);
        var error = CodeEditorView.ToRgba(view.ErrorBrush)!.Value;
        var mark = ruler.Frame!.Rects.Where(r => r.Color == error).ShouldHaveSingleItem();
        // the right lane
        mark.X.ShouldBe(ruler.Frame.Settings.X[(int)OverviewRulerLane.Right]);
        mark.Y.ShouldBeGreaterThan(0);

        // the minimap shows the marker too, in the minimap's error color
        var minimapError = CodeEditorView.ToRgba(view.ErrorBrush, 0.7)!.Value;
        view.MinimapView!.Frame!.Decorations.ShouldContain(r => r.Color == minimapError);
    }

    [AvaloniaFact]
    public void Decoration_Colors_Resolve_Hex_And_Upstream_Theme_Ids()
    {
        var (_, view, _) = Show("x");
        view.ResolveDecorationColor("#ff000080").ShouldBe(new RgbaColor(255, 0, 0, 128));
        view.ResolveDecorationColor("editorOverviewRuler.errorForeground").ShouldBe(CodeEditorView.ToRgba(view.ErrorBrush));
        view.ResolveDecorationColor("editorOverviewRuler.findMatchForeground").ShouldBe(CodeEditorView.ToRgba(view.WarningBrush, 0.5));
        view.ResolveDecorationColor("minimap.selectionHighlight").ShouldBe(CodeEditorView.ToRgba(view.MinimapSelectionBrush));
        view.ResolveDecorationColor("no.such.color").ShouldBeNull();
    }

    [AvaloniaFact]
    public void New_Theme_Brushes_Repaint_The_Minimap_And_The_Ruler()
    {
        var (_, view, window) = Show(Lines(10));
        var minimapCount = view.MinimapView!.RenderCount;
        var rulerCount = view.OverviewRulerView!.RenderCount;
        view.MinimapBackgroundBrush = Brushes.Black;
        view.OverviewRulerCursorBrush = Brushes.Red;
        Render(window);
        view.MinimapView.RenderCount.ShouldBeGreaterThan(minimapCount);
        view.OverviewRulerView.RenderCount.ShouldBeGreaterThan(rulerCount);
        view.MinimapPart!.Options.BackgroundColor.ShouldBe(new RgbaColor(0, 0, 0));
        view.OverviewRulerView.Frame!.Rects.ShouldContain(r => r.Color == new RgbaColor(255, 0, 0, 179));
    }

    [AvaloniaFact]
    public void A_Narrow_Editor_Keeps_A_Small_Minimap_And_Wraps_Before_It()
    {
        var (editor, view, window) = Show(Lines(10), width: 300);
        editor.WordWrap = true;
        Render(window);
        view.IsMinimapVisible.ShouldBeTrue();
        var minimapWidth = view.MinimapLayout.MinimapWidth;
        minimapWidth.ShouldBeLessThan(60);
        // the wrapping column fits the text area
        var textWidth = view.TextAreaRight - view.GutterWidth;
        textWidth.ShouldBeGreaterThan(0);
        (view.TextAreaRight + minimapWidth + view.OverviewRulerWidth).ShouldBe(Math.Truncate(view.Bounds.Width), 1);
    }
}

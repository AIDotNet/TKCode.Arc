using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The drawing of <see cref="CodeEditorView"/>: its layers and what invalidates each, the line cache, the column to x
/// mapping against the glyphs as drawn, and long lines.
/// </summary>
public class CodeEditorRenderTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window) Show(string text, double width = 400, double height = 200)
    {
        var editor = new CodeEditor { Text = text, Width = width, Height = height };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        Render(window);
        return (editor, view, window);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
    }

    private static (int Background, int Text, int Cursors, int Gutter) Counts(CodeEditorView view) =>
        (view.BackgroundLayer.RenderCount, view.TextLayer.RenderCount, view.CursorLayer.RenderCount, view.GutterLayer.RenderCount);

    private static CodeEditorShapedLine Line(CodeEditorView view, int lineNumber) =>
        view.TextCache!.Get(view.ViewModel!.GetLineContent(lineNumber), 4);

    // the x (from the start of the text) at which the glyph of Text[index] is drawn
    private static double DrawnX(CodeEditorShapedLine line, int index)
    {
        foreach (var (i, x) in line.GetDrawnClusterPositions())
        {
            if (i == index)
            {
                return x;
            }
        }

        throw new InvalidOperationException($"no glyph drawn for index {index} of '{line.Text}'");
    }

    [AvaloniaFact]
    public void The_View_Draws_In_Five_Layers_Bottom_To_Top()
    {
        var (_, view, _) = Show("hello");
        var layers = view.GetVisualChildren().OfType<CodeEditorLayer>().Select(l => l.Kind).ToList();
        layers.ShouldBe([CodeEditorLayers.Background, CodeEditorLayers.Text, CodeEditorLayers.Decorations, CodeEditorLayers.Cursors, CodeEditorLayers.Gutter]);
        view.GetVisualChildren().OfType<CodeEditorLayer>().ShouldAllBe(l => !l.IsHitTestVisible);
        view.TextLayer.RenderCount.ShouldBeGreaterThan(0);
        view.TextLayer.Bounds.Size.ShouldBe(view.Bounds.Size);
    }

    [AvaloniaFact]
    public void A_Cursor_Blink_Redraws_Only_The_Cursor_Layer()
    {
        var (_, view, window) = Show("one\ntwo");
        var before = Counts(view);
        view.InvalidateCursors();
        Render(window);
        var after = Counts(view);
        after.Cursors.ShouldBe(before.Cursors + 1);
        after.Text.ShouldBe(before.Text);
        after.Background.ShouldBe(before.Background);
        after.Gutter.ShouldBe(before.Gutter);
    }

    [AvaloniaFact]
    public void Moving_The_Cursor_Does_Not_Redraw_The_Text()
    {
        var (editor, view, window) = Show("one\ntwo\nthree");
        var before = Counts(view);
        var shapedBefore = view.TextCache!.LinesCreated;
        _ = editor.ExecuteCommand("cursorDown");
        _ = editor.ExecuteCommand("cursorRight");
        _ = editor.ExecuteCommand("cursorRightSelect");
        Render(window);
        var after = Counts(view);
        view.ViewModel!.GetPrimaryCursorState().ModelState.Selection.ShouldBe(new TextSelection(2, 2, 2, 3));
        after.Text.ShouldBe(before.Text);
        after.Cursors.ShouldBeGreaterThan(before.Cursors);
        after.Background.ShouldBeGreaterThan(before.Background);
        after.Gutter.ShouldBeGreaterThan(before.Gutter);
        view.TextCache.LinesCreated.ShouldBe(shapedBefore);
    }

    [AvaloniaFact]
    public void Typing_Redraws_The_Text_And_Shapes_Only_The_Changed_Line()
    {
        var (_, view, window) = Show("one\ntwo\nthree");
        var before = Counts(view);
        var created = view.TextCache!.LinesCreated;
        view.TypeText("x");
        Render(window);
        Counts(view).Text.ShouldBe(before.Text + 1);
        view.TextCache.LinesCreated.ShouldBe(created + 1);
    }

    [AvaloniaFact]
    public void Scrolling_Redraws_The_Text_From_The_Cache()
    {
        var text = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"line {i}"));
        var (_, view, window) = Show(text);
        var visible = view.ViewModel!.ViewLayout.GetLinesViewportData();
        var visibleCount = visible.EndLineNumber - visible.StartLineNumber + 1;
        var before = Counts(view);
        var created = view.TextCache!.LinesCreated;
        created.ShouldBe(visibleCount);

        view.ViewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(0, view.LineHeight * 2), ScrollType.Immediate);
        Render(window);
        Counts(view).Text.ShouldBe(before.Text + 1);
        view.ScrollTop.ShouldBe(view.LineHeight * 2);

        // only the lines that scrolled in are new; the rest came from the cache
        var scrolled = view.ViewModel.ViewLayout.GetLinesViewportData();
        var scrolledIn = scrolled.EndLineNumber - visible.EndLineNumber;
        scrolledIn.ShouldBeInRange(1, 3);
        view.TextCache.LinesCreated.ShouldBe(created + scrolledIn);

        // and back: the lines that scrolled out are still there
        view.ViewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(0, 0), ScrollType.Immediate);
        Render(window);
        view.TextCache.LinesCreated.ShouldBe(created + scrolledIn);
    }

    [AvaloniaFact]
    public void The_Line_Cache_Stays_Bounded()
    {
        var text = string.Join("\n", Enumerable.Range(1, 3000).Select(i => $"line {i}"));
        var (_, view, window) = Show(text);
        var visible = view.ViewModel!.ViewLayout.GetLinesViewportData();
        var visibleCount = visible.EndLineNumber - visible.StartLineNumber + 1;
        for (var top = 0.0; top < view.LineHeight * 3000; top += view.Bounds.Height)
        {
            view.ViewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(0, top), ScrollType.Immediate);
            Render(window);
            view.TextCache!.Count.ShouldBeLessThanOrEqualTo(CodeEditorTextCache.Capacity + (2 * visibleCount));
        }

        view.TextCache!.LinesCreated.ShouldBeGreaterThan(2900);
    }

    [AvaloniaFact]
    public void Ascii_Lines_Take_The_Fast_Path_And_Others_Fall_Back()
    {
        var (_, view, _) = Show("plain\tascii\n中文 text\nemoji 😀\nשלום");
        var plain = Line(view, 1);
        // the headless test font has no real glyphs, so the fast path is off here (the Skia shots exercise it); with a
        // monospaced font that maps ASCII it is on
        plain.IsFastPath.ShouldBe(view.TextCache!.Metrics.GlyphTypeface is not null);

        plain.Text.ShouldBe("plain   ascii");
        Line(view, 2).IsFastPath.ShouldBeFalse();
        Line(view, 3).IsFastPath.ShouldBeFalse();
        Line(view, 4).IsFastPath.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_X_Of_A_Column_After_A_Tab_Is_Where_Its_Glyph_Is_Drawn()
    {
        var (_, view, _) = Show("\tab\n  \tcd");
        var line = Line(view, 1);
        line.Text.ShouldBe("    ab");
        var x = view.GetX(1, 2) - view.GutterWidth + view.ScrollLeft;
        x.ShouldBe(DrawnX(line, 4), 0.01);
        x.ShouldBe(view.CharWidth * 4, 0.01);
        (view.GetX(1, 3) - view.GutterWidth).ShouldBe(DrawnX(line, 5), 0.01);

        // a tab after two spaces reaches the same stop
        var second = Line(view, 2);
        second.Text.ShouldBe("    cd");
        (view.GetX(2, 4) - view.GutterWidth).ShouldBe(DrawnX(second, 4), 0.01);

        // and back: x to column, also inside the tab's spaces (nearer edge)
        view.GetColumnAtX(1, view.GutterWidth + (view.CharWidth * 4)).ShouldBe(2);
        view.GetColumnAtX(1, view.GutterWidth + (view.CharWidth * 1)).ShouldBe(1);
        view.GetColumnAtX(1, view.GutterWidth + (view.CharWidth * 3)).ShouldBe(2);
    }

    [AvaloniaFact]
    public void The_X_Of_A_Column_After_CJK_Text_Is_Where_Its_Glyph_Is_Drawn()
    {
        var (_, view, _) = Show("中文ab\n\t日本x");
        var line = Line(view, 1);
        line.IsFastPath.ShouldBeFalse();

        // columns 2 and 3 start the second CJK character and the 'a'
        (view.GetX(1, 2) - view.GutterWidth).ShouldBe(DrawnX(line, 1), 0.5);
        (view.GetX(1, 3) - view.GutterWidth).ShouldBe(DrawnX(line, 2), 0.5);
        (view.GetX(1, 4) - view.GutterWidth).ShouldBe(DrawnX(line, 3), 0.5);
        view.GetX(1, 3).ShouldBeGreaterThan(view.GetX(1, 2));

        // a tab before CJK text: the text starts at the tab stop
        var second = Line(view, 2);
        second.Text.ShouldBe("    日本x");
        (view.GetX(2, 2) - view.GutterWidth).ShouldBe(DrawnX(second, 4), 0.5);
        (view.GetX(2, 4) - view.GutterWidth).ShouldBe(DrawnX(second, 6), 0.5);

        // x to column is the inverse on the drawn glyphs
        for (var column = 1; column <= 5; column++)
        {
            view.GetColumnAtX(1, view.GetX(1, column)).ShouldBe(column);
        }
    }

    [AvaloniaFact]
    public void Selections_And_Cursors_Use_The_Drawn_Positions()
    {
        var (editor, view, window) = Show("中文ab");
        _ = editor.ExecuteCommand("cursorEnd");
        Render(window);
        var line = Line(view, 1);
        var end = view.GetX(1, 5) - view.GutterWidth;
        end.ShouldBe(line.Width, 0.5);
        end.ShouldBeGreaterThan(DrawnX(line, 3));
    }

    [AvaloniaFact]
    public void A_Long_Line_Shapes_And_Draws_Only_The_Visible_Pieces()
    {
        var (_, view, window) = Show(new string('a', 100_000) + "\n" + string.Concat(Enumerable.Repeat("中a", 20_000)));
        var line = Line(view, 1);
        line.PieceCount.ShouldBeGreaterThan(100);
        line.ShapedPieceCount.ShouldBeLessThanOrEqualTo(4);
        var cjk = Line(view, 2);
        cjk.PieceCount.ShouldBeGreaterThan(100);
        cjk.ShapedPieceCount.ShouldBeLessThanOrEqualTo(5);

        // far to the right: the pieces there are shaped, the ones in between never are
        view.ViewModel!.ViewLayout.GetScrollWidth().ShouldBeGreaterThan(view.Bounds.Width);
        var x = view.CharWidth * 50_000;
        view.ViewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(x, 0), ScrollType.Immediate);
        Render(window);
        view.ScrollLeft.ShouldBe(x);
        line.ShapedPieceCount.ShouldBeLessThanOrEqualTo(8);
        cjk.ShapedPieceCount.ShouldBeLessThanOrEqualTo(10);

        // the x of a column far along is still exact
        (view.GetX(1, 50_001) + view.ScrollLeft - view.GutterWidth).ShouldBe(x, 0.01);
        line.GetDrawnClusterPositions().ShouldContain(p => p.Index == 50_000 && Math.Abs(p.X - x) < 0.01);
    }

    [AvaloniaFact]
    public void A_Theme_Change_Redraws_The_Layers_With_The_New_Brushes()
    {
        var (_, view, window) = Show("abc\n中文");
        var before = Counts(view);
        view.SelectionBrush = Avalonia.Media.Brushes.Red;
        view.CursorBrush = Avalonia.Media.Brushes.Green;
        view.LineNumberBrush = Avalonia.Media.Brushes.Blue;
        Render(window);
        var after = Counts(view);
        after.Background.ShouldBe(before.Background + 1);
        after.Cursors.ShouldBe(before.Cursors + 1);
        after.Gutter.ShouldBe(before.Gutter + 1);
        after.Text.ShouldBe(before.Text);

        Avalonia.Controls.Documents.TextElement.SetForeground(view, Avalonia.Media.Brushes.Orange);
        Render(window);
        Counts(view).Text.ShouldBe(after.Text + 1);
        view.TextCache!.Metrics.Foreground.ShouldBe(Avalonia.Media.Brushes.Orange);
    }
}

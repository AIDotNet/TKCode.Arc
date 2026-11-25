using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Decoration rendering: class names mapped to registered styles (backgrounds, whole lines, underlines, inline colors
/// and font styles, faded text), the markers' squiggles, and the gutter columns with margin decorations.
/// </summary>
public class CodeEditorDecorationTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Decorate(CodeEditor editor, params (TextRange Range, ModelDecorationOptions Options)[] decorations) =>
        editor.Model.DeltaDecorations([], [.. decorations.Select(d => new ModelDeltaDecoration(d.Range, d.Options))]);

    [AvaloniaFact]
    public void A_class_name_draws_its_background_over_the_range_and_whole_lines_span_the_text_area()
    {
        var (editor, view, window) = Show("alpha beta\ngamma\ndelta");
        var fill = new SolidColorBrush(Colors.Orange);
        editor.DecorationStyles.Register("test-range", new CodeEditorDecorationStyle { Background = DecorationBrush.From(fill) });
        editor.DecorationStyles.Register("test-line", new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.RangeHighlight });
        Decorate(editor,
            (new TextRange(1, 7, 1, 11), new ModelDecorationOptions("t") { ClassName = "test-range" }),
            (new TextRange(2, 1, 2, 1), new ModelDecorationOptions("t") { ClassName = "test-line", IsWholeLine = true }),
            (new TextRange(3, 1, 3, 3), new ModelDecorationOptions("t") { ClassName = "no-style" }));
        Render(window);

        var rects = view.GetDecorationRects(1, 3);
        var range = rects.Single(r => r.ClassName == "test-range");
        range.Kind.ShouldBe(CodeEditorView.DecorationDrawKind.Range);
        range.Fill.ShouldBe(fill);
        range.Bounds.X.ShouldBe(view.GetX(1, 7), 0.01);
        range.Bounds.Width.ShouldBe(view.GetX(1, 11) - view.GetX(1, 7), 0.01);
        range.Bounds.Y.ShouldBe(view.GetY(1), 0.01);
        range.Bounds.Height.ShouldBe(view.LineHeight);

        var line = rects.Single(r => r.ClassName == "test-line");
        line.Kind.ShouldBe(CodeEditorView.DecorationDrawKind.WholeLine);
        line.Fill.ShouldBe(view.RangeHighlightBrush);
        line.Fill.ShouldNotBeNull();
        line.Bounds.X.ShouldBe(view.GutterWidth, 0.01);
        line.Bounds.Right.ShouldBe(view.TextAreaRight, 0.01);

        rects.ShouldNotContain(r => r.ClassName == "no-style");
    }

    [AvaloniaFact]
    public void Inline_class_names_color_and_style_the_text()
    {
        var (editor, view, window) = Show("let value = 1;");
        var red = new SolidColorBrush(Colors.Red);
        editor.DecorationStyles.Register("test-inline", new CodeEditorDecorationStyle { Foreground = DecorationBrush.From(red), FontStyle = TokenFontStyle.Bold });
        editor.DecorationStyles.Register("test-faded", new CodeEditorDecorationStyle { Opacity = 0.5 });
        Decorate(editor,
            (new TextRange(1, 5, 1, 10), new ModelDecorationOptions("t") { InlineClassName = "test-inline" }),
            (new TextRange(1, 13, 1, 14), new ModelDecorationOptions("t") { InlineClassName = "test-faded" }));
        Render(window);

        var colors = view.GetLineColors(1, new TextRange(1, 1, 1, 15)).ShouldNotBeNull();
        var cache = view.TextCache!;
        var fallback = Brushes.Transparent;

        // "value" (offsets 4..8) is red and bold; "let " keeps the default color
        cache.GetBrush(colors.GetForegroundAt(4), fallback).ShouldBe(red);
        cache.GetBrush(colors.GetForegroundAt(8), fallback).ShouldBe(red);
        cache.GetBrush(colors.GetForegroundAt(0), fallback).ShouldNotBe(red);
        var styleOfValue = Enumerable.Range(0, colors.Count).First(i => colors.GetEndOffset(i) > 4);
        colors.GetFontStyle(styleOfValue).HasFlag(TokenFontStyle.Bold).ShouldBeTrue();

        // "1" is drawn faded: the color of its token at half the opacity
        var faded = cache.GetBrush(colors.GetForegroundAt(12), fallback).ShouldBeAssignableTo<ISolidColorBrush>()!;
        faded.Opacity.ShouldBe(0.5, 0.01);
    }

    [AvaloniaFact]
    public void Markers_show_as_squiggles_in_the_theme_colors()
    {
        var (editor, view, window) = Show("int x = y;\nreturn;");
        editor.MarkerService.ChangeOne("test", editor.Model.Uri,
        [
            new MarkerData { Severity = MarkerSeverity.Error, Message = "unknown y", StartLineNumber = 1, StartColumn = 9, EndLineNumber = 1, EndColumn = 10 },
            new MarkerData { Severity = MarkerSeverity.Warning, Message = "unused", StartLineNumber = 2, StartColumn = 1, EndLineNumber = 2, EndColumn = 7 },
        ]);
        Render(window);

        var underlines = view.GetDecorationRects(1, 2).Where(r => r.Kind == CodeEditorView.DecorationDrawKind.Underline).ToList();
        underlines.Count.ShouldBe(2);
        var error = underlines.Single(r => r.ViewLineNumber == 1);
        error.ClassName.ShouldBe(DecorationClassNames.EditorErrorDecoration);
        error.Underline.ShouldBe(DecorationUnderlineKind.Squiggly);
        error.Stroke.ShouldBe(view.ErrorBrush);
        error.Stroke.ShouldNotBeNull();
        error.Bounds.X.ShouldBe(view.GetX(1, 9), 0.01);
        underlines.Single(r => r.ViewLineNumber == 2).Stroke.ShouldBe(view.WarningBrush);
        view.DecorationsLayer.RenderCount.ShouldBeGreaterThan(0);

        // the hover can find the marker behind a decoration
        var decoration = editor.Model.GetAllDecorations().First(d => d.Options.ClassName == DecorationClassNames.EditorErrorDecoration);
        editor.MarkerDecorationsService.GetMarker(editor.Model.Uri, decoration)!.Message.ShouldBe("unknown y");

        editor.MarkerService.ChangeOne("test", editor.Model.Uri, []);
        Render(window);
        view.GetDecorationRects(1, 2).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_registered_style_replaces_a_built_in_one_until_disposed()
    {
        var (editor, view, window) = Show("abc");
        Decorate(editor, (new TextRange(1, 1, 1, 3), new ModelDecorationOptions("t") { ClassName = DecorationClassNames.EditorErrorDecoration }));
        var registration = editor.DecorationStyles.Register(DecorationClassNames.EditorErrorDecoration, new CodeEditorDecorationStyle { Underline = DecorationUnderlineKind.Solid, UnderlineBrush = CodeEditorThemeColor.Info });
        Render(window);
        view.GetDecorationRects(1, 1).Single().Underline.ShouldBe(DecorationUnderlineKind.Solid);

        registration.Dispose();
        view.GetDecorationRects(1, 1).Single().Underline.ShouldBe(DecorationUnderlineKind.Squiggly);
    }

    [AvaloniaFact]
    public void Without_columns_the_gutter_is_the_line_numbers_and_columns_go_around_them()
    {
        var (editor, view, window) = Show("a\nb\nc");
        editor.Folding = false; // the folding column is a column too
        Render(window);
        var plain = view.GetGutterLayout();
        plain.Columns.ShouldBeEmpty();
        plain.Width.ShouldBe(view.GutterLeadingPadding + (2 * view.CharWidth) + view.GutterPadding, 0.01);
        plain.LineNumbersRight.ShouldBe(plain.Width - view.GutterPadding, 0.01);

        editor.Gutter.GlyphMargin.IsVisible = true;
        var folding = new CodeEditorGutterColumn("folding", CodeEditorGutter.FoldingOrder, 14);
        editor.Gutter.Add(folding);
        editor.Gutter.LinesDecorations.IsVisible = true;
        Render(window);

        var layout = view.GetGutterLayout();
        layout.Columns.Select(c => c.Column.Id).ShouldBe([CodeEditorGutter.GlyphMarginId, CodeEditorGutter.LinesDecorationsId, "folding"]);
        layout.Columns[0].X.ShouldBe(0);
        layout.LineNumbersLeft.ShouldBe(editor.Gutter.GlyphMargin.Width);
        layout.Columns[1].X.ShouldBe(layout.LineNumbersRight, 0.01);
        layout.Columns[2].X.ShouldBe(layout.Columns[1].X + editor.Gutter.LinesDecorations.Width, 0.01);
        layout.Width.ShouldBe(plain.Width + editor.Gutter.GlyphMargin.Width + editor.Gutter.LinesDecorations.Width + 14, 0.01);

        // the text moved right with the gutter
        view.GutterWidth.ShouldBe(layout.Width);
        view.GetX(1, 1).ShouldBe(layout.Width, 0.01);

        editor.ShowLineNumbers = false;
        view.GetGutterLayout().Width.ShouldBe(editor.Gutter.GlyphMargin.Width + editor.Gutter.LinesDecorations.Width + 14 + view.GutterPadding, 0.01);
    }

    [AvaloniaFact]
    public void Gutter_columns_draw_each_line_and_take_the_press()
    {
        var (editor, view, window) = Show("one\ntwo\nthree");
        var drawn = new List<CodeEditorGutterLine>();
        var pressed = new List<int>();
        var column = new CodeEditorGutterColumn("test", CodeEditorGutter.FoldingOrder, 16)
        {
            Draw = (_, line) => drawn.Add(line),
            PointerPressed = e =>
            {
                pressed.Add(e.ModelLineNumber);
                return true;
            },
        };
        editor.Gutter.Add(column);
        Render(window);

        drawn.Select(l => l.ModelLineNumber).Distinct().ShouldBe([1, 2, 3]);
        var cell = drawn.Last(l => l.ModelLineNumber == 2).Bounds;
        cell.Width.ShouldBe(16);
        cell.Height.ShouldBe(view.LineHeight);

        // a press on the column goes to it and leaves the selection alone
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var downs = new List<CodeEditorMouseTargetType>();
        editor.MouseDown += e => downs.Add(e.Target.Type);
        window.MouseDown(origin + new Point(cell.X + 8, cell.Y + (cell.Height / 2)), MouseButton.Left);
        downs.ShouldBe([CodeEditorMouseTargetType.GutterLineDecorations]);
        window.MouseUp(origin + new Point(cell.X + 8, cell.Y + (cell.Height / 2)), MouseButton.Left);
        pressed.ShouldBe([2]);
        view.ViewModel!.GetSelection().IsEmpty.ShouldBeTrue();
        view.ViewModel.GetPosition().ShouldBe(new TextPosition(1, 1));

        view.GetMouseTargetAt(new Point(cell.X + 8, cell.Y + 2)).ShouldSatisfyAllConditions(
            t => t.Type.ShouldBe(CodeEditorMouseTargetType.GutterLineDecorations),
            t => t.Detail.ShouldBe("test"),
            t => t.Position!.Value.LineNumber.ShouldBe(2));
    }

    [AvaloniaFact]
    public void Glyph_margin_and_lines_decorations_draw_the_margin_class_names()
    {
        var (editor, view, window) = Show("one\ntwo\nthree");
        editor.Gutter.GlyphMargin.IsVisible = true;
        editor.Gutter.LinesDecorations.IsVisible = true;
        editor.DecorationStyles.Register("test-glyph", new CodeEditorDecorationStyle { Glyph = Geometry.Parse("M0,0 L16,8 L0,16 Z"), GlyphBrush = CodeEditorThemeColor.Error });
        editor.DecorationStyles.Register("test-bar", new CodeEditorDecorationStyle { MarginBackground = CodeEditorThemeColor.Warning, MarginWidth = 3 });
        Decorate(editor,
            (new TextRange(2, 1, 2, 1), new ModelDecorationOptions("t") { GlyphMarginClassName = "test-glyph" }),
            (new TextRange(1, 1, 3, 1), new ModelDecorationOptions("t") { LinesDecorationsClassName = "test-bar" }));
        var before = view.GutterLayer.RenderCount;
        Render(window);
        view.GutterLayer.RenderCount.ShouldBeGreaterThan(before);

        // drawn without throwing into a bitmap, and the targets say where the pointer is
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 200)))
        {
            bitmap.Render(view);
        }

        var layout = view.GetGutterLayout();
        view.GetMouseTargetAt(new Point(2, view.GetY(2) + 2)).Type.ShouldBe(CodeEditorMouseTargetType.GutterGlyphMargin);
        view.GetMouseTargetAt(new Point(layout.LineNumbersLeft + 2, view.GetY(1) + 2)).Type.ShouldBe(CodeEditorMouseTargetType.GutterLineNumbers);
        view.GetMouseTargetAt(new Point(view.GetX(1, 2), view.GetY(1) + 2)).Type.ShouldBe(CodeEditorMouseTargetType.ContentText);
        view.GetMouseTargetAt(new Point(view.Bounds.Width - 5, view.GetY(1) + 2)).Type.ShouldBe(CodeEditorMouseTargetType.ContentEmpty);
    }
}

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;

namespace TKCode.Arc.Tests.Editor;

/// <summary>Indent guides in the control (task 4.7).</summary>
public class CodeEditorIndentGuideTests
{
    private const string Code = "class A {\n    void F() {\n        x();\n\n        y();\n    }\n}";

    private static (CodeEditor Editor, CodeEditorView View) Show(string text, string languageId = "csharp")
    {
        var editor = new CodeEditor { Text = text, LanguageId = languageId, Width = 500, Height = 200 };
        var window = TestHelpers.Show(editor);
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        return (editor, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    private static List<CodeEditorView.IndentGuideLine> Guides(CodeEditor editor, CodeEditorView view, TextPosition cursor) =>
        view.GetIndentGuides(1, editor.Model.GetLineCount(), cursor);

    [AvaloniaFact]
    public void Each_indent_level_has_a_guide_and_blank_lines_take_the_level_around_them()
    {
        var (editor, view) = Show(Code);
        var guides = Guides(editor, view, new TextPosition(1, 1));
        guides.GroupBy(g => g.LineNumber).ToDictionary(g => g.Key, g => g.Count())
            .ShouldBe(new Dictionary<int, int> { [2] = 1, [3] = 2, [4] = 2, [5] = 2, [6] = 1 });

        // the second level is one indent size (4 spaces) right of the first
        var line3 = guides.Where(g => g.LineNumber == 3).Select(g => g.X).Order().ToArray();
        (line3[1] - line3[0]).ShouldBe(4 * view.CharWidth, 0.01);
    }

    [AvaloniaFact]
    public void The_guide_of_the_block_with_the_cursor_is_active()
    {
        var (editor, view) = Show(Code);
        var guides = Guides(editor, view, new TextPosition(3, 9));
        var active = guides.Where(g => g.IsActive).ToArray();
        active.Select(g => g.LineNumber).ShouldBe([3, 4, 5]);
        active.Select(g => g.X).Distinct().Count().ShouldBe(1);
        active[0].X.ShouldBe(guides.Where(g => g.LineNumber == 3).Max(g => g.X));

        // on the line that opens a block, the block's guide is active (upstream: the child scope)
        Guides(editor, view, new TextPosition(2, 5)).Where(g => g.IsActive).Select(g => g.LineNumber).ShouldBe([3, 4, 5]);
    }

    [AvaloniaFact]
    public void The_indent_size_spaces_the_guides()
    {
        var (editor, view) = Show("a\n  b\n    c");
        editor.TabSize = 2;
        Dispatcher.UIThread.RunJobs();
        var guides = Guides(editor, view, new TextPosition(1, 1));
        guides.Count(g => g.LineNumber == 3).ShouldBe(2);
        var xs = guides.Where(g => g.LineNumber == 3).Select(g => g.X).Order().ToArray();
        (xs[1] - xs[0]).ShouldBe(2 * view.CharWidth, 0.01);
    }

    [AvaloniaFact]
    public void Turning_the_guides_off_draws_none()
    {
        var (editor, view) = Show(Code);
        editor.ShowIndentGuides = false;
        Guides(editor, view, new TextPosition(1, 1)).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Python_blank_lines_belong_to_the_block_below()
    {
        // off-side languages: a blank line between a block and a lower one takes the level below
        var (editor, view) = Show("def f():\n    x = 1\n\ny = 2", "python");
        Guides(editor, view, new TextPosition(1, 1)).Select(g => g.LineNumber).ShouldBe([2]);
    }
}


using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Tests.Editor;

/// <summary>The editor operations features call (upstream's ICodeEditor: selections, reveal, executeEdits, positions).</summary>
public class CodeEditorApiTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    [AvaloniaFact]
    public void Selections_edits_and_undo()
    {
        var (editor, _, _) = Show("hello world");
        editor.SetSelection(new TextSelection(1, 7, 1, 12));
        editor.GetSelection().ShouldBe(new TextSelection(1, 7, 1, 12));

        editor.ExecuteEdits("test", [EditOperation.Replace(new TextRange(1, 7, 1, 12), "there")], [new TextSelection(1, 12, 1, 12)]).ShouldBeTrue();
        editor.Text.ShouldBe("hello there");
        editor.GetPosition().ShouldBe(new TextPosition(1, 12));

        editor.Model.Undo();
        editor.Text.ShouldBe("hello world");

        editor.IsReadOnly = true;
        editor.ExecuteEdits("test", [EditOperation.Insert(new TextPosition(1, 1), "x")]).ShouldBeFalse();
        editor.Text.ShouldBe("hello world");
    }

    [AvaloniaFact]
    public void Reveal_in_center_and_visible_positions()
    {
        var (editor, view, window) = Show(string.Join('\n', Enumerable.Range(1, 200).Select(i => "line " + i)));
        editor.GetScrolledVisiblePosition(new TextPosition(100, 1)).ShouldBeNull();

        editor.RevealLine(100, VerticalRevealType.Center, ScrollType.Immediate);
        Dispatcher.UIThread.RunJobs();
        var visible = editor.GetScrolledVisiblePosition(new TextPosition(100, 3)).ShouldNotBeNull();
        visible.Top.ShouldBe((view.Bounds.Height - view.LineHeight) / 2, view.LineHeight);
        visible.Left.ShouldBe(view.GetX(100, 3), 0.01);
        visible.Height.ShouldBe(view.LineHeight);
        editor.GetTopForLineNumber(100).ShouldBe(99 * view.LineHeight, 0.01);
    }
}

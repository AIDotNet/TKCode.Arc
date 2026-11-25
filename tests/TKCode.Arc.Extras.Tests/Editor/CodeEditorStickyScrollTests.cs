// Tests of sticky scroll in the code editor (the port of vscode's src/vs/editor/contrib/stickyScroll/browser/
// stickyScrollController.ts and stickyScrollWidget.ts, commit b57d5c9e): the widget at the top shows the scopes around
// the top of the view, follows scrolling and the options, and a click reveals the line. Upstream's stickyScroll.test.ts
// is ported in the engine tests (StickyScrollTests); these are own.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorStickyScrollTests
{
    private static readonly string Code = string.Join('\n', Enumerable.Range(0, 1).SelectMany(_ => new[]
    {
        "class Outer {",
        "    void First() {",
        "        a();",
        "        b();",
        "        c();",
        "        d();",
        "        e();",
        "        f();",
        "        g();",
        "        h();",
        "    }",
        "    void Second() {",
        "        if (x) {",
        "            i();",
        "            j();",
        "            k();",
        "            l();",
        "            m();",
        "            n();",
        "            o();",
        "        }",
        "    }",
        "}",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
    }));

    private static (CodeEditor Editor, Avalonia.Controls.Window Window, StickyScrollController Controller) Show(string text, int height = 200)
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = height, LanguageId = "stickylang" };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        var controller = StickyScrollController.Get(editor).ShouldNotBeNull();
        Pump(controller.UpdateNow());
        return (editor, window, controller);
    }

    private static void Pump(Task task)
    {
        TestHelpers.PumpUntil(() => task.IsCompleted, 5000).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
    }

    private static void ScrollTo(CodeEditor editor, Avalonia.Controls.Window window, double scrollTop)
    {
        editor.ViewModel!.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: scrollTop), ScrollType.Immediate);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void The_scopes_around_the_top_stick_while_scrolling()
    {
        var (editor, window, controller) = Show(Code);
        var lineHeight = editor.View!.LineHeight;
        controller.WidgetState.StartLineNumbers.ShouldBeEmpty();
        controller.Widget.IsVisible.ShouldBeFalse();

        // line 4 at the top: inside First, inside Outer
        ScrollTo(editor, window, (3 * lineHeight) + 1);
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 2]);
        controller.Widget.IsVisible.ShouldBeTrue();
        controller.Widget.Bounds.Height.ShouldBe((2 * lineHeight) + 1, 0.5);
        controller.Widget.Bounds.Top.ShouldBe(0);
        controller.Widget.Bounds.Width.ShouldBeGreaterThan(400);
        editor.ContextKeyService.GetContextKeyValue("stickyScrollVisible").ShouldBe(true);

        // line 15 at the top: Outer, Second, the if block
        ScrollTo(editor, window, (14 * lineHeight) + 1);
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 12, 13]);

        // the widget draws without throwing
        window.CaptureRenderedFrame()?.Dispose();

        // back at the top: nothing sticks
        ScrollTo(editor, window, 0);
        controller.WidgetState.StartLineNumbers.ShouldBeEmpty();
        editor.ContextKeyService.GetContextKeyValue("stickyScrollVisible").ShouldBe(false);
        window.Close();
    }

    [AvaloniaFact]
    public void Document_symbols_give_the_scopes_when_a_provider_has_them()
    {
        var editor = new CodeEditor { Text = Code, Width = 500, Height = 200, LanguageId = "stickylang" };
        using var registration = editor.LanguageFeatures.DocumentSymbolProvider.Register("stickylang", new Symbols());
        var window = TestHelpers.Show(editor);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        var controller = StickyScrollController.Get(editor).ShouldNotBeNull();
        Pump(controller.UpdateNow());
        controller.CandidateProvider.Model!.Element!.Children.Single().Range!.Value.StartLineNumber.ShouldBe(12);

        // the symbols know only Second: inside it, only Second's line sticks
        ScrollTo(editor, window, (14 * editor.View!.LineHeight) + 1);
        controller.WidgetState.StartLineNumbers.ShouldBe([12]);
        window.Close();
    }

    [AvaloniaFact]
    public void The_max_line_count_and_the_option_are_followed()
    {
        var (editor, window, controller) = Show(Code, height: 400);
        var lineHeight = editor.View!.LineHeight;
        ScrollTo(editor, window, (14 * lineHeight) + 1);
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 12, 13]);

        editor.StickyScrollMaxLineCount = 2;
        Dispatcher.UIThread.RunJobs();
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 12]);

        editor.StickyScroll = false;
        Dispatcher.UIThread.RunJobs();
        controller.Widget.IsVisible.ShouldBeFalse();
        controller.Widget.Parent.ShouldBeNull();

        editor.StickyScroll = true;
        Pump(controller.LastUpdate);
        Dispatcher.UIThread.RunJobs();
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 12]);
        window.Close();
    }

    [AvaloniaFact]
    public void A_click_on_a_sticky_line_reveals_it_and_puts_the_cursor_there()
    {
        var (editor, window, controller) = Show(Code);
        var lineHeight = editor.View!.LineHeight;
        ScrollTo(editor, window, (14 * lineHeight) + 1);
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 12, 13]);

        // the second sticky line (Second), on its text
        var origin = controller.Widget.TranslatePoint(new Point(0, 0), window)!.Value;
        var x = editor.View.GutterWidth + (editor.View.CharWidth * 9.5);
        var point = new Point(origin.X + x, origin.Y + (lineHeight * 1.5));
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        editor.GetPosition()!.Value.LineNumber.ShouldBe(12);
        editor.GetPosition()!.Value.Column.ShouldBe(10);
        // line 12 is now in view
        var top = editor.GetTopForLineNumber(12);
        var scrollTop = editor.ViewModel!.ViewLayout.GetCurrentScrollTop();
        top.ShouldBeGreaterThanOrEqualTo(scrollTop - 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Edits_update_the_scopes_after_the_debounce()
    {
        var (editor, window, controller) = Show(Code);
        var lineHeight = editor.View!.LineHeight;
        ScrollTo(editor, window, (3 * lineHeight) + 1);
        controller.WidgetState.StartLineNumbers.ShouldBe([1, 2]);

        // un-indent the body of First: its scope is gone
        var edits = Enumerable.Range(3, 8).Select(line => EditOperation.Replace(new TextRange(line, 1, line, 5), string.Empty)).ToList();
        editor.ExecuteEdits("test", edits);
        TestHelpers.PumpUntil(() => controller.CandidateProvider.GetVersionId() == editor.Model.GetVersionId(), 5000).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
        controller.RenderStickyScroll();
        controller.WidgetState.StartLineNumbers.ShouldBe([1]);
        window.Close();
    }

    private sealed class Symbols : IDocumentSymbolProvider
    {
        public Task<IReadOnlyList<DocumentSymbol>?> ProvideDocumentSymbols(TextModel model, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<DocumentSymbol>?>(
            [
                new DocumentSymbol("Second", string.Empty, SymbolKind.Method, new TextRange(12, 1, 22, 6), new TextRange(12, 10, 12, 16)),
            ]);
    }
}

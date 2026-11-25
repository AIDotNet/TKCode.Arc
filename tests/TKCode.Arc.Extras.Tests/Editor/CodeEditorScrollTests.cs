using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Editor;

/// <summary>The code editor's scroll bars, the wheel and smooth scrolling (task 3.3).</summary>
public class CodeEditorScrollTests
{
    private static (CodeEditor Editor, CodeEditorView View, Avalonia.Controls.Window Window) Show(string text, bool smooth = false)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200, SmoothScrolling = smooth };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    private static string Lines(int count) => string.Join("\n", Enumerable.Range(1, count).Select(i => "line " + i));

    private static ScrollBar Bar(CodeEditor editor, string name) => TestHelpers.Descendant<ScrollBar>(editor, name);

    private static Point Center(CodeEditorView view, Avalonia.Controls.Window window) =>
        view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public void The_Horizontal_Bar_Hides_While_The_Lines_Fit()
    {
        // the vertical bar stays: the last line can scroll up to the top (Monaco's scrollBeyondLastLine)
        var (editor, _, _) = Show("short\ntext");
        Bar(editor, CodeEditor.PartHorizontalScrollBar).IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Vertical_Bar_Follows_And_Drives_The_Scroll_Position()
    {
        var (editor, view, window) = Show(Lines(200));
        var bar = Bar(editor, CodeEditor.PartVerticalScrollBar);
        bar.IsVisible.ShouldBeTrue();
        var layout = view.ViewModel!.ViewLayout;
        bar.ViewportSize.ShouldBe(view.Bounds.Height);
        bar.Maximum.ShouldBe(layout.GetScrollHeight() - view.Bounds.Height, 0.5);

        // the keyboard reveals the end: the bar follows
        var ctrl = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(OperatingSystem.IsMacOS() ? PhysicalKey.ArrowDown : PhysicalKey.End, ctrl);
        Dispatcher.UIThread.RunJobs();
        layout.GetCurrentScrollTop().ShouldBeGreaterThan(0);
        bar.Value.ShouldBe(layout.GetCurrentScrollTop());

        // moving the bar scrolls the view
        bar.Value = 100;
        layout.GetCurrentScrollTop().ShouldBe(100);
    }

    [AvaloniaFact]
    public void A_Long_Line_Shows_The_Horizontal_Bar_Beside_The_Line_Numbers()
    {
        var (editor, view, _) = Show(new string('x', 300));
        Dispatcher.UIThread.RunJobs();
        var bar = Bar(editor, CodeEditor.PartHorizontalScrollBar);
        bar.IsVisible.ShouldBeTrue();
        bar.Margin.Left.ShouldBe(view.GutterWidth);
        bar.Value = 50;
        view.ViewModel!.ViewLayout.GetCurrentScrollLeft().ShouldBe(50);
    }

    [AvaloniaFact]
    public void The_Wheel_Scrolls_And_Shift_Turns_It_Sideways()
    {
        var (editor, view, window) = Show(new string('x', 300) + "\n" + Lines(200));
        Dispatcher.UIThread.RunJobs();
        var layout = view.ViewModel!.ViewLayout;
        var point = Center(view, window);
        window.MouseWheel(point, new Vector(0, -1));
        layout.GetCurrentScrollTop().ShouldBe(view.LineHeight * 3, 0.5);
        Bar(editor, CodeEditor.PartVerticalScrollBar).Value.ShouldBe(layout.GetCurrentScrollTop());

        window.MouseWheel(point, new Vector(0, -1), RawInputModifiers.Shift);
        layout.GetCurrentScrollLeft().ShouldBe(view.CharWidth * 3, 0.5);
    }

    [AvaloniaFact]
    public void Smooth_Scrolling_Animates_To_The_Target()
    {
        var (_, view, window) = Show(Lines(200), smooth: true);
        var layout = view.ViewModel!.ViewLayout;
        window.MouseWheel(Center(view, window), new Vector(0, -2));
        var target = view.LineHeight * 6;
        layout.GetScrollable().GetFutureScrollPosition().ScrollTop.ShouldBe(target, 0.5);
        layout.GetCurrentScrollTop().ShouldBeLessThan(target);
        TestHelpers.PumpUntil(() =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            return Math.Abs(layout.GetCurrentScrollTop() - target) < 0.5;
        }, 3000).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Bar_That_Appears_After_The_Lines_Are_Drawn_Gets_Laid_Out()
    {
        // the width of the lines is learnt while they are drawn, after the first layout measured the bar hidden
        var (editor, view, window) = Show(string.Join("\n", Enumerable.Range(1, 40).Select(i => "    var value" + i + " = Compute(" + i + "); // a comment that runs on")));
        for (var i = 0; i < 3; i++)
        {
            window.CaptureRenderedFrame()?.Dispose();
            Dispatcher.UIThread.RunJobs();
        }

        var bar = Bar(editor, CodeEditor.PartHorizontalScrollBar);
        bar.IsVisible.ShouldBeTrue();
        bar.Bounds.Height.ShouldBeGreaterThan(0);
        bar.Bounds.Width.ShouldBeGreaterThan(0);
        view.ViewModel!.ViewLayout.GetScrollWidth().ShouldBeGreaterThan(view.Bounds.Width - view.GutterWidth);
    }
}

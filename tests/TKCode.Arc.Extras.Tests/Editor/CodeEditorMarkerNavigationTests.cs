// Next / previous problem and the problem widget (upstream's gotoError.ts and gotoErrorWidget.ts, commit b57d5c9e);
// written for this port (upstream has no tests for them). Also the squiggles of markers in a real editor.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform.Markers;
using static TKCode.Arc.Tests.Editor.HoverTestSupport;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorMarkerNavigationTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window) Show(string text, double height = 260)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = height };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    private static void Click(Control control) =>
        control.RaiseEvent(new PointerPressedEventArgs(control, new Pointer(0, PointerType.Mouse, true), control, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

    private static string Lines(int count) => string.Join('\n', Enumerable.Range(1, count).Select(i => "line " + i));

    private static MarkerData Problem(MarkerSeverity severity, int line, string message) => new()
    {
        Severity = severity,
        Message = message,
        Source = "lint",
        StartLineNumber = line,
        StartColumn = 1,
        EndLineNumber = line,
        EndColumn = 5,
    };

    [AvaloniaFact]
    public void F8_opens_the_problem_widget_after_the_next_problem_and_walks_the_problems()
    {
        var (editor, view, window) = Show(Lines(30));
        editor.MarkerService.ChangeOne("lint", editor.Model.Uri,
        [
            Problem(MarkerSeverity.Warning, 3, "warn here"),
            Problem(MarkerSeverity.Error, 6, "error here"),
            Problem(MarkerSeverity.Hint, 8, "hint, not listed"),
        ]);
        editor.SetPosition(new TextPosition(1, 1));

        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);
        Render(window);

        var controller = MarkerController.Get(editor).ShouldNotBeNull();
        var widget = controller.Widget.ShouldNotBeNull();
        // by severity: the error first
        widget.Marker!.Message.ShouldBe("error here");
        widget.Heading.Detail.ShouldBe("1 of 2 problems");
        editor.ContextKeyService.GetContextKeyValue("markersNavigationVisible").ShouldBe(true);
        var zone = editor.GetViewZone(widget.ZoneId!).ShouldNotBeNull();
        zone.AfterLineNumber.ShouldBe(6);
        zone.HeightInLines.ShouldBe(4);
        editor.GetViewZoneBounds(widget.ZoneId!).ShouldNotBeNull().Top.ShouldBe(view.GetY(7) - zone.HeightInLines!.Value * view.LineHeight, 0.5);
        TextOf(widget.Surface).ShouldContain("error here  lint");

        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);
        Render(window);
        controller.Widget!.Marker!.Message.ShouldBe("warn here");
        controller.Widget.Heading.Detail.ShouldBe("2 of 2 problems");
        editor.GetViewZone(controller.Widget.ZoneId!)!.AfterLineNumber.ShouldBe(3);

        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.Shift);
        Render(window);
        controller.Widget!.Marker!.Message.ShouldBe("error here");

        // Escape closes it
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);
        controller.Widget.ShouldBeNull();
        editor.ContextKeyService.GetContextKeyValue("markersNavigationVisible").ShouldBe(false);
    }

    [AvaloniaFact]
    public void Alt_F8_starts_at_the_problem_after_the_cursor()
    {
        var (editor, _, window) = Show(Lines(20));
        editor.MarkerService.ChangeOne("lint", editor.Model.Uri,
        [
            Problem(MarkerSeverity.Error, 2, "first"),
            Problem(MarkerSeverity.Error, 10, "second"),
        ]);
        editor.SetPosition(new TextPosition(5, 1));

        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.Alt);
        Render(window);
        MarkerController.Get(editor)!.Widget!.Marker!.Message.ShouldBe("second");

        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.Alt | RawInputModifiers.Shift);
        Render(window);
        MarkerController.Get(editor)!.Widget!.Marker!.Message.ShouldBe("first");
    }

    [AvaloniaFact]
    public void The_widget_buttons_navigate_and_close_and_a_changed_problem_updates_it()
    {
        var (editor, _, window) = Show(Lines(20));
        editor.MarkerService.ChangeOne("lint", editor.Model.Uri,
        [
            Problem(MarkerSeverity.Error, 2, "first"),
            Problem(MarkerSeverity.Error, 4, "second"),
        ]);
        var controller = MarkerController.Get(editor)!;
        controller.Navigate(next: true, multiFile: false);
        Render(window);
        var widget = controller.Widget!;
        widget.Marker!.Message.ShouldBe("first");

        // the problem at the widget changes: the widget follows
        editor.MarkerService.ChangeOne("lint", editor.Model.Uri,
        [
            Problem(MarkerSeverity.Error, 2, "first, reworded"),
            Problem(MarkerSeverity.Error, 4, "second"),
        ]);
        Render(window);
        TextOf(widget.Surface).ShouldContain("first, reworded");

        // the change reset the list (upstream): next starts from the cursor again, then moves on
        var next = widget.Surface.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Next");
        Click(next);
        Render(window);
        controller.Widget!.Marker!.Message.ShouldBe("first, reworded");
        Click(controller.Widget.Surface.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Next"));
        Render(window);
        controller.Widget!.Marker!.Message.ShouldBe("second");
        controller.Widget.Heading.Detail.ShouldBe("2 of 2 problems");

        var close = controller.Widget.Surface.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Close");
        Click(close);
        Render(window);
        controller.Widget.ShouldBeNull();
    }

    [AvaloniaFact]
    public void View_problem_in_the_hover_opens_the_problem_widget()
    {
        var (editor, view, window) = Show(Lines(10));
        editor.MarkerService.ChangeOne("lint", editor.Model.Uri, [Problem(MarkerSeverity.Warning, 3, "look here")]);
        var hover = ContentHoverController.Get(editor)!;
        hover.ShowContentHover(new TextRange(3, 2, 3, 2), TKCode.Arc.Editor.Contrib.Hover.HoverStartMode.Immediate, TKCode.Arc.Editor.Contrib.Hover.HoverStartSource.Keyboard, false);
        Render(window);
        hover.IsHoverVisible.ShouldBeTrue();

        var action = hover.Widget!.RenderedHover!.StatusBar!.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_ViewProblem");
        Click(action);
        Render(window);

        hover.IsHoverVisible.ShouldBeFalse();
        MarkerController.Get(editor)!.Widget!.Marker!.Message.ShouldBe("look here");
    }

    [AvaloniaFact]
    public void Markers_draw_squiggles_in_their_severity_style()
    {
        var (editor, view, window) = Show("let a = b;\nlet c = d;");
        editor.MarkerService.ChangeOne("lint", editor.Model.Uri,
        [
            new MarkerData { Severity = MarkerSeverity.Error, Message = "e", StartLineNumber = 1, StartColumn = 9, EndLineNumber = 1, EndColumn = 10 },
            new MarkerData { Severity = MarkerSeverity.Warning, Message = "w", StartLineNumber = 2, StartColumn = 5, EndLineNumber = 2, EndColumn = 6 },
        ]);
        Render(window);

        var rects = view.GetDecorationRects(1, 2);
        rects.ShouldContain(r => r.ClassName.Contains("squiggly-error"));
        rects.ShouldContain(r => r.ClassName.Contains("squiggly-warning"));
    }
}

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Services;
using TextSelection = TKCode.Arc.Editor.TextSelection;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Links (Ctrl+hover and Ctrl+click), drag and drop of selected text, the selection anchor and the unicode
/// highlighter in the control (phase 5), headless.
/// </summary>
public class CodeEditorLinkAndDndTests
{
    private static readonly RawInputModifiers CtrlCmd = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    // the modifier that copies instead of moving dragged text: Alt on macOS, Ctrl elsewhere
    private static readonly RawInputModifiers DragCopy = OperatingSystem.IsMacOS() ? RawInputModifiers.Alt : RawInputModifiers.Control;

    private static (CodeEditor Editor, Avalonia.Controls.Window Window, CodeEditorView View) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = 240 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    private static Point At(Avalonia.Controls.Window window, CodeEditorView view, int line, int column)
    {
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var x = origin.X + view.GutterWidth + (view.CharWidth * (column - 1)) + 1 - view.ScrollLeft;
        var y = origin.Y + (view.LineHeight * (line - 0.5)) - view.ScrollTop;
        return new Point(x, y);
    }

    private static string[] Classes(CodeEditor editor, string className) =>
        editor.Model.GetAllDecorations()
            .Where(d => d.Options.InlineClassName == className || d.Options.ClassName == className)
            .Select(static d => d.Range.ToString())
            .ToArray();

    private static LinkDetector WaitForLinks(CodeEditor editor, int count)
    {
        TestHelpers.PumpUntil(() => LinkDetector.Get(editor) is { } d && d.Links.Count == count).ShouldBeTrue();
        return LinkDetector.Get(editor)!;
    }

    // ---- links

    [AvaloniaFact]
    public void Urls_In_The_Text_Are_Underlined_As_Links()
    {
        var (editor, _, _) = Show("see https://example.com/a for more\nand http://b.example");
        WaitForLinks(editor, 2);

        Classes(editor, LinkDetector.LinkClassName).ShouldBe(
            [new TextRange(1, 5, 1, 26).ToString(), new TextRange(2, 5, 2, 21).ToString()]);
    }

    [AvaloniaFact]
    public void Links_Are_Computed_Again_After_An_Edit()
    {
        var (editor, window, _) = Show("x");
        WaitForLinks(editor, 0);

        editor.SetPosition(new TextPosition(1, 2));
        window.KeyTextInput(" https://later.example");
        WaitForLinks(editor, 1).Links[0].Url.ShouldBe(new Uri("https://later.example"));
    }

    [AvaloniaFact]
    public void Ctrl_Hover_Activates_The_Link_And_Ctrl_Click_Opens_It_Through_The_Event()
    {
        var (editor, window, view) = Show("open https://example.com/x now");
        var detector = WaitForLinks(editor, 1);
        var opened = new List<CodeEditorLinkOpeningEventArgs>();
        editor.LinkOpening += (_, e) =>
        {
            e.Handled = true;
            opened.Add(e);
        };

        var point = At(window, view, 1, 12);
        window.MouseMove(point, CtrlCmd);
        detector.ActiveLinkRange.ShouldBe(new TextRange(1, 6, 1, 27));
        Classes(editor, LinkDetector.ActiveLinkClassName).ShouldHaveSingleItem();

        // without the modifier it is a plain link again
        window.MouseMove(point, RawInputModifiers.None);
        detector.ActiveLinkRange.ShouldBeNull();

        // a plain click opens nothing; a Ctrl+click does
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        opened.ShouldBeEmpty();

        window.MouseDown(point, MouseButton.Left, CtrlCmd);
        window.MouseUp(point, MouseButton.Left, CtrlCmd);
        Dispatcher.UIThread.RunJobs();
        var e = opened.ShouldHaveSingleItem();
        e.Uri.ShouldBe(new Uri("https://example.com/x"));
        e.Target.ShouldBe("https://example.com/x");
        e.FromUserGesture.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Open_Link_Action_Opens_The_Link_At_The_Cursor()
    {
        var (editor, _, _) = Show("x http://a.example y");
        WaitForLinks(editor, 1);
        var opened = new List<string>();
        editor.LinkOpening += (_, e) =>
        {
            e.Handled = true;
            opened.Add(e.Target);
        };

        editor.SetPosition(new TextPosition(1, 8));
        editor.Trigger("api", "editor.action.openLink", null);
        Dispatcher.UIThread.RunJobs();
        opened.ShouldBe(["http://a.example"]);
        opened.Clear();

        editor.SetPosition(new TextPosition(1, 1));
        editor.Trigger("api", "editor.action.openLink", null);
        Dispatcher.UIThread.RunJobs();
        opened.ShouldBeEmpty();
    }

    // ---- drag and drop

    [AvaloniaFact]
    public void Dragging_A_Selection_Moves_Its_Text_With_A_Drop_Marker_On_The_Way()
    {
        var (editor, window, view) = Show("hello world\nsecond line");
        editor.SetSelection(new TextSelection(1, 1, 1, 6));
        var dnd = TestHelpers.PumpUntil(() => DragAndDropController.Get(editor) is not null) ? DragAndDropController.Get(editor)! : null;
        dnd.ShouldNotBeNull();

        window.MouseDown(At(window, view, 1, 3), MouseButton.Left, RawInputModifiers.None);
        dnd.IsDragging.ShouldBeTrue();
        editor.GetSelection()!.Value.ToString().ShouldBe(new TextSelection(1, 1, 1, 6).ToString()); // the press keeps the selection

        window.MouseMove(At(window, view, 2, 7), RawInputModifiers.LeftMouseButton);
        dnd.DropMarkerPosition.ShouldBe(new TextPosition(2, 7));
        view.GetDecorationRects(2, 2).ShouldContain(r => r.ClassName == DragAndDropController.DropTargetClassName && r.Fill != null);

        window.MouseUp(At(window, view, 2, 7), MouseButton.Left, RawInputModifiers.None);
        editor.Text.ShouldBe(" world\nsecondhello line");
        editor.GetSelection()!.Value.ToString().ShouldBe(new TextSelection(2, 7, 2, 12).ToString());
        dnd.DropMarkerPosition.ShouldBeNull();
        dnd.IsDragging.ShouldBeFalse();

        // one undo step
        editor.Trigger("keyboard", "undo", null);
        editor.Text.ShouldBe("hello world\nsecond line");
    }

    [AvaloniaFact]
    public void Dragging_With_The_Copy_Modifier_Copies_The_Text()
    {
        var (editor, window, view) = Show("abc def");
        editor.SetSelection(new TextSelection(1, 1, 1, 4));
        TestHelpers.PumpUntil(() => DragAndDropController.Get(editor) is not null).ShouldBeTrue();

        window.MouseDown(At(window, view, 1, 2), MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(At(window, view, 1, 8), RawInputModifiers.LeftMouseButton | DragCopy);
        window.MouseUp(At(window, view, 1, 8), MouseButton.Left, DragCopy);
        editor.Text.ShouldBe("abc defabc");
    }

    [AvaloniaFact]
    public void A_Click_Inside_The_Selection_Without_Dragging_Places_The_Cursor_And_Escape_Cancels_A_Drag()
    {
        var (editor, window, view) = Show("one two three");
        editor.SetSelection(new TextSelection(1, 5, 1, 8));
        TestHelpers.PumpUntil(() => DragAndDropController.Get(editor) is not null).ShouldBeTrue();

        window.MouseDown(At(window, view, 1, 6), MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(At(window, view, 1, 6), MouseButton.Left, RawInputModifiers.None);
        editor.GetSelection()!.Value.ToString().ShouldBe(new TextSelection(1, 6, 1, 6).ToString());
        editor.Text.ShouldBe("one two three");

        // (a second press at the same point would be a double click, which starts no drag)
        editor.SetSelection(new TextSelection(1, 5, 1, 8));
        window.MouseDown(At(window, view, 1, 8), MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(At(window, view, 1, 12), RawInputModifiers.LeftMouseButton);
        DragAndDropController.Get(editor)!.DropMarkerPosition.ShouldNotBeNull();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        DragAndDropController.Get(editor)!.DropMarkerPosition.ShouldBeNull();
        window.MouseUp(At(window, view, 1, 12), MouseButton.Left, RawInputModifiers.None);
        editor.Text.ShouldBe("one two three");
    }

    [AvaloniaFact]
    public void Text_Dropped_From_Outside_Is_Inserted_And_Selected()
    {
        var (editor, _, _) = Show("ab\ncd");
        TestHelpers.PumpUntil(() => DragAndDropController.Get(editor) is not null).ShouldBeTrue();

        DragAndDropController.Get(editor)!.DropText(new TextPosition(2, 2), "XY\nZ");
        editor.Text.ShouldBe("ab\ncXY\nZd");
        editor.GetSelection()!.Value.ToString().ShouldBe(new TextSelection(2, 2, 3, 2).ToString());
    }

    // ---- selection anchor

    [AvaloniaFact]
    public void The_Selection_Anchor_Selects_From_Where_It_Was_Set()
    {
        var (editor, window, view) = Show("first line\nsecond line");
        editor.SetPosition(new TextPosition(1, 3));

        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd);
        window.KeyPressQwerty(PhysicalKey.B, CtrlCmd);
        var anchor = SelectionAnchorController.Get(editor).ShouldNotBeNull();
        anchor.AnchorPosition.ShouldBe(new TextPosition(1, 3));
        editor.ContextKeyService.GetContextKeyValue("selectionAnchorSet").ShouldBe(true);
        view.GetDecorationRects(1, 1).ShouldContain(r => r.ClassName == SelectionAnchorController.ClassName && r.Bounds.Width == 2);

        editor.SetPosition(new TextPosition(2, 4));
        editor.Trigger("keyboard", "editor.action.goToSelectionAnchor", null);
        editor.GetPosition().ShouldBe(new TextPosition(1, 3));

        editor.SetPosition(new TextPosition(2, 4));
        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd);
        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd);
        editor.GetSelection()!.Value.ToString().ShouldBe(new TextSelection(1, 3, 2, 4).ToString());
        anchor.AnchorPosition.ShouldBeNull();
        editor.ContextKeyService.GetContextKeyValue("selectionAnchorSet").ShouldBe(false);
    }

    [AvaloniaFact]
    public void Escape_Cancels_The_Selection_Anchor()
    {
        var (editor, window, _) = Show("abc");
        editor.Trigger("keyboard", "editor.action.setSelectionAnchor", null);
        SelectionAnchorController.Get(editor)!.AnchorPosition.ShouldNotBeNull();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        SelectionAnchorController.Get(editor)!.AnchorPosition.ShouldBeNull();
    }

    // ---- unicode highlighter

    [AvaloniaFact]
    public void Invisible_And_Ambiguous_Characters_Are_Boxed()
    {
        var (editor, _, view) = Show("var pаss = 1;\nzero​width\nplain");
        TestHelpers.PumpUntil(() => UnicodeHighlighterContribution.Get(editor) is { HighlightedRanges.Count: 2 }).ShouldBeTrue();
        var highlighter = UnicodeHighlighterContribution.Get(editor)!;

        highlighter.HighlightedRanges.Select(static r => r.ToString()).ShouldBe(
            [new TextRange(1, 6, 1, 7).ToString(), new TextRange(2, 5, 2, 6).ToString()]);
        var rects = view.GetDecorationRects(1, 2).Where(r => r.ClassName == UnicodeHighlighterContribution.ClassName).ToList();
        rects.Count.ShouldBe(2);
        rects.ShouldAllBe(r => r.Stroke != null && r.Bounds.Width > 0);

        var id = editor.Model.GetAllDecorations().First(d => d.Options.ClassName == UnicodeHighlighterContribution.ClassName).Id;
        highlighter.GetDecorationReason(id).ShouldNotBeNull().ConfusableWith.ShouldBe("a");

        // an edit that removes them removes the boxes
        editor.Text = "plain";
        TestHelpers.PumpUntil(() => highlighter.HighlightedRanges.Count == 0).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Unicode_Highlighter_Can_Be_Turned_Off()
    {
        var (editor, _, _) = Show("a​b");
        TestHelpers.PumpUntil(() => UnicodeHighlighterContribution.Get(editor) is { HighlightedRanges.Count: 1 }).ShouldBeTrue();
        var highlighter = UnicodeHighlighterContribution.Get(editor)!;

        highlighter.Options = highlighter.Options with { InvisibleCharacters = false, AmbiguousCharacters = false };
        highlighter.HighlightedRanges.ShouldBeEmpty();

        highlighter.Options = highlighter.Options with { NonBasicAscii = true };
        TestHelpers.PumpUntil(() => highlighter.HighlightedRanges.Count == 1).ShouldBeTrue();
        highlighter.State.ShouldNotBeNull().NonBasicAsciiCharacterCount.ShouldBe(1);
    }
}

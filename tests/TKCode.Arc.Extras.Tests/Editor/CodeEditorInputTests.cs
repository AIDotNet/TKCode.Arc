using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;
using TextSelection = TKCode.Arc.Editor.TextSelection;

namespace TKCode.Arc.Tests.Editor;

/// <summary>The code editor's input (phase 3.4): the input method client, the mouse like upstream's MouseHandler
/// (multi-cursor click, column selection, word and line drags, the gutter, auto-scroll), Tab and paging, and the
/// context keys, headless.</summary>
public class CodeEditorInputTests
{
    private static (CodeEditor Editor, Avalonia.Controls.Window Window, CodeEditorView View) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    // The window point of a model position (line, column), in the middle of the line, a little right of the column's
    // left edge.
    private static Point At(Avalonia.Controls.Window window, CodeEditorView view, int line, int column)
    {
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var x = origin.X + view.GutterWidth + (view.CharWidth * (column - 1)) + 1 - view.ScrollLeft;
        var y = origin.Y + (view.LineHeight * (line - 0.5)) - view.ScrollTop;
        return new Point(x, y);
    }

    private static Point Origin(Avalonia.Controls.Window window, CodeEditorView view) =>
        view.TranslatePoint(new Point(0, 0), window)!.Value;

    private static TextSelection[] Selections(CodeEditorView view) =>
        view.ViewModel!.GetCursorStates().Select(s => s.ModelState.Selection).ToArray();

    private static TextInputMethodClient RequestClient(CodeEditor editor)
    {
        var args = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        editor.RaiseEvent(args);
        return args.Client.ShouldNotBeNull();
    }

    // ---- input method

    [AvaloniaFact]
    public void Input_Method_Client_Shows_Preedit_And_Commits_Through_Type()
    {
        var (editor, window, view) = Show("ab");
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        var client = RequestClient(editor);
        client.SupportsPreedit.ShouldBeTrue();
        client.TextViewVisual.ShouldBe(view);

        client.SetPreeditText("ni", 2);
        view.PreeditText.ShouldBe("ni");
        view.PreeditCursor.ShouldBe(2);
        editor.Text.ShouldBe("ab"); // the preedit is not in the document
        var layer = view.GetVisualChildren().OfType<CodeEditorPreeditLayer>().ShouldHaveSingleItem();
        layer.Bounds.Size.ShouldBe(view.Bounds.Size);
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 200)))
        {
            bitmap.Render(view); // draws the preedit layer too
        }

        // the candidate window goes to the caret inside the preedit
        var rect = client.CursorRectangle;
        rect.Height.ShouldBe(view.LineHeight);
        rect.X.ShouldBeGreaterThan(view.GutterWidth + (view.CharWidth * 2));

        window.KeyTextInput("你");
        client.SetPreeditText(null);
        view.PreeditText.ShouldBeNull();
        editor.Text.ShouldBe("ab你");
        view.ViewModel!.GetPrimaryCursorState().ModelState.Position.ShouldBe(new TextPosition(1, 4));
    }

    [AvaloniaFact]
    public void Input_Method_Composition_Swallows_Keys_And_Can_Be_Cancelled()
    {
        var (editor, window, view) = Show("ab");
        var client = RequestClient(editor);
        client.SetPreeditText("x");
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        editor.Text.ShouldBe("ab"); // the input method owns the keys while it composes
        client.SetPreeditText(string.Empty);
        view.PreeditText.ShouldBeNull();
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        editor.Text.ShouldBe("b");
    }

    [AvaloniaFact]
    public void Input_Method_Is_Off_In_A_Read_Only_Editor()
    {
        var (editor, _, _) = Show("ab");
        editor.IsReadOnly = true;
        var args = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        editor.RaiseEvent(args);
        args.Client.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Cursor_Rectangle_Follows_The_Primary_Cursor()
    {
        var (editor, window, view) = Show("one\ntwo\nthree");
        var client = RequestClient(editor);
        var before = client.CursorRectangle;
        var changed = 0;
        client.CursorRectangleChanged += (_, _) => changed++;
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        changed.ShouldBeGreaterThan(0);
        client.CursorRectangle.Y.ShouldBe(before.Y + view.LineHeight, 0.01);
    }

    // ---- mouse

    [AvaloniaFact]
    public void Alt_Click_Adds_A_Cursor_And_Again_Removes_It()
    {
        var (editor, window, view) = Show("alpha\nbeta\ngamma");
        window.MouseDown(At(window, view, 1, 2), MouseButton.Left);
        window.MouseUp(At(window, view, 1, 2), MouseButton.Left);
        window.MouseDown(At(window, view, 3, 3), MouseButton.Left, RawInputModifiers.Alt);
        window.MouseUp(At(window, view, 3, 3), MouseButton.Left, RawInputModifiers.Alt);
        var selections = Selections(view);
        selections.Length.ShouldBe(2);
        selections.Select(s => s.Position).ShouldBe([new TextPosition(1, 2), new TextPosition(3, 3)], ignoreOrder: true);

        window.KeyTextInput("X");
        editor.Text.ShouldBe("aXlpha\nbeta\ngaXmma");
    }

    [AvaloniaFact]
    public void Alt_Shift_Drag_Selects_A_Column()
    {
        var (_, window, view) = Show("abcdef\nabcdef\nabcdef\nabcdef");
        var modifiers = RawInputModifiers.Alt | RawInputModifiers.Shift;
        window.MouseDown(At(window, view, 1, 2), MouseButton.Left);
        window.MouseUp(At(window, view, 1, 2), MouseButton.Left);
        // Alt+Shift+press starts a column selection from the cursor (a second press on the same spot would be a
        // double-click), the drag extends it
        window.MouseDown(At(window, view, 2, 2), MouseButton.Left, modifiers);
        window.MouseMove(At(window, view, 3, 5), modifiers | RawInputModifiers.LeftMouseButton);
        window.MouseUp(At(window, view, 3, 5), MouseButton.Left, modifiers);
        var selections = Selections(view).OrderBy(s => s.StartLineNumber).ToArray();
        selections.Length.ShouldBe(3);
        for (var i = 0; i < 3; i++)
        {
            selections[i].StartLineNumber.ShouldBe(i + 1);
            selections[i].EndLineNumber.ShouldBe(i + 1);
            selections[i].StartColumn.ShouldBe(2);
            selections[i].EndColumn.ShouldBe(5);
        }
    }

    [AvaloniaFact]
    public void Middle_Button_Drag_Selects_A_Column()
    {
        var (_, window, view) = Show("abcdef\nabcdef\nabcdef");
        window.MouseDown(At(window, view, 1, 4), MouseButton.Middle);
        window.MouseMove(At(window, view, 3, 2), RawInputModifiers.MiddleMouseButton);
        window.MouseUp(At(window, view, 3, 2), MouseButton.Middle);
        var selections = Selections(view);
        selections.Length.ShouldBe(3);
        selections.ShouldAllBe(s => s.StartColumn == 2 && s.EndColumn == 4);
    }

    [AvaloniaFact]
    public void Double_Click_Drag_Extends_By_Words()
    {
        var (_, window, view) = Show("one two three four");
        var start = At(window, view, 1, 6);
        window.MouseDown(start, MouseButton.Left);
        window.MouseUp(start, MouseButton.Left);
        window.MouseDown(start, MouseButton.Left);
        Selections(view)[0].ShouldBe(new TextSelection(1, 5, 1, 8));
        window.MouseMove(At(window, view, 1, 11), RawInputModifiers.LeftMouseButton);
        window.MouseUp(At(window, view, 1, 11), MouseButton.Left);
        var selection = Selections(view).ShouldHaveSingleItem();
        selection.StartColumn.ShouldBe(5);
        selection.EndColumn.ShouldBe(14); // the whole word "three"
    }

    [AvaloniaFact]
    public void Triple_Click_Drag_Extends_By_Lines()
    {
        var (_, window, view) = Show("one\ntwo\nthree\nfour");
        var start = At(window, view, 1, 2);
        for (var i = 0; i < 2; i++)
        {
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
        }

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(At(window, view, 3, 2), RawInputModifiers.LeftMouseButton);
        window.MouseUp(At(window, view, 3, 2), MouseButton.Left);
        var selection = Selections(view).ShouldHaveSingleItem();
        selection.StartLineNumber.ShouldBe(1);
        selection.StartColumn.ShouldBe(1);
        selection.EndLineNumber.ShouldBe(4);
        selection.EndColumn.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Gutter_Click_Selects_The_Line_And_Drag_Selects_Lines()
    {
        var (_, window, view) = Show("one\ntwo\nthree\nfour");
        var origin = Origin(window, view);
        var gutter = new Point(origin.X + 4, At(window, view, 2, 1).Y);
        window.MouseDown(gutter, MouseButton.Left);
        window.MouseUp(gutter, MouseButton.Left);
        Selections(view).ShouldHaveSingleItem().ShouldBe(new TextSelection(2, 1, 3, 1));

        window.MouseDown(gutter, MouseButton.Left);
        window.MouseMove(new Point(gutter.X, At(window, view, 3, 1).Y), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(gutter.X, At(window, view, 3, 1).Y), MouseButton.Left);
        var selection = Selections(view).ShouldHaveSingleItem();
        selection.StartLineNumber.ShouldBe(2);
        selection.EndLineNumber.ShouldBe(4);
        selection.EndColumn.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Dragging_Below_The_View_Scrolls_And_Extends_The_Selection()
    {
        var text = string.Join("\n", Enumerable.Range(1, 200).Select(i => "line " + i));
        var (_, window, view) = Show(text);
        var start = At(window, view, 2, 1);
        window.MouseDown(start, MouseButton.Left);
        var below = new Point(start.X, Origin(window, view).Y + view.Bounds.Height + 30);
        window.MouseMove(below, RawInputModifiers.LeftMouseButton);
        view.IsAutoScrolling.ShouldBeTrue();
        var endBefore = Selections(view)[0].EndLineNumber;

        view.AutoScrollTick(100);
        view.AutoScrollTick(100);
        view.ScrollTop.ShouldBeGreaterThan(0);
        var endAfter = Selections(view)[0].EndLineNumber;
        endAfter.ShouldBeGreaterThan(endBefore);
        Selections(view)[0].StartLineNumber.ShouldBe(2);

        // back inside: the timer stops
        var inside = new Point(start.X, Origin(window, view).Y + (view.LineHeight * 2.5));
        window.MouseMove(inside, RawInputModifiers.LeftMouseButton);
        view.IsAutoScrolling.ShouldBeFalse();
        window.MouseUp(inside, MouseButton.Left);
    }

    [AvaloniaFact]
    public void Dragging_Above_The_View_Scrolls_Up()
    {
        var text = string.Join("\n", Enumerable.Range(1, 200).Select(i => "line " + i));
        var (_, window, view) = Show(text);
        view.ViewModel!.ViewLayout.DeltaScrollNow(0, view.LineHeight * 50);
        var top = view.ScrollTop;
        top.ShouldBeGreaterThan(0);
        var start = new Point(At(window, view, 1, 1).X, Origin(window, view).Y + (view.LineHeight * 3.5));
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X, Origin(window, view).Y - 20), RawInputModifiers.LeftMouseButton);
        view.AutoScrollTick(100);
        view.ScrollTop.ShouldBeLessThan(top);
        window.MouseUp(new Point(start.X, Origin(window, view).Y - 20), MouseButton.Left);
        view.IsAutoScrolling.ShouldBeFalse();
    }

    // ---- keys

    [AvaloniaFact]
    public void Tab_Indents_And_Keeps_The_Focus()
    {
        var editor = new CodeEditor { Text = "x", Width = 400, Height = 100 };
        var other = new Avalonia.Controls.Button { Content = "next" };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { editor, other } });
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        editor.IsKeyboardFocusWithin.ShouldBeTrue();
        editor.Text.ShouldBe("    x");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        editor.IsKeyboardFocusWithin.ShouldBeTrue();
        editor.Text.ShouldBe("x");
    }

    [AvaloniaFact]
    public void Escape_Removes_Secondary_Cursors()
    {
        var (_, window, view) = Show("one\ntwo");
        window.MouseDown(At(window, view, 2, 2), MouseButton.Left, RawInputModifiers.Alt);
        window.MouseUp(At(window, view, 2, 2), MouseButton.Left, RawInputModifiers.Alt);
        Selections(view).Length.ShouldBe(2);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Selections(view).Length.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Page_Down_Moves_By_The_Viewport_Height()
    {
        var text = string.Join("\n", Enumerable.Range(1, 200).Select(i => "line " + i));
        var (_, window, view) = Show(text);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        var line = view.ViewModel!.GetPrimaryCursorState().ModelState.Position.LineNumber;
        var expectedPage = Math.Max(1, (int)Math.Floor(view.Bounds.Height / view.LineHeight) - 2);
        expectedPage.ShouldBeGreaterThan(3);
        line.ShouldBe(1 + expectedPage);
    }

    // ---- context keys

    [AvaloniaFact]
    public void Context_Keys_Follow_Focus_Read_Only_And_Selections()
    {
        var (editor, window, view) = Show("one two\nthree");
        var context = editor.ContextKeyService.GetContext();
        context.GetValue("editorTextFocus").ShouldBe(true);
        context.GetValue("editorReadonly").ShouldBe(false);
        context.GetValue("editorHasSelection").ShouldBe(false);

        window.MouseDown(At(window, view, 1, 2), MouseButton.Left);
        window.MouseUp(At(window, view, 1, 2), MouseButton.Left);
        window.MouseDown(At(window, view, 1, 2), MouseButton.Left);
        window.MouseUp(At(window, view, 1, 2), MouseButton.Left);
        editor.ContextKeyService.GetContext().GetValue("editorHasSelection").ShouldBe(true);

        window.MouseDown(At(window, view, 2, 2), MouseButton.Left, RawInputModifiers.Alt);
        window.MouseUp(At(window, view, 2, 2), MouseButton.Left, RawInputModifiers.Alt);
        editor.ContextKeyService.GetContext().GetValue("editorHasMultipleSelections").ShouldBe(true);

        editor.IsReadOnly = true;
        editor.ContextKeyService.GetContext().GetValue("editorReadonly").ShouldBe(true);

        window.Content = new Avalonia.Controls.Button { Content = "x" };
        Dispatcher.UIThread.RunJobs();
        ((Avalonia.Controls.Button)window.Content).Focus();
        Dispatcher.UIThread.RunJobs();
        editor.ContextKeyService.GetContext().GetValue("editorTextFocus").ShouldBe(false);
    }

    [AvaloniaFact]
    public void A_Click_On_A_Wide_Character_Line_Lands_On_The_Drawn_Glyph()
    {
        var editor = new CodeEditor { Text = "中文中文abc", Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        // the x the renderer gives column 5 (after the four wide characters) maps back to column 5
        var x = view.GetX(1, 5) + 1;
        var origin = view.TranslatePoint(new Avalonia.Point(0, 0), window)!.Value;
        var point = new Avalonia.Point(origin.X + x, origin.Y + (view.LineHeight / 2));
        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        view.ViewModel!.GetPrimaryCursorState().ModelState.Position.Column.ShouldBe(5);
    }
}

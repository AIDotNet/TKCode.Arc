using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using TKCode.Arc.Editor;

namespace TKCode.Arc.Tests.Editor;

/// <summary>The code editor control: the template, text sync, typing, keybindings and the pointer, headless.
/// The engine itself is covered by TKCode.Arc.Editor.Tests.</summary>
public class CodeEditorTests
{
    private static (CodeEditor Editor, Avalonia.Controls.Window Window) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    [AvaloniaFact]
    public void Has_Defaults_And_A_View()
    {
        var (editor, _) = Show("hello");
        editor.LanguageId.ShouldBe("plaintext");
        editor.TabSize.ShouldBe(4);
        editor.InsertSpaces.ShouldBeTrue();
        editor.ShowLineNumbers.ShouldBeTrue();
        editor.WordWrap.ShouldBeFalse();
        editor.IsReadOnly.ShouldBeFalse();
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        view.ViewModel.ShouldNotBeNull();
        view.LineHeight.ShouldBeGreaterThan(0);
        view.GutterWidth.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Text_Property_And_Model_Stay_In_Sync()
    {
        var (editor, window) = Show("abc");
        var changes = 0;
        editor.TextChanged += (_, _) => changes++;
        editor.Text = "line 1\nline 2";
        editor.Model.GetLineCount().ShouldBe(2);

        window.KeyTextInput("x");
        editor.Text.ShouldBe("xline 1\nline 2");
        changes.ShouldBeGreaterThanOrEqualTo(2);
    }

    [AvaloniaFact]
    public void Keys_Run_The_Core_Commands()
    {
        var (editor, window) = Show("one\ntwo");
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        window.KeyTextInput("!");
        editor.Text.ShouldBe("one\ntwo!");

        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        editor.Text.ShouldBe("one\ntwo");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        editor.Model.GetLineCount().ShouldBe(3);
    }

    [AvaloniaFact]
    public void Undo_And_Redo_Through_Keybindings()
    {
        var (editor, window) = Show("a");
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        window.KeyTextInput("b");
        editor.Text.ShouldBe("ab");
        var undo = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.Z, undo);
        editor.Text.ShouldBe("a");
        window.KeyPressQwerty(PhysicalKey.Z, undo | RawInputModifiers.Shift);
        editor.Text.ShouldBe("ab");
    }

    [AvaloniaFact]
    public void Read_Only_Ignores_Typing()
    {
        var (editor, window) = Show("fixed");
        editor.IsReadOnly = true;
        window.KeyTextInput("x");
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        editor.Text.ShouldBe("fixed");
    }

    [AvaloniaFact]
    public void A_Click_Places_The_Cursor()
    {
        var (editor, window) = Show("first line\nsecond line");
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var x = origin.X + view.GutterWidth + (view.CharWidth * 3) + 1;
        var y = origin.Y + (view.LineHeight * 1.5);
        window.MouseDown(new Point(x, y), MouseButton.Left);
        window.MouseUp(new Point(x, y), MouseButton.Left);
        var position = view.ViewModel!.GetPrimaryCursorState().ModelState.Position;
        position.ShouldBe(new TextPosition(2, 4));
    }

    [AvaloniaFact]
    public void Word_Wrap_Splits_Long_Lines_Into_View_Lines()
    {
        var (editor, _) = Show(new string('a', 400));
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        view.ViewModel!.GetLineCount().ShouldBe(1);
        editor.WordWrap = true;
        view.ViewModel!.GetLineCount().ShouldBeGreaterThan(1);
    }
}

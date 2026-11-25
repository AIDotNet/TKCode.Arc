using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The editor for assistive technology (phase 8): the automation peer (an edit control with the value pattern, its name,
/// line and column), the screen reader announcements of caret moves and selections, upstream's aria alert / status
/// containers, the tab focus mode (Ctrl+M) and the announce cursor position action.
/// </summary>
public class CodeEditorAccessibilityTests
{
    private static (CodeEditor Editor, Window Window) Show(string text, Control? after = null)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200 };
        var window = after is null
            ? TestHelpers.Show(editor)
            : TestHelpers.Show(new StackPanel { Children = { editor, after } });
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    private static CodeEditorAutomationPeer Peer(CodeEditor editor) =>
        ControlAutomationPeer.CreatePeerForElement(editor).ShouldBeOfType<CodeEditorAutomationPeer>();

    private static string? Announced(CodeEditor editor)
    {
        Dispatcher.UIThread.RunJobs();
        return editor.Aria.LastMessage;
    }

    [AvaloniaFact]
    public void Peer_Is_An_Edit_Control_With_The_Document_As_Its_Value()
    {
        var (editor, _) = Show("hello\nworld");
        var peer = Peer(editor);

        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Edit);
        peer.GetLocalizedControlType().ShouldBe("editor");
        peer.GetClassName().ShouldBe("CodeEditor");
        peer.GetName().ShouldBe("Editor content");
        peer.IsKeyboardFocusable().ShouldBeTrue();
        peer.GetItemStatus().ShouldBe("Line 1, Column 1");

        var value = peer.GetProvider<IValueProvider>().ShouldNotBeNull();
        value.Value.ShouldBe("hello\nworld");
        value.IsReadOnly.ShouldBeFalse();

        AutomationProperties.SetName(editor, "Program.cs");
        peer.GetName().ShouldBe("Program.cs");
        editor.IsReadOnly = true;
        value.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => value.SetValue("x"));
    }

    [AvaloniaFact]
    public void Setting_The_Value_Is_One_Undo_Step_And_Raises_Value_Changes()
    {
        var (editor, window) = Show("hello");
        var peer = Peer(editor);
        var changes = new List<(AutomationProperty Property, object? New)>();
        peer.PropertyChanged += (_, e) => changes.Add((e.Property, e.NewValue));

        peer.GetProvider<IValueProvider>()!.SetValue("bye");
        editor.Text.ShouldBe("bye");
        changes.ShouldContain(c => c.Property == ValuePatternIdentifiers.ValueProperty && (string?)c.New == "bye");

        window.KeyPressQwerty(PhysicalKey.Z, OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control);
        editor.Text.ShouldBe("hello");
    }

    [AvaloniaFact]
    public void Item_Status_Follows_The_Cursor()
    {
        var (editor, window) = Show("hello\nworld");
        var peer = Peer(editor);
        var statuses = new List<object?>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == AutomationElementIdentifiers.ItemStatusProperty)
            {
                statuses.Add(e.NewValue);
            }
        };

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        statuses.ShouldBe(["Line 2, Column 1", "Line 2, Column 2 (1 selected)"]);
        peer.GetItemStatus().ShouldBe("Line 2, Column 2 (1 selected)");
    }

    [AvaloniaFact]
    public void Caret_Moves_Announce_The_Line_The_Characters_Passed_And_The_Selection()
    {
        var (editor, window) = Show("int count = 1;\n\nreturn count;");
        Peer(editor);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Announced(editor).ShouldBe("i");

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Announced(editor).ShouldBe("n");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Announced(editor).ShouldBe("t");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Announced(editor).ShouldBe("space");

        // a longer jump along the line reads what it passed over
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Announced(editor).ShouldBe("count = 1;");

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Announced(editor).ShouldBe("blank");

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Announced(editor).ShouldBe("return count;");

        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.Shift);
        Announced(editor).ShouldBe("return count; selected");
    }

    [AvaloniaFact]
    public void Key_Repeats_In_One_Dispatcher_Turn_Make_One_Announcement()
    {
        var (editor, window) = Show("abcdef");
        Peer(editor);
        var messages = new List<string?>();
        editor.Aria.Containers[2].PropertyChanged += (_, e) =>
        {
            if (e.Property == AutomationProperties.NameProperty)
            {
                messages.Add((string?)e.NewValue);
            }
        };

        // three moves before the dispatcher idles: the characters passed by all three
        editor.SetPosition(new TextPosition(1, 2), "keyboard");
        editor.SetPosition(new TextPosition(1, 3), "keyboard");
        editor.SetPosition(new TextPosition(1, 4), "keyboard");
        Dispatcher.UIThread.RunJobs();
        messages.Where(m => m is not null).ShouldBe(["abc"]);
    }

    [AvaloniaFact]
    public void Typing_Is_Not_Announced_And_Nothing_Is_Without_A_Peer()
    {
        var (editor, window) = Show("ab");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Announced(editor).ShouldBeNull();

        Peer(editor);
        window.KeyTextInput("x");
        Announced(editor).ShouldBeNull();
        editor.Text.ShouldBe("axb");

        // the next move reads from where typing left the caret
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Announced(editor).ShouldBe("b");
    }

    [AvaloniaFact]
    public void Aria_Containers_Are_Live_Regions_That_Alternate_For_Repeated_Messages()
    {
        var (editor, _) = Show("x");
        var aria = editor.Aria;
        aria.Containers.ShouldAllBe(c => c.Parent == editor.WidgetHost);
        AutomationProperties.GetLiveSetting(aria.Containers[0]).ShouldBe(AutomationLiveSetting.Assertive);
        AutomationProperties.GetLiveSetting(aria.Containers[2]).ShouldBe(AutomationLiveSetting.Polite);

        editor.Alert("saved");
        AutomationProperties.GetName(aria.Containers[0]).ShouldBe("saved");
        editor.Alert("saved");
        AutomationProperties.GetName(aria.Containers[0]).ShouldBeNull();
        AutomationProperties.GetName(aria.Containers[1]).ShouldBe("saved");

        editor.Status(new string('a', CodeEditorAria.MaxMessageLength + 10));
        aria.LastMessage!.Length.ShouldBe(CodeEditorAria.MaxMessageLength);

        // a live element's name change reaches its peer, which the platform turns into a live region event
        var containerPeer = ControlAutomationPeer.CreatePeerForElement(aria.Containers[3]);
        var raised = false;
        containerPeer.PropertyChanged += (_, e) => raised |= e.Property == AutomationElementIdentifiers.NameProperty;
        editor.Status("one");
        editor.Status("one");
        raised.ShouldBeTrue();
        containerPeer.GetLiveSetting().ShouldBe(AutomationLiveSetting.Polite);
    }

    [AvaloniaFact]
    public void Ctrl_M_Toggles_Whether_Tab_Moves_The_Focus()
    {
        var other = new Button { Content = "next" };
        var (editor, window) = Show("x", other);
        var toggle = OperatingSystem.IsMacOS() ? RawInputModifiers.Control | RawInputModifiers.Shift : RawInputModifiers.Control;

        window.KeyPressQwerty(PhysicalKey.M, toggle);
        editor.TabFocusMode.ShouldBeTrue();
        editor.ContextKeyService.GetContextKeyValue("editorTabMovesFocus").ShouldBe(true);
        editor.Aria.LastMessage.ShouldBe(ToggleTabFocusModeAction.OnMessage);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        editor.Text.ShouldBe("x");
        other.IsFocused.ShouldBeTrue();

        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.M, toggle);
        editor.TabFocusMode.ShouldBeFalse();
        editor.Aria.LastMessage.ShouldBe(ToggleTabFocusModeAction.OffMessage);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        editor.Text.ShouldBe("    x");
        editor.IsKeyboardFocusWithin.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Tab_Focus_Mode_Property_Lets_Shift_Tab_Leave_Too()
    {
        var before = new Button { Content = "before" };
        var editor = new CodeEditor { Text = "x", Width = 400, Height = 100, TabFocusMode = true };
        var window = TestHelpers.Show(new StackPanel { Children = { before, editor } });
        editor.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        before.IsFocused.ShouldBeTrue();
        editor.Text.ShouldBe("x");
    }

    [AvaloniaFact]
    public void Announce_Cursor_Position_Alerts_The_Line_And_Column()
    {
        var (editor, window) = Show("hello\nworld");
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);

        var shortcut = RawInputModifiers.Alt | RawInputModifiers.Shift | (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control);
        window.KeyPressQwerty(PhysicalKey.G, shortcut);
        editor.Aria.LastMessage.ShouldBe("Line 2, Column 3 (2 selected)");
        editor.GetAction(AnnounceCursorPositionAction.Id).ShouldNotBeNull().Label.ShouldBe("Announce Cursor Position");
    }
}

using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="RowEditor"/>: Enter, Shift+Enter and Escape (only from the box, never while an input method composes),
/// validation before <c>Submitted</c>, the live count past the limit, errors that clear on the next edit, the saving
/// state, focus with the caret at the end, the box growing with its lines, the bottom row wrapping in a narrow editor
/// and what screen readers get. Sample memories are invented. The headless stub font advances 1em per character.
/// </summary>
public class RowEditorTests
{
    private const string Hint = "Enter to save, Esc to cancel";
    private const string Empty = "Write something to remember";

    private sealed class Rig
    {
        public RowEditor Editor { get; }
        public Window Window { get; }
        public List<string> Submitted { get; } = new();
        public int Cancelled { get; private set; }

        public Rig(string text = "Prefers tables to long bullet lists", double width = 600, int maxLength = 200,
            ArcMotionLevel level = ArcMotionLevel.Off)
        {
            Editor = new RowEditor
            {
                Width = width,
                Text = text,
                MaxLength = maxLength,
                HintText = Hint,
                CancelText = "Cancel",
                SaveText = "Save",
                CountText = (length, limit) => $"{length} of {limit}",
                EmptyErrorText = Empty,
                TooLongErrorText = limit => $"Keep it under {limit} characters",
                AccessibleName = "Edit memory: " + text,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            Editor.Submitted += (_, _) => Submitted.Add(Editor.Text);
            Editor.Cancelled += (_, _) => Cancelled++;
            Window = TestHelpers.Show(Editor, level);
            Window.Width = 800;
            Window.Height = 600;
            Settle();
        }

        public RowEditorTextBox Box => Editor.TextBox!;

        public void Settle()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Settle();
        }

        public void Type(string text)
        {
            Window.KeyTextInput(text);
            Settle();
        }

        public void FocusBox()
        {
            Editor.FocusEditor();
            Settle();
        }
    }

    private static string Letters(int count) => new('m', count);

    #region Keys

    [AvaloniaFact]
    public void Enter_Submits_A_Valid_Draft_Without_A_New_Line()
    {
        var rig = new Rig();
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(["Prefers tables to long bullet lists"], rig.Submitted);
        Assert.Equal("Prefers tables to long bullet lists", rig.Box.Text);
        Assert.Equal(0, rig.Cancelled);
    }

    [AvaloniaFact]
    public void Shift_Enter_Starts_A_New_Line()
    {
        var rig = new Rig("Prefers tables");
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        Assert.Empty(rig.Submitted);
        Assert.Contains('\n', rig.Editor.Text.Replace("\r", "\n"));
        Assert.StartsWith("Prefers tables", rig.Editor.Text);
    }

    [AvaloniaFact]
    public void Ctrl_Enter_Submits_Too()
    {
        var rig = new Rig();
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Control);
        Assert.Single(rig.Submitted);
    }

    [AvaloniaFact]
    public void Escape_In_The_Box_Cancels()
    {
        var rig = new Rig();
        rig.FocusBox();
        rig.Key(PhysicalKey.Escape);
        Assert.Equal(1, rig.Cancelled);
        Assert.Empty(rig.Submitted);
    }

    [AvaloniaFact]
    public void Escape_From_A_Button_Does_Nothing()
    {
        var rig = new Rig();
        rig.Editor.CancelButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Escape);
        rig.Editor.SaveButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Escape);
        Assert.Equal(0, rig.Cancelled);
        Assert.Empty(rig.Submitted);
    }

    [AvaloniaFact]
    public void The_Buttons_Cancel_And_Save()
    {
        var rig = new Rig();
        rig.Editor.CancelButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(1, rig.Cancelled);

        rig.Editor.SaveButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Single(rig.Submitted);
    }

    [AvaloniaFact]
    public void Typing_Updates_The_Draft()
    {
        var rig = new Rig("Prefers tables");
        rig.FocusBox();
        rig.Type(" and charts");
        Assert.Equal("Prefers tables and charts", rig.Editor.Text);
    }

    [AvaloniaFact]
    public void Text_Set_By_The_Owner_Reaches_The_Box()
    {
        var rig = new Rig("Prefers tables");
        rig.Editor.Text = "Prefers charts";
        Assert.Equal("Prefers charts", rig.Box.Text);
    }

    #endregion

    #region Input method

    private static TextInputMethodClient Client(RowEditorTextBox box)
    {
        var args = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        box.RaiseEvent(args);
        return args.Client!;
    }

    [AvaloniaFact]
    public void Enter_And_Escape_Belong_To_A_Composing_Input_Method()
    {
        var rig = new Rig("Likes");
        rig.FocusBox();
        var client = Client(rig.Box);
        client.SetPreeditText("ni hao");
        rig.Settle();
        Assert.True(rig.Box.IsComposing);

        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Escape);
        Assert.Empty(rig.Submitted);
        Assert.Equal(0, rig.Cancelled);

        client.SetPreeditText(null);
        rig.Settle();
        Assert.False(rig.Box.IsComposing);
        rig.Key(PhysicalKey.Enter);
        Assert.Single(rig.Submitted);
    }

    [AvaloniaFact]
    public void An_Ime_Processed_Key_Is_Not_Enter()
    {
        var rig = new Rig();
        rig.FocusBox();
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.ImeProcessed, Source = rig.Box };
        rig.Box.RaiseEvent(args);
        Assert.False(args.Handled);
        Assert.Empty(rig.Submitted);
    }

    #endregion

    #region Validation

    [AvaloniaFact]
    public void An_Empty_Draft_Shows_The_Error_And_Is_Not_Submitted()
    {
        var rig = new Rig("   ");
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.Empty(rig.Submitted);
        Assert.Equal(Empty, rig.Editor.ErrorText);
        Assert.Equal(RowEditorHintKind.Error, rig.Editor.HintKind);
        Assert.True(rig.Editor.HintLine!.IsAlert);
        Assert.Equal(Empty, rig.Editor.HintLine.Text);
        Assert.True(rig.Editor.IsInvalid);
        Assert.True(rig.Box.IsInvalid);
        Assert.Contains(RowEditorTextBox.InvalidPseudoClass, rig.Box.Classes);
        Assert.Contains(RowEditor.InvalidPseudoClass, rig.Editor.Classes);
    }

    [AvaloniaFact]
    public void A_Draft_Over_The_Limit_Shows_The_Error_And_Is_Not_Submitted()
    {
        var rig = new Rig(Letters(201));
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.Empty(rig.Submitted);
        Assert.Equal("Keep it under 200 characters", rig.Editor.ErrorText);

        rig.Editor.SaveButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Empty(rig.Submitted);
        // A refused Save sends the person back to the box.
        Assert.True(rig.Box.IsFocused);
    }

    [AvaloniaFact]
    public void Submitted_Never_Carries_An_Invalid_Draft_And_Trims_The_Valid_One()
    {
        var rig = new Rig("");
        rig.FocusBox();
        foreach (var draft in new[] { "", "  \n ", Letters(250), "  Runs builds from the terminal  ", Letters(200), " " + Letters(200) + "  " })
        {
            rig.Editor.Text = draft;
            rig.Key(PhysicalKey.Enter);
        }
        Assert.Equal(["Runs builds from the terminal", Letters(200), Letters(200)], rig.Submitted);
        Assert.All(rig.Submitted, text => Assert.True(text.Length is > 0 and <= 200 && text == text.Trim()));
    }

    [AvaloniaFact]
    public void Zero_Max_Length_Means_No_Limit()
    {
        var rig = new Rig(Letters(500), maxLength: 0);
        Assert.False(rig.Editor.IsOverLimit);
        Assert.Equal(RowEditorHintKind.Hint, rig.Editor.HintKind);
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.Single(rig.Submitted);
    }

    [AvaloniaFact]
    public void The_Count_Shows_Only_Past_The_Limit_And_Updates_In_Place()
    {
        var rig = new Rig(Letters(199));
        rig.FocusBox();
        Assert.Equal(RowEditorHintKind.Hint, rig.Editor.HintKind);
        Assert.Equal(Hint, rig.Editor.HintLine!.Text);
        Assert.False(rig.Editor.HintLine.IsAlert);

        rig.Type("m");
        Assert.Equal(200, rig.Editor.DraftLength);
        Assert.Equal(RowEditorHintKind.Hint, rig.Editor.HintKind);

        rig.Type("m");
        Assert.True(rig.Editor.IsOverLimit);
        Assert.Contains(RowEditor.OverLimitPseudoClass, rig.Editor.Classes);
        Assert.Equal(RowEditorHintKind.Count, rig.Editor.HintKind);
        var line = rig.Editor.HintLine;
        Assert.Equal("201 of 200", line.Text);
        Assert.True(line.IsAlert);
        // Over the limit is not an error: the box keeps its normal border until a submit fails.
        Assert.False(rig.Box.IsInvalid);

        rig.Type("mm");
        Assert.Same(line, rig.Editor.HintLine);
        Assert.Equal("203 of 200", line.Text);

        // Trailing spaces do not count.
        rig.Editor.Text = Letters(200) + "    ";
        Assert.False(rig.Editor.IsOverLimit);
        Assert.Equal(RowEditorHintKind.Hint, rig.Editor.HintKind);
    }

    [AvaloniaFact]
    public void Typing_Is_Never_Blocked_At_The_Limit()
    {
        var rig = new Rig(Letters(200));
        rig.FocusBox();
        rig.Type("mmm");
        Assert.Equal(203, rig.Editor.Text.Length);
        Assert.Equal(0, rig.Box.MaxLength);
    }

    [AvaloniaFact]
    public void An_Error_Clears_As_Soon_As_The_Draft_Changes()
    {
        var rig = new Rig("");
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.NotNull(rig.Editor.ErrorText);

        rig.Type("R");
        Assert.Null(rig.Editor.ErrorText);
        Assert.Equal(RowEditorHintKind.Hint, rig.Editor.HintKind);
        Assert.False(rig.Box.IsInvalid);

        // An error the owner sets (a failed save) clears the same way.
        rig.Editor.ErrorText = "Couldn’t save. Try again";
        Assert.Equal(RowEditorHintKind.Error, rig.Editor.HintKind);
        rig.Key(PhysicalKey.Backspace);
        Assert.Null(rig.Editor.ErrorText);
    }

    [AvaloniaFact]
    public void An_Error_Over_The_Limit_Gives_Way_To_The_Count_On_The_Next_Edit()
    {
        var rig = new Rig(Letters(210));
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(RowEditorHintKind.Error, rig.Editor.HintKind);
        rig.Type("m");
        Assert.Equal(RowEditorHintKind.Count, rig.Editor.HintKind);
        Assert.Equal("211 of 200", rig.Editor.HintLine!.Text);
    }

    [AvaloniaFact]
    public void Text_Set_By_The_Owner_Keeps_Its_Error()
    {
        var rig = new Rig("Likes");
        rig.Editor.ErrorText = "Couldn’t save. Try again";
        rig.Editor.Text = "Likes trains";
        Assert.Equal("Couldn’t save. Try again", rig.Editor.ErrorText);
    }

    #endregion

    #region Saving

    [AvaloniaFact]
    public void Saving_Locks_The_Box_And_The_Buttons()
    {
        var rig = new Rig();
        rig.FocusBox();
        rig.Editor.IsSaving = true;
        rig.Settle();
        Assert.True(rig.Box.IsReadOnly);
        Assert.True(rig.Editor.SaveButton!.IsLoading);
        Assert.False(rig.Editor.CancelButton!.IsEffectivelyEnabled);
        Assert.Contains(RowEditor.SavingPseudoClass, rig.Editor.Classes);

        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Escape);
        Assert.False(rig.Editor.Submit());
        rig.Editor.Cancel();
        Assert.Empty(rig.Submitted);
        Assert.Equal(0, rig.Cancelled);
        // Read-only: Enter did not add a line either.
        Assert.Equal("Prefers tables to long bullet lists", rig.Editor.Text);

        rig.Editor.SaveButton.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Empty(rig.Submitted);

        rig.Editor.IsSaving = false;
        rig.Settle();
        Assert.False(rig.Box.IsReadOnly);
        Assert.False(rig.Editor.SaveButton.IsLoading);
        Assert.True(rig.Editor.CancelButton.IsEffectivelyEnabled);
    }

    #endregion

    #region Focus and layout

    [AvaloniaFact]
    public void FocusEditor_Puts_The_Caret_At_The_End()
    {
        var rig = new Rig("Prefers tables");
        rig.Box.CaretIndex = 3;
        rig.FocusBox();
        Assert.True(rig.Box.IsFocused);
        Assert.Equal(14, rig.Box.CaretIndex);
        Assert.Equal(rig.Box.SelectionStart, rig.Box.SelectionEnd);
    }

    [AvaloniaFact]
    public void FocusEditor_Can_Keep_The_Caret()
    {
        var rig = new Rig("Prefers tables");
        rig.Box.CaretIndex = 3;
        rig.Editor.FocusEditor(caretAtEnd: false);
        rig.Settle();
        Assert.True(rig.Box.IsFocused);
        Assert.Equal(3, rig.Box.CaretIndex);
    }

    [AvaloniaFact]
    public void FocusEditor_Before_The_Editor_Is_Shown_Focuses_It_Once_Shown()
    {
        var editor = new RowEditor { Text = "Prefers tables", Width = 600 };
        editor.FocusEditor();
        var window = TestHelpers.Show(editor);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.TextBox!.IsFocused);
        Assert.Equal(14, editor.TextBox.CaretIndex);
    }

    [AvaloniaFact]
    public void The_Box_Grows_With_Its_Lines_Up_To_Six()
    {
        var rig = new Rig("One line");
        Assert.Equal(38, rig.Box.Bounds.Height);
        // One line of text is a 93px editor (spec 3.7): 1 + 38 + 8 + 36 + 10.
        Assert.Equal(93, rig.Editor.Bounds.Height);

        rig.Editor.Text = "One\nTwo\nThree";
        rig.Settle();
        Assert.Equal(82, rig.Box.Bounds.Height);

        rig.Editor.Text = string.Join("\n", Enumerable.Range(1, 10).Select(i => "Line " + i));
        rig.Settle();
        Assert.Equal(148, rig.Box.Bounds.Height);
    }

    [AvaloniaFact]
    public void The_Bottom_Row_Holds_Hint_And_Buttons_On_One_Line_When_They_Fit()
    {
        var rig = new Rig();
        var footer = TestHelpers.Descendant<RowEditorFooter>(rig.Editor);
        Assert.False(footer.IsWrapped);
        Assert.Equal(36, footer.Bounds.Height);
        var hint = rig.Editor.HintSwap!;
        var buttons = rig.Editor.SaveButton!.GetVisualParent<StackPanel>()!;
        // The hint starts 11px in and the buttons end at the edge; both are centered on the 36px line.
        Assert.Equal(11, hint.Bounds.X);
        Assert.Equal(footer.Bounds.Width, buttons.Bounds.Right, 3);
        Assert.Equal(18, hint.Bounds.Center.Y, 3);
        Assert.Equal(18, buttons.Bounds.Center.Y, 3);
        Assert.Equal(36, rig.Editor.SaveButton!.Bounds.Height);
        Assert.Equal(8, rig.Editor.SaveButton.Bounds.X - rig.Editor.CancelButton!.Bounds.Right, 3);
    }

    [AvaloniaFact]
    public void In_A_Narrow_Editor_The_Hint_Takes_Its_Own_Line_Above_The_Buttons()
    {
        var rig = new Rig(width: 288);
        var footer = TestHelpers.Descendant<RowEditorFooter>(rig.Editor);
        Assert.True(footer.IsWrapped);
        var hint = rig.Editor.HintSwap!;
        var buttons = rig.Editor.SaveButton!.GetVisualParent<StackPanel>()!;
        Assert.Equal(hint.Bounds.Bottom + 8, buttons.Bounds.Top, 3);
        Assert.Equal(footer.Bounds.Width, buttons.Bounds.Right, 3);
        Assert.Equal(hint.Bounds.Height + 8 + 36, footer.Bounds.Height, 3);
        // Nothing spills out of the editor.
        Assert.True(footer.Bounds.Width <= 288);
    }

    #endregion

    #region Motion and automation

    [AvaloniaFact]
    public void The_Line_Crossfades_Only_When_The_Kind_Of_Message_Changes()
    {
        var rig = new Rig(Letters(200), level: ArcMotionLevel.Full);
        rig.FocusBox();
        var swap = rig.Editor.HintSwap!;
        var hint = rig.Editor.HintLine;
        Assert.Same(hint, swap.Content);

        rig.Type("m");
        var count = rig.Editor.HintLine;
        Assert.NotSame(hint, count);
        Assert.Same(count, swap.Content);
        Assert.Equal(ContentSwapMode.Label, swap.Mode);

        rig.Type("m");
        Assert.Same(count, swap.Content);
    }

    [AvaloniaFact]
    public void The_Box_Is_Named_And_Described_By_The_Line_Under_It()
    {
        var rig = new Rig("Prefers tables");
        Assert.Equal("Edit memory: Prefers tables", AutomationProperties.GetName(rig.Box));
        Assert.Equal(Hint, AutomationProperties.GetHelpText(rig.Box));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(rig.Editor.HintSwap!));
        Assert.Equal(Hint, AutomationProperties.GetName(rig.Editor.HintSwap!));

        rig.Editor.Text = "";
        rig.FocusBox();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(Empty, AutomationProperties.GetHelpText(rig.Box));
        Assert.Equal(Empty, AutomationProperties.GetName(rig.Editor.HintSwap!));

        rig.Editor.AccessibleName = "Edit memory: Likes trains";
        Assert.Equal("Edit memory: Likes trains", AutomationProperties.GetName(rig.Box));
    }

    #endregion
}

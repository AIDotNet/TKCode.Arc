using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiUserBubble"/> and its parts: the inline code parser (CommonMark code spans) and the chips it makes, the
/// attachment list following the message, when Copy and Edit show, the copied check and its timing (a manual scheduler),
/// the editor (Enter, Shift+Enter, Escape, IME, empty and unchanged drafts), where the focus goes, what screen readers
/// get, the widths of the bubble and the form, and the swap at each motion level. Sample messages are invented. The
/// headless stub font advances 1em per character, so geometry is checked against the layout rules, not real text.
/// </summary>
public class AiUserBubbleTests
{
    private const string Short = "How much time would the cache fix save us per week?";
    private const string WithCode = "Why does `restore-keys` miss when the `cache-key` changes?";

    /// <summary>A manual stand-in for the dispatcher timer that ends the copied check.</summary>
    private sealed class ManualSchedule
    {
        public List<(TimeSpan Delay, Action Action, Handle Handle)> Pending { get; } = new();

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            var handle = new Handle();
            Pending.Add((delay, action, handle));
            return handle;
        }

        /// <summary>Runs every scheduled action that was not cancelled.</summary>
        public void Fire()
        {
            var due = Pending.ToArray();
            Pending.Clear();
            foreach (var (_, action, handle) in due)
            {
                if (!handle.IsDisposed)
                {
                    action();
                }
            }
        }

        public sealed class Handle : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }
    }

    private sealed class Rig
    {
        public AiUserBubble Bubble { get; }
        public AiChatUserMessage Message { get; }
        public Window Window { get; }
        public ManualSchedule Schedule { get; } = new();
        public List<string> Copied { get; } = new();
        public List<string> Submitted { get; } = new();
        public int Cancelled { get; private set; }
        public int EditRequests { get; private set; }

        public Rig(string text = Short, double width = 760, bool narrow = false, ArcMotionLevel level = ArcMotionLevel.Off,
            bool handleCopy = true, Action<AiChatUserMessage>? configure = null)
        {
            Message = new AiChatUserMessage { Text = text };
            configure?.Invoke(Message);
            Bubble = new AiUserBubble
            {
                Message = Message,
                IsNarrow = narrow,
                Width = width,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CopyText = "Copy",
                CopiedText = "Copied",
                EditText = "Edit",
                EditMessageText = "Edit message",
                CancelText = "Cancel",
                SendText = "Send",
                AttachmentsText = "Attachments"
            };
            Bubble.ScheduleCopyReset = Schedule.Schedule;
            Bubble.CopyRequested += (_, e) =>
            {
                Copied.Add(e.Text);
                e.Handled = handleCopy;
            };
            Bubble.EditRequested += (_, _) => EditRequests++;
            Bubble.EditSubmitted += (_, e) => Submitted.Add(e.Text);
            Bubble.EditCancelled += (_, _) => Cancelled++;
            Window = TestHelpers.Show(Bubble, level);
            Window.Width = 900;
            Window.Height = 700;
            Settle();
        }

        public AiUserBubbleRow Row => Bubble.Row!;

        public AiMessageEditor Editor => Bubble.Editor!;

        public TokenTextEditor Area => Editor.TextArea!;

        public void Settle()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Window.UpdateLayout();
            }
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

        public void Click(Control control)
        {
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)!.Value;
            Window.MouseMove(point, RawInputModifiers.None);
            Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Settle();
        }

        public void OpenEditor()
        {
            Click(Row.EditButton!);
        }

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();
    }

    #region Inline code parser

    [AvaloniaFact]
    public void Plain_Text_Is_One_Segment()
    {
        AiInlineCode.Parse(Short).ShouldBe([new AiInlineSegment(Short, false)]);
        AiInlineCode.Parse("").ShouldBeEmpty();
        AiInlineCode.Parse(null).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Ticks_Make_Code_Between_Plain_Text()
    {
        AiInlineCode.Parse(WithCode).ShouldBe([
            new AiInlineSegment("Why does ", false),
            new AiInlineSegment("restore-keys", true),
            new AiInlineSegment(" miss when the ", false),
            new AiInlineSegment("cache-key", true),
            new AiInlineSegment(" changes?", false)
        ]);
    }

    [AvaloniaFact]
    public void Code_At_The_Edges_Has_No_Empty_Plain_Segments()
    {
        AiInlineCode.Parse("`a` and `b`").ShouldBe([
            new AiInlineSegment("a", true),
            new AiInlineSegment(" and ", false),
            new AiInlineSegment("b", true)
        ]);
    }

    [AvaloniaFact]
    public void An_Unmatched_Tick_Stays_Text()
    {
        AiInlineCode.Parse("It costs 5` per run").ShouldBe([new AiInlineSegment("It costs 5` per run", false)]);
        AiInlineCode.Parse("one `two` three `four").ShouldBe([
            new AiInlineSegment("one ", false),
            new AiInlineSegment("two", true),
            new AiInlineSegment(" three `four", false)
        ]);
    }

    [AvaloniaFact]
    public void Only_A_Run_Of_The_Same_Length_Closes()
    {
        AiInlineCode.Parse("``a ` b``").ShouldBe([new AiInlineSegment("a ` b", true)]);
        // A single tick is not closed by a double one: both stay text.
        AiInlineCode.Parse("`a``").ShouldBe([new AiInlineSegment("`a``", false)]);
    }

    [AvaloniaFact]
    public void One_Space_Is_Stripped_From_Both_Ends_Unless_All_Spaces()
    {
        AiInlineCode.Parse("`` `tick` ``").ShouldBe([new AiInlineSegment("`tick`", true)]);
        AiInlineCode.Parse("` `").ShouldBe([new AiInlineSegment(" ", true)]);
        AiInlineCode.Parse("` a`").ShouldBe([new AiInlineSegment(" a", true)]);
    }

    [AvaloniaFact]
    public void Line_Endings_Are_Spaces_In_Code_And_Breaks_Outside()
    {
        AiInlineCode.Parse("first\r\nsecond `a\nb`").ShouldBe([
            new AiInlineSegment("first\nsecond ", false),
            new AiInlineSegment("a b", true)
        ]);
        AiInlineCode.Parse("a\rb").ShouldBe([new AiInlineSegment("a\nb", false)]);
    }

    [AvaloniaFact]
    public void A_Backslash_Escapes_A_Tick_Outside_Code_Only()
    {
        AiInlineCode.Parse(@"use \`ticks\` here").ShouldBe([new AiInlineSegment("use `ticks` here", false)]);
        AiInlineCode.Parse(@"C:\temp and `C:\temp`").ShouldBe([
            new AiInlineSegment(@"C:\temp and ", false),
            new AiInlineSegment(@"C:\temp", true)
        ]);
    }

    [AvaloniaFact]
    public void Display_Text_Drops_The_Ticks()
    {
        AiInlineCode.DisplayText(WithCode).ShouldBe("Why does restore-keys miss when the cache-key changes?");
    }

    #endregion

    #region Chips

    [AvaloniaFact]
    public void Each_Code_Span_Gets_A_Chip_Over_Its_Spacers_And_Code()
    {
        var rig = new Rig(WithCode);
        var text = rig.Row.TextBlock!;
        text.Chips.Count.ShouldBe(2);
        // "Why does " (9), spacer, "restore-keys" (12), spacer → chip 9..23.
        text.Chips[0].Start.ShouldBe(9);
        text.Chips[0].Length.ShouldBe(14);
        // then " miss when the " (15): 23 + 15 = 38, spacer, "cache-key" (9), spacer → 38..49.
        text.Chips[1].Start.ShouldBe(38);
        text.Chips[1].Length.ShouldBe(11);
        text.Chips[0].CornerRadius.ShouldBe(6);
        text.DisplayText.ShouldBe("Why does restore-keys miss when the cache-key changes?");
    }

    [AvaloniaFact]
    public void Code_Runs_Are_Monospace_At_086em_Between_036em_Spacers()
    {
        var rig = new Rig(WithCode);
        var text = rig.Row.TextBlock!;
        var inlines = text.Inlines!.ToList();
        inlines.Count.ShouldBe(9);
        var code = inlines[2].ShouldBeOfType<Avalonia.Controls.Documents.Run>();
        code.Text.ShouldBe("restore-keys");
        code.FontSize.ShouldBe(16 * 0.86, 0.001);
        code.FontFamily.ShouldBe(text.CodeFontFamily);
        var spacer = inlines[1].ShouldBeOfType<Avalonia.Controls.Documents.InlineUIContainer>();
        spacer.Child.Width.ShouldBe(16 * 0.86 * 0.36, 0.001);
        spacer.Child.Height.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Chips_Are_The_Text_Color_At_7_Percent_And_Reused()
    {
        var rig = new Rig(WithCode);
        var text = rig.Row.TextBlock!;
        var fill = TestHelpers.ColorOf(text.Chips[0].Background);
        var fore = TestHelpers.ColorOf(text.Foreground);
        fill.R.ShouldBe(fore.R);
        ((double)fill.A).ShouldBe(fore.A * 0.07, 1);
        var first = text.Chips[0];
        rig.Message.Text = "Only `one` now";
        rig.Settle();
        text.Chips.Count.ShouldBe(1);
        text.Chips[0].ShouldBeSameAs(first);
        text.Chips[0].Start.ShouldBe(5);
        text.Chips[0].Length.ShouldBe(5);
    }

    [AvaloniaFact]
    public void A_Wrapping_Bubble_Takes_Its_Full_Width_Like_Css()
    {
        var rig = new Rig("word word word word word word word word word word word word word word");
        var text = rig.Row.TextBlock!;
        text.IsSoftWrapped().ShouldBeTrue();
        rig.Row.Bubble!.Bounds.Width.ShouldBe(544, 0.5);
        var lines = new Rig("one\ntwo");
        lines.Row.TextBlock!.IsSoftWrapped().ShouldBeFalse();
        lines.Row.Bubble!.Bounds.Width.ShouldBeLessThan(544);
    }

    #endregion

    #region Attachments

    private static void TwoFiles(AiChatUserMessage message)
    {
        message.Attachments.Add(new AiAttachment { Id = "a", Name = "stage-timings.csv", Size = 1200 });
        message.Attachments.Add(new AiAttachment { Id = "b", Name = "pipeline.png", Size = 84_000 });
    }

    private static List<AiUserBubbleAttachment> Items(Rig rig) =>
        rig.Row.AttachmentsPanel!.Children.OfType<AiUserBubbleAttachment>().ToList();

    [AvaloniaFact]
    public void Attachments_Are_Listed_Above_The_Text_With_A_File_Glyph()
    {
        var rig = new Rig(configure: TwoFiles);
        var items = Items(rig);
        items.Select(i => i.FileName).ShouldBe(["stage-timings.csv", "pipeline.png"]);
        items.ShouldAllBe(i => ReferenceEquals(i.Icon, ArcIcons.FileText));
        var panel = rig.Row.AttachmentsPanel!;
        panel.IsVisible.ShouldBeTrue();
        panel.Margin.ShouldBe(new Thickness(0, 2, 0, 8));
        var panelBottom = panel.TranslatePoint(new Point(0, panel.Bounds.Height), rig.Window)!.Value.Y;
        var textTop = rig.Row.TextBlock!.TranslatePoint(default, rig.Window)!.Value.Y;
        textTop.ShouldBe(panelBottom + 8, 0.5);
        AutomationProperties.GetName(panel).ShouldBe("Attachments");
    }

    [AvaloniaFact]
    public void Attachments_Follow_The_Message()
    {
        var rig = new Rig(configure: TwoFiles);
        var kept = Items(rig)[0];
        rig.Message.Attachments.RemoveAt(1);
        rig.Message.Attachments.Add(new AiAttachment { Id = "c", Name = "runner-7.log" });
        rig.Settle();
        Items(rig).Select(i => i.FileName).ShouldBe(["stage-timings.csv", "runner-7.log"]);
        Items(rig)[0].ShouldBeSameAs(kept);
        rig.Message.Attachments[0].Name = "stage-timings-v2.csv";
        rig.Settle();
        Items(rig)[0].FileName.ShouldBe("stage-timings-v2.csv");
        rig.Message.Attachments.Clear();
        rig.Settle();
        rig.Row.AttachmentsPanel!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Files_Without_Text_Hide_The_Text_And_The_Gap()
    {
        var rig = new Rig("", configure: TwoFiles);
        rig.Row.TextBlock!.IsVisible.ShouldBeFalse();
        rig.Row.AttachmentsPanel!.Margin.ShouldBe(new Thickness(0, 2, 0, 2));
    }

    [AvaloniaFact]
    public void A_New_Message_Replaces_Text_And_Files()
    {
        var rig = new Rig(configure: TwoFiles);
        rig.Bubble.Message = new AiChatUserMessage { Text = "Flaky `login` test" };
        rig.Settle();
        rig.Row.TextBlock!.DisplayText.ShouldBe("Flaky login test");
        Items(rig).ShouldBeEmpty();
    }

    #endregion

    #region Actions

    private static double ActionsOpacity(Rig rig) => rig.Row.Actions!.Opacity;

    [AvaloniaFact]
    public void Copy_And_Edit_Hide_Until_Revealed()
    {
        var rig = new Rig();
        rig.Bubble.ShowsActions.ShouldBeFalse();
        ActionsOpacity(rig).ShouldBe(0);
        rig.Bubble.IsRevealed = true;
        rig.Settle();
        rig.Bubble.ShowsActions.ShouldBeTrue();
        ActionsOpacity(rig).ShouldBe(1);
        rig.Bubble.IsRevealed = false;
        rig.Bubble.AlwaysVisible = true;
        rig.Settle();
        ActionsOpacity(rig).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Hovering_Anywhere_On_The_Row_Shows_Them_With_A_Mouse()
    {
        var rig = new Rig();
        // The far left of the row: no bubble there, the transparent fill still hovers.
        var point = rig.Bubble.TranslatePoint(new Point(5, rig.Bubble.Bounds.Height - 5), rig.Window)!.Value;
        rig.Window.MouseMove(point, RawInputModifiers.None);
        rig.Settle();
        rig.Bubble.ShowsActions.ShouldBeTrue();
        rig.Window.MouseMove(new Point(890, 690), RawInputModifiers.None);
        rig.Settle();
        rig.Bubble.ShowsActions.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Touch_Does_Not_Hover()
    {
        var rig = new Rig();
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        rig.Bubble.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, rig.Bubble, pointer, rig.Window, default, 0,
            new PointerPointProperties(), KeyModifiers.None));
        rig.Settle();
        rig.Bubble.ShowsActions.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Inside_Shows_Them()
    {
        var rig = new Rig();
        rig.Row.CopyButton!.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Bubble.ShowsActions.ShouldBeTrue();
        ActionsOpacity(rig).ShouldBe(1);
        rig.Row.CopyButton.Classes.Contains(":focus-visible").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Actions_Sit_Left_Of_The_Bubble_4px_Up_From_Its_Bottom()
    {
        var rig = new Rig();
        var bubble = rig.Row.Bubble!.Bounds;
        var actions = rig.Row.Actions!;
        actions.Bounds.Right.ShouldBe(bubble.X - 4, 0.5);
        // The group's 4px bottom margin lifts the buttons off the bubble's bottom edge.
        actions.Bounds.Bottom.ShouldBe(bubble.Bottom - 4, 0.5);
        rig.Row.CopyButton!.Bounds.Size.ShouldBe(new Size(30, 30));
        rig.Row.EditButton!.Bounds.Size.ShouldBe(new Size(30, 30));
        var editBottom = rig.Row.EditButton.TranslatePoint(new Point(0, 30), rig.Row)!.Value.Y;
        editBottom.ShouldBe(bubble.Bottom - 4, 0.5);
        rig.Bubble.Bounds.Width.ShouldBe(760);
    }

    #endregion

    #region Copy

    [AvaloniaFact]
    public void Copy_Asks_With_The_Source_Text_And_Shows_A_Check()
    {
        var rig = new Rig(WithCode);
        rig.Click(rig.Row.CopyButton!);
        rig.Copied.ShouldBe([WithCode]);
        rig.Bubble.IsCopied.ShouldBeTrue();
        rig.Row.CopyButton!.ShownGlyph.ShouldBeSameAs(ArcIcons.Check);
        AutomationProperties.GetName(rig.Row.CopyButton).ShouldBe("Copied");
        Tooltip.GetTip(rig.Row.CopyButton).ShouldBe("Copied");
        rig.Schedule.Pending.Single().Delay.ShouldBe(TimeSpan.FromMilliseconds(1600));
    }

    [AvaloniaFact]
    public void The_Check_Goes_After_1600ms()
    {
        var rig = new Rig();
        rig.Bubble.Copy();
        rig.Schedule.Fire();
        rig.Settle();
        rig.Bubble.IsCopied.ShouldBeFalse();
        rig.Row.CopyButton!.ShownGlyph.ShouldBeSameAs(ArcIcons.Copy);
        AutomationProperties.GetName(rig.Row.CopyButton).ShouldBe("Copy");
    }

    [AvaloniaFact]
    public void Copying_Again_Restarts_The_Check()
    {
        var rig = new Rig();
        rig.Bubble.Copy();
        var first = rig.Schedule.Pending[0].Handle;
        rig.Bubble.Copy();
        first.IsDisposed.ShouldBeTrue();
        rig.Schedule.Pending.Count.ShouldBe(2);
        rig.Schedule.Fire();
        rig.Bubble.IsCopied.ShouldBeFalse();
        rig.Copied.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Unhandled_Copy_Writes_The_Clipboard_Itself_Without_Failing()
    {
        var rig = new Rig(handleCopy: false);
        rig.Bubble.Copy();
        rig.Settle();
        rig.Bubble.IsCopied.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Cancels_The_Check()
    {
        var rig = new Rig();
        rig.Bubble.Copy();
        rig.Window.Content = null;
        rig.Settle();
        rig.Bubble.IsCopied.ShouldBeFalse();
        rig.Schedule.Pending[0].Handle.IsDisposed.ShouldBeTrue();
    }

    #endregion

    #region Editing

    [AvaloniaFact]
    public void Edit_Opens_The_Editor_With_The_Source_Text_And_The_Caret_At_The_End()
    {
        var rig = new Rig(WithCode);
        rig.OpenEditor();
        rig.EditRequests.ShouldBe(1);
        rig.Bubble.IsEditing.ShouldBeTrue();
        rig.Bubble.Classes.Contains(":editing").ShouldBeTrue();
        rig.Editor.Text.ShouldBe(WithCode);
        rig.Area.Text.ShouldBe(WithCode);
        rig.Area.IsKeyboardFocusWithin.ShouldBeTrue();
        rig.Area.CaretIndex.ShouldBe(WithCode.Length);
        rig.Bubble.Row.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Handled_Edit_Request_Keeps_The_Bubble()
    {
        var rig = new Rig();
        rig.Bubble.EditRequested += (_, e) => e.Handled = true;
        rig.OpenEditor();
        rig.Bubble.IsEditing.ShouldBeFalse();
        rig.Bubble.Editor.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Enter_Sends_A_Changed_Text_Trimmed()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        rig.Type(", please  ");
        rig.Key(PhysicalKey.Enter);
        rig.Submitted.ShouldBe(["Keep it short, please"]);
        rig.Cancelled.ShouldBe(0);
        rig.Bubble.IsEditing.ShouldBeFalse();
        rig.Bubble.Row.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Shift_Enter_Adds_A_Line()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Submitted.ShouldBeEmpty();
        rig.Bubble.IsEditing.ShouldBeTrue();
        rig.Editor.Text.Replace("\r", "").ShouldBe("Keep it short\n");
    }

    [AvaloniaFact]
    public void Escape_Cancels_From_The_Text_Area()
    {
        var rig = new Rig();
        rig.OpenEditor();
        rig.Type(" now");
        rig.Key(PhysicalKey.Escape);
        rig.Cancelled.ShouldBe(1);
        rig.Submitted.ShouldBeEmpty();
        rig.Bubble.IsEditing.ShouldBeFalse();
        rig.Message.Text.ShouldBe(Short);
    }

    [AvaloniaFact]
    public void Escape_On_A_Button_Does_Not_Cancel()
    {
        var rig = new Rig();
        rig.OpenEditor();
        rig.Editor.SendButton!.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Escape);
        rig.Bubble.IsEditing.ShouldBeTrue();
        rig.Cancelled.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Enter_And_Escape_Belong_To_A_Composing_Input_Method()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        rig.Type(" more");
        var presenter = rig.Area.Box!.Presenter!;
        presenter.PreeditText = "hao";
        rig.Area.IsComposing.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Escape);
        rig.Bubble.IsEditing.ShouldBeTrue();
        rig.Submitted.ShouldBeEmpty();
        rig.Cancelled.ShouldBe(0);
        presenter.PreeditText = null;
        rig.Key(PhysicalKey.Enter);
        rig.Submitted.ShouldBe(["Keep it short more"]);
    }

    [AvaloniaFact]
    public void An_Empty_Draft_Cannot_Be_Sent()
    {
        var rig = new Rig();
        rig.OpenEditor();
        rig.Editor.Text = "   \n  ";
        rig.Settle();
        rig.Editor.CanSend.ShouldBeFalse();
        rig.Editor.SendButton!.IsEnabled.ShouldBeFalse();
        rig.Area.Focus();
        rig.Key(PhysicalKey.Enter);
        rig.Bubble.IsEditing.ShouldBeTrue();
        rig.Submitted.ShouldBeEmpty();
        rig.Cancelled.ShouldBe(0);
    }

    [AvaloniaFact]
    public void An_Unchanged_Draft_Ends_As_A_Cancel()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        rig.Type("  ");
        rig.Editor.CanSend.ShouldBeTrue();
        rig.Editor.IsUnchanged.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Submitted.ShouldBeEmpty();
        rig.Cancelled.ShouldBe(1);
        rig.Bubble.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Send_And_Cancel_Buttons_End_The_Edit()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        rig.Type(" and clear");
        rig.Click(rig.Editor.SendButton!);
        rig.Submitted.ShouldBe(["Keep it short and clear"]);
        rig.OpenEditor();
        rig.Click(rig.Editor.CancelButton!);
        rig.Cancelled.ShouldBe(1);
        rig.Bubble.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Setting_IsEditing_Opens_And_Closes_Without_Events()
    {
        var rig = new Rig();
        rig.Bubble.IsEditing = true;
        rig.Settle();
        rig.Bubble.Editor.ShouldNotBeNull();
        rig.EditRequests.ShouldBe(0);
        rig.Bubble.IsEditing = false;
        rig.Settle();
        rig.Bubble.Row.ShouldNotBeNull();
        rig.Cancelled.ShouldBe(0);
        rig.Submitted.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Handler_Can_Show_The_New_Version_In_The_Returning_Bubble()
    {
        var rig = new Rig("Keep it short");
        rig.Bubble.EditSubmitted += (_, e) => rig.Bubble.Message = new AiChatUserMessage { Text = e.Text };
        rig.OpenEditor();
        rig.Type(" and `clear`");
        rig.Key(PhysicalKey.Enter);
        rig.Row.TextBlock!.DisplayText.ShouldBe("Keep it short and clear");
        rig.Row.TextBlock.Chips.Count.ShouldBe(1);
    }

    #endregion

    #region Focus

    [AvaloniaFact]
    public void Escape_Returns_The_Focus_To_Edit_With_The_Ring()
    {
        var rig = new Rig();
        rig.OpenEditor();
        rig.Key(PhysicalKey.Escape);
        rig.Settle();
        rig.Focused.ShouldBeSameAs(rig.Row.EditButton);
        rig.Row.EditButton!.Classes.Contains(":focus-visible").ShouldBeTrue();
        rig.Bubble.ShowsActions.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Enter_Returns_The_Focus_To_Edit_After_Sending()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        rig.Type("!");
        rig.Key(PhysicalKey.Enter);
        rig.Focused.ShouldBeSameAs(rig.Row.EditButton);
    }

    [AvaloniaFact]
    public void A_Click_On_Cancel_Returns_The_Focus_Without_The_Ring()
    {
        var rig = new Rig();
        rig.OpenEditor();
        rig.Click(rig.Editor.CancelButton!);
        rig.Focused.ShouldBeSameAs(rig.Row.EditButton);
        rig.Row.EditButton!.Classes.Contains(":focus-visible").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Enter_On_The_Cancel_Button_Counts_As_Keyboard()
    {
        var rig = new Rig();
        rig.OpenEditor();
        rig.Editor.CancelButton!.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        rig.Cancelled.ShouldBe(1);
        rig.Focused.ShouldBeSameAs(rig.Row.EditButton);
        rig.Row.EditButton!.Classes.Contains(":focus-visible").ShouldBeTrue();
    }

    #endregion

    #region Automation

    [AvaloniaFact]
    public void Screen_Readers_Get_Names_From_The_Owner()
    {
        var rig = new Rig(WithCode);
        AutomationProperties.GetName(rig.Row.CopyButton!).ShouldBe("Copy");
        AutomationProperties.GetName(rig.Row.EditButton!).ShouldBe("Edit");
        Tooltip.GetTip(rig.Row.EditButton!).ShouldBe("Edit");
        Tooltip.GetSide(rig.Row.EditButton!).ShouldBe(TooltipSide.Bottom);
        AutomationProperties.GetName(rig.Row.TextBlock!).ShouldBe("Why does restore-keys miss when the cache-key changes?");
        rig.Bubble.CopyText = "Copy message";
        rig.Settle();
        AutomationProperties.GetName(rig.Row.CopyButton!).ShouldBe("Copy message");
        rig.OpenEditor();
        AutomationProperties.GetName(rig.Area).ShouldBe("Edit message");
        AutomationProperties.GetName(rig.Area.Box!).ShouldBe("Edit message");
        AutomationProperties.GetName(rig.Editor.CancelButton!).ShouldBe("Cancel");
        AutomationProperties.GetName(rig.Editor.SendButton!).ShouldBe("Send");
        rig.Editor.CancelButton!.Content.ShouldBe("Cancel");
        rig.Editor.SendButton!.Content.ShouldBe("Send");
    }

    [AvaloniaFact]
    public void The_Bubble_Holds_No_Words_Of_Its_Own()
    {
        var bubble = new AiUserBubble { Message = new AiChatUserMessage { Text = Short } };
        var window = TestHelpers.Show(bubble);
        try
        {
            AutomationProperties.GetName(bubble.Row!.CopyButton!).ShouldBeNull();
            AutomationProperties.GetName(bubble.Row.EditButton!).ShouldBeNull();
            AutomationProperties.GetName(bubble.Row.AttachmentsPanel!).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Widths

    [AvaloniaFact]
    public void The_Bubble_Is_At_Most_544_Or_82_Percent()
    {
        AiUserBubbleLane.BubbleLimit(760, false).ShouldBe(544);
        AiUserBubbleLane.BubbleLimit(600, false).ShouldBe(492, 0.001);
        AiUserBubbleLane.BubbleLimit(355, true).ShouldBe(312.4, 0.001);
        AiUserBubbleLane.BubbleLimit(double.PositiveInfinity, true).ShouldBe(544);
    }

    [AvaloniaFact]
    public void A_Narrow_Row_Gives_The_Buttons_Room_First()
    {
        // Arc at 390: the column is 355, 88% would be 312, the buttons take 64, so the bubble gets 291.
        var rig = new Rig("word word word word word word word word word", width: 355, narrow: true);
        rig.Row.Bubble!.Bounds.Width.ShouldBe(355 - 60 - 4, 0.5);
        rig.Row.Actions!.Bounds.X.ShouldBe(0, 0.5);
    }

    [AvaloniaFact]
    public void The_Form_Is_As_Wide_As_The_Widest_Bubble_Even_For_A_Short_Message()
    {
        var rig = new Rig("Hi");
        rig.Row.Bubble!.Bounds.Width.ShouldBeLessThan(200);
        rig.OpenEditor();
        rig.Editor.Form!.Bounds.Width.ShouldBe(544, 0.5);
        rig.Editor.Form.Bounds.Right.ShouldBe(760, 0.5);
        var narrow = new Rig("Hi", width: 355, narrow: true);
        narrow.OpenEditor();
        // Layout rounding puts 312.4 on a whole pixel.
        narrow.Editor.Form!.Bounds.Width.ShouldBe(355 * 0.88, 1);
    }

    [AvaloniaFact]
    public void The_Form_Grows_By_24_Per_Line()
    {
        var rig = new Rig("Keep it short");
        rig.OpenEditor();
        // 1 + 10 + (24 + 4) + 8 + 36 + 10 + 1.
        rig.Editor.Form!.Bounds.Height.ShouldBe(94, 0.5);
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Type("x");
        rig.Editor.Form.Bounds.Height.ShouldBe(118, 0.5);
        rig.Bubble.Bounds.Height.ShouldBe(118, 0.5);
    }

    #endregion

    #region Motion

    private static List<ContentPresenter> Layers(Rig rig) =>
        rig.Bubble.Swap!.GetVisualChildren().OfType<ContentPresenter>().ToList();

    /// <summary>
    /// Waits (briefly, in real time) for the swap to drop a finished layer: it removes it from a task continuation that
    /// posts back to the UI thread.
    /// </summary>
    private static int SettledLayers(Rig rig)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (Layers(rig).Count > 1 && clock.ElapsedMilliseconds < 2000)
        {
            Thread.Sleep(5);
            rig.Settle();
        }
        return Layers(rig).Count;
    }

    [AvaloniaFact]
    public void Off_Swaps_And_Sizes_At_Once()
    {
        var rig = new Rig();
        var before = rig.Bubble.Bounds.Height;
        rig.OpenEditor();
        Layers(rig).Count.ShouldBe(1);
        rig.Bubble.Sizer!.IsMoving.ShouldBeFalse();
        rig.Bubble.Bounds.Height.ShouldBe(rig.Editor.Bounds.Height, 0.5);
        rig.Bubble.Bounds.Height.ShouldBeGreaterThan(before);
    }

    [AvaloniaFact]
    public void Full_Crossfades_And_Springs_The_Height_From_The_Bottom()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig("Keep it short", level: ArcMotionLevel.Full);
        var start = rig.Bubble.Bounds.Height;
        rig.Bubble.BeginEdit();
        rig.Settle();
        var sizer = rig.Bubble.Sizer!;
        sizer.IsMoving.ShouldBeTrue();
        Layers(rig).Count.ShouldBe(2);
        var target = sizer.TargetHeight;
        target.ShouldBe(94, 0.5);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        rig.Settle();
        var mid = rig.Bubble.Bounds.Height;
        mid.ShouldBeGreaterThan(start);
        mid.ShouldBeLessThan(target);
        // Anchored at the bottom: the form's top is above the row, clipped, so it slides in from above.
        var formTop = rig.Editor.TranslatePoint(default, rig.Bubble)!.Value.Y;
        formTop.ShouldBe(mid - target, 0.6);
        sizer.Clip.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromMilliseconds(900));
        rig.Settle();
        sizer.IsMoving.ShouldBeFalse();
        rig.Bubble.Bounds.Height.ShouldBe(target, 0.5);
        sizer.Clip.ShouldBeNull();
        SettledLayers(rig).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Typing_A_New_Line_Does_Not_Spring_The_Row()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig("Keep it short", level: ArcMotionLevel.Full);
        rig.Bubble.BeginEdit();
        rig.Settle();
        clock.Advance(TimeSpan.FromSeconds(1));
        rig.Settle();
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Type("x");
        rig.Bubble.Sizer!.IsMoving.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Reduced_Crossfades_Without_Moving_The_Height()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig("Keep it short", level: ArcMotionLevel.Reduced);
        rig.Bubble.BeginEdit();
        rig.Settle();
        rig.Bubble.Sizer!.IsMoving.ShouldBeFalse();
        rig.Bubble.Bounds.Height.ShouldBe(94, 0.5);
        Layers(rig).Count.ShouldBe(2);
        clock.Advance(TimeSpan.FromMilliseconds(200));
        rig.Settle();
        SettledLayers(rig).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Cancelling_Mid_Swap_Builds_A_Fresh_Row()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig("Keep it short", level: ArcMotionLevel.Full);
        var first = rig.Row;
        rig.Bubble.BeginEdit();
        rig.Settle();
        rig.Key(PhysicalKey.Escape);
        rig.Bubble.Row.ShouldNotBeNull();
        rig.Bubble.Row.ShouldNotBeSameAs(first);
        clock.Advance(TimeSpan.FromSeconds(1));
        rig.Settle();
        SettledLayers(rig).ShouldBe(1);
        rig.Bubble.Bounds.Height.ShouldBe(rig.Row.Bounds.Height, 0.5);
    }

    #endregion
}

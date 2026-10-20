using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.SpecialInputs;

public class MentionInputTests
{
    private static readonly MentionPerson[] People =
    [
        new("u_maya", "Maya Chen") { Role = "Product designer" },
        new("u_jordan", "Jordan Lee") { Role = "Engineering manager" },
        new("u_emma", "Emma Schultz") { Role = "Marketing" },
        new("u_sam", "Sam Okafor") { Role = "Data scientist" }
    ];

    private static readonly MentionChannel[] Channels =
    [
        new("c_design", "design") { Description = "Critiques and reviews", Members = 48 },
        new("c_eng", "engineering") { Description = "Builds and deploys", Members = 132 }
    ];

    private static MentionInput Create(Action<MentionInput>? configure = null)
    {
        var input = new MentionInput { Label = "Comment", People = People, Channels = Channels, Width = 360 };
        configure?.Invoke(input);
        return input;
    }

    private static Window Show(Control control, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } }, level);
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static MentionInput Focused(MentionInput input)
    {
        input.Focus();
        Dispatcher.UIThread.RunJobs();
        return input;
    }

    private static Mention Person(string text, string label, string id)
    {
        var start = text.IndexOf("@" + label, StringComparison.Ordinal);
        return new Mention(MentionKind.Person, id, label, start, start + label.Length + 1);
    }

    #region Model

    [Fact]
    public void Serializer_Uses_Ids_By_Default_And_Accepts_A_Format()
    {
        const string text = "Hi @Maya Chen, see #design";
        var value = new MentionValue(text,
        [
            new Mention(MentionKind.Channel, "c_design", "design", 19, 26),
            new Mention(MentionKind.Person, "u_maya", "Maya Chen", 3, 13)
        ]);
        value.Mentions[0].Start.ShouldBe(3); // sorted
        MentionSerializer.Serialize(value).ShouldBe("Hi <@u_maya>, see <#c_design>");
        MentionSerializer.Serialize(value, m => $"[{m.Label}]").ShouldBe("Hi [Maya Chen], see [design]");
        MentionSerializer.MentionText(MentionKind.Person, "Maya").ShouldBe("@Maya");
        MentionSerializer.MentionText(MentionKind.Channel, "design").ShouldBe("#design");
        value.Equals(new MentionValue(text, value.Mentions)).ShouldBeTrue();
        value.Equals(new MentionValue(text)).ShouldBeFalse();
    }

    [Fact]
    public void Reconcile_Moves_Mentions_And_Drops_Touched_Ones()
    {
        const string text = "Hi @Maya Chen and @Sam Okafor";
        var value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya"), Person(text, "Sam Okafor", "u_sam")]);
        // Typing before both shifts them.
        var moved = MentionInputEngine.Reconcile(value, "Oh hi @Maya Chen and @Sam Okafor", 0);
        moved.Select(m => m.Start).ShouldBe([6, 21]);
        // Editing inside the first drops only it.
        var touched = MentionInputEngine.Reconcile(value, "Hi @Maya Chn and @Sam Okafor", 10);
        touched.Count.ShouldBe(1);
        touched[0].Label.ShouldBe("Sam Okafor");
        touched[0].Start.ShouldBe(17);
        // Repeated characters settle with the caret hint: deleting one of the two spaces after "Hi".
        var spaced = new MentionValue("Hi  @Maya Chen", [new Mention(MentionKind.Person, "u_maya", "Maya Chen", 4, 14)]);
        MentionInputEngine.Reconcile(spaced, "Hi @Maya Chen", 3).Single().Start.ShouldBe(3);
    }

    [Fact]
    public void FindTrigger_Follows_Arc_Rules()
    {
        MentionInputEngine.FindTrigger("@ma", 3, [], true, true).ShouldBe(new MentionInputTrigger(MentionKind.Person, 0, "ma"));
        MentionInputEngine.FindTrigger("hi (@ma", 7, [], true, true)!.Value.Start.ShouldBe(4);
        MentionInputEngine.FindTrigger("mail@ma", 7, [], true, true).ShouldBeNull(); // not after whitespace
        MentionInputEngine.FindTrigger("@maya chen", 10, [], true, true)!.Value.Query.ShouldBe("maya chen"); // one space
        MentionInputEngine.FindTrigger("@a b c", 6, [], true, true).ShouldBeNull(); // two spaces
        MentionInputEngine.FindTrigger("#des ign", 8, [], true, true).ShouldBeNull(); // channels take no space
        MentionInputEngine.FindTrigger("@ma\nx", 5, [], true, true).ShouldBeNull(); // never across a line
        MentionInputEngine.FindTrigger("@ma", 3, [], false, true).ShouldBeNull(); // people off
        MentionInputEngine.FindTrigger("@" + new string('a', 48), 49, [], true, true).ShouldBeNull(); // beyond 48 characters
        var inside = new Mention(MentionKind.Person, "u", "Maya", 0, 5);
        MentionInputEngine.FindTrigger("@Maya x", 7, [inside], true, true).ShouldBeNull(); // inside a mention
    }

    [Fact]
    public void Ranking_Prefers_Prefix_Then_Word_Start_Then_Contains_Then_Meta()
    {
        MentionInputEngine.Rank("Maya Chen", "Designer", "ma").ShouldBe(4);
        MentionInputEngine.Rank("Jordan Lee", null, "le").ShouldBe(3);
        MentionInputEngine.Rank("Emma Schultz", null, "ma").ShouldBe(2);
        MentionInputEngine.Rank("Sam Okafor", "Data scientist", "sci").ShouldBe(1);
        MentionInputEngine.Rank("Sam Okafor", null, "zz").ShouldBe(0);
        MentionInputEngine.Rank("Sam Okafor", null, "").ShouldBe(1);
        var trigger = new MentionInputTrigger(MentionKind.Person, 0, "ma");
        MentionInputEngine.Suggest(trigger, People, Channels, 6).Select(s => s.Name).ShouldBe(["Maya Chen", "Emma Schultz", "Jordan Lee"]);
        MentionInputEngine.Suggest(trigger, People, Channels, 1).Count.ShouldBe(1);
        MentionInputEngine.Initials("Maya  Chen Li").ShouldBe("MC");
    }

    #endregion

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            input.Value.ShouldBe(MentionValue.Empty);
            input.MinLines.ShouldBe(1);
            input.MaxLines.ShouldBe(8);
            input.MaxSuggestions.ShouldBe(6);
            input.SuggestionPlacement.ShouldBe(MentionSuggestionPlacement.Auto);
            input.IsSubmitOnEnter.ShouldBeFalse();
            MentionInput.ValueProperty.GetMetadata(typeof(MentionInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
            var shell = TestHelpers.Descendant<Border>(input, MentionInput.ShellPart);
            shell.Bounds.Height.ShouldBe(48, 0.5); // 24px line + 11px padding twice + 1px border twice
            shell.CornerRadius.TopLeft.ShouldBe(18);
            var editor = input.Editor!;
            editor.AcceptsReturn.ShouldBeTrue();
            editor.TextWrapping.ShouldBe(TextWrapping.Wrap);
            editor.FontSize.ShouldBe(16);
            editor.LineHeight.ShouldBe(24);
            editor.Padding.ShouldBe(new Thickness(14, 11));
            TestHelpers.ColorOf(editor.Foreground).A.ShouldBe((byte)0); // the mirror paints the text
            AutomationProperties.GetName(editor).ShouldBe("Comment");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Border_Strengthens_On_Focus_Only()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            var shell = TestHelpers.Descendant<Border>(input, MentionInput.ShellPart);
            var rest = TestHelpers.ColorOf(shell.BorderBrush);
            rest.ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.Border));
            Focused(input);
            input.Editor!.IsFocused.ShouldBeTrue();
            TestHelpers.ColorOf(shell.BorderBrush).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.BorderStrong));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_A_Trigger_Opens_Ranked_Suggestions_And_Announces_The_Count()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "Hi @ma");
            input.IsSuggestionsOpen.ShouldBeTrue();
            input.Suggestions.Select(s => s.Name).ShouldBe(["Maya Chen", "Emma Schultz", "Jordan Lee"]);
            input.LiveRegion!.Message.ShouldBe("3 people");
            input.SuggestionList!.ListName.ShouldBe("People");
            AutomationProperties.GetItemStatus(input.Editor!).ShouldBe("Maya Chen, Product designer");
            Type(window, "y");
            input.Suggestions.Count.ShouldBe(1);
            input.LiveRegion.Message.ShouldBe("1 person");
            Type(window, "#");
            input.IsSuggestionsOpen.ShouldBeFalse(); // '#' inside a query breaks it
            input.LiveRegion.Message.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Wrap_And_Enter_Chooses_Through_The_TextBox()
    {
        var input = Create();
        var window = Show(input);
        Mention? added = null;
        input.MentionAdded += (_, e) => added = e.Mention;
        try
        {
            Focused(input);
            Type(window, "@ma");
            input.ActiveSuggestion.ShouldBe(0);
            Press(window, PhysicalKey.ArrowUp);
            input.ActiveSuggestion.ShouldBe(2); // wraps to the end
            Press(window, PhysicalKey.ArrowDown);
            input.ActiveSuggestion.ShouldBe(0); // wraps back
            Press(window, PhysicalKey.ArrowDown);
            input.SuggestionList!.ActiveIndex.ShouldBe(1);
            Press(window, PhysicalKey.Enter);
            input.Value.Text.ShouldBe("@Emma Schultz ");
            input.Value.Mentions.Single().ShouldBe(new Mention(MentionKind.Person, "u_emma", "Emma Schultz", 0, 13));
            input.Mentions.Count.ShouldBe(1);
            added.ShouldBe(input.Value.Mentions[0]);
            input.IsSuggestionsOpen.ShouldBeFalse();
            input.Editor!.CaretIndex.ShouldBe(14);
            // The insertion went through the TextBox: undo takes it back (and the mention with it).
            input.Editor.CanUndo.ShouldBeTrue();
            input.Editor.Undo();
            Dispatcher.UIThread.RunJobs();
            input.Value.Text.ShouldBe("@ma");
            input.Value.Mentions.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Chooses_And_A_Following_Space_Is_Not_Doubled()
    {
        var input = Create(i => i.Value = new MentionValue("#des now"));
        var window = Show(input);
        try
        {
            Focused(input);
            input.Editor!.Select(4, 4);
            Dispatcher.UIThread.RunJobs();
            input.IsSuggestionsOpen.ShouldBeTrue();
            input.SuggestionList!.ListName.ShouldBe("Channels");
            input.LiveRegion!.Message.ShouldBe("1 channel");
            Press(window, PhysicalKey.Tab);
            input.Value.Text.ShouldBe("#design now");
            input.Value.Mentions.Single().Kind.ShouldBe(MentionKind.Channel);
            input.Editor.IsFocused.ShouldBeTrue(); // Tab was consumed
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Dismisses_This_Trigger_Only()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "@ma");
            Press(window, PhysicalKey.Escape);
            input.IsSuggestionsOpen.ShouldBeFalse();
            Type(window, "y");
            input.IsSuggestionsOpen.ShouldBeFalse(); // same trigger stays dismissed
            Type(window, " and @s");
            input.IsSuggestionsOpen.ShouldBeTrue(); // a new trigger opens again
            input.Suggestions[0].Name.ShouldBe("Sam Okafor");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_State_Shows_And_A_Continuing_Sentence_Closes()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "@zz");
            input.IsSuggestionsOpen.ShouldBeTrue();
            input.Suggestions.ShouldBeEmpty();
            input.SuggestionList!.EmptyText.ShouldBe("No people match “zz”");
            input.LiveRegion!.Message.ShouldBe("No matches");
            Type(window, " t");
            input.IsSuggestionsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Kinds_Without_Items_Never_Open()
    {
        var input = Create(i => i.People = Array.Empty<MentionPerson>());
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "@ma");
            input.IsSuggestionsOpen.ShouldBeFalse();
            Type(window, " #d");
            input.IsSuggestionsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_And_Delete_Remove_The_Whole_Token_In_One_Keystroke()
    {
        const string text = "Hi @Maya Chen ok";
        var input = Create(i => i.Value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya")]));
        var window = Show(input);
        try
        {
            Focused(input);
            input.Editor!.Select(13, 13);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Backspace);
            input.Value.Text.ShouldBe("Hi  ok");
            input.Value.Mentions.ShouldBeEmpty();
            input.Editor.Undo();
            Dispatcher.UIThread.RunJobs();
            input.Value.Text.ShouldBe(text); // undo restores the text

            input.Value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya")]);
            input.Editor.Select(3, 3);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Delete);
            input.Value.Text.ShouldBe("Hi  ok");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Caret_Never_Rests_Inside_A_Token()
    {
        const string text = "Hi @Maya Chen ok";
        var input = Create(i => i.Value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya")]));
        var window = Show(input);
        try
        {
            Focused(input);
            var editor = input.Editor!;
            editor.Select(13, 13);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowLeft);
            editor.CaretIndex.ShouldBe(3); // one step hops over the token
            Press(window, PhysicalKey.ArrowRight);
            editor.CaretIndex.ShouldBe(13);
            // A jump into the middle (a click) lands on the nearest edge.
            editor.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            editor.CaretIndex.ShouldBe(3);
            editor.CaretIndex = 11;
            Dispatcher.UIThread.RunJobs();
            editor.CaretIndex.ShouldBe(13);
            // Selections grow to whole tokens.
            editor.Select(1, 6);
            Dispatcher.UIThread.RunJobs();
            editor.SelectionStart.ShouldBe(1);
            editor.SelectionEnd.ShouldBe(13);
            editor.Select(15, 9);
            Dispatcher.UIThread.RunJobs();
            editor.SelectionStart.ShouldBe(15);
            editor.SelectionEnd.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editing_Around_A_Token_Moves_It_And_Editing_Inside_Drops_It()
    {
        const string text = "Hi @Maya Chen";
        var input = Create(i => i.Value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya")]));
        var window = Show(input);
        try
        {
            Focused(input);
            input.Editor!.Select(0, 0);
            Dispatcher.UIThread.RunJobs();
            Type(window, "Oh ");
            input.Value.Mentions.Single().Start.ShouldBe(6);
            input.Editor.Select(input.Value.Text.Length, input.Value.Text.Length);
            Dispatcher.UIThread.RunJobs();
            Type(window, "!");
            input.Value.Mentions.Single().End.ShouldBe(16);
            // Typing right after the token keeps it: the token text is untouched.
            input.Value.Text.ShouldBe("Oh Hi @Maya Chen!");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Submits_With_IsSubmitOnEnter_And_Shift_Enter_Adds_A_Line()
    {
        var submitted = new List<MentionValue>();
        var command = new RecordingCommand();
        var input = Create(i =>
        {
            i.IsSubmitOnEnter = true;
            i.SubmitCommand = command;
        });
        input.Submitted += (_, e) => submitted.Add(e.Value);
        var window = Show(input);
        try
        {
            Focused(input);
            Press(window, PhysicalKey.Enter);
            submitted.ShouldBeEmpty(); // blank text never submits
            Type(window, "Ship it");
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            input.Value.Text.ShouldBe("Ship it\n");
            Type(window, "@ma");
            Press(window, PhysicalKey.Enter); // picks the suggestion, does not submit
            submitted.ShouldBeEmpty();
            Press(window, PhysicalKey.Enter);
            submitted.Single().Text.ShouldBe("Ship it\n@Maya Chen ");
            command.Executed.Single().ShouldBe(submitted[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_IsSubmitOnEnter_Enter_Adds_A_Line()
    {
        var input = Create();
        var submitted = 0;
        input.Submitted += (_, _) => submitted++;
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "one");
            Press(window, PhysicalKey.Enter);
            Type(window, "two");
            input.Value.Text.ShouldBe("one\ntwo");
            submitted.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Height_Grows_Line_By_Line_Up_To_MaxLines()
    {
        var input = Create(i =>
        {
            i.MinLines = 2;
            i.MaxLines = 4;
        });
        var window = Show(input);
        try
        {
            var shell = TestHelpers.Descendant<Border>(input, MentionInput.ShellPart);
            shell.Bounds.Height.ShouldBe(2 * 24 + 22 + 2, 0.5);
            Focused(input);
            Type(window, "a");
            Press(window, PhysicalKey.Enter);
            Type(window, "b");
            Press(window, PhysicalKey.Enter);
            Type(window, "c");
            shell.Bounds.Height.ShouldBe(3 * 24 + 22 + 2, 0.5);
            for (var i = 0; i < 4; i++)
            {
                Press(window, PhysicalKey.Enter);
                Type(window, "x");
            }
            input.TargetFieldHeight.ShouldBe(4 * 24 + 22, 0.5);
            shell.Bounds.Height.ShouldBe(4 * 24 + 22 + 2, 0.5);
            input.Editor!.Scroller!.Extent.Height.ShouldBeGreaterThan(input.Editor.Scroller.Viewport.Height);
            input.Editor.Scroller.VerticalScrollBarVisibility.ShouldBe(Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Trailing_Line_Break_Grows_The_Field()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "a");
            Press(window, PhysicalKey.Enter);
            TestHelpers.Descendant<Border>(input, MentionInput.ShellPart).Bounds.Height.ShouldBe(2 * 24 + 22 + 2, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Placement_Top_Opens_Above_And_Bottom_Below()
    {
        var input = Create(i => i.SuggestionPlacement = MentionSuggestionPlacement.Top);
        // Room above for the panel, so the positioner keeps the requested side.
        var window = Show(new StackPanel { Margin = new Thickness(0, 360, 0, 0), Children = { input } });
        try
        {
            Focused(input);
            Type(window, "@m");
            input.SuggestionPopup!.IsOpen.ShouldBeTrue();
            input.SuggestionPopup.IsAbove.ShouldBeTrue();
            input.SuggestionPopup.MarginToAnchor.ShouldBe(6);
            var (trigger, surface) = Geometry(input);
            surface.Bottom.ShouldBe(trigger.Top - 6, 1); // 6px above the line
            surface.X.ShouldBe(trigger.X - 10, 1);
            Press(window, PhysicalKey.Escape);
            input.SuggestionPlacement = MentionSuggestionPlacement.Bottom;
            Type(window, " @j");
            input.SuggestionPopup.IsAbove.ShouldBeFalse();
            (trigger, surface) = Geometry(input);
            surface.Top.ShouldBe(trigger.Bottom + 6, 1); // 6px below the line
            surface.X.ShouldBe(trigger.X - 10, 1);
            surface.Width.ShouldBe(272);
            var anchor = input.SuggestionPopup.PlacementRect!.Value;
            anchor.Height.ShouldBe(24, 0.5);
            anchor.X.ShouldBeGreaterThanOrEqualTo(0);
            anchor.X.ShouldBeLessThanOrEqualTo(input.Bounds.Width - 272 + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_Is_Narrower_On_A_Narrow_Field_And_Stays_Inside_It()
    {
        var input = Create(i => i.Width = 200);
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "@m");
            var surface = input.SuggestionList!.GetVisualParent<Border>()!;
            surface.Width.ShouldBe(200);
            input.SuggestionPopup!.PlacementRect!.Value.X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Row_Chooses_It_And_Keeps_Focus_In_The_Editor()
    {
        var input = Create();
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "@");
            var rows = input.SuggestionList!.Rows;
            rows.Count.ShouldBe(4);
            var row = rows[1];
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            input.ActiveSuggestion.ShouldBe(1); // pointer moves activate rows
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Editor!.IsFocused.ShouldBeTrue();
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Value.Text.ShouldBe("@Jordan Lee ");
            input.Editor.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Presses_In_The_Field_Move_The_Caret_Without_Light_Dismiss()
    {
        var input = Create();
        var outside = new Border { Height = 80, Background = Brushes.Transparent };
        var window = Show(new StackPanel { Children = { input, outside } });
        try
        {
            Focused(input);
            Type(window, "@ma");
            input.SuggestionPopup!.IsLightDismissEnabled.ShouldBeFalse();
            var shell = TestHelpers.Descendant<Border>(input, MentionInput.ShellPart);
            var end = shell.TranslatePoint(new Point(shell.Bounds.Width - 20, shell.Bounds.Height / 2), window)!.Value;
            window.MouseDown(end, MouseButton.Left);
            window.MouseUp(end, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Editor!.IsFocused.ShouldBeTrue();
            input.IsSuggestionsOpen.ShouldBeTrue(); // the caret is still at the end of the query
            var start = shell.TranslatePoint(new Point(4, shell.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Editor.CaretIndex.ShouldBe(0);
            input.IsSuggestionsOpen.ShouldBeFalse(); // the caret left the trigger
            Press(window, PhysicalKey.End);
            input.IsSuggestionsOpen.ShouldBeTrue();
            // A press elsewhere closes the list, as the textarea's blur does in a browser.
            var away = outside.TranslatePoint(new Point(outside.Bounds.Width - 10, 40), window)!.Value; // clear of the panel
            window.MouseDown(away, MouseButton.Left);
            window.MouseUp(away, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.IsSuggestionsOpen.ShouldBeFalse();
            Type(window, "y");
            input.IsSuggestionsOpen.ShouldBeTrue(); // typing again reopens it
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Programmatic_Value_Updates_The_Editor_Quietly()
    {
        var input = Create();
        var changes = new List<MentionInputValueChangedEventArgs>();
        input.ValueChanged += (_, e) => changes.Add(e);
        var window = Show(input);
        try
        {
            const string text = "Ping @Maya Chen";
            var value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya")]);
            input.Value = value;
            Dispatcher.UIThread.RunJobs();
            input.Editor!.Text.ShouldBe(text);
            input.Mentions.Single().Id.ShouldBe("u_maya");
            changes.Single().NewValue.ShouldBe(value);
            changes[0].OldValue.ShouldBe(MentionValue.Empty);
            input.Value = new MentionValue(text, [Person(text, "Maya Chen", "u_maya")]);
            changes.Count.ShouldBe(1); // equal values are not a change
            input.LiveRegion!.Message.ShouldBe(string.Empty);
            var mirror = TestHelpers.Descendant<MentionInputMirror>(input);
            mirror.EnsureLayout()!.TextLines.Count.ShouldBeGreaterThan(0);
            input.Editor.Presenter!.Tokens.Single().Start.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Insert_OpenSuggestions_And_Clear()
    {
        var input = Create(i => i.Value = new MentionValue("Hello"));
        var window = Show(input);
        try
        {
            input.OpenSuggestions(MentionKind.Person);
            Dispatcher.UIThread.RunJobs();
            input.Value.Text.ShouldBe("Hello @");
            input.Editor!.IsFocused.ShouldBeTrue();
            input.IsSuggestionsOpen.ShouldBeTrue();
            input.Insert("sa");
            Dispatcher.UIThread.RunJobs();
            input.Value.Text.ShouldBe("Hello @sa");
            input.Suggestions[0].Name.ShouldBe("Sam Okafor");
            input.Clear();
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(MentionValue.Empty);
            input.Editor.Text.ShouldBe(string.Empty);
            input.IsSuggestionsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Goes_To_The_Editor_And_Blur_Closes_The_List()
    {
        var other = new TextBox();
        var input = Create();
        var window = Show(new StackPanel { Children = { input, other } });
        try
        {
            Focused(input);
            input.Editor!.IsFocused.ShouldBeTrue();
            Type(window, "@m");
            input.IsSuggestionsOpen.ShouldBeTrue();
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            input.IsSuggestionsOpen.ShouldBeFalse();
            input.SuggestionPopup!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Fades_The_Root_And_Takes_No_Input()
    {
        var input = Create(i => i.IsEnabled = false);
        var window = Show(input);
        try
        {
            input.Opacity.ShouldBe(0.55, 0.001);
            input.Editor!.IsEffectivelyEnabled.ShouldBeFalse();
            input.Focus();
            Dispatcher.UIThread.RunJobs();
            input.Editor.IsFocused.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mirror_Tints_Tokens_With_Their_Kind()
    {
        const string text = "@Maya Chen in #design";
        var input = Create(i => i.Value = new MentionValue(text,
        [
            Person(text, "Maya Chen", "u_maya"),
            new Mention(MentionKind.Channel, "c_design", "design", 14, 21)
        ]));
        var window = Show(input);
        try
        {
            var mirror = TestHelpers.Descendant<MentionInputMirror>(input);
            TestHelpers.ColorOf(mirror.PersonFill).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.AccentSubtle));
            TestHelpers.ColorOf(mirror.PersonForeground).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.AccentStrong));
            TestHelpers.ColorOf(mirror.ChannelFill).A.ShouldBe((byte)Math.Round(0.07 * 255));
            var presenter = input.Editor!.Presenter!;
            presenter.Tokens.Count.ShouldBe(2);
            // Same runs, same wrapping: the transparent editor text and the mirror agree glyph for glyph.
            var layout = mirror.EnsureLayout()!;
            layout.TextLines.Count.ShouldBe(presenter.TextLayout.TextLines.Count);
            layout.HitTestTextPosition(14).X.ShouldBe(presenter.TextLayout.HitTestTextPosition(14).X, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Show_Portraits_Initials_And_Member_Counts()
    {
        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96));
        var input = Create(i => i.People =
        [
            new MentionPerson("u_a", "Ada Lovelace") { AvatarImage = bitmap },
            new MentionPerson("u_b", "Ada Byron") { Avatar = "avares://TKCode.Arc/missing-avatar.png" },
            new MentionPerson("u_c", "Ada Chen")
        ]);
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "@ada");
            var rows = input.SuggestionList!.Rows;
            rows.Count.ShouldBe(3);
            rows[1].GetVisualChildren().OfType<Border>().Single().IsVisible.ShouldBeTrue(); // the async portrait
            rows[0].GetVisualChildren().ShouldBeEmpty(); // an in-memory image is drawn, not hosted
            rows.All(r => r.Bounds.Height is > 41.5 and < 42.5).ShouldBeTrue(); // 28px slot + 7px padding twice
            input.SuggestionList.Rows[0].LabelLayout.ShouldNotBeNull();
            Press(window, PhysicalKey.Escape);
            Type(window, " #e");
            var channel = input.SuggestionList.Rows[0];
            channel.Item.Channel!.Members.ShouldBe(132);
            channel.Bounds.Height.ShouldBe(7 + 14 * 1.4 + 12 * 1.4 + 7, 1); // label and meta lines (layout rounding)
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Height_After_The_First_Measure()
    {
        var input = Create();
        var window = Show(input, Motion.ArcMotionLevel.Full);
        try
        {
            var shell = TestHelpers.Descendant<Border>(input, MentionInput.ShellPart);
            shell.Bounds.Height.ShouldBe(48, 0.5); // the first measure jumps
            Focused(input);
            Type(window, "a");
            Press(window, PhysicalKey.Enter);
            Type(window, "b");
            input.TargetFieldHeight.ShouldBe(2 * 24 + 22, 0.5);
            shell.Bounds.Height.ShouldBeLessThan(2 * 24 + 22 + 2 - 0.5); // still on its way
            var previous = shell.Bounds.Height;
            for (var i = 0; i < 20; i++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                shell.Bounds.Height.ShouldBeGreaterThanOrEqualTo(previous); // grows toward the target, frame by frame
                previous = shell.Bounds.Height;
            }
            previous.ShouldBeGreaterThan(48);
            previous.ShouldBeLessThanOrEqualTo(72.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Height()
    {
        var input = Create();
        var window = Show(input, Motion.ArcMotionLevel.Reduced);
        try
        {
            Focused(input);
            Type(window, "a");
            Press(window, PhysicalKey.Enter);
            Type(window, "b");
            TestHelpers.Descendant<Border>(input, MentionInput.ShellPart).Bounds.Height.ShouldBe(2 * 24 + 22 + 2, 0.5);
            Type(window, " @m");
            input.SuggestionPopup!.IsMotionEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The trigger character's line box and the panel's bounds, both in the field's coordinates.</summary>
    private static (Rect Trigger, Rect Surface) Geometry(MentionInput input)
    {
        var presenter = input.Editor!.Presenter!;
        var text = input.Value.Text;
        var at = Math.Max(text.LastIndexOf('@'), text.LastIndexOf('#'));
        var hit = presenter.TextLayout.HitTestTextPosition(at);
        var trigger = new Rect(presenter.TranslatePoint(hit.Position, input)!.Value, new Size(1, hit.Height));
        var surface = input.SuggestionList!.GetVisualParent<Border>()!;
        var origin = surface.TranslatePoint(default, input)!.Value;
        return (trigger, new Rect(origin, surface.Bounds.Size));
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Executed { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executed.Add(parameter);
    }
}

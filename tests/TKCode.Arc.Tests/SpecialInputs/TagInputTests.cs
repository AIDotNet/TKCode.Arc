using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.SpecialInputs;

public class TagInputTests
{
    private static TagInput Create(params string[] tags) => new() { Label = "Tags", Value = tags, Width = 360 };

    private static TagInputPanel Panel(TagInput input) => input.GetVisualDescendants().OfType<TagInputPanel>().First();

    private static TagInputEditor Editor(TagInput input) => Panel(input).Editor;

    private static FieldFrame Frame(TagInput input) => input.GetVisualDescendants().OfType<FieldFrame>().First();

    private static IReadOnlyList<string> Chips(TagInput input) => Panel(input).Chips.Select(c => c.TagText).ToList();

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var input = new TagInput();
        input.Value.ShouldBeEmpty();
        input.Placeholder.ShouldBe("Add a tag");
        input.MaxTags.ShouldBeNull();
        input.Value = null!;
        input.Value.ShouldNotBeNull();
        input.Value.ShouldBeEmpty();
        input.RemoveLabelFormat.ShouldBe("Remove {0}");

        input.Label = "Tags";
        input.Description = "Short values.";
        var window = TestHelpers.Show(input);
        try
        {
            var frame = Frame(input);
            frame.Bounds.Height.ShouldBe(44, 0.5);
            var panel = Panel(input);
            panel.Bounds.Height.ShouldBe(42, 0.5);
            panel.IsPlaceholderShown.ShouldBeTrue();
            panel.Placeholder.Text.ShouldBe("Add a tag");
            var editor = Editor(input);
            editor.Bounds.Height.ShouldBe(28, 0.5);
            // A lone editor takes the whole line inside the 7px padding.
            editor.Bounds.Width.ShouldBe(panel.Bounds.Width - 14, 0.5);
            AutomationProperties.GetName(editor).ShouldBe("Tags");
            AutomationProperties.GetHelpText(editor).ShouldBe("Short values.");
            // Arc rests on Border, not BorderStrong.
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.Border));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_And_Comma_Add_The_Trimmed_Draft()
    {
        var input = Create();
        var window = TestHelpers.Show(input);
        try
        {
            var events = new List<TagInputValueChangedEventArgs>();
            input.ValueChanged += (_, e) => events.Add(e);
            input.FocusEditor().ShouldBeTrue();
            Type(window, "  design ");
            Panel(input).IsPlaceholderShown.ShouldBeFalse();
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(["design"]);
            input.Draft.ShouldBeEmpty();
            input.LastAnnouncement.ShouldBe("Added design");
            events.Count.ShouldBe(1);
            events[0].OldValue.ShouldBeEmpty();
            events[0].Added.ShouldBe(["design"]);
            events[0].Removed.ShouldBeEmpty();

            Type(window, "motion");
            Type(window, ",");
            input.Value.ShouldBe(["design", "motion"]);
            input.Draft.ShouldBeEmpty();
            Chips(input).ShouldBe(["design", "motion"]);

            // An empty or blank draft does nothing.
            Type(window, "   ");
            Press(window, PhysicalKey.Enter);
            input.Value.Count.ShouldBe(2);
            events.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Duplicate_Is_Refused_Case_Insensitively_And_Keeps_The_Draft()
    {
        var input = Create("Design");
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            Type(window, "design");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(["Design"]);
            input.Draft.ShouldBe("design");
            input.LastAnnouncement.ShouldBe("Design is already added");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_Picks_Then_Removes_And_Arrows_Move_The_Pick()
    {
        var input = Create("a", "b", "c");
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            Press(window, PhysicalKey.Backspace);
            input.PickedTag.ShouldBe("c");
            input.LastAnnouncement.ShouldBe("c selected. Press Backspace to remove it.");
            Panel(input).PickedChip!.TagText.ShouldBe("c");
            Panel(input).PickedChip!.RemoveButton.Classes.ShouldContain(TagInputRemoveButton.PickedPseudoClass);
            Panel(input).Ring.At.ShouldBe(Panel(input).PickedChip!.LastRect);

            Press(window, PhysicalKey.ArrowLeft);
            input.PickedTag.ShouldBe("b");
            Press(window, PhysicalKey.ArrowLeft);
            input.PickedTag.ShouldBe("a");
            // At the first tag ArrowLeft has nowhere to go.
            Press(window, PhysicalKey.ArrowLeft);
            input.PickedTag.ShouldBe("a");
            Press(window, PhysicalKey.ArrowRight);
            input.PickedTag.ShouldBe("b");
            Press(window, PhysicalKey.Escape);
            input.PickedTag.ShouldBeNull();
            Panel(input).Ring.At.ShouldBeNull();

            // ArrowLeft at the start picks the last tag; ArrowRight past the last clears the pick.
            Press(window, PhysicalKey.ArrowLeft);
            input.PickedTag.ShouldBe("c");
            Press(window, PhysicalKey.ArrowRight);
            input.PickedTag.ShouldBeNull();

            Press(window, PhysicalKey.Backspace);
            Press(window, PhysicalKey.ArrowLeft);
            input.PickedTag.ShouldBe("b");
            TagInputValueChangedEventArgs? change = null;
            input.ValueChanged += (_, e) => change = e;
            Press(window, PhysicalKey.Delete);
            input.Value.ShouldBe(["a", "c"]);
            input.PickedTag.ShouldBeNull();
            input.LastAnnouncement.ShouldBe("Removed b");
            change!.Removed.ShouldBe(["b"]);
            Chips(input).ShouldBe(["a", "c"]);
            Editor(input).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_Inside_The_Draft_Edits_Text_And_Typing_Clears_The_Pick()
    {
        var input = Create("a", "b");
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            Type(window, "xy");
            Press(window, PhysicalKey.Backspace);
            input.Draft.ShouldBe("x");
            input.PickedTag.ShouldBeNull();

            var editor = Editor(input);
            editor.CaretIndex = 0;
            Press(window, PhysicalKey.Backspace);
            input.PickedTag.ShouldBe("b");
            Type(window, "z");
            input.PickedTag.ShouldBeNull();
            input.Value.ShouldBe(["a", "b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Field_Commits_The_Draft_And_Clears_The_Pick()
    {
        var input = Create("a");
        var other = new TextBox();
        var root = new StackPanel { Children = { input, other } };
        var window = TestHelpers.Show(root);
        try
        {
            input.FocusEditor();
            Type(window, "b");
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(["a", "b"]);
            input.Draft.ShouldBeEmpty();

            input.FocusEditor();
            Press(window, PhysicalKey.Backspace);
            input.PickedTag.ShouldBe("b");
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            input.PickedTag.ShouldBeNull();
            input.Value.ShouldBe(["a", "b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Darkens_The_Border_And_Draws_No_Halo()
    {
        var input = new TagInput { Label = "Tags", Width = 420 };
        var window = TestHelpers.Show(input);
        try
        {
            var frame = Frame(input);
            input.FocusEditor().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            frame.Classes.ShouldContain(FieldFrame.FocusActivePseudoClass);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.Foreground));
            // Arc's 3px halo is a transparent focus-ring colour: focus is the border colour alone.
            frame.GetVisualDescendants().OfType<Border>().ShouldNotContain(b => b.Name == "PART_FocusRing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Remove_Buttons_Are_Named_And_Return_Focus_To_The_Editor()
    {
        var input = Create("alpha", "beta");
        var window = TestHelpers.Show(input);
        try
        {
            var chip = Panel(input).Chips[0];
            AutomationProperties.GetName(chip.RemoveButton).ShouldBe("Remove alpha");
            chip.RemoveButton.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(["beta"]);
            input.LastAnnouncement.ShouldBe("Removed alpha");
            Editor(input).IsFocused.ShouldBeTrue();

            input.RemoveLabelFormat = "Delete {0}";
            AutomationProperties.GetName(Panel(input).Chips[0].RemoveButton).ShouldBe("Delete beta");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Tag_Toggles_The_Pick_And_Keeps_Focus_In_The_Editor()
    {
        var input = Create("one", "two");
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            Type(window, "draft");
            var chip = Panel(input).Chips[0];
            var point = chip.TranslatePoint(new Point(8, chip.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.PickedTag.ShouldBe("one");
            Editor(input).IsFocused.ShouldBeTrue();
            // Focus never left the editor, so the draft was not committed.
            input.Value.ShouldBe(["one", "two"]);
            input.Draft.ShouldBe("draft");

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.PickedTag.ShouldBeNull();

            // A press on the empty part of the shell focuses the editor.
            var other = new TextBox();
            other.Focus();
            var panel = Panel(input);
            var empty = panel.TranslatePoint(new Point(panel.Bounds.Width - 3, panel.Bounds.Height - 2), window)!.Value;
            window.MouseDown(empty, MouseButton.Left);
            window.MouseUp(empty, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Editor(input).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pasting_A_List_Adds_Each_Piece_And_Keeps_The_Tail()
    {
        var input = Create("red");
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            TagInputValueChangedEventArgs? change = null;
            input.ValueChanged += (_, e) => change = e;
            input.HandlePaste("Red, green,, blue\nteal\tcyan").ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(["red", "green", "blue", "teal"]);
            input.Draft.ShouldBe("cyan");
            Editor(input).CaretIndex.ShouldBe(4);
            change!.Added.ShouldBe(["green", "blue", "teal"]);
            input.LastAnnouncement.ShouldBe("Added green, blue, teal");

            // Without separators the paste stays plain text.
            input.HandlePaste("plain").ShouldBeFalse();

            // A paste of duplicates only adds nothing and says so.
            Editor(input).Text = string.Empty;
            input.HandlePaste("BLUE,").ShouldBeTrue();
            input.Value.Count.ShouldBe(4);
            input.LastAnnouncement.ShouldBe("blue is already added");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typed_Text_With_Separators_Splits_Like_A_Paste()
    {
        var input = Create();
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            Type(window, "a,b");
            input.Value.ShouldBe(["a"]);
            input.Draft.ShouldBe("b");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MaxTags_Refuses_Adds_With_An_Announcement()
    {
        var input = Create("a", "b");
        input.MaxTags = 2;
        var window = TestHelpers.Show(input);
        try
        {
            input.FocusEditor();
            Type(window, "c");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(["a", "b"]);
            input.Draft.ShouldBe("c");
            input.LastAnnouncement.ShouldBe("You can add up to 2 tags");
            input.AddTag("c").ShouldBeFalse();

            input.MaxTags = 3;
            Editor(input).Text = string.Empty;
            input.HandlePaste("c, d, e").ShouldBeTrue();
            input.Value.ShouldBe(["a", "b", "c"]);
            input.Draft.ShouldBe("d, e");
            input.LastAnnouncement.ShouldBe("Added c. You can add up to 3 tags");

            input.MaxTags = -4;
            input.MaxTags.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Code_Changes_Update_The_Tags_Without_Announcing()
    {
        var input = Create("a");
        var window = TestHelpers.Show(input);
        try
        {
            var events = new List<TagInputValueChangedEventArgs>();
            input.ValueChanged += (_, e) => events.Add(e);
            input.Value = ["b", "a", "c"];
            Dispatcher.UIThread.RunJobs();
            Chips(input).ShouldBe(["b", "a", "c"]);
            events.Count.ShouldBe(1);
            events[0].Added.ShouldBe(["b", "c"]);
            input.LastAnnouncement.ShouldBeNullOrEmpty();
            // Remove buttons stay in value order before the editor (tab order).
            var panel = Panel(input);
            var order = panel.Children.OfType<TagInputChip>().Select(c => c.TagText).ToList();
            order.ShouldBe(["b", "a", "c"]);
            panel.Children.IndexOf(panel.Editor).ShouldBeGreaterThan(panel.Children.IndexOf(panel.Chips[^1]));

            // Same content in a new list is not a change.
            input.Value = ["b", "a", "c"];
            events.Count.ShouldBe(1);

            input.AddTag(" d ").ShouldBeTrue();
            input.AddTag("D").ShouldBeFalse();
            input.AddTag("  ").ShouldBeFalse();
            input.RemoveTag("A").ShouldBeTrue();
            input.RemoveTag("zzz").ShouldBeFalse();
            input.Value.ShouldBe(["b", "c", "d"]);
            input.LastAnnouncement.ShouldBeNullOrEmpty();
            panel.IsPlaceholderShown.ShouldBeFalse();
            input.Value = [];
            Dispatcher.UIThread.RunJobs();
            panel.IsPlaceholderShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tags_Wrap_Onto_Rows_And_The_Editor_Wraps_When_Less_Than_Its_Basis_Is_Left()
    {
        var input = Create("ada@example.com", "grace@example.com", "linus@example.com", "margaret@example.com");
        var window = TestHelpers.Show(input);
        try
        {
            var panel = Panel(input);
            var chips = panel.Chips;
            chips.Select(c => c.Bounds.Height).ShouldAllBe(h => Math.Abs(h - 28) < 0.5);
            // Rows are 28 tall with a 6px gap: the second row starts at 7 + 28 + 6.
            chips.Select(c => c.Bounds.Y).Distinct().Count().ShouldBeGreaterThan(1);
            chips.Any(c => Math.Abs(c.Bounds.Y - 41) < 0.5).ShouldBeTrue();
            panel.Bounds.Height.ShouldBeGreaterThan(42);
            var editor = panel.Editor.Bounds;
            editor.Width.ShouldBeGreaterThanOrEqualTo(100 - 0.5);
            editor.Right.ShouldBe(panel.Bounds.Width - 7, 0.5);
            Frame(input).Bounds.Height.ShouldBe(panel.Bounds.Height + 2, 0.5);
            // Fills sit under each label at the same box.
            chips[0].Fill.Bounds.ShouldBe(chips[0].Bounds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Message_Makes_The_Field_Invalid()
    {
        var input = Create();
        input.ErrorMessage = "Add at least one tag.";
        var window = TestHelpers.Show(input);
        try
        {
            input.IsInvalid.ShouldBeTrue();
            input.Classes.ShouldContain(TagInput.InvalidPseudoClass);
            Frame(input).Classes.ShouldContain(FieldFrame.InvalidPseudoClass);
            AutomationProperties.GetHelpText(Editor(input)).ShouldBe("Add at least one tag.");
            input.ErrorMessage = null;
            input.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Glides_Pulses_Shakes_And_Rings()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var input = Create("alpha", "beta");
        input.MaxTags = 3;
        var window = TestHelpers.Show(input, ArcMotionLevel.Full);
        try
        {
            var panel = Panel(input);
            input.FocusEditor();
            Type(window, "gamma");
            Press(window, PhysicalKey.Enter);
            // The new tag grows from 0.9 out of a soft blur where it was typed; the editor glides from there.
            var gamma = panel.FindChip("gamma")!;
            gamma.PresenceScale.ShouldBe(0.9, 0.02);
            gamma.BlurRadius.ShouldBeGreaterThan(3);
            gamma.Opacity.ShouldBeLessThan(0.1);
            var editorShift = panel.EditorFlipOffset;
            editorShift.Length.ShouldBeGreaterThan(10);
            // It starts exactly where the new tag now sits (where the text was typed).
            (Editor(input).Bounds.X + editorShift.X).ShouldBe(gamma.Bounds.X, 1);
            (Editor(input).Bounds.Y + editorShift.Y).ShouldBe(gamma.Bounds.Y, 1);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            gamma.PresenceScale.ShouldBe(1, 0.001);
            gamma.Opacity.ShouldBe(1, 0.001);
            gamma.Effect.ShouldBeNull();
            panel.EditorFlipOffset.Length.ShouldBe(0, 0.01);

            // Duplicate: one pulse up to 1.06 and back.
            Type(window, "ALPHA");
            Press(window, PhysicalKey.Enter);
            var alpha = panel.FindChip("alpha")!;
            alpha.IsPulsing.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(180));
            alpha.PulseScale.ShouldBe(1.06, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            alpha.PulseScale.ShouldBe(1, 0.001);

            // The limit refuses with a sideways kick that rings home.
            Editor(input).Text = "delta";
            Press(window, PhysicalKey.Enter);
            input.Value.Count.ShouldBe(3);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            panel.ShakeOffset.ShouldBeLessThan(-3);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            panel.ShakeOffset.ShouldBe(0, 0.05);

            // The ring appears in place from 0.9, then glides to the next pick.
            Editor(input).Text = string.Empty;
            Press(window, PhysicalKey.Backspace);
            panel.Ring.RingScale.ShouldBe(0.9, 0.02);
            clock.Advance(TimeSpan.FromSeconds(1));
            panel.Ring.RingOpacity.ShouldBe(1, 0.001);
            panel.Ring.Box.ShouldBe(gamma.LastRect);
            Press(window, PhysicalKey.ArrowLeft);
            clock.Advance(TimeSpan.FromMilliseconds(40));
            var box = panel.Ring.Box;
            box.X.ShouldBeLessThan(gamma.LastRect.X - 1);
            box.X.ShouldBeGreaterThan(panel.FindChip("beta")!.LastRect.X + 1);
            clock.Advance(TimeSpan.FromSeconds(1));
            panel.Ring.Box.ShouldBe(panel.FindChip("beta")!.LastRect);

            // Removing the pick: the tag leaves where it was (out of the flow) and the rest glide in.
            Press(window, PhysicalKey.Backspace);
            input.Value.ShouldBe(["alpha", "gamma"]);
            panel.LeavingChips.Count.ShouldBe(1);
            panel.Ring.At.ShouldBeNull();
            gamma.FlipOffset.X.ShouldBeGreaterThan(10);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            panel.LeavingChips.ShouldBeEmpty();
            panel.Ring.RingOpacity.ShouldBe(0, 0.001);
            panel.Children.OfType<TagInputChip>().Count().ShouldBe(2);
            gamma.FlipOffset.Length.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Only_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var input = Create("alpha");
        input.MaxTags = 2;
        var window = TestHelpers.Show(input, ArcMotionLevel.Reduced);
        try
        {
            var panel = Panel(input);
            input.FocusEditor();
            Type(window, "beta");
            Press(window, PhysicalKey.Enter);
            var beta = panel.FindChip("beta")!;
            beta.PresenceScale.ShouldBe(1);
            beta.BlurRadius.ShouldBe(0);
            beta.Opacity.ShouldBeLessThan(0.1);
            panel.EditorFlipOffset.Length.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            beta.Opacity.ShouldBe(1, 0.001);

            Type(window, "Alpha");
            Press(window, PhysicalKey.Enter);
            panel.FindChip("alpha")!.IsPulsing.ShouldBeFalse();
            Editor(input).Text = "gamma";
            Press(window, PhysicalKey.Enter);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            panel.ShakeOffset.ShouldBe(0);

            // Removed tags go at once.
            Editor(input).Text = string.Empty;
            Press(window, PhysicalKey.Backspace);
            Press(window, PhysicalKey.Backspace);
            panel.LeavingChips.ShouldBeEmpty();
            Chips(input).ShouldBe(["alpha"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Clipboard_Paste_With_Separators_Splits_And_A_Plain_Paste_Inserts_Text()
    {
        var input = Create();
        var window = TestHelpers.Show(input);
        try
        {
            var clipboard = TopLevel.GetTopLevel(window)!.Clipboard;
            clipboard.ShouldNotBeNull();
            input.FocusEditor();
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard!, "x, y\nz");
            Editor(input).Paste();
            for (var i = 0; i < 20 && input.Value.Count < 2; i++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            input.Value.ShouldBe(["x", "y"]);
            input.Draft.ShouldBe("z");

            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard!, "plain");
            Editor(input).Paste();
            for (var i = 0; i < 20 && input.Draft != "zplain"; i++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            input.Draft.ShouldBe("zplain");
            input.Value.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mouse_Hover_Hints_BorderStrong_Until_Focused_And_The_Label_Focuses_The_Editor()
    {
        var input = Create("a");
        var other = new TextBox();
        var window = TestHelpers.Show(new StackPanel { Children = { input, other } });
        try
        {
            var frame = Frame(input);
            var inside = frame.TranslatePoint(new Point(frame.Bounds.Width - 4, frame.Bounds.Height / 2), window)!.Value;
            window.MouseMove(inside);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.BorderStrong));
            input.FocusEditor();
            Dispatcher.UIThread.RunJobs();
            frame.Classes.ShouldContain(FieldFrame.FocusActivePseudoClass);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.Foreground));

            other.Focus();
            Dispatcher.UIThread.RunJobs();
            Editor(input).IsFocused.ShouldBeFalse();
            var label = input.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == Field.LabelPart);
            var onLabel = label.TranslatePoint(new Point(4, label.Bounds.Height / 2), window)!.Value;
            window.MouseDown(onLabel, MouseButton.Left);
            window.MouseUp(onLabel, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Editor(input).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Remove_Button_Presses_Quickly_And_Releases_On_A_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var input = Create("a", "b");
        var window = TestHelpers.Show(input, ArcMotionLevel.Full);
        try
        {
            var button = Panel(input).Chips[0].RemoveButton;
            var center = button.TranslatePoint(new Point(11, 11), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(130));
            button.CurrentScale.ShouldBe(0.96, 0.001);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            button.CurrentScale.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromMilliseconds(700));
            button.CurrentScale.ShouldBe(1, 0.001);
            input.Value.ShouldBe(["b"]);
        }
        finally
        {
            window.Close();
        }
    }
}

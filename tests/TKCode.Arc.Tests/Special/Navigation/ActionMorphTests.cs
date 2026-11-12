using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>
/// The action morph: the button, the quick-action menu and the composers, the keyboard model, submitting (empty, async,
/// failing, cancelled), drafts, the success check and its reset, automation, and the surface's spring at full motion.
/// </summary>
public class ActionMorphTests
{
    private static ActionMorph Morph(Func<ActionMorphSubmission, CancellationToken, Task>? handler = null)
    {
        var todo = new ActionMorphAction
        {
            Id = "todo",
            Label = "To-do",
            Description = "Something to finish",
            Glyph = ArcIcons.ListTodo,
            Shortcut = "d",
            Title = "New to-do",
            ChoicesLabel = "When",
            SubmitLabel = "Add to-do",
            SuccessLabel = "To-do added",
            EmptyHint = "Name it first"
        };
        todo.Choices.Add("Today");
        todo.Choices.Add("This week");
        todo.Choices.Add("Someday");
        var note = new ActionMorphAction { Id = "note", Label = "Note", Shortcut = "n", IsMultiline = true, Glyph = ArcIcons.FileText };
        var morph = new ActionMorph
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(16),
            SubmitHandler = handler
        };
        morph.Actions.Add(todo);
        morph.Actions.Add(note);
        return morph;
    }

    private static Window Show(ActionMorph morph, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new Panel { Children = { morph } }, level);
        window.Width = 640;
        window.Height = 520;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Open(Window window, ActionMorph morph)
    {
        morph.Trigger.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
    }

    private static IInputElement? Focused(Window window) => window.FocusManager!.GetFocusedElement();

    [AvaloniaFact]
    public void It_starts_as_a_named_56px_button_that_opens_a_menu()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.View.ShouldBe(ActionMorphView.Button);
            morph.IsExpanded.ShouldBeFalse();
            morph.Bounds.Size.ShouldBe(new Size(56, 56));
            morph.Surface!.Current.ShouldBeSameAs(morph.Trigger);
            AutomationProperties.GetName(morph.Trigger).ShouldBe("Create");
            var peer = ControlAutomationPeer.CreatePeerForElement(morph.Trigger);
            var expand = peer.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
            expand.ShowsMenu.ShouldBeTrue();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            var fresh = new ActionMorph();
            fresh.Label.ShouldBe("Create");
            fresh.Corner.ShouldBe(ActionMorphCorner.End);
            fresh.ResetAfter.ShouldBe(TimeSpan.FromMilliseconds(1600));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_on_the_button_opens_the_menu_with_focus_and_highlight_on_the_first_row()
    {
        var morph = Morph();
        var expanded = 0;
        morph.Expanded += (_, _) => expanded++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            morph.View.ShouldBe(ActionMorphView.Menu);
            morph.IsExpanded.ShouldBeTrue();
            expanded.ShouldBe(1);
            morph.Classes.Contains(ActionMorph.OpenPseudoClass).ShouldBeTrue();
            var menu = morph.Menu.ShouldNotBeNull();
            morph.Surface!.Current.ShouldBeSameAs(menu);
            menu.Rows.Count.ShouldBe(2);
            menu.Rows[0].Shortcut.ShouldBe("D");
            AutomationProperties.GetName(menu).ShouldBe("Create");
            AutomationProperties.GetControlTypeOverride(menu).ShouldBe(AutomationControlType.Menu);
            AutomationProperties.GetControlTypeOverride(menu.Rows[0]).ShouldBe(AutomationControlType.MenuItem);
            AutomationProperties.GetAcceleratorKey(menu.Rows[0]).ShouldBe("D");
            AutomationProperties.GetHelpText(menu.Rows[0]).ShouldBe("Something to finish");
            Focused(window).ShouldBeSameAs(menu.Rows[0]);
            menu.ActiveRow.ShouldBeSameAs(menu.Rows[0]);
            menu.Highlight!.HasTarget.ShouldBeTrue();
            // The open surface is the menu's size, grown up and to the left of the button.
            morph.Surface.TargetSize.Width.ShouldBe(256, 0.5);
            menu.TranslatePoint(default, morph)!.Value.X.ShouldBeLessThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_wrap_through_the_rows_and_Home_End_jump()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            Open(window, morph);
            var rows = morph.Menu!.Rows;
            Press(window, PhysicalKey.ArrowDown);
            Focused(window).ShouldBeSameAs(rows[1]);
            morph.Menu.ActiveRow.ShouldBeSameAs(rows[1]);
            Press(window, PhysicalKey.ArrowDown);
            Focused(window).ShouldBeSameAs(rows[0]);
            Press(window, PhysicalKey.ArrowUp);
            Focused(window).ShouldBeSameAs(rows[1]);
            Press(window, PhysicalKey.Home);
            Focused(window).ShouldBeSameAs(rows[0]);
            Press(window, PhysicalKey.End);
            Focused(window).ShouldBeSameAs(rows[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_shortcut_letter_opens_the_composer_and_Escape_goes_back_one_level_at_a_time()
    {
        var morph = Morph();
        var collapsed = 0;
        morph.Collapsed += (_, _) => collapsed++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.N);
            morph.View.ShouldBe(ActionMorphView.Composer);
            morph.CurrentAction!.Id.ShouldBe("note");
            var composer = morph.ComposerOf(morph.Actions[1])!;
            Focused(window).ShouldBeSameAs(composer.Field);
            composer.Field!.AcceptsReturn.ShouldBeTrue();
            composer.Classes.Contains(ActionMorphComposer.HasChoicesPseudoClass).ShouldBeFalse();

            Press(window, PhysicalKey.Escape);
            morph.View.ShouldBe(ActionMorphView.Menu);
            Focused(window).ShouldBeSameAs(morph.Menu!.RowOf(morph.Actions[1]));
            collapsed.ShouldBe(0);

            Press(window, PhysicalKey.Escape);
            morph.View.ShouldBe(ActionMorphView.Button);
            morph.IsExpanded.ShouldBeFalse();
            collapsed.ShouldBe(1);
            Focused(window).ShouldBeSameAs(morph.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_empty_submit_marks_the_field_and_shows_the_hint_until_the_person_types()
    {
        var morph = Morph();
        var submitted = 0;
        morph.Submitted += (_, _) => submitted++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.D);
            var composer = morph.ComposerOf(morph.Actions[0])!;
            composer.Field!.Text = "   ";
            Press(window, PhysicalKey.Enter);
            submitted.ShouldBe(0);
            morph.View.ShouldBe(ActionMorphView.Composer);
            composer.Message.ShouldBe("Name it first");
            composer.IsInvalid.ShouldBeTrue();
            composer.Field.Classes.Contains(ActionMorphField.InvalidPseudoClass).ShouldBeTrue();
            AutomationProperties.GetHelpText(composer.Field).ShouldBe("Name it first");

            composer.Field.Text = "Water the plants";
            Dispatcher.UIThread.RunJobs();
            composer.Message.ShouldBeNull();
            composer.IsInvalid.ShouldBeFalse();

            // An action without its own hint uses the morph's.
            morph.Back();
            morph.OpenComposer(morph.Actions[1]);
            Dispatcher.UIThread.RunJobs();
            var note = morph.ComposerOf(morph.Actions[1])!;
            morph.SubmitFrom(note);
            note.Message.ShouldBe(morph.EmptyHint);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_submits_folds_back_with_a_check_and_the_plus_returns_after_ResetAfter()
    {
        var morph = Morph();
        morph.ResetAfter = TimeSpan.FromMilliseconds(60);
        ActionMorphSubmission? submission = null;
        morph.Submitted += (_, e) => submission = e.Submission;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.D);
            var composer = morph.ComposerOf(morph.Actions[0])!;
            composer.Field!.Text = "  Water the plants ";
            Press(window, PhysicalKey.Enter);

            submission.ShouldNotBeNull();
            submission.Action.ShouldBeSameAs(morph.Actions[0]);
            submission.Text.ShouldBe("Water the plants");
            submission.Choice.ShouldBe("Today");
            morph.View.ShouldBe(ActionMorphView.Button);
            morph.IsSuccess.ShouldBeTrue();
            morph.Classes.Contains(ActionMorph.SuccessPseudoClass).ShouldBeTrue();
            morph.Trigger.IsSuccess.ShouldBeTrue();
            AutomationProperties.GetName(morph.Trigger).ShouldBe("Create. To-do added");
            morph.Status!.Text.ShouldBe("To-do added");
            Focused(window).ShouldBeSameAs(morph.Trigger);

            TestHelpers.PumpUntil(() => !morph.IsSuccess, 3000).ShouldBeTrue();
            AutomationProperties.GetName(morph.Trigger).ShouldBe("Create");
            morph.Trigger.IsSuccess.ShouldBeFalse();

            // The draft was cleared by the successful submit.
            morph.OpenComposer(morph.Actions[0]);
            Dispatcher.UIThread.RunJobs();
            composer.Text.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_multiline_composer_submits_on_Ctrl_Enter_only()
    {
        var morph = Morph();
        var submitted = 0;
        morph.Submitted += (_, _) => submitted++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.N);
            var composer = morph.ComposerOf(morph.Actions[1])!;
            composer.Field!.Text = "Line one";
            Press(window, PhysicalKey.Enter);
            submitted.ShouldBe(0);
            morph.View.ShouldBe(ActionMorphView.Composer);
            Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
            submitted.ShouldBe(1);
            morph.View.ShouldBe(ActionMorphView.Button);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_async_submit_shows_busy_and_a_failure_keeps_the_draft_with_an_error()
    {
        TaskCompletionSource? pending = null;
        var morph = Morph((_, _) =>
        {
            pending = new TaskCompletionSource();
            return pending.Task;
        });
        var submitted = 0;
        morph.Submitted += (_, _) => submitted++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.D);
            var composer = morph.ComposerOf(morph.Actions[0])!;
            composer.Field!.Text = "Call the plumber";
            Press(window, PhysicalKey.Enter);

            morph.IsSubmitting.ShouldBeTrue();
            composer.IsBusy.ShouldBeTrue();
            composer.Submit!.IsBusy.ShouldBeTrue();
            composer.Submit.Classes.Contains(ActionMorphSubmit.BusyPseudoClass).ShouldBeTrue();
            composer.Field.IsReadOnly.ShouldBeTrue();
            AutomationProperties.GetItemStatus(composer).ShouldBe("Saving");
            // A second submit while busy is ignored.
            Press(window, PhysicalKey.Enter);

            pending!.SetException(new InvalidOperationException("offline"));
            TestHelpers.PumpUntil(() => !morph.IsSubmitting, 3000).ShouldBeTrue();
            submitted.ShouldBe(0);
            morph.View.ShouldBe(ActionMorphView.Composer);
            composer.Text.ShouldBe("Call the plumber");
            composer.Message.ShouldBe(morph.ErrorMessage);
            composer.IsInvalid.ShouldBeTrue();
            composer.Field.IsReadOnly.ShouldBeFalse();

            Press(window, PhysicalKey.Enter);
            morph.IsSubmitting.ShouldBeTrue();
            pending.SetResult();
            TestHelpers.PumpUntil(() => morph.View == ActionMorphView.Button, 3000).ShouldBeTrue();
            submitted.ShouldBe(1);
            morph.IsSuccess.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Going_back_while_saving_cancels_the_handler()
    {
        var token = CancellationToken.None;
        var morph = Morph((_, t) =>
        {
            token = t;
            return Task.Delay(Timeout.Infinite, t);
        });
        var submitted = 0;
        morph.Submitted += (_, _) => submitted++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.D);
            var composer = morph.ComposerOf(morph.Actions[0])!;
            composer.Field!.Text = "Plan the trip";
            Press(window, PhysicalKey.Enter);
            morph.IsSubmitting.ShouldBeTrue();

            Press(window, PhysicalKey.Escape);
            token.IsCancellationRequested.ShouldBeTrue();
            morph.IsSubmitting.ShouldBeFalse();
            composer.IsBusy.ShouldBeFalse();
            morph.View.ShouldBe(ActionMorphView.Menu);
            TestHelpers.PumpUntil(() => false, 100);
            submitted.ShouldBe(0);
            composer.Message.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drafts_are_kept_per_action_across_closing_and_reopening()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.OpenComposer(morph.Actions[0]);
            Dispatcher.UIThread.RunJobs();
            var todo = morph.ComposerOf(morph.Actions[0])!;
            todo.Field!.Text = "Half a thought";
            todo.ChoicesGroup!.Select("Someday", animate: false);
            morph.Collapse();
            morph.OpenComposer(morph.Actions[1]);
            Dispatcher.UIThread.RunJobs();
            morph.ComposerOf(morph.Actions[1])!.Field!.Text = "Another";
            morph.Back();
            morph.OpenComposer(morph.Actions[0]);
            Dispatcher.UIThread.RunJobs();
            todo.Text.ShouldBe("Half a thought");
            todo.Choice.ShouldBe("Someday");
            morph.ComposerOf(morph.Actions[1])!.Text.ShouldBe("Another");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choices_are_a_radio_group_moved_by_arrows_with_one_tab_stop()
    {
        var morph = Morph();
        ActionMorphSubmission? submission = null;
        morph.Submitted += (_, e) => submission = e.Submission;
        morph.Actions[0].DefaultChoice = "This week";
        var window = Show(morph);
        try
        {
            morph.OpenComposer(morph.Actions[0]);
            Dispatcher.UIThread.RunJobs();
            var composer = morph.ComposerOf(morph.Actions[0])!;
            var group = composer.ChoicesGroup!;
            AutomationProperties.GetName(group).ShouldBe("When");
            group.Selected.ShouldBe("This week");
            group.Choices.Count(c => c.IsTabStop).ShouldBe(1);
            group.Choices[1].IsTabStop.ShouldBeTrue();

            group.Choices[1].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            group.Selected.ShouldBe("Someday");
            Focused(window).ShouldBeSameAs(group.Choices[2]);
            Press(window, PhysicalKey.ArrowRight);
            group.Selected.ShouldBe("Today");
            group.Choices[0].IsChecked.ShouldBe(true);
            group.Choices[0].IsTabStop.ShouldBeTrue();
            group.Choices[2].IsTabStop.ShouldBeFalse();

            composer.Field!.Text = "Sort the photos";
            morph.SubmitFrom(composer);
            submission!.Choice.ShouldBe("Today");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_close_buttons_and_a_press_outside_fold_it_back()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            var close = morph.Menu!.CloseButton!;
            AutomationProperties.GetName(close).ShouldBe("Close");
            close.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.View.ShouldBe(ActionMorphView.Button);

            morph.OpenComposer(morph.Actions[0]);
            Dispatcher.UIThread.RunJobs();
            var composer = morph.ComposerOf(morph.Actions[0])!;
            AutomationProperties.GetName(composer.BackButton!).ShouldBe("Back");
            composer.BackButton!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.View.ShouldBe(ActionMorphView.Menu);

            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var outside = new Point(20, 20);
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            morph.View.ShouldBe(ActionMorphView.Button);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_on_a_row_past_the_button_square_opens_its_composer()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            morph.Surface!.FinishExits();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var row = morph.Menu!.Rows[1];
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            // The row lies outside the morph's own 56 × 56 box.
            var box = new Rect(morph.TranslatePoint(default, window)!.Value, morph.Bounds.Size);
            box.Contains(center).ShouldBeFalse();
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            morph.View.ShouldBe(ActionMorphView.Composer);
            // At rest the faces are not clipped (the clip is only for the motion).
            morph.Surface.StagePanel.Clip.ShouldBeNull();
            morph.CurrentAction.ShouldBeSameAs(morph.Actions[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsExpanded_opens_and_folds_it_from_code()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            morph.View.ShouldBe(ActionMorphView.Menu);
            morph.OpenComposer(morph.Actions[0]);
            morph.IsExpanded.ShouldBeTrue();
            morph.IsExpanded = false;
            morph.View.ShouldBe(ActionMorphView.Button);
            morph.CurrentAction.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Corner_start_grows_the_surface_to_the_right()
    {
        var morph = Morph();
        morph.Corner = ActionMorphCorner.Start;
        morph.HorizontalAlignment = HorizontalAlignment.Left;
        var window = Show(morph);
        try
        {
            morph.Classes.Contains(ActionMorph.StartPseudoClass).ShouldBeTrue();
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            var menu = morph.Menu!;
            menu.TranslatePoint(default, morph)!.Value.X.ShouldBe(0, 0.5);
            menu.TranslatePoint(new Point(0, menu.Bounds.Height), morph)!.Value.Y.ShouldBe(56, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_the_actions_rebuilds_the_rows()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            morph.Actions.Add(new ActionMorphAction { Label = "Event", Shortcut = "e" });
            morph.Menu!.Rows.Count.ShouldBe(3);
            morph.OpenComposer(morph.Actions[0]);
            morph.Actions.RemoveAt(0);
            morph.View.ShouldBe(ActionMorphView.Menu);
            morph.Menu.Rows.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_the_surface_springs_open_and_the_rows_stagger_in()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var morph = Morph();
        var window = Show(morph, ArcMotionLevel.Full);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            var surface = morph.Surface!;
            surface.CurrentSize.Width.ShouldBe(56, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Dispatcher.UIThread.RunJobs();
            var width = surface.CurrentSize.Width;
            width.ShouldBeGreaterThan(70);
            width.ShouldBeLessThan(250);
            var rows = morph.Menu!.Rows;
            rows[0].Opacity.ShouldBeGreaterThan(rows[1].Opacity);
            surface.CurrentRadius.ShouldBeLessThan(28);
            surface.CurrentRadius.ShouldBeGreaterThan(24);

            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            surface.CurrentSize.Width.ShouldBe(256, 0.5);
            surface.CurrentRadius.ShouldBe(24, 0.01);
            rows[1].Opacity.ShouldBe(1, 0.001);

            // Folding back uses the quicker spring: more of the way after the same time.
            morph.Collapse();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Dispatcher.UIThread.RunJobs();
            var folded = (256 - surface.CurrentSize.Width) / 200;
            var opened = (width - 56) / 200;
            folded.ShouldBeGreaterThan(opened);
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            surface.CurrentSize.ShouldBe(new Size(56, 56));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_a_deeper_face_slides_in_from_the_right_and_the_old_one_leaves_left()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var morph = Morph();
        var window = Show(morph, ArcMotionLevel.Full);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            var menu = morph.Menu!;
            morph.OpenComposer(morph.Actions[0]);
            Dispatcher.UIThread.RunJobs();
            var composer = morph.ComposerOf(morph.Actions[0])!;
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            OffsetOf(composer).ShouldBeGreaterThan(0);
            OffsetOf(menu).ShouldBeLessThan(0);
            menu.IsHitTestVisible.ShouldBeFalse();
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            OffsetOf(composer).ShouldBe(0, 0.01);
            composer.Opacity.ShouldBe(1, 0.001);
            TestHelpers.PumpUntil(() => !morph.Surface!.Faces.Contains(menu), 3000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_the_success_check_draws_itself()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var morph = Morph();
        var window = Show(morph, ArcMotionLevel.Full);
        try
        {
            morph.OpenComposer(morph.Actions[1]);
            Dispatcher.UIThread.RunJobs();
            var composer = morph.ComposerOf(morph.Actions[1])!;
            composer.Field!.Text = "A note";
            morph.SubmitFrom(composer);
            Dispatcher.UIThread.RunJobs();
            var check = morph.Trigger.Check!;
            check.Progress.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            check.Progress.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            check.Progress.ShouldBe(1, 0.001);
            check.Opacity.ShouldBe(1, 0.01);
            morph.Trigger.Plus!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private static double OffsetOf(Control face) =>
        face.RenderTransform is Avalonia.Media.TransformGroup group
            ? group.Children.OfType<Avalonia.Media.TranslateTransform>().First().X
            : 0;
}

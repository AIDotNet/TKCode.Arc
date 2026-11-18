using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>
/// FeedbackMorph: the button, the panel and its faces, the keyboard model, sending (empty, async, failing, cancelled),
/// drafts, the thank-you pill and its reset, both sides, automation, and the shape's spring at full motion.
/// </summary>
public class FeedbackMorphTests
{
    private static FeedbackMorph Morph(Func<FeedbackMorphSubmission, CancellationToken, Task>? handler = null) =>
        new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(16),
            SubmitHandler = handler
        };

    private static Window Show(FeedbackMorph morph, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new Panel { Children = { morph } }, level);
        window.Width = 640;
        window.Height = 560;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Open(Window window, FeedbackMorph morph)
    {
        morph.Trigger.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        Dispatcher.UIThread.RunJobs();
    }

    private static IInputElement? Focused(Window window) => window.FocusManager!.GetFocusedElement();

    private static void Type(FeedbackMorph morph, string text)
    {
        morph.Panel!.Note!.Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void It_starts_as_a_named_40px_button()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.State.ShouldBe(FeedbackMorphState.Closed);
            morph.IsOpen.ShouldBeFalse();
            morph.Bounds.Height.ShouldBe(40);
            morph.Bounds.Width.ShouldBe(morph.Trigger.Bounds.Width);
            morph.Trigger.Label.ShouldBe("Give feedback");
            AutomationProperties.GetName(morph.Trigger).ShouldBe("Give feedback");
            morph.Panel.ShouldBeNull();
            morph.Surface!.CurrentSize.ShouldBe(morph.Trigger.DesiredSize);
            morph.EffectiveMoods.Count.ShouldBe(5);
            morph.Side.ShouldBe(FeedbackMorphSide.Bottom);
            morph.Align.ShouldBe(FeedbackMorphAlign.Center);
            morph.ResetAfter.ShouldBe(TimeSpan.FromMilliseconds(3200));
            morph.MaxLength.ShouldBe(500);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_opens_a_352_by_274_panel_with_focus_on_the_first_face()
    {
        var morph = Morph();
        var states = new List<FeedbackMorphState>();
        morph.StateChanged += (_, e) => states.Add(e.NewState);
        var window = Show(morph);
        try
        {
            Open(window, morph);

            morph.State.ShouldBe(FeedbackMorphState.Open);
            morph.IsOpen.ShouldBeTrue();
            states.ShouldBe([FeedbackMorphState.Open]);
            morph.Classes.Contains(FeedbackMorph.OpenPseudoClass).ShouldBeTrue();
            var panel = morph.Panel!;
            morph.Surface!.TargetSize.ShouldBe(new Size(352, 274));
            Focused(window).ShouldBeSameAs(panel.Moods!.Buttons[0]);
            panel.Moods.Buttons.Count.ShouldBe(5);
            panel.Moods.Buttons.Count(b => b.IsTabStop).ShouldBe(1);
            panel.CaptionText.ShouldBe(morph.CaptionHint);
            panel.PromptText.ShouldBe(morph.Placeholder);
            panel.Send!.IsEmpty.ShouldBeTrue();
            panel.StatusKind.ShouldBe(FeedbackStatusKind.None);
            AutomationProperties.GetName(panel).ShouldBe(morph.Title);
            AutomationProperties.GetName(panel.Close!).ShouldBe(morph.CloseLabel);
            AutomationProperties.GetName(panel.Moods).ShouldBe("Mood");
            AutomationProperties.GetName(panel.Moods.Buttons[2]).ShouldBe("Fine");
            AutomationProperties.GetAccessibilityView(morph.Trigger).ShouldBe(AccessibilityView.Raw);
            morph.Trigger.IsTabStop.ShouldBeFalse();
            // The control keeps the button's room in the layout; the panel grows past it.
            var width = morph.Bounds.Width;
            morph.InvalidateMeasure();
            window.UpdateLayout();
            morph.Bounds.Height.ShouldBe(40);
            morph.Bounds.Width.ShouldBe(width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_panel_fits_a_narrow_viewport()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            window.Width = 300;
            Dispatcher.UIThread.RunJobs();
            morph.Open();
            Dispatcher.UIThread.RunJobs();
            morph.Panel!.Width.ShouldBe(300 - 32);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_pick_faces_with_wrapping_and_Home_End_jump()
    {
        var morph = Morph();
        var changes = new List<(FeedbackMood? Old, FeedbackMood? New)>();
        morph.SelectionChanged += (_, e) => changes.Add((e.OldItem, e.NewItem));
        var window = Show(morph);
        try
        {
            Open(window, morph);
            var moods = morph.EffectiveMoods;
            var picker = morph.Panel!.Moods!;

            Press(window, PhysicalKey.ArrowRight);
            morph.SelectedMood.ShouldBeSameAs(moods[1]);
            Focused(window).ShouldBeSameAs(picker.Buttons[1]);
            picker.Buttons[1].IsChecked.ShouldBe(true);
            picker.Buttons.Count(b => b.IsTabStop).ShouldBe(1);
            picker.Buttons[1].IsTabStop.ShouldBeTrue();
            morph.Panel.CaptionText.ShouldBe(moods[1].Label);
            morph.Panel.PromptText.ShouldBe(moods[1].Prompt);
            morph.Panel.Send!.IsEmpty.ShouldBeFalse();
            picker.Buttons[0].Classes.Contains(FeedbackMoodButton.DimmedPseudoClass).ShouldBeTrue();

            Press(window, PhysicalKey.ArrowLeft);
            Press(window, PhysicalKey.ArrowLeft);
            morph.SelectedMood.ShouldBeSameAs(moods[4]);
            Press(window, PhysicalKey.ArrowDown);
            morph.SelectedMood.ShouldBeSameAs(moods[0]);
            Press(window, PhysicalKey.ArrowUp);
            morph.SelectedMood.ShouldBeSameAs(moods[4]);
            Press(window, PhysicalKey.Home);
            morph.SelectedMood.ShouldBeSameAs(moods[0]);
            Press(window, PhysicalKey.End);
            morph.SelectedMood.ShouldBeSameAs(moods[4]);
            changes[0].ShouldBe((null, moods[1]));
            changes.Count.ShouldBe(7);

            // A click on the picked face keeps it.
            picker.Buttons[4].RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.SelectedMood.ShouldBeSameAs(moods[4]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_empty_send_shows_the_hint_and_typing_shows_the_counter()
    {
        var morph = Morph();
        var sent = 0;
        morph.Submitted += (_, _) => sent++;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            var panel = morph.Panel!;
            panel.Send!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            sent.ShouldBe(0);
            morph.State.ShouldBe(FeedbackMorphState.Open);
            panel.StatusKind.ShouldBe(FeedbackStatusKind.Hint);
            morph.Announcement.ShouldBe(morph.EmptyHint);

            Type(morph, "Faster builds");
            morph.Message.ShouldBe("Faster builds");
            panel.StatusKind.ShouldBe(FeedbackStatusKind.None);
            panel.Digits!.Value.ShouldBe(500 - 13);
            panel.IsPromptVisible.ShouldBeFalse();
            panel.Send.IsEmpty.ShouldBeFalse();
            panel.Note!.MaxLength.ShouldBe(500);

            // White space alone is nothing to send.
            Type(morph, "   ");
            panel.Send.IsEmpty.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Enter_sends_shows_the_thank_you_and_folds_back_after_ResetAfter()
    {
        var morph = Morph();
        morph.ResetAfter = TimeSpan.FromMilliseconds(60);
        FeedbackMorphSubmission? submission = null;
        morph.Submitted += (_, e) => submission = e.Submission;
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.ArrowRight);
            Press(window, PhysicalKey.ArrowRight);
            var picked = morph.SelectedMood;
            morph.Panel!.Note!.Focus(NavigationMethod.Tab);
            Type(morph, "  The new log view is great ");
            Press(window, PhysicalKey.Enter, RawInputModifiers.Control);

            submission.ShouldNotBeNull();
            submission.MoodItem.ShouldBeSameAs(picked);
            submission.Mood.ShouldBe("fine");
            submission.Message.ShouldBe("The new log view is great");
            morph.State.ShouldBe(FeedbackMorphState.Sent);
            morph.IsOpen.ShouldBeFalse();
            morph.Classes.Contains(FeedbackMorph.SentPseudoClass).ShouldBeTrue();
            morph.Announcement.ShouldBe(morph.SuccessLabel);
            morph.SentFace!.Words.Select(w => w.Text).ShouldBe(["Got", "it,", "thank", "you", "for", "writing"]);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBeSameAs(morph.SentFace);

            TestHelpers.PumpUntil(() => morph.State == FeedbackMorphState.Closed, 3000).ShouldBeTrue();
            Focused(window).ShouldBeSameAs(morph.Trigger);
            morph.SelectedMood.ShouldBeNull();
            morph.Message.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_failed_send_keeps_the_draft_with_an_error_and_a_retry_succeeds()
    {
        TaskCompletionSource? pending = null;
        var morph = Morph((_, _) =>
        {
            pending = new TaskCompletionSource();
            return pending.Task;
        });
        var errors = new List<Exception?>();
        morph.StateChanged += (_, e) => errors.Add(e.Error);
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Type(morph, "Search misses old tickets");
            var panel = morph.Panel!;
            panel.Send!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

            morph.State.ShouldBe(FeedbackMorphState.Sending);
            panel.Send.IsBusy.ShouldBeTrue();
            panel.Send.Classes.Contains(FeedbackMorphSend.BusyPseudoClass).ShouldBeTrue();
            panel.Note!.IsReadOnly.ShouldBeTrue();
            panel.Classes.Contains(FeedbackMorphPanel.BusyPseudoClass).ShouldBeTrue();
            morph.Announcement.ShouldBe(morph.SendingLabel);
            AutomationProperties.GetName(panel.Send).ShouldBe(morph.SendingLabel);

            var error = new InvalidOperationException("offline");
            pending!.SetException(error);
            TestHelpers.PumpUntil(() => morph.State == FeedbackMorphState.Open, 3000).ShouldBeTrue();
            errors.Last().ShouldBeSameAs(error);
            panel.StatusKind.ShouldBe(FeedbackStatusKind.Error);
            morph.Announcement.ShouldBe(morph.ErrorMessage);
            morph.Message.ShouldBe("Search misses old tickets");
            panel.Note.IsReadOnly.ShouldBeFalse();
            panel.Send.IsBusy.ShouldBeFalse();

            panel.Send.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.State.ShouldBe(FeedbackMorphState.Sending);
            pending.SetResult();
            TestHelpers.PumpUntil(() => morph.State == FeedbackMorphState.Sent, 3000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_while_sending_cancels_the_handler_and_keeps_the_draft()
    {
        CancellationToken token = default;
        var morph = Morph((_, ct) =>
        {
            token = ct;
            return Task.Delay(Timeout.Infinite, ct);
        });
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Type(morph, "Dark mode please");
            morph.Submit();
            morph.State.ShouldBe(FeedbackMorphState.Sending);

            Press(window, PhysicalKey.Escape);
            morph.State.ShouldBe(FeedbackMorphState.Closed);
            token.IsCancellationRequested.ShouldBeTrue();
            morph.Message.ShouldBe("Dark mode please");
            Focused(window).ShouldBeSameAs(morph.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_and_the_close_button_fold_it_back_with_the_draft_kept()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            Open(window, morph);
            Press(window, PhysicalKey.End);
            Type(morph, "Kept for later");
            Press(window, PhysicalKey.Escape);

            morph.State.ShouldBe(FeedbackMorphState.Closed);
            morph.IsOpen.ShouldBeFalse();
            Focused(window).ShouldBeSameAs(morph.Trigger);
            morph.Trigger.IsTabStop.ShouldBeTrue();

            Open(window, morph);
            morph.Panel!.Note!.Text.ShouldBe("Kept for later");
            morph.SelectedMood.ShouldBeSameAs(morph.EffectiveMoods[4]);
            Focused(window).ShouldBeSameAs(morph.Panel.Moods!.Buttons[4]);

            morph.Panel.Close!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.State.ShouldBe(FeedbackMorphState.Closed);
            Focused(window).ShouldBeSameAs(morph.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabbing_past_the_send_button_folds_the_panel()
    {
        var morph = Morph();
        var after = new Avalonia.Controls.Button { Content = "Next", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
        var window = TestHelpers.Show(new Panel { Children = { morph, after } });
        try
        {
            window.Width = 640;
            window.Height = 560;
            Dispatcher.UIThread.RunJobs();
            Open(window, morph);
            morph.Panel!.Send!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            morph.State.ShouldBe(FeedbackMorphState.Open);
            Press(window, PhysicalKey.Tab);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBeSameAs(after);
            morph.State.ShouldBe(FeedbackMorphState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_outside_and_IsOpen_fold_it_back()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            morph.State.ShouldBe(FeedbackMorphState.Open);

            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var outside = new Point(20, 520);
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            morph.State.ShouldBe(FeedbackMorphState.Closed);

            morph.Open();
            Dispatcher.UIThread.RunJobs();
            morph.IsOpen = false;
            morph.State.ShouldBe(FeedbackMorphState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Side_top_grows_the_panel_upward_from_the_button()
    {
        var morph = Morph();
        morph.Side = FeedbackMorphSide.Top;
        morph.VerticalAlignment = VerticalAlignment.Bottom;
        var window = Show(morph);
        try
        {
            morph.Open();
            Dispatcher.UIThread.RunJobs();
            var surface = morph.Surface!;
            var rect = surface.SurfaceRect();
            rect.Bottom.ShouldBe(morph.Bounds.Height, 0.01);
            rect.Height.ShouldBe(274, 0.01);
            rect.Center.X.ShouldBe(morph.Bounds.Width / 2, 0.01);

            morph.Align = FeedbackMorphAlign.Start;
            surface.SurfaceRect().X.ShouldBe(0);
            morph.Align = FeedbackMorphAlign.End;
            surface.SurfaceRect().Right.ShouldBe(morph.Bounds.Width, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_moods_replace_the_scale_and_drop_a_stale_pick()
    {
        var morph = Morph();
        var window = Show(morph);
        try
        {
            morph.Open();
            Dispatcher.UIThread.RunJobs();
            morph.SelectedMood = morph.EffectiveMoods[3];
            var three = new FeedbackMoodCollection
            {
                new FeedbackMood { Value = "no", Label = "No", Face = FeedbackFace.Sad },
                new FeedbackMood { Value = "maybe", Label = "Maybe", Face = FeedbackFace.Neutral },
                new FeedbackMood { Value = "yes", Label = "Yes", Face = FeedbackFace.Happy, Prompt = "What helped?" }
            };
            morph.Moods = three;
            Dispatcher.UIThread.RunJobs();
            morph.SelectedMood.ShouldBeNull();
            morph.Panel!.Moods!.Buttons.Count.ShouldBe(3);

            morph.SelectedMood = three[2];
            morph.Panel.PromptText.ShouldBe("What helped?");
            morph.Panel.Moods.Buttons[2].IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_the_shape_springs_open_and_the_rows_stagger_in()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var morph = Morph();
        var window = Show(morph, ArcMotionLevel.Full);
        try
        {
            var pill = morph.Surface!.CurrentSize;
            morph.Open();
            Dispatcher.UIThread.RunJobs();
            var surface = morph.Surface!;
            surface.CurrentSize.ShouldBe(pill);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Dispatcher.UIThread.RunJobs();
            var width = surface.CurrentSize.Width;
            width.ShouldBeGreaterThan(pill.Width + 40);
            width.ShouldBeLessThan(340);
            surface.IsMoving.ShouldBeTrue();
            // Rows come in 50 ms apart: the title is further along than the action row.
            var panel = morph.Panel!;
            var header = TestHelpers.Descendant<Control>(panel, FeedbackMorphPanel.HeaderPart);
            var actions = TestHelpers.Descendant<Control>(panel, FeedbackMorphPanel.ActionsPart);
            header.Opacity.ShouldBeGreaterThan(actions.Opacity);

            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            surface.CurrentSize.ShouldBe(new Size(352, 274));
            actions.Opacity.ShouldBe(1, 0.001);
            panel.Moods!.Buttons[0].Opacity.ShouldBe(1, 0.001);

            morph.Close();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            surface.CurrentSize.ShouldBe(pill);
            surface.Faces.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_reduced_motion_the_shape_jumps()
    {
        var morph = Morph();
        var window = Show(morph, ArcMotionLevel.Reduced);
        try
        {
            morph.Open();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            morph.Surface!.CurrentSize.ShouldBe(new Size(352, 274));
            morph.Surface.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

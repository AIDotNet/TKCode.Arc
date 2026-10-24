using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class MultiStepFormTests
{
    internal static Window Show(Control control, double width = 1000, double height = 800, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    internal static T Part<T>(Visual root, string name) where T : Control => TestHelpers.Descendant<T>(root, name);

    /// <summary>Two steps like the Gallery preview: a text box with a description, then a second text box without one.</summary>
    internal static MultiStepForm TwoSteps(out TextBox first, out TextBox second)
    {
        first = new TextBox { Text = "Northwind studio" };
        second = new TextBox { Text = "Priya Raman" };
        var form = new MultiStepForm();
        form.Steps.Add(new MultiStepFormStep { Id = "workspace", Title = "Workspace basics", Description = "Give it a short, memorable name.", Content = first });
        form.Steps.Add(new MultiStepFormStep { Id = "lead", Title = "Pick a lead", Content = second });
        return form;
    }

    internal static MultiStepButton NextButton(MultiStepForm form) => Part<MultiStepButton>(form, MultiStepForm.NextPart);

    internal static MultiStepButton BackButton(MultiStepForm form) => Part<MultiStepButton>(form, MultiStepForm.BackPart);

    /// <summary>Presses a button with the keyboard (Enter), the way a keyboard user does.</summary>
    internal static void Press(Window window, Avalonia.Controls.Button button)
    {
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static string CountText(MultiStepForm form) =>
        Part<TextBlock>(form, MultiStepForm.CountPrefixPart).Text
        + Part<MultiStepSwap>(form, MultiStepForm.CountDigitPart).Text
        + Part<TextBlock>(form, MultiStepForm.CountSuffixPart).Text;

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    #region Defaults

    [AvaloniaFact]
    public void Defaults_match_the_reference_wording_and_state()
    {
        var form = new MultiStepForm();
        form.NextLabel.ShouldBe("Continue");
        form.CompleteLabel.ShouldBe("Finish");
        form.BackLabel.ShouldBe("Back");
        form.CountFormat.ShouldBe("Step {0} of {1}");
        form.AnnouncementFormat.ShouldBe("Step {0} of {1}");
        form.FormName.ShouldBe("Multi-step form");
        form.SuccessTitle.ShouldNotBeNullOrWhiteSpace();
        form.SuccessDescription.ShouldNotBeNullOrWhiteSpace();
        form.CurrentIndex.ShouldBe(0);
        form.IsComplete.ShouldBeFalse();
        form.IsBusy.ShouldBeFalse();
        form.StepCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Without_steps_nothing_is_drawn()
    {
        var form = new MultiStepForm();
        var window = Show(form);
        try
        {
            form.Classes.Contains(MultiStepForm.EmptyPseudoClass).ShouldBeTrue();
            Part<Border>(form, MultiStepForm.CardPart).IsVisible.ShouldBeFalse();
            form.Steps.Add(new MultiStepFormStep { Title = "Only" });
            Dispatcher.UIThread.RunJobs();
            form.Classes.Contains(MultiStepForm.EmptyPseudoClass).ShouldBeFalse();
            Part<Border>(form, MultiStepForm.CardPart).IsVisible.ShouldBeTrue();
            form.StepCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void First_step_shows_progress_count_and_a_disabled_back_button()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            CountText(form).ShouldBe("Step 1 of 2");
            form.Classes.Contains(MultiStepForm.FirstPseudoClass).ShouldBeTrue();
            form.Classes.Contains(MultiStepForm.LastPseudoClass).ShouldBeFalse();
            BackButton(form).IsEnabled.ShouldBeFalse();
            BackButton(form).Label.ShouldBe("Back");
            NextButton(form).Label.ShouldBe("Continue");
            var items = form.ProgressItems;
            items.Count.ShouldBe(2);
            items[0].Classes.Contains(MultiStepProgressItem.CurrentPseudoClass).ShouldBeTrue();
            items[0].IsDone.ShouldBeFalse();
            items[1].Classes.Contains(MultiStepProgressItem.CurrentPseudoClass).ShouldBeFalse();
            items[1].Classes.Contains(MultiStepProgressItem.CompletePseudoClass).ShouldBeFalse();
            items[0].Title.ShouldBe("Workspace basics");
            items[1].Marker!.Number.ShouldBe(2);
            var view = form.ViewOf(form.Steps[0])!;
            view.IsVisible.ShouldBeTrue();
            view.Legend!.Text.ShouldBe("Workspace basics");
            view.Classes.Contains(MultiStepStepView.DescribedPseudoClass).ShouldBeTrue();
            // Only the first step was ever shown.
            form.ViewOf(form.Steps[1]).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_is_540_wide_and_383_high_like_the_reference()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form, width: 1200);
        try
        {
            var card = Part<Border>(form, MultiStepForm.CardPart);
            card.Bounds.Width.ShouldBe(540, 0.5);
            // 1 + header 53 + 24 + step area 224 + buttons 60 + 20 + 1.
            card.Bounds.Height.ShouldBe(383, 0.6);
            Part<Border>(form, "PART_HeaderFrame").Bounds.Height.ShouldBe(53, 0.6);
            Part<MultiStepViewport>(form, MultiStepForm.ViewportPart).Bounds.Height.ShouldBe(224, 0.6);
            NextButton(form).Bounds.Height.ShouldBe(36, 0.5);
            // Centered in a wider host.
            var x = card.TranslatePoint(default, form)!.Value.X;
            x.ShouldBe((form.Bounds.Width - 540) / 2, 0.6);
            form.Classes.Contains(MultiStepForm.NarrowPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_cards_stack_the_count_and_tighten_like_the_reference()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form, width: 358);
        try
        {
            form.Classes.Contains(MultiStepForm.NarrowPseudoClass).ShouldBeTrue();
            var header = Part<MultiStepHeaderPanel>(form, MultiStepForm.HeaderPart);
            header.IsStacked.ShouldBeTrue();
            var count = Part<StackPanel>(form, "PART_Count");
            var list = Part<ScrollViewer>(form, MultiStepForm.ProgressScrollerPart);
            count.TranslatePoint(default, header)!.Value.Y.ShouldBe(0, 0.5);
            list.TranslatePoint(default, header)!.Value.Y.ShouldBeGreaterThan(count.Bounds.Height);
            var card = Part<Border>(form, MultiStepForm.CardPart);
            card.Bounds.Width.ShouldBe(358, 0.5);
            // 1 + header 69.8 + 20 + step area 240 + buttons 56 + 16 + 1 (Arc: 403.8).
            card.Bounds.Height.ShouldBe(403.8, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_breakpoint_follows_the_card_width_not_the_window()
    {
        var form = TwoSteps(out _, out _);
        form.Width = 389;
        var window = Show(form, width: 1200);
        try
        {
            form.Classes.Contains(MultiStepForm.NarrowPseudoClass).ShouldBeFalse();
            form.Width = 388;
            Dispatcher.UIThread.RunJobs();
            form.Classes.Contains(MultiStepForm.NarrowPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Navigation

    [AvaloniaFact]
    public void Continue_moves_forward_and_updates_progress_count_buttons_and_focus()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            var changes = new List<MultiStepFormStepChangedEventArgs>();
            form.StepChanged += (_, e) => changes.Add(e);
            Press(window, NextButton(form));
            form.CurrentIndex.ShouldBe(1);
            changes.Count.ShouldBe(1);
            changes[0].OldIndex.ShouldBe(0);
            changes[0].NewIndex.ShouldBe(1);
            changes[0].Direction.ShouldBe(1);
            CountText(form).ShouldBe("Step 2 of 2");
            form.ProgressItems[0].IsDone.ShouldBeTrue();
            form.ProgressItems[0].Marker!.IsChecked.ShouldBeTrue();
            form.ProgressItems[1].Classes.Contains(MultiStepProgressItem.CurrentPseudoClass).ShouldBeTrue();
            BackButton(form).IsEnabled.ShouldBeTrue();
            NextButton(form).Label.ShouldBe("Finish");
            form.Classes.Contains(MultiStepForm.LastPseudoClass).ShouldBeTrue();
            var view = form.ViewOf(form.Steps[1])!;
            view.IsVisible.ShouldBeTrue();
            form.ViewOf(form.Steps[0])!.IsVisible.ShouldBeFalse();
            view.Classes.Contains(MultiStepStepView.DescribedPseudoClass).ShouldBeFalse();
            // Focus lands on the new heading (same frame, as in Arc).
            Focused(window).ShouldBeSameAs(view.Legend);
            Part<AuthAnnouncer>(form, MultiStepForm.AnnouncerPart).Announcement.ShouldBe("Step 2 of 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_returns_and_keeps_the_focus_on_the_form()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            Press(window, NextButton(form));
            var changes = new List<MultiStepFormStepChangedEventArgs>();
            form.StepChanged += (_, e) => changes.Add(e);
            Press(window, BackButton(form));
            form.CurrentIndex.ShouldBe(0);
            changes.Single().Direction.ShouldBe(-1);
            BackButton(form).IsEnabled.ShouldBeFalse();
            CountText(form).ShouldBe("Step 1 of 2");
            form.ProgressItems[0].IsDone.ShouldBeFalse();
            form.ProgressItems[0].Marker!.IsChecked.ShouldBeFalse();
            NextButton(form).Label.ShouldBe("Continue");
            // Back turned disabled, but the focus had already moved to the heading.
            Focused(window).ShouldBeSameAs(form.ViewOf(form.Steps[0])!.Legend);
            Part<AuthAnnouncer>(form, MultiStepForm.AnnouncerPart).Announcement.ShouldBe("Step 1 of 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Field_values_survive_back_and_continue()
    {
        var form = TwoSteps(out var first, out var second);
        var window = Show(form);
        try
        {
            first.Text = "Typed by hand";
            Press(window, NextButton(form));
            second.Text = "Someone else";
            Press(window, BackButton(form));
            first.Text.ShouldBe("Typed by hand");
            first.IsEffectivelyVisible.ShouldBeTrue();
            form.ViewOf(form.Steps[0])!.IsVisualAncestorOf(first).ShouldBeTrue();
            Press(window, NextButton(form));
            second.Text.ShouldBe("Someone else");
            // The hidden step's fields stay in the tree (bindings and data context keep working) but invisible.
            TopLevel.GetTopLevel(first).ShouldNotBeNull();
            first.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Finishing_shows_the_success_block_and_folds_the_buttons()
    {
        var form = TwoSteps(out _, out _);
        var command = new RecordingCommand();
        form.CompleteCommand = command;
        var window = Show(form);
        try
        {
            var completed = new List<MultiStepFormCompletedEventArgs>();
            form.Completed += (_, e) => completed.Add(e);
            Press(window, NextButton(form));
            Press(window, NextButton(form));
            form.IsComplete.ShouldBeTrue();
            completed.Count.ShouldBe(1);
            completed[0].Steps.Count.ShouldBe(2);
            command.Parameters.Single().ShouldBeSameAs(completed[0]);
            form.Classes.Contains(MultiStepForm.CompletePseudoClass).ShouldBeTrue();
            var success = form.SuccessView!;
            success.IsVisible.ShouldBeTrue();
            success.Title.ShouldBe(form.SuccessTitle);
            success.Glyph!.Progress.ShouldBe(1);
            form.ViewOf(form.Steps[1])!.IsVisible.ShouldBeFalse();
            var actions = Part<MultiStepCollapse>(form, MultiStepForm.ActionsPart);
            actions.IsVisible.ShouldBeFalse();
            // Every entry is done; the last one keeps the current-step status (Arc keeps aria-current there).
            form.ProgressItems.All(i => i.IsDone).ShouldBeTrue();
            form.ProgressItems[1].IsCurrent.ShouldBeTrue();
            CountText(form).ShouldBe("Step 2 of 2");
            Focused(window).ShouldBeSameAs(success.TitleBlock);
            Part<AuthAnnouncer>(form, MultiStepForm.AnnouncerPart).Announcement
                .ShouldBe(form.SuccessTitle + ". " + form.SuccessDescription);
            // 1 + 53 + 24 + 224 + 20 + 1 (Arc: 323).
            Part<Border>(form, MultiStepForm.CardPart).Bounds.Height.ShouldBe(323, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Restart_shows_the_first_step_and_the_buttons_again()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            Press(window, NextButton(form));
            Press(window, NextButton(form));
            var changes = new List<MultiStepFormStepChangedEventArgs>();
            form.StepChanged += (_, e) => changes.Add(e);
            form.Restart();
            Dispatcher.UIThread.RunJobs();
            form.IsComplete.ShouldBeFalse();
            form.CurrentIndex.ShouldBe(0);
            changes.Single().Direction.ShouldBe(-1);
            Part<MultiStepCollapse>(form, MultiStepForm.ActionsPart).IsVisible.ShouldBeTrue();
            form.SuccessView!.IsVisible.ShouldBeFalse();
            form.ViewOf(form.Steps[0])!.IsVisible.ShouldBeTrue();
            form.ProgressItems.Any(i => i.IsDone).ShouldBeFalse();
            BackButton(form).IsEnabled.ShouldBeFalse();
            Part<Border>(form, MultiStepForm.CardPart).Bounds.Height.ShouldBe(383, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_IsComplete_shows_success_without_raising_Completed()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            var completed = 0;
            form.Completed += (_, _) => completed++;
            form.IsComplete = true;
            Dispatcher.UIThread.RunJobs();
            form.SuccessView!.IsVisible.ShouldBeTrue();
            completed.ShouldBe(0);
            form.IsComplete = false;
            Dispatcher.UIThread.RunJobs();
            form.ViewOf(form.Steps[0])!.IsVisible.ShouldBeTrue();
            form.SuccessView!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_form_that_starts_complete_shows_success_at_once()
    {
        var form = TwoSteps(out _, out _);
        form.IsComplete = true;
        var window = Show(form);
        try
        {
            form.SuccessView!.IsVisible.ShouldBeTrue();
            Part<MultiStepCollapse>(form, MultiStepForm.ActionsPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CurrentIndex_is_clamped_once_there_are_steps()
    {
        // XAML sets attributes before children: an index set before the steps waits for them.
        var form = new MultiStepForm { CurrentIndex = 1 };
        form.Steps.Add(new MultiStepFormStep { Title = "One" });
        form.Steps.Add(new MultiStepFormStep { Title = "Two" });
        var window = Show(form);
        try
        {
            form.CurrentIndex.ShouldBe(1);
            form.ViewOf(form.Steps[1])!.IsVisible.ShouldBeTrue();
            form.CurrentIndex = 7;
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex.ShouldBe(1);
            form.CurrentIndex = -3;
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex.ShouldBe(0);
            form.ViewOf(form.Steps[0])!.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_single_step_is_first_and_last()
    {
        var form = new MultiStepForm();
        form.Steps.Add(new MultiStepFormStep { Title = "Only" });
        var window = Show(form);
        try
        {
            BackButton(form).IsEnabled.ShouldBeFalse();
            NextButton(form).Label.ShouldBe("Finish");
            CountText(form).ShouldBe("Step 1 of 1");
            Press(window, NextButton(form));
            form.IsComplete.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_CurrentIndex_from_outside_does_not_take_the_focus()
    {
        var outside = new Avalonia.Controls.Button { Content = "Elsewhere" };
        var form = TwoSteps(out _, out _);
        var host = new StackPanel { Children = { outside, form } };
        var window = Show(host);
        try
        {
            outside.Focus(NavigationMethod.Tab);
            form.CurrentIndex = 1;
            Dispatcher.UIThread.RunJobs();
            form.ViewOf(form.Steps[1])!.IsVisible.ShouldBeTrue();
            Focused(window).ShouldBeSameAs(outside);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StepsSource_wins_and_is_followed()
    {
        var source = new ObservableCollection<MultiStepFormStep>
        {
            new() { Title = "A" },
            new() { Title = "B" }
        };
        var form = new MultiStepForm();
        form.Steps.Add(new MultiStepFormStep { Title = "Ignored" });
        form.StepsSource = source;
        var window = Show(form);
        try
        {
            form.StepCount.ShouldBe(2);
            form.ProgressItems.Select(i => i.Title).ShouldBe(new[] { "A", "B" });
            source.Add(new MultiStepFormStep { Title = "C" });
            Dispatcher.UIThread.RunJobs();
            form.StepCount.ShouldBe(3);
            CountText(form).ShouldBe("Step 1 of 3");
            form.ProgressItems.Select(i => i.Title).ShouldBe(new[] { "A", "B", "C" });
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Person
    {
        public string Name { get; set; } = "Ada Byron";
    }

    [AvaloniaFact]
    public void Step_fields_bind_to_the_forms_data_context_also_after_being_hidden()
    {
        var label = new TextBlock { [!TextBlock.TextProperty] = new Binding(nameof(Person.Name)) };
        var form = new MultiStepForm { DataContext = new Person() };
        form.Steps.Add(new MultiStepFormStep { Title = "Name", Content = label });
        form.Steps.Add(new MultiStepFormStep { Title = "Next" });
        var window = Show(form);
        try
        {
            label.Text.ShouldBe("Ada Byron");
            form.CurrentIndex = 1;
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            label.Text.ShouldBe("Ada Byron");
            label.DataContext.ShouldBeSameAs(form.DataContext);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_removed_step_lets_go_of_its_fields()
    {
        var form = TwoSteps(out var first, out _);
        var window = Show(form);
        try
        {
            var step = form.Steps[0];
            form.Steps.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            first.Parent.ShouldBeNull();
            form.CurrentIndex.ShouldBe(0);
            form.ViewOf(form.Steps[0])!.IsVisible.ShouldBeTrue();
            // The same step (and control) can be shown by another form.
            var other = new MultiStepForm();
            other.Steps.Add(step);
            var host = (StackPanel)new StackPanel();
            ((Avalonia.Controls.Primitives.VisualLayerManager)window.Content!).Child = host;
            host.Children.Add(form);
            host.Children.Add(other);
            Dispatcher.UIThread.RunJobs();
            other.ViewOf(step)!.IsVisualAncestorOf(first).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_detached_form_stops_following_its_source_and_catches_up_when_back()
    {
        var source = new ObservableCollection<MultiStepFormStep> { new() { Title = "A" } };
        var form = new MultiStepForm { StepsSource = source };
        var holder = new ContentControl { Content = form };
        var window = Show(holder);
        try
        {
            form.StepCount.ShouldBe(1);
            holder.Content = null;
            Dispatcher.UIThread.RunJobs();
            source.Add(new MultiStepFormStep { Title = "B" });
            source[0].Title = "Renamed";
            form.StepCount.ShouldBe(1);
            holder.Content = form;
            Dispatcher.UIThread.RunJobs();
            form.StepCount.ShouldBe(2);
            form.ProgressItems[0].Title.ShouldBe("Renamed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Step_title_and_description_changes_reach_the_screen()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            form.Steps[0].Title = "Your workspace";
            form.Steps[0].Description = null;
            Dispatcher.UIThread.RunJobs();
            var view = form.ViewOf(form.Steps[0])!;
            view.Legend!.Text.ShouldBe("Your workspace");
            view.Classes.Contains(MultiStepStepView.DescribedPseudoClass).ShouldBeFalse();
            form.ProgressItems[0].Title.ShouldBe("Your workspace");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_and_formats_are_replaceable()
    {
        var form = TwoSteps(out _, out _);
        form.NextLabel = "Next step";
        form.CompleteLabel = "Send";
        form.BackLabel = "Previous";
        form.CountFormat = "{0} / {1}";
        form.AnnouncementFormat = "Now on {0} of {1}";
        var window = Show(form);
        try
        {
            NextButton(form).Label.ShouldBe("Next step");
            BackButton(form).Label.ShouldBe("Previous");
            CountText(form).ShouldBe("1 / 2");
            Press(window, NextButton(form));
            NextButton(form).Label.ShouldBe("Send");
            Part<AuthAnnouncer>(form, MultiStepForm.AnnouncerPart).Announcement.ShouldBe("Now on 2 of 2");
            form.CountFormat = "Page {0}";
            Dispatcher.UIThread.RunJobs();
            CountText(form).ShouldBe("Page 2");
            AutomationProperties.GetName(NextButton(form)).ShouldBe("Send");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void Enter_in_a_text_field_submits_the_step()
    {
        var form = TwoSteps(out var first, out var second);
        var window = Show(form);
        try
        {
            first.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex.ShouldBe(1);
            Focused(window).ShouldBeSameAs(form.ViewOf(form.Steps[1])!.Legend);
            second.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.IsComplete.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_in_a_multi_line_field_does_not_submit()
    {
        var notes = new TextBox { AcceptsReturn = true };
        var form = new MultiStepForm();
        form.Steps.Add(new MultiStepFormStep { Title = "Notes", Content = notes });
        form.Steps.Add(new MultiStepFormStep { Title = "Done" });
        var window = Show(form);
        try
        {
            notes.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_on_an_open_combobox_picks_without_submitting()
    {
        var lead = new Combobox { Label = "Lead" };
        lead.Items.Add(new SelectItem { Content = "Priya Raman" });
        lead.Items.Add(new SelectItem { Content = "Tomás Ortega" });
        var form = new MultiStepForm();
        form.Steps.Add(new MultiStepFormStep { Title = "Pick a lead", Content = lead });
        form.Steps.Add(new MultiStepFormStep { Title = "Done" });
        var window = Show(form);
        try
        {
            lead.FocusInput();
            Dispatcher.UIThread.RunJobs();
            lead.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            lead.SelectedIndex.ShouldBeGreaterThanOrEqualTo(0);
            form.CurrentIndex.ShouldBe(0);
            // Closed: Enter submits.
            lead.IsOpen.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_skips_the_disabled_back_button_and_the_headings()
    {
        var form = TwoSteps(out var first, out _);
        var window = Show(form);
        try
        {
            first.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBeSameAs(NextButton(form));
            var legend = form.ViewOf(form.Steps[0])!.Legend!;
            legend.Focusable.ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(legend).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Validation and async work

    [AvaloniaFact]
    public void StepValidating_can_keep_the_form_on_the_step()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            MultiStepFormStepValidatingEventArgs? seen = null;
            form.StepValidating += (_, e) =>
            {
                seen = e;
                e.Cancel = true;
            };
            Press(window, NextButton(form));
            seen!.Index.ShouldBe(0);
            seen.Step.ShouldBeSameAs(form.Steps[0]);
            form.CurrentIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ValidateAsync_runs_with_a_busy_button_and_decides()
    {
        var form = TwoSteps(out _, out _);
        var gate = new TaskCompletionSource<bool>();
        CancellationToken seenToken = default;
        form.Steps[0].ValidateAsync = token =>
        {
            seenToken = token;
            return gate.Task;
        };
        var window = Show(form);
        try
        {
            var next = form.NextAsync();
            Dispatcher.UIThread.RunJobs();
            form.IsBusy.ShouldBeTrue();
            form.Classes.Contains(MultiStepForm.BusyPseudoClass).ShouldBeTrue();
            NextButton(form).IsLoading.ShouldBeTrue();
            BackButton(form).IsEnabled.ShouldBeFalse();
            (await form.NextAsync()).ShouldBeFalse();
            gate.SetResult(false);
            (await next).ShouldBeFalse();
            form.IsBusy.ShouldBeFalse();
            NextButton(form).IsLoading.ShouldBeFalse();
            form.CurrentIndex.ShouldBe(0);
            seenToken.CanBeCanceled.ShouldBeTrue();

            form.Steps[0].ValidateAsync = _ => Task.FromResult(true);
            (await form.NextAsync()).ShouldBeTrue();
            form.CurrentIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_throwing_check_keeps_the_step()
    {
        var form = TwoSteps(out _, out _);
        form.Steps[0].ValidateAsync = _ => throw new InvalidOperationException("no");
        var window = Show(form);
        try
        {
            (await form.NextAsync()).ShouldBeFalse();
            form.CurrentIndex.ShouldBe(0);
            form.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Back_cancels_a_running_check()
    {
        var form = TwoSteps(out _, out _);
        form.CurrentIndex = 1;
        var gate = new TaskCompletionSource<bool>();
        CancellationToken seen = default;
        form.Steps[1].ValidateAsync = token =>
        {
            seen = token;
            return gate.Task;
        };
        var window = Show(form);
        try
        {
            var next = form.NextAsync();
            form.IsBusy.ShouldBeTrue();
            form.Back();
            seen.IsCancellationRequested.ShouldBeTrue();
            form.IsBusy.ShouldBeFalse();
            form.CurrentIndex.ShouldBe(0);
            gate.SetResult(true);
            (await next).ShouldBeFalse();
            form.IsComplete.ShouldBeFalse();
            form.CurrentIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_step_change_from_outside_cancels_a_running_check()
    {
        var form = new MultiStepForm();
        for (var i = 0; i < 3; i++)
        {
            form.Steps.Add(new MultiStepFormStep { Title = "Step " + i });
        }
        var gate = new TaskCompletionSource<bool>();
        CancellationToken seen = default;
        form.Steps[0].ValidateAsync = token =>
        {
            seen = token;
            return gate.Task;
        };
        var window = Show(form);
        try
        {
            var next = form.NextAsync();
            form.IsBusy.ShouldBeTrue();
            form.CurrentIndex = 2;
            Dispatcher.UIThread.RunJobs();
            seen.IsCancellationRequested.ShouldBeTrue();
            form.IsBusy.ShouldBeFalse();
            gate.SetResult(true);
            (await next).ShouldBeFalse();
            form.CurrentIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_field_with_a_data_validation_error_blocks_and_takes_the_focus()
    {
        var form = TwoSteps(out var first, out _);
        var window = Show(form);
        try
        {
            DataValidationErrors.SetError(first, new DataValidationException("Required"));
            Press(window, NextButton(form));
            form.CurrentIndex.ShouldBe(0);
            Focused(window).ShouldBeSameAs(first);
            DataValidationErrors.ClearErrors(first);
            Press(window, NextButton(form));
            form.CurrentIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CompleteAsync_runs_before_success_and_failure_keeps_the_last_step()
    {
        var form = TwoSteps(out _, out _);
        form.CurrentIndex = 1;
        var gate = new TaskCompletionSource();
        form.CompleteAsync = _ => gate.Task;
        var window = Show(form);
        try
        {
            var failures = new List<Exception>();
            form.CompleteFailed += (_, e) => failures.Add(e.Exception);
            var completed = 0;
            form.Completed += (_, _) => completed++;
            var next = form.NextAsync();
            form.IsBusy.ShouldBeTrue();
            form.IsComplete.ShouldBeFalse();
            NextButton(form).IsLoading.ShouldBeTrue();
            gate.SetException(new InvalidOperationException("Server said no"));
            (await next).ShouldBeFalse();
            failures.Single().Message.ShouldBe("Server said no");
            form.IsComplete.ShouldBeFalse();
            form.IsBusy.ShouldBeFalse();
            completed.ShouldBe(0);

            form.CompleteAsync = _ => Task.Delay(1);
            (await form.NextAsync()).ShouldBeTrue();
            form.IsComplete.ShouldBeTrue();
            completed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_loading_button_ignores_presses()
    {
        var form = TwoSteps(out _, out _);
        var gate = new TaskCompletionSource<bool>();
        var calls = 0;
        form.Steps[0].ValidateAsync = _ =>
        {
            calls++;
            return gate.Task;
        };
        var window = Show(form);
        try
        {
            Press(window, NextButton(form));
            Press(window, NextButton(form));
            calls.ShouldBe(1);
            Focused(window).ShouldBeSameAs(NextButton(form));
            gate.SetResult(true);
            Dispatcher.UIThread.RunJobs();
            form.CurrentIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void Names_landmarks_and_statuses()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            var card = Part<Border>(form, MultiStepForm.CardPart);
            AutomationProperties.GetName(card).ShouldBe("Multi-step form");
            AutomationProperties.GetLandmarkType(card).ShouldBe(AutomationLandmarkType.Form);
            var header = Part<MultiStepHeaderPanel>(form, MultiStepForm.HeaderPart);
            AutomationProperties.GetLandmarkType(header).ShouldBe(AutomationLandmarkType.Navigation);
            AutomationProperties.GetName(header).ShouldBe("Progress, step 1 of 2");
            AutomationProperties.GetAccessibilityView(Part<StackPanel>(form, "PART_Count")).ShouldBe(AccessibilityView.Raw);
            AutomationProperties.GetItemStatus(form.ProgressItems[0]).ShouldBe("Current step");
            AutomationProperties.GetItemStatus(form.ProgressItems[1]).ShouldBeNull();
            AutomationProperties.GetName(form.ProgressItems[1]).ShouldBe("Pick a lead");
            ControlAutomationPeer.CreatePeerForElement(form.ProgressItems[0]).GetAutomationControlType()
                .ShouldBe(AutomationControlType.ListItem);
            ControlAutomationPeer.CreatePeerForElement(Part<MultiStepProgressList>(form, MultiStepForm.ProgressListPart))
                .GetAutomationControlType().ShouldBe(AutomationControlType.List);
            var view = form.ViewOf(form.Steps[0])!;
            AutomationProperties.GetName(view).ShouldBe("Workspace basics");
            ControlAutomationPeer.CreatePeerForElement(view).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            AutomationProperties.GetName(BackButton(form)).ShouldBe("Back");
            Press(window, NextButton(form));
            AutomationProperties.GetName(header).ShouldBe("Progress, step 2 of 2");
            AutomationProperties.GetItemStatus(form.ProgressItems[0]).ShouldBe("Completed");
            Press(window, NextButton(form));
            AutomationProperties.GetItemStatus(form.ProgressItems[1]).ShouldBe("Current step, Completed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_answers_a_mouse_only()
    {
        var form = TwoSteps(out _, out _);
        var window = Show(form);
        try
        {
            var next = NextButton(form);
            var center = next.TranslatePoint(new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            next.Classes.Contains(MultiStepButton.HoverPseudoClass).ShouldBeTrue();
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            next.Classes.Contains(MultiStepButton.HoverPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}

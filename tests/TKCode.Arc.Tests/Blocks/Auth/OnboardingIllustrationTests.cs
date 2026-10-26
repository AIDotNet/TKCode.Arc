using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class OnboardingIllustrationTests
{
    internal static Window Show(Control control, double width = 800, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 760;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, Avalonia.Controls.Button button)
    {
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Visual visual)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static OnboardingDotState[] DotStates(OnboardingIllustration block) => block.DotRow!.Dots.Select(d => d.State).ToArray();

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

    [AvaloniaFact]
    public void Defaults_Show_The_Four_Sample_Steps_On_The_First_Step()
    {
        var block = new OnboardingIllustration();
        var window = Show(block);
        try
        {
            block.Steps.ShouldBeEmpty();
            block.CurrentIndex.ShouldBe(0);
            block.IsCompleted.ShouldBeFalse();
            var samples = OnboardingIllustrationSamples.Steps();
            samples.Select(s => s.Scene).ShouldBe([OnboardingScene.Folder, OnboardingScene.Files, OnboardingScene.Team, OnboardingScene.Badge]);
            block.DisplayedCopy.ShouldBe((samples[0].Title, samples[0].Description));
            block.DisplayedPrimary.ShouldBe((OnboardingPrimaryKind.Next, "Next"));
            block.BackButton!.IsInert.ShouldBeTrue();
            block.SkipButton!.IsVisible.ShouldBeTrue();
            block.SkipButton.Content.ShouldBe("Skip");
            DotStates(block).ShouldBe([OnboardingDotState.Current, OnboardingDotState.Upcoming, OnboardingDotState.Upcoming, OnboardingDotState.Upcoming]);
            block.Classes.ShouldContain(OnboardingIllustration.FirstPseudoClass);
            block.Classes.ShouldNotContain(OnboardingIllustration.LastPseudoClass);
            block.Art!.Scene.ShouldBeSameAs(OnboardingArtScenes.Folder);

            AutomationProperties.GetName(block).ShouldBe("Onboarding");
            AutomationProperties.GetLandmarkType(block).ShouldBe(AutomationLandmarkType.Region);
            AutomationProperties.GetName(block.Art).ShouldBe("An empty open box with a plus sign beside it");
            ControlAutomationPeer.CreatePeerForElement(block.Art).GetAutomationControlType().ShouldBe(AutomationControlType.Image);
            AutomationProperties.GetName(block.DotRow!).ShouldBe("Onboarding steps");
            ControlAutomationPeer.CreatePeerForElement(block.DotRow!).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            AutomationProperties.GetName(block.DotRow!.Dots[1]).ShouldBe("Step 2: Bring the files you already use");
            AutomationProperties.GetItemStatus(block.DotRow.Dots[0]).ShouldBe("Current step");
            AutomationProperties.GetItemStatus(block.DotRow.Dots[1]).ShouldBe(string.Empty);
            AutomationProperties.GetName(block.BackButton).ShouldBe("Back");
            AutomationProperties.GetName(block.PrimaryButton!).ShouldBe("Next");
            // Back is unavailable but stays in the Tab order (Arc's aria-disabled).
            block.BackButton.Focusable.ShouldBeTrue();
            block.BackButton.IsEnabled.ShouldBeTrue();
            ControlAutomationPeer.CreatePeerForElement(block.BackButton).IsEnabled().ShouldBeFalse();
            block.Announcement.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Geometry_Matches_The_Reference()
    {
        var block = new OnboardingIllustration();
        var window = Show(block);
        try
        {
            var card = TestHelpers.Descendant<Border>(block, OnboardingIllustration.CardPart);
            card.Bounds.Width.ShouldBe(440, 0.5);
            card.CornerRadius.ShouldBe(new CornerRadius(20));
            card.BorderThickness.ShouldBe(new Thickness(1));
            TestHelpers.Descendant<Panel>(block, OnboardingIllustration.StagePart).Bounds.Height.ShouldBe(277, 0.5);
            block.Art!.Bounds.Width.ShouldBe(352, 0.5);
            block.Art.Bounds.Height.ShouldBe(220, 0.5);
            block.PrimaryButton!.Bounds.Width.ShouldBe(136, 0.5);
            block.PrimaryButton.Bounds.Height.ShouldBe(36, 0.5);
            block.BackButton!.Bounds.Height.ShouldBe(36, 0.5);
            block.SkipButton!.Bounds.Height.ShouldBe(28, 0.5);
            // Skip sits 10 from the stage's top right corner.
            var stage = TestHelpers.Descendant<Panel>(block, OnboardingIllustration.StagePart);
            var skipRight = block.SkipButton.TranslatePoint(new Point(block.SkipButton.Bounds.Width, 0), stage)!.Value;
            skipRight.X.ShouldBe(stage.Bounds.Width - 10, 0.5);
            skipRight.Y.ShouldBe(10, 0.5);
            // Dots: 32 high hit areas, 30 wide on the current step and 16 on the others.
            block.DotRow!.Dots.Select(d => Math.Round(d.Bounds.Width)).ShouldBe([30.0, 16.0, 16.0, 16.0]);
            block.DotRow.Dots.ShouldAllBe(d => Math.Abs(d.Bounds.Height - 32) < 0.5);
            block.CopySwap!.Bounds.Height.ShouldBeGreaterThanOrEqualTo(72);
            block.Classes.ShouldNotContain(OnboardingIllustration.CompactPseudoClass);

            // A host that leaves the width open still gets the full card.
            var open = new OnboardingIllustration { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var other = Show(open);
            try
            {
                TestHelpers.Descendant<Border>(open, OnboardingIllustration.CardPart).Bounds.Width.ShouldBe(440, 0.5);
            }
            finally
            {
                other.Close();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_And_Back_Step_Through_And_Report_The_Reason()
    {
        var block = new OnboardingIllustration();
        var changes = new List<OnboardingStepChangedEventArgs>();
        block.StepChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            Press(window, block.PrimaryButton!);
            block.CurrentIndex.ShouldBe(1);
            changes.Count.ShouldBe(1);
            changes[0].OldIndex.ShouldBe(0);
            changes[0].NewIndex.ShouldBe(1);
            changes[0].Reason.ShouldBe(OnboardingStepChangeReason.Next);
            changes[0].Source.ShouldBe(block);
            block.DisplayedCopy.Title.ShouldBe("Bring the files you already use");
            block.Art!.Scene.ShouldBeSameAs(OnboardingArtScenes.Files);
            block.BackButton!.IsInert.ShouldBeFalse();
            DotStates(block).ShouldBe([OnboardingDotState.Complete, OnboardingDotState.Current, OnboardingDotState.Upcoming, OnboardingDotState.Upcoming]);
            block.Announcement.ShouldBe("Bring the files you already use. Drag in slides, sheets, and PDFs. Northdesk sorts them by kind and keeps every earlier version. Step 2 of 4");
            // Focus stays on the main button.
            block.PrimaryButton!.IsFocused.ShouldBeTrue();

            Press(window, block.BackButton);
            block.CurrentIndex.ShouldBe(0);
            changes[^1].Reason.ShouldBe(OnboardingStepChangeReason.Back);
            block.BackButton.IsInert.ShouldBeTrue();
            // Back on the first step keeps focus (it is inert, not disabled) and does nothing.
            block.BackButton.IsFocused.ShouldBeTrue();
            Press(window, block.BackButton);
            block.CurrentIndex.ShouldBe(0);
            changes.Count.ShouldBe(2);

            block.Next().ShouldBeTrue();
            block.Next().ShouldBeTrue();
            block.Next().ShouldBeTrue();
            block.Next().ShouldBeFalse();
            block.CurrentIndex.ShouldBe(3);
            block.DisplayedPrimary.ShouldBe((OnboardingPrimaryKind.GetStarted, "Get started"));
            block.Classes.ShouldContain(OnboardingIllustration.LastPseudoClass);
            block.SkipButton!.IsVisible.ShouldBeFalse();
            block.Back().ShouldBeTrue();
            block.CurrentIndex.ShouldBe(2);
            block.SkipButton.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Get_Started_Raises_The_Event_Runs_The_Command_And_Completes()
    {
        var command = new RecordingCommand();
        var block = new OnboardingIllustration { CurrentIndex = 3, GetStartedCommand = command };
        var started = new List<RoutedEventArgs>();
        block.GetStarted += (_, e) => started.Add(e);
        var window = Show(block);
        try
        {
            Press(window, block.PrimaryButton!);
            started.Count.ShouldBe(1);
            command.Parameters.ShouldBe([started[0]]);
            block.IsCompleted.ShouldBeTrue();
            block.CurrentIndex.ShouldBe(3);
            block.Classes.ShouldContain(OnboardingIllustration.CompletedPseudoClass);
            block.DisplayedCopy.ShouldBe(("Your space is open", "Replay the tour to walk through each step again."));
            block.DisplayedPrimary.ShouldBe((OnboardingPrimaryKind.Replay, "Replay intro"));
            block.BackButton!.IsInert.ShouldBeTrue();
            block.SkipButton!.IsVisible.ShouldBeFalse();
            block.Art!.Scene.ShouldBeSameAs(OnboardingArtScenes.Badge);
            DotStates(block).ShouldBe([OnboardingDotState.Complete, OnboardingDotState.Complete, OnboardingDotState.Complete, OnboardingDotState.Current]);
            block.Announcement.ShouldBe("Your space is open. Replay the tour to walk through each step again. Onboarding complete");
            block.PrimaryButton!.IsFocused.ShouldBeTrue();

            // The arrows do nothing once complete; Back is inert.
            Key(window, PhysicalKey.ArrowLeft);
            block.CurrentIndex.ShouldBe(3);
            block.IsCompleted.ShouldBeTrue();
            block.Back().ShouldBeFalse();
            block.Next().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Completion_Get_Started_Only_Raises_The_Event()
    {
        var block = new OnboardingIllustration { CurrentIndex = 3, IsCompletionVisible = false };
        var started = 0;
        block.GetStarted += (_, _) => started++;
        var window = Show(block);
        try
        {
            Press(window, block.PrimaryButton!);
            Press(window, block.PrimaryButton!);
            started.ShouldBe(2);
            block.IsCompleted.ShouldBeFalse();
            block.DisplayedPrimary.Kind.ShouldBe(OnboardingPrimaryKind.GetStarted);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Goes_Back_To_The_First_Step_And_Keeps_Focus()
    {
        var block = new OnboardingIllustration();
        var changes = new List<OnboardingStepChangedEventArgs>();
        var replayed = 0;
        block.StepChanged += (_, e) => changes.Add(e);
        block.Replayed += (_, _) => replayed++;
        var window = Show(block);
        try
        {
            block.Complete().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            block.CurrentIndex.ShouldBe(3);
            block.IsCompleted.ShouldBeTrue();
            changes.Single().Reason.ShouldBe(OnboardingStepChangeReason.Programmatic);

            Press(window, block.PrimaryButton!);
            block.IsCompleted.ShouldBeFalse();
            block.CurrentIndex.ShouldBe(0);
            changes[^1].Reason.ShouldBe(OnboardingStepChangeReason.Replay);
            changes[^1].OldIndex.ShouldBe(3);
            replayed.ShouldBe(1);
            block.DisplayedPrimary.Kind.ShouldBe(OnboardingPrimaryKind.Next);
            block.PrimaryButton!.IsFocused.ShouldBeTrue();
            block.Announcement.ShouldBe("Set up your first space. Each project keeps one home. Begin with the spring launch notes and add files when you are ready. Step 1 of 4");
            block.Replay().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dots_Jump_To_Any_Step_And_Leave_The_Completed_State()
    {
        var block = new OnboardingIllustration();
        var changes = new List<OnboardingStepChangedEventArgs>();
        block.StepChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            Press(window, block.DotRow!.Dots[3]);
            block.CurrentIndex.ShouldBe(3);
            changes.Single().Reason.ShouldBe(OnboardingStepChangeReason.Dot);
            // Pressing the current dot does nothing.
            Press(window, block.DotRow.Dots[3]);
            changes.Count.ShouldBe(1);

            block.Complete();
            Dispatcher.UIThread.RunJobs();
            // The current dot of the completed state brings Get started back, on the same step.
            Press(window, block.DotRow.Dots[3]);
            block.IsCompleted.ShouldBeFalse();
            block.CurrentIndex.ShouldBe(3);
            block.DisplayedPrimary.Kind.ShouldBe(OnboardingPrimaryKind.GetStarted);

            block.Complete();
            Dispatcher.UIThread.RunJobs();
            Press(window, block.DotRow.Dots[1]);
            block.IsCompleted.ShouldBeFalse();
            block.CurrentIndex.ShouldBe(1);
            changes[^1].Reason.ShouldBe(OnboardingStepChangeReason.Dot);
            block.DotRow.Dots[1].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Skip_Jumps_To_The_Last_Step_And_Moves_Focus_To_The_Main_Button()
    {
        var command = new RecordingCommand();
        var block = new OnboardingIllustration { SkipCommand = command };
        var changes = new List<OnboardingStepChangedEventArgs>();
        var skipped = new List<RoutedEventArgs>();
        block.StepChanged += (_, e) => changes.Add(e);
        block.Skipped += (_, e) => skipped.Add(e);
        var window = Show(block);
        try
        {
            var skip = block.SkipButton!;
            Press(window, skip);
            block.CurrentIndex.ShouldBe(3);
            block.IsCompleted.ShouldBeFalse();
            changes.Single().Reason.ShouldBe(OnboardingStepChangeReason.Skip);
            skipped.Count.ShouldBe(1);
            command.Parameters.ShouldBe([skipped[0]]);
            skip.IsVisible.ShouldBeFalse();
            // Arc drops focus to the page here; the block keeps it on the main button, still showing the ring.
            block.PrimaryButton!.IsFocused.ShouldBeTrue();
            block.PrimaryButton.Classes.ShouldContain(":focus-visible");
            block.DisplayedPrimary.Kind.ShouldBe(OnboardingPrimaryKind.GetStarted);
            block.Skip().ShouldBeFalse();

            // With the pointer too (no ring then).
            block.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            skip.IsVisible.ShouldBeTrue();
            Click(window, skip);
            block.CurrentIndex.ShouldBe(3);
            block.PrimaryButton.IsFocused.ShouldBeTrue();
            block.PrimaryButton.Classes.ShouldNotContain(":focus-visible");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Skip_Can_Be_Hidden()
    {
        var block = new OnboardingIllustration { IsSkipVisible = false };
        var window = Show(block);
        try
        {
            block.SkipButton!.IsVisible.ShouldBeFalse();
            block.IsSkipVisible = true;
            Dispatcher.UIThread.RunJobs();
            block.SkipButton.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Step_From_Anywhere_In_The_Card_Without_Moving_Focus()
    {
        var block = new OnboardingIllustration();
        var changes = new List<OnboardingStepChangedEventArgs>();
        block.StepChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            var dot = block.DotRow!.Dots[0];
            dot.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowLeft);
            block.CurrentIndex.ShouldBe(0);
            changes.ShouldBeEmpty();
            Key(window, PhysicalKey.ArrowRight);
            block.CurrentIndex.ShouldBe(1);
            changes.Single().Reason.ShouldBe(OnboardingStepChangeReason.Keyboard);
            dot.IsFocused.ShouldBeTrue();

            block.SkipButton!.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            Key(window, PhysicalKey.ArrowRight);
            block.CurrentIndex.ShouldBe(3);
            // Skip went away while focused: focus moved to the main button.
            block.PrimaryButton!.IsFocused.ShouldBeTrue();
            // Right on the last step never presses Get started.
            Key(window, PhysicalKey.ArrowRight);
            block.CurrentIndex.ShouldBe(3);
            block.IsCompleted.ShouldBeFalse();
            Key(window, PhysicalKey.ArrowLeft);
            block.CurrentIndex.ShouldBe(2);
            block.PrimaryButton.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Completed_Property_Moves_To_The_Last_Step_And_A_New_Index_Leaves_It()
    {
        var block = new OnboardingIllustration { CurrentIndex = 1 };
        var window = Show(block);
        try
        {
            block.IsCompleted = true;
            Dispatcher.UIThread.RunJobs();
            block.CurrentIndex.ShouldBe(3);
            block.DisplayedCopy.Title.ShouldBe("Your space is open");
            block.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            block.IsCompleted.ShouldBeFalse();
            block.DisplayedCopy.Title.ShouldBe("Set up your first space");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Steps_Drive_The_Dots_Copy_And_Drawing()
    {
        var block = new OnboardingIllustration { CurrentIndex = 5, Label = "Atlas onboarding" };
        block.Steps.Add(new OnboardingStep(OnboardingScene.Team, "Invite your team", "Add the people you work with.", "Three people around a board"));
        block.Steps.Add(new OnboardingStep(OnboardingScene.Badge, "Ready", "Your team can join now.", "A badge with a check"));
        var window = Show(block);
        try
        {
            // An index past the steps is clamped.
            block.CurrentIndex.ShouldBe(1);
            block.DotRow!.Dots.Count.ShouldBe(2);
            block.Art!.Scene.ShouldBeSameAs(OnboardingArtScenes.Badge);
            AutomationProperties.GetName(block).ShouldBe("Atlas onboarding");
            AutomationProperties.GetName(block.Art).ShouldBe("A badge with a check");
            block.DisplayedPrimary.Kind.ShouldBe(OnboardingPrimaryKind.GetStarted);

            // Editing the step shown updates the copy and the dot's name.
            block.Steps[1].Title = "All ready";
            Dispatcher.UIThread.RunJobs();
            block.DisplayedCopy.Title.ShouldBe("All ready");
            AutomationProperties.GetName(block.DotRow.Dots[1]).ShouldBe("Step 2: All ready");

            // A source wins over Steps, and its changes show.
            var source = new ObservableCollection<OnboardingStep>
            {
                new(OnboardingScene.Folder, "One", "First.", "A box"),
                new(OnboardingScene.Files, "Two", "Second.", "Files"),
                new(OnboardingScene.Team, "Three", "Third.", "People")
            };
            block.StepsSource = source;
            Dispatcher.UIThread.RunJobs();
            block.DotRow.Dots.Count.ShouldBe(3);
            block.DisplayedCopy.Title.ShouldBe("Two");
            source.RemoveAt(2);
            source.RemoveAt(1);
            Dispatcher.UIThread.RunJobs();
            block.DotRow.Dots.Count.ShouldBe(1);
            block.CurrentIndex.ShouldBe(0);
            block.DisplayedCopy.Title.ShouldBe("One");
            // One step: Get started only, no Skip, Back inert.
            block.DisplayedPrimary.Kind.ShouldBe(OnboardingPrimaryKind.GetStarted);
            block.SkipButton!.IsVisible.ShouldBeFalse();
            block.BackButton!.IsInert.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Completed_Copy_Can_Be_Replaced()
    {
        var block = new OnboardingIllustration
        {
            CompletedTitle = "Welcome aboard",
            CompletedDescription = "Start the tour again any time.",
            Labels = new OnboardingIllustrationLabels
            {
                Next = "Continue",
                Back = "Previous",
                Skip = "Skip tour",
                GetStarted = "Open workspace",
                ReplayIntro = "Start over",
                StepsGroupName = "Tour steps",
                StepStatus = (step, count) => $"{step}/{count}",
                CompletedStatus = "Tour finished",
                DotName = (step, title) => $"{title} ({step})"
            }
        };
        var window = Show(block);
        try
        {
            block.DisplayedPrimary.Text.ShouldBe("Continue");
            AutomationProperties.GetName(block.BackButton!).ShouldBe("Previous");
            TestHelpers.Descendant<TextBlock>(block, OnboardingIllustration.BackTextPart).Text.ShouldBe("Previous");
            block.SkipButton!.Content.ShouldBe("Skip tour");
            AutomationProperties.GetName(block.DotRow!).ShouldBe("Tour steps");
            AutomationProperties.GetName(block.DotRow!.Dots[0]).ShouldBe("Set up your first space (1)");
            block.Next();
            Dispatcher.UIThread.RunJobs();
            block.Announcement!.ShouldEndWith("2/4");
            block.GoTo(3);
            Dispatcher.UIThread.RunJobs();
            block.DisplayedPrimary.Text.ShouldBe("Open workspace");
            block.Complete();
            Dispatcher.UIThread.RunJobs();
            block.DisplayedPrimary.Text.ShouldBe("Start over");
            block.Announcement.ShouldBe("Welcome aboard. Start the tour again any time. Tour finished");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Layout_Follows_The_Blocks_Own_Width()
    {
        var block = new OnboardingIllustration();
        var window = Show(block, width: 380);
        try
        {
            block.Classes.ShouldContain(OnboardingIllustration.CompactPseudoClass);
            block.PrimaryButton!.Bounds.Width.ShouldBe(128, 0.5);
            block.BackButton!.Bounds.Width.ShouldBe(36, 0.5);
            TestHelpers.Descendant<TextBlock>(block, OnboardingIllustration.BackTextPart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(block.BackButton).ShouldBe("Back");
            block.CopySwap!.MinHeight.ShouldBe(88);
            ((OnboardingCopy)block.CopySwap.CurrentContent!).IsCompact.ShouldBeTrue();
            TestHelpers.Descendant<Border>(block, "PART_StageFrame").Padding.ShouldBe(new Thickness(12, 42, 12, 12));

            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            block.Classes.ShouldNotContain(OnboardingIllustration.CompactPseudoClass);
            ((OnboardingCopy)block.CopySwap.CurrentContent!).IsCompact.ShouldBeFalse();
            block.PrimaryButton.Bounds.Width.ShouldBe(136, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unframed_Card_Has_No_Border_Radius_Or_Shadow()
    {
        var block = new OnboardingIllustration { IsFramed = false };
        var window = Show(block, width: 360);
        try
        {
            block.Classes.ShouldContain(OnboardingIllustration.UnframedPseudoClass);
            var card = TestHelpers.Descendant<Border>(block, OnboardingIllustration.CardPart);
            card.BorderThickness.ShouldBe(default);
            card.CornerRadius.ShouldBe(default);
            card.BoxShadow.Count.ShouldBe(0);
            card.Bounds.Width.ShouldBe(360, 0.5);
            // The drawing shrinks with the card: 360 less 12 on each side.
            block.Art!.Bounds.Width.ShouldBe(336, 0.5);
            block.Art.Bounds.Height.ShouldBe(210, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Swaps_The_Copy_And_Calls_A_Leaving_Step_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration();
        var window = Show(block, level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.3));
            var first = block.CopySwap!.CurrentContent!;
            block.Next();
            Dispatcher.UIThread.RunJobs();
            block.CopySwap.ExitingCount.ShouldBe(1);
            var second = block.CopySwap.CurrentContent!;
            second.ShouldNotBeSameAs(first);
            // The new copy waits 80ms, then rises out of a blur.
            clock.Advance(TimeSpan.FromMilliseconds(50));
            second.Opacity.ShouldBe(0, 0.001);
            first.Opacity.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            second.Opacity.ShouldBeGreaterThan(0);

            // Back before the first copy has gone: it comes back from where it is.
            block.Back();
            Dispatcher.UIThread.RunJobs();
            block.CopySwap.CurrentContent.ShouldBeSameAs(first);
            block.CopySwap.ExitingCount.ShouldBe(1);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ShouldDrain(() => block.CopySwap.ExitingCount);
            first.Opacity.ShouldBe(1, 0.001);
            first.Effect.ShouldBeNull();
            ((TranslateTransform)first.RenderTransform!).Y.ShouldBe(0, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Dot_Width_And_Fades_Skip()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration();
        var window = Show(block, level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.3));
            block.Next();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var dots = block.DotRow!.Dots;
            dots[0].DisplayedMarkWidth.ShouldBeInRange(7, 19);
            dots[1].DisplayedMarkWidth.ShouldBeInRange(7, 19);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            dots[0].DisplayedMarkWidth.ShouldBe(6, 0.01);
            dots[1].DisplayedMarkWidth.ShouldBe(20, 0.01);

            block.Skip();
            Dispatcher.UIThread.RunJobs();
            var skip = block.SkipButton!;
            skip.IsVisible.ShouldBeTrue();
            skip.IsHitTestVisible.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(80));
            skip.Opacity.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            skip.IsVisible.ShouldBeFalse();

            block.Back();
            Dispatcher.UIThread.RunJobs();
            skip.IsVisible.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(300));
            skip.Opacity.ShouldBe(1, 0.001);
            skip.IsHitTestVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_The_Copy_Without_Travel_Or_Blur()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration();
        var window = Show(block, level: ArcMotionLevel.Reduced);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.3));
            block.Next();
            Dispatcher.UIThread.RunJobs();
            var incoming = block.CopySwap!.CurrentContent!;
            clock.Advance(TimeSpan.FromMilliseconds(50));
            // No 80ms wait, no rise, no blur.
            incoming.Opacity.ShouldBeGreaterThan(0);
            incoming.Effect.ShouldBeNull();
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.001);
            // The dots jump.
            block.DotRow!.Dots[1].DisplayedMarkWidth.ShouldBe(20, 0.01);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ShouldDrain(() => block.CopySwap.ExitingCount);
        }
        finally
        {
            window.Close();
        }
    }
}

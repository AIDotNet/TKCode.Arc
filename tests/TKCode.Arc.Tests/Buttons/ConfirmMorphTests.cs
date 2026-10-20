using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Buttons;

public class ConfirmMorphTests
{
    private static (Window Window, ConfirmMorph Morph) Show(ConfirmMorph morph, Control? sibling = null)
    {
        var panel = new StackPanel { Spacing = 20, Children = { morph } };
        if (sibling is not null)
        {
            panel.Children.Add(sibling);
        }
        return (TestHelpers.Show(panel), morph);
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static Panel Face(ConfirmMorph morph, string name) => Part<Panel>(morph, name);

    [AvaloniaFact]
    public void Rests_As_A_36px_Pill_With_The_Label()
    {
        var (window, morph) = Show(new ConfirmMorph { Icon = ArcIcons.Trash2 });
        try
        {
            morph.State.ShouldBe(ConfirmMorphState.Idle);
            Part<Border>(morph, "PART_Pill").Bounds.Height.ShouldBe(36);
            Face(morph, "PART_IdleFace").IsVisible.ShouldBeTrue();
            Face(morph, "PART_ConfirmFace").IsVisible.ShouldBeFalse();
            morph.ShownPrompt.ShouldBe("Delete project?");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_Flow_Asks_Works_Reports_And_Undoes()
    {
        var gate = new TaskCompletionSource();
        var undone = 0;
        var states = new List<ConfirmMorphState>();
        var morph = new ConfirmMorph
        {
            ConfirmAction = () => gate.Task,
            UndoAction = () =>
            {
                undone++;
                return Task.CompletedTask;
            }
        };
        morph.StateChanged += (_, e) => states.Add(e.NewState);
        var (window, _) = Show(morph);
        try
        {
            var trigger = Part<AvaloniaButton>(morph, "PART_Trigger");
            trigger.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            morph.State.ShouldBe(ConfirmMorphState.Confirming);
            Face(morph, "PART_ConfirmFace").IsVisible.ShouldBeTrue();
            // Focus moves to Confirm.
            Part<AvaloniaButton>(morph, "PART_Confirm").IsFocused.ShouldBeTrue();
            // The tail of the press that opened the question is not an answer.
            Press(window, PhysicalKey.Enter);
            morph.State.ShouldBe(ConfirmMorphState.Confirming);
            Pump((int)ConfirmMorph.ArmDelay.TotalMilliseconds + 40);
            Press(window, PhysicalKey.Enter);
            morph.State.ShouldBe(ConfirmMorphState.Pending);
            morph.WorkingLabel.ShouldBe("Deleting");
            morph.IsFocused.ShouldBeTrue();
            gate.SetResult();
            WaitFor(() => morph.State == ConfirmMorphState.Done).ShouldBeTrue();
            Part<AvaloniaButton>(morph, "PART_Undo").IsVisible.ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            Part<AvaloniaButton>(morph, "PART_Undo").IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            WaitFor(() => morph.State == ConfirmMorphState.Idle).ShouldBeTrue();
            undone.ShouldBe(1);
            states.ShouldBe([ConfirmMorphState.Confirming, ConfirmMorphState.Pending, ConfirmMorphState.Done, ConfirmMorphState.Idle]);
            trigger.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_And_Outside_Press_Cancel_The_Question()
    {
        var cancelled = 0;
        var other = new AvaloniaButton { Width = 80, Height = 30, Theme = null };
        var morph = new ConfirmMorph();
        morph.Cancelled += (_, _) => cancelled++;
        var (window, _) = Show(morph, other);
        try
        {
            morph.Ask();
            Dispatcher.UIThread.RunJobs();
            morph.State.ShouldBe(ConfirmMorphState.Confirming);
            Press(window, PhysicalKey.Escape);
            morph.State.ShouldBe(ConfirmMorphState.Idle);

            morph.Ask();
            Dispatcher.UIThread.RunJobs();
            var outside = other.TranslatePoint(new Point(5, 5), window)!.Value;
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            morph.State.ShouldBe(ConfirmMorphState.Idle);
            cancelled.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unanswered_Question_Times_Out()
    {
        var morph = new ConfirmMorph { ConfirmTimeout = TimeSpan.FromMilliseconds(60) };
        var (window, _) = Show(morph);
        try
        {
            morph.Ask();
            WaitFor(() => morph.State == ConfirmMorphState.Idle).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Can_Be_Stopped_Or_Runs_The_Action()
    {
        var runs = 0;
        var morph = new ConfirmMorph
        {
            ConfirmMode = ConfirmMorphMode.Countdown,
            CountdownSeconds = 1,
            ConfirmAction = () =>
            {
                runs++;
                return Task.CompletedTask;
            }
        };
        var (window, _) = Show(morph);
        try
        {
            morph.Ask();
            Pump((int)ConfirmMorph.ArmDelay.TotalMilliseconds + 40);
            Part<AvaloniaButton>(morph, "PART_Confirm").Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            morph.Classes.ShouldContain(":counting");
            Face(morph, "PART_CountFace").IsVisible.ShouldBeTrue();
            morph.SecondsLeft.ShouldBe(1);
            Press(window, PhysicalKey.Escape);
            morph.State.ShouldBe(ConfirmMorphState.Idle);
            runs.ShouldBe(0);

            morph.Ask();
            Pump((int)ConfirmMorph.ArmDelay.TotalMilliseconds + 40);
            Part<AvaloniaButton>(morph, "PART_Confirm").Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            WaitFor(() => morph.State == ConfirmMorphState.Done, 4000).ShouldBeTrue();
            runs.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Failure_Offers_Retry()
    {
        var attempts = 0;
        var morph = new ConfirmMorph
        {
            ConfirmAction = async () =>
            {
                await Task.Yield();
                if (++attempts == 1)
                {
                    throw new InvalidOperationException("offline");
                }
            }
        };
        var (window, _) = Show(morph);
        try
        {
            morph.Ask();
            Pump((int)ConfirmMorph.ArmDelay.TotalMilliseconds + 40);
            Part<AvaloniaButton>(morph, "PART_Confirm").Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            WaitFor(() => morph.State == ConfirmMorphState.Error).ShouldBeTrue();
            Face(morph, "PART_ErrorFace").IsVisible.ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            var retry = Part<AvaloniaButton>(morph, "PART_Retry");
            retry.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            WaitFor(() => morph.State == ConfirmMorphState.Done).ShouldBeTrue();
            attempts.ShouldBe(2);
            // Without UndoAction the result has no Undo button.
            Part<AvaloniaButton>(morph, "PART_Undo").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hold_Mode_Confirms_After_Holding_Space()
    {
        var runs = 0;
        var morph = new ConfirmMorph
        {
            ConfirmMode = ConfirmMorphMode.Hold,
            HoldDuration = TimeSpan.FromMilliseconds(80),
            ConfirmAction = () =>
            {
                runs++;
                return Task.CompletedTask;
            }
        };
        var (window, _) = Show(morph);
        try
        {
            morph.Ask();
            Dispatcher.UIThread.RunJobs();
            var confirm = Part<AvaloniaButton>(morph, "PART_Confirm");
            Avalonia.Automation.AutomationProperties.GetHelpText(confirm).ShouldBe("Press and hold");
            confirm.Focus(NavigationMethod.Tab);
            // A quick press is not enough.
            Press(window, PhysicalKey.Space);
            morph.State.ShouldBe(ConfirmMorphState.Confirming);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            WaitFor(() => morph.State == ConfirmMorphState.Done).ShouldBeTrue();
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            runs.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Controlled_State_Shows_The_Face_Without_Running_Actions()
    {
        var runs = 0;
        var morph = new ConfirmMorph
        {
            ConfirmAction = () =>
            {
                runs++;
                return Task.CompletedTask;
            }
        };
        var (window, _) = Show(morph);
        try
        {
            morph.State = ConfirmMorphState.Done;
            Dispatcher.UIThread.RunJobs();
            Face(morph, "PART_DoneFace").IsVisible.ShouldBeTrue();
            Face(morph, "PART_IdleFace").IsVisible.ShouldBeFalse();
            morph.State = ConfirmMorphState.Pending;
            Dispatcher.UIThread.RunJobs();
            Face(morph, "PART_PendingFace").IsVisible.ShouldBeTrue();
            runs.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}

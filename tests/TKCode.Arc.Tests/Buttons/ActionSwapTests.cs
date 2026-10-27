using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

public class ActionSwapTests
{
    private static ActionSwap Create(Action<ActionSwap>? configure = null)
    {
        var swap = new ActionSwap();
        swap.States.Add(new ActionSwapState { Id = "idle", Label = "Send invite", Icon = ArcIcons.Send });
        swap.States.Add(new ActionSwapState
        {
            Id = "sent", Label = "Sent", Icon = ArcIcons.Check, Tone = ActionSwapTone.Success, PendingLabel = "Sending"
        });
        configure?.Invoke(swap);
        return swap;
    }

    private static ActionSwapSurface Surface(ActionSwap swap) => Part<ActionSwapSurface>(swap, ActionSwap.SurfacePart);

    private static void Press(Window window, ActionSwap swap)
    {
        swap.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var swap = Create();
        var window = TestHelpers.Show(swap);
        try
        {
            swap.Value.ShouldBeNull();
            swap.CurrentState!.Id.ShouldBe("idle");
            swap.NextState!.Id.ShouldBe("sent");
            swap.FailureLabel.ShouldBe("Try again");
            swap.Size.ShouldBe(ControlSize.Medium);
            swap.Phase.ShouldBe(ActionSwapPhase.Idle);
            swap.Bounds.Height.ShouldBe(44, 0.5);
            swap.Padding.Left.ShouldBe(20);
            swap.Classes.ShouldContain(":neutral");
            TestHelpers.ColorOf(swap.Background).ShouldBe(ArcTokens.GetColor(swap, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(swap.Foreground).ShouldBe(ArcTokens.GetColor(swap, ArcDesignTokenKind.Background));
            Surface(swap).Text.ShouldBe("Send invite");
            Surface(swap).IconSlotWidth.ShouldBe(16, 0.01);
            Peer(swap).GetName().ShouldBe("Send invite");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Follow_The_Arc_Control_Heights()
    {
        foreach (var (size, height, pad) in new[] { (ControlSize.Small, 36.0, 16.0), (ControlSize.Medium, 44.0, 20.0), (ControlSize.Large, 50.0, 24.0) })
        {
            var swap = Create(s => s.Size = size);
            var window = TestHelpers.Show(swap);
            try
            {
                swap.Bounds.Height.ShouldBe(height, 0.5);
                swap.Padding.Left.ShouldBe(pad);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void A_Press_Moves_To_The_Next_State_And_Wraps_Around()
    {
        ActionSwapValueChangedEventArgs? last = null;
        var count = 0;
        var swap = Create(s => s.ValueChanged += (_, e) =>
        {
            last = e;
            count++;
        });
        var window = TestHelpers.Show(swap);
        try
        {
            Press(window, swap);
            swap.Value.ShouldBe("sent");
            swap.Classes.ShouldContain(":success");
            swap.Classes.ShouldNotContain(":neutral");
            last!.Value.ShouldBe("sent");
            last.State.Label.ShouldBe("Sent");
            Surface(swap).Text.ShouldBe("Sent");
            Peer(swap).GetName().ShouldBe("Sent");

            Press(window, swap);
            swap.Value.ShouldBe("idle");
            swap.Classes.ShouldContain(":neutral");
            Surface(swap).Text.ShouldBe("Send invite");
            count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Value_Can_Be_Set_From_Outside_Without_Running_The_Action()
    {
        var runs = 0;
        var swap = Create(s => s.AsyncAction = (_, _) =>
        {
            runs++;
            return null;
        });
        var window = TestHelpers.Show(swap);
        try
        {
            swap.Value = "sent";
            Dispatcher.UIThread.RunJobs();
            Surface(swap).Text.ShouldBe("Sent");
            swap.Classes.ShouldContain(":success");
            runs.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fewer_Than_Two_States_Is_Inert()
    {
        var swap = new ActionSwap();
        swap.States.Add(new ActionSwapState { Id = "only", Label = "Only" });
        var window = TestHelpers.Show(swap);
        try
        {
            swap.Classes.ShouldContain(":inactive");
            Press(window, swap);
            swap.Value.ShouldBeNull();
            Surface(swap).Text.ShouldBe("Only");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Action_Task_Shows_The_Working_Phase_Then_Commits()
    {
        var gate = new TaskCompletionSource();
        var runs = 0;
        ActionSwapState? seenNext = null, seenCurrent = null;
        var swap = Create(s => s.AsyncAction = (next, current) =>
        {
            runs++;
            seenNext = next;
            seenCurrent = current;
            return gate.Task;
        });
        var window = TestHelpers.Show(swap);
        try
        {
            Press(window, swap);
            seenNext!.Id.ShouldBe("sent");
            seenCurrent!.Id.ShouldBe("idle");
            swap.Phase.ShouldBe(ActionSwapPhase.Working);
            swap.Classes.ShouldContain(":working");
            swap.Classes.ShouldNotContain(":folded");
            Surface(swap).Text.ShouldBe("Sending");
            Surface(swap).IconKey.ShouldBe("working");
            // The state has not moved yet; working swallows presses but keeps focus.
            swap.Value.ShouldBeNull();
            Press(window, swap);
            runs.ShouldBe(1);
            swap.IsFocused.ShouldBeTrue();
            Peer(swap).IsEnabled().ShouldBeFalse();

            gate.SetResult();
            WaitFor(() => swap.Value == "sent").ShouldBeTrue();
            swap.Phase.ShouldBe(ActionSwapPhase.Idle);
            Surface(swap).Text.ShouldBe("Sent");
            Surface(swap).IconKey.ShouldBe("state:sent");
            Peer(swap).IsEnabled().ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Fast_Task_Still_Shows_The_Working_Shape_For_At_Least_450ms()
    {
        var swap = Create(s => s.AsyncAction = (_, _) => Task.CompletedTask);
        var window = TestHelpers.Show(swap);
        try
        {
            var started = DateTime.UtcNow;
            Press(window, swap);
            swap.Phase.ShouldBe(ActionSwapPhase.Working);
            WaitFor(() => swap.Value == "sent").ShouldBeTrue();
            (DateTime.UtcNow - started).TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Pending_Label_The_Pill_Folds_Into_A_Spinner()
    {
        var gate = new TaskCompletionSource();
        var swap = new ActionSwap { AsyncAction = (_, _) => gate.Task };
        swap.States.Add(new ActionSwapState { Id = "add", Label = "Add to library", Icon = ArcIcons.Plus });
        swap.States.Add(new ActionSwapState { Id = "added", Label = "Added", Icon = ArcIcons.Check });
        var window = TestHelpers.Show(swap);
        try
        {
            var restWidth = swap.Bounds.Width;
            Press(window, swap);
            Dispatcher.UIThread.RunJobs();
            swap.Classes.ShouldContain(":working");
            swap.Classes.ShouldContain(":folded");
            var surface = Surface(swap);
            surface.Text.ShouldBe(string.Empty);
            surface.FoldValue.ShouldBe(1, 0.001);
            surface.TextWidth.ShouldBe(0, 0.001);
            // A circle of the control height: the padding eased down and the icon slot is all that is left.
            swap.Bounds.Width.ShouldBe(44, 1);
            swap.Bounds.Width.ShouldBeLessThan(restWidth);

            gate.SetResult();
            WaitFor(() => swap.Value == "added").ShouldBeTrue();
            surface.FoldValue.ShouldBe(0, 0.001);
            surface.Text.ShouldBe("Added");
            swap.Bounds.Width.ShouldBeGreaterThan(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Rejected_Action_Shows_The_Failure_Then_Returns_To_The_Current_State()
    {
        var swap = Create(s =>
        {
            s.FailureLabel = "Couldn't send";
            s.AsyncAction = (_, _) => Task.FromException(new InvalidOperationException("down"));
        });
        var changed = 0;
        swap.ValueChanged += (_, _) => changed++;
        var window = TestHelpers.Show(swap);
        try
        {
            Press(window, swap);
            WaitFor(() => swap.Phase == ActionSwapPhase.Failed).ShouldBeTrue();
            swap.Classes.ShouldContain(":failed");
            swap.Classes.ShouldContain(":danger");
            swap.Classes.ShouldNotContain(":neutral");
            Surface(swap).Text.ShouldBe("Couldn't send");
            Surface(swap).IconKey.ShouldBe("failed");
            swap.Value.ShouldBeNull();
            changed.ShouldBe(0);

            WaitFor(() => swap.Phase == ActionSwapPhase.Idle, 5000).ShouldBeTrue();
            Surface(swap).Text.ShouldBe("Send invite");
            swap.Classes.ShouldContain(":neutral");
            swap.Classes.ShouldNotContain(":danger");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Action_That_Throws_Synchronously_Fails_Too()
    {
        var swap = Create(s => s.AsyncAction = (_, _) => throw new InvalidOperationException("nope"));
        var window = TestHelpers.Show(swap);
        try
        {
            Press(window, swap);
            swap.Phase.ShouldBe(ActionSwapPhase.Failed);
            swap.Value.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HoldFor_Moves_On_By_Itself()
    {
        var swap = new ActionSwap();
        swap.States.Add(new ActionSwapState { Id = "idle", Label = "Send invite" });
        swap.States.Add(new ActionSwapState { Id = "sent", Label = "Sent", Tone = ActionSwapTone.Success, HoldFor = TimeSpan.FromMilliseconds(250) });
        var window = TestHelpers.Show(swap);
        try
        {
            Press(window, swap);
            swap.Value.ShouldBe("sent");
            WaitFor(() => swap.Value == "idle", 5000).ShouldBeTrue();
            Surface(swap).Text.ShouldBe("Send invite");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tone_Pseudo_Classes_And_Brushes_Follow_The_State()
    {
        var swap = new ActionSwap();
        foreach (var tone in new[] { ActionSwapTone.Neutral, ActionSwapTone.Accent, ActionSwapTone.Success, ActionSwapTone.Warning, ActionSwapTone.Quiet })
        {
            swap.States.Add(new ActionSwapState { Id = tone.ToString(), Label = tone.ToString(), Tone = tone });
        }
        var window = TestHelpers.Show(swap);
        try
        {
            swap.Classes.ShouldContain(":neutral");
            Press(window, swap);
            swap.Classes.ShouldContain(":accent");
            TestHelpers.ColorOf(swap.Background).ShouldBe(ArcTokens.GetColor(swap, ArcDesignTokenKind.Accent));
            Press(window, swap);
            swap.Classes.ShouldContain(":success");
            Press(window, swap);
            swap.Classes.ShouldContain(":warning");
            Press(window, swap);
            swap.Classes.ShouldContain(":quiet");
            TestHelpers.ColorOf(swap.Background).ShouldBe(ArcTokens.GetColor(swap, ArcDesignTokenKind.Surface));
            swap.Classes.ShouldNotContain(":warning");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Icon_Slot_Closes_When_A_State_Has_No_Icon()
    {
        var swap = new ActionSwap();
        swap.States.Add(new ActionSwapState { Id = "follow", Label = "Follow", Tone = ActionSwapTone.Accent });
        swap.States.Add(new ActionSwapState { Id = "following", Label = "Following", Icon = ArcIcons.Check, Tone = ActionSwapTone.Quiet });
        var window = TestHelpers.Show(swap);
        try
        {
            var surface = Surface(swap);
            surface.IconSlotWidth.ShouldBe(0, 0.001);
            surface.IconKey.ShouldBeNull();
            Press(window, swap);
            surface.IconSlotWidth.ShouldBe(16, 0.001);
            surface.IconKey.ShouldBe("state:following");
            Press(window, swap);
            surface.IconSlotWidth.ShouldBe(0, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Does_Not_Advance()
    {
        var swap = Create(s => s.IsEnabled = false);
        var window = TestHelpers.Show(swap);
        try
        {
            swap.Classes.ShouldContain(":inactive");
            swap.Opacity.ShouldBe(0.6, 0.01);
            Press(window, swap);
            swap.Value.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Keeps_Leaving_Faces_Until_They_Have_Faded()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var swap = Create();
            var window = TestHelpers.Show(swap, ArcMotionLevel.Full);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var surface = Surface(swap);
                var restWidth = swap.Bounds.Width;
                Press(window, swap);
                clock.Advance(TimeSpan.FromMilliseconds(40));
                Dispatcher.UIThread.RunJobs();
                // Mid-flight: the old icon is still leaving next to the new one, and some old letters are leaving.
                surface.IconFaceCount.ShouldBe(2);
                surface.LeavingGlyphCount.ShouldBeGreaterThan(0);
                for (var t = 0; t < 1500; t += 50)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(50));
                    Dispatcher.UIThread.RunJobs();
                }
                surface.IconFaceCount.ShouldBe(1);
                surface.LeavingGlyphCount.ShouldBe(0);
                surface.Text.ShouldBe("Sent");
                swap.Bounds.Width.ShouldBeLessThan(restWidth);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void The_Spinner_Turns_Only_At_Full_Motion_And_Stops_When_The_Action_Ends()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var gate = new TaskCompletionSource();
            var swap = Create(s => s.AsyncAction = (_, _) => gate.Task);
            var window = TestHelpers.Show(swap, ArcMotionLevel.Full);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var surface = Surface(swap);
                surface.IsSpinning.ShouldBeFalse();
                Press(window, swap);
                clock.Advance(TimeSpan.FromMilliseconds(50));
                Dispatcher.UIThread.RunJobs();
                surface.IsSpinning.ShouldBeTrue();
                gate.SetResult();
                WaitFor(() => swap.Value == "sent").ShouldBeTrue();
                for (var t = 0; t < 600; t += 50)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(50));
                    Dispatcher.UIThread.RunJobs();
                }
                surface.IsSpinning.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Only_Fades()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var swap = Create();
            var window = TestHelpers.Show(swap, ArcMotionLevel.Reduced);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var surface = Surface(swap);
                Press(window, swap);
                // Width jumps straight to the new label; letters and icon fade for a short time.
                surface.IconFaceCount.ShouldBe(2);
                for (var t = 0; t < 400; t += 50)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(50));
                    Dispatcher.UIThread.RunJobs();
                }
                surface.IconFaceCount.ShouldBe(1);
                surface.LeavingGlyphCount.ShouldBe(0);
            }
            finally
            {
                window.Close();
            }
        }
    }
}

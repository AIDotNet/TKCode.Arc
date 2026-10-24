using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The agent status pill: states, timer, actions, the panel, focus, keyboard, announcements and motion levels.</summary>
public class AgentStatusTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private sealed class Fixture : IDisposable
    {
        public AgentStatusTestClock Time = new();
        public ManualFrameClock Frames = new();
        public AgentStatus Status = null!;
        public AgentStatusFace Face => Status.Face;
        public Window Window = null!;
        private readonly IDisposable _install;

        public Fixture() => _install = Frames.Install();

        public void Open(AgentStatus status, ArcMotionLevel level = ArcMotionLevel.Full, double width = 700)
        {
            Status = status;
            status.TimeProvider = Time;
            Window = TestHelpers.Show(status, level);
            Window.Width = width;
            Window.Height = 600;
            Pump(0.1);
        }

        public void Pump(double seconds)
        {
            Frames.Advance(TimeSpan.FromSeconds(seconds), Frame);
            Dispatcher.UIThread.RunJobs();
        }

        public void AdvanceTime(double ms)
        {
            Time.Advance(ms);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose()
        {
            Window?.Close();
            _install.Dispose();
        }
    }

    private static AgentStatus Pill(AgentStatusRunState state, string? label = null, DateTimeOffset? started = null,
        AgentStatusActions actions = AgentStatusActions.All)
    {
        var status = new AgentStatus
        {
            Status = state,
            Agent = new AgentModel("Atlas Scout 2"),
            Activity = label is null ? null : new AgentStatusActivity(label, ArcIcons.Search),
            Actions = actions
        };
        if (started is { } s)
        {
            status.StartedAt = s;
        }
        return status;
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void With_No_Data_It_Plays_The_First_Sample_Run()
    {
        using var f = new Fixture();
        f.Open(new AgentStatus());
        f.Status.Simulation.ShouldNotBeNull();
        f.Status.Simulation!.Scenario.ShouldBe(AgentStatusSamples.Coding);
        f.Status.DisplayState.ShouldBe(AgentStatusRunState.Idle);
        f.Face.LabelText.ShouldBe("Ready");
        f.AdvanceTime(900);
        f.Status.DisplayState.ShouldBe(AgentStatusRunState.Working);
        f.Face.LabelText.ShouldBe("Scanning 9 modules");
        f.Face.LabelTone.ShouldBe(AgentStatusLabelTone.Live);
        f.Status.Classes.Contains(":working").ShouldBeTrue();
        f.AdvanceTime(11182);
        f.Status.DisplayState.ShouldBe(AgentStatusRunState.NeedsInput);
        f.Face.LabelText.ShouldBe("Waiting on you to deploy to staging");
        f.Status.InlineApproveButton.IsVisible.ShouldBeTrue();
        f.Status.InlineDenyButton.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void With_Host_Data_It_Does_Not_Start_A_Simulation_Or_Change_Its_Own_Status()
    {
        using var f = new Fixture();
        f.Open(Pill(AgentStatusRunState.NeedsInput, "Needs your approval"));
        f.Status.Simulation.ShouldBeNull();
        f.AdvanceTime(60_000);
        f.Status.DisplayState.ShouldBe(AgentStatusRunState.NeedsInput);
        var raised = new List<AgentStatusActions>();
        f.Status.ApproveRequested += (_, e) => raised.Add(e.Action);
        f.Status.Invoke(AgentStatusActions.Approve);
        raised.ShouldBe([AgentStatusActions.Approve]);
        f.Status.DisplayState.ShouldBe(AgentStatusRunState.NeedsInput);
    }

    [AvaloniaFact]
    public void Each_State_Has_Its_Words_Glyph_And_Pseudo_Class()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Idle);
        f.Open(status, ArcMotionLevel.Off);
        f.Face.LabelText.ShouldBe("Ready");
        f.Face.LabelTone.ShouldBe(AgentStatusLabelTone.Quiet);
        f.Face.Glyph.ShouldBe(AgentStatusGlyph.Mark);
        status.Activity = new AgentStatusActivity("Reading 12 files", ArcIcons.Search);
        status.Status = AgentStatusRunState.Working;
        f.Face.LabelText.ShouldBe("Reading 12 files");
        f.Face.TrackOpacity.ShouldBe(1);
        status.Status = AgentStatusRunState.NeedsInput;
        f.Face.DotOpacity.ShouldBe(1);
        f.Face.ArcOpacity.ShouldBe(0);
        status.Status = AgentStatusRunState.Paused;
        f.Face.LabelText.ShouldBe("Paused");
        f.Face.GlyphOpacity(AgentStatusGlyph.Mark).ShouldBe(0.5, 0.001);
        status.Activity = new AgentStatusActivity("5 files touched");
        status.Status = AgentStatusRunState.Done;
        f.Face.LabelText.ShouldStartWith("Done in ");
        f.Face.LabelText.ShouldEndWith(" · 5 files touched");
        f.Face.Glyph.ShouldBe(AgentStatusGlyph.Check);
        f.Face.ClosedProgress.ShouldBe(1);
        status.Activity = new AgentStatusActivity("Fetch failed");
        status.Status = AgentStatusRunState.Failed;
        f.Face.LabelText.ShouldBe("Fetch failed");
        f.Face.Glyph.ShouldBe(AgentStatusGlyph.Cross);
        status.Status = AgentStatusRunState.Stopped;
        f.Face.LabelText.ShouldStartWith("Stopped after ");
        f.Face.Glyph.ShouldBe(AgentStatusGlyph.Stop);
        f.Face.ClosedOpacity.ShouldBe(0);
        status.Classes.Contains(":stopped").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Count_Shows_In_The_Label_And_Ticks_Without_Rolling()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working);
        status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, 8, 12);
        f.Open(status);
        f.Face.LabelText.ShouldBe("Checking specs 8 of 12");
        status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, 9, 12);
        f.Face.LabelText.ShouldBe("Checking specs 9 of 12");
        f.Face.LeavingCount.ShouldBe(0);
        status.Activity = new AgentStatusActivity("Opening pages", ArcIcons.Search);
        f.Face.LeavingCount.ShouldBe(1);
        f.Pump(0.3);
        f.Face.LeavingCount.ShouldBe(0);
        f.Face.LabelText.ShouldBe("Opening pages");
    }

    [AvaloniaFact]
    public void The_Timer_Counts_Active_Time_And_Leaves_Out_Paused_And_Failed_Time()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading", started: f.Time.GetUtcNow());
        f.Open(status);
        f.AdvanceTime(10_000);
        status.Tick();
        f.Face.TimerText.ShouldBe("0:10");
        f.Face.IsTimerVisible.ShouldBeTrue();
        status.Status = AgentStatusRunState.Paused;
        f.AdvanceTime(30_000);
        status.Tick();
        f.Face.TimerText.ShouldBe("0:10");
        status.Status = AgentStatusRunState.Working;
        f.AdvanceTime(5_000);
        status.Tick();
        f.Face.TimerText.ShouldBe("0:15");
        status.Status = AgentStatusRunState.NeedsInput;
        f.AdvanceTime(60_000);
        status.Tick();
        f.Face.TimerText.ShouldBe("1:15");
        status.Status = AgentStatusRunState.Failed;
        f.Face.IsTimerVisible.ShouldBeFalse();
        f.AdvanceTime(120_000);
        status.Status = AgentStatusRunState.Working;
        f.AdvanceTime(2_000);
        status.Tick();
        f.Face.TimerText.ShouldBe("1:17");
    }

    [AvaloniaFact]
    public void The_Timer_Ticks_Four_Times_A_Second_Only_While_Live()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Idle);
        f.Open(status);
        f.Time.ActiveTimers.ShouldBe(0);
        status.Status = AgentStatusRunState.Working;
        f.Time.ActiveTimers.ShouldBe(1);
        status.Status = AgentStatusRunState.Paused;
        f.Time.ActiveTimers.ShouldBe(0);
        status.Status = AgentStatusRunState.NeedsInput;
        f.Time.ActiveTimers.ShouldBe(1);
        status.Status = AgentStatusRunState.Done;
        // Done: no tick, one settle timer.
        f.Time.ActiveTimers.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Done_Writes_The_Total_And_Settles_Back_To_Ready_After_Four_Seconds()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading", started: f.Time.GetUtcNow());
        f.Open(status);
        f.AdvanceTime(143_400);
        status.Activity = new AgentStatusActivity("5 files touched");
        status.Status = AgentStatusRunState.Done;
        f.Face.LabelText.ShouldBe("Done in 2m 23s · 5 files touched");
        var settled = 0;
        status.Settled += (_, _) => settled++;
        f.AdvanceTime(3_999);
        f.Face.LabelText!.ShouldStartWith("Done in");
        f.AdvanceTime(1);
        f.Face.LabelText.ShouldBe("Ready");
        status.DisplayState.ShouldBe(AgentStatusRunState.Idle);
        settled.ShouldBe(1);
        f.Face.Glyph.ShouldBe(AgentStatusGlyph.Mark);
    }

    [AvaloniaFact]
    public void A_Null_SettleAfter_Keeps_The_Result_On_Screen()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading", started: f.Time.GetUtcNow());
        status.SettleAfter = null;
        f.Open(status);
        status.Status = AgentStatusRunState.Stopped;
        f.AdvanceTime(60_000);
        f.Face.LabelText.ShouldStartWith("Stopped after");
    }

    [AvaloniaFact]
    public void Actions_Choose_Which_Buttons_Show()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.NeedsInput, "Needs approval", actions: AgentStatusActions.Approve);
        f.Open(status);
        status.InlineApproveButton.IsVisible.ShouldBeTrue();
        status.InlineDenyButton.IsVisible.ShouldBeFalse();
        status.Actions = AgentStatusActions.None;
        status.InlineApproveButton.IsVisible.ShouldBeFalse();
        status.Actions = AgentStatusActions.Retry;
        status.Status = AgentStatusRunState.Failed;
        status.InlineRetryButton.IsVisible.ShouldBeTrue();
        status.Actions = AgentStatusActions.All & ~AgentStatusActions.Retry;
        status.InlineRetryButton.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Panel_Offers_The_Buttons_Of_Each_State()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        f.Open(status, ArcMotionLevel.Off);
        status.Expand();
        f.Pump(0.1);
        void Check(string? first, string? second)
        {
            (status.PrimaryButton.IsVisible ? AutomationProperties.GetName(status.PrimaryButton) : null).ShouldBe(first);
            (status.SecondaryButton.IsVisible ? AutomationProperties.GetName(status.SecondaryButton) : null).ShouldBe(second);
            status.OpenRunButton.IsVisible.ShouldBeTrue();
        }
        Check("Pause", "Stop");
        status.Status = AgentStatusRunState.Paused;
        Check("Resume", "Stop");
        status.Status = AgentStatusRunState.NeedsInput;
        Check("Approve", "Deny");
        status.InlineApproveButton.IsVisible.ShouldBeFalse();
        status.Status = AgentStatusRunState.Failed;
        Check("Retry", null);
        status.Status = AgentStatusRunState.Done;
        Check(null, null);
        status.Actions = AgentStatusActions.All & ~AgentStatusActions.OpenRun;
        status.OpenRunButton.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Panel_Floats_In_The_Overlay_And_Grows_From_The_Pill()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading 12 files", started: f.Time.GetUtcNow());
        for (var i = 0; i < 7; i++)
        {
            status.Steps.Add(new AgentStatusStep("s" + i, "Step " + i, i < 5 ? AgentStatusStepState.Done : i == 5 ? AgentStatusStepState.Active : AgentStatusStepState.Pending)
            {
                Duration = TimeSpan.FromMilliseconds(1500), StartedAt = f.Time.GetUtcNow()
            });
        }
        f.Open(status);
        f.Pump(0.5);
        var pill = status.Surface.Width;
        pill.ShouldBeLessThan(384);
        status.Surface.Height.ShouldBe(40, 0.01);
        status.IsFloating.ShouldBeFalse();

        Click(f.Window, status.Head);
        status.IsExpanded.ShouldBeTrue();
        status.IsFloating.ShouldBeTrue();
        status.Surface.GetVisualAncestors().OfType<OverlayLayer>().Any().ShouldBeTrue();
        f.Pump(1 / 60.0 * 3);
        status.Surface.Width.ShouldBeGreaterThan(pill);
        status.Surface.Width.ShouldBeLessThan(384);
        f.Pump(1.5);
        status.Surface.Width.ShouldBe(384, 0.5);
        status.PanelWidth.ShouldBe(384);
        status.Surface.Height.ShouldBe(status.Body.DesiredSize.Height + 40, 0.5);
        status.StepList.RowCount.ShouldBe(5, "the latest five of six listed steps");
        status.Body.Opacity.ShouldBe(1, 0.001);
        status.CounterText.Text.ShouldBe("Step 6 of 7");
        status.Segments.Count.ShouldBe(7);
        status.Segments.Finished.ShouldBe(5);
        status.Segments.ValueText.ShouldBe("5 of 7 steps finished");

        // Centered on the pill: the panel's center is the footprint's center.
        var layer = status.Surface.GetVisualAncestors().OfType<OverlayLayer>().First();
        var slot = status.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Slot");
        var slotCenter = slot.TranslatePoint(new Point(slot.Bounds.Width / 2, 0), layer)!.Value.X;
        (Canvas.GetLeft(status.Surface) + status.Surface.Width / 2).ShouldBe(slotCenter, 0.5);

        Click(f.Window, status.Head);
        status.IsExpanded.ShouldBeFalse();
        f.Pump(1.5);
        status.IsFloating.ShouldBeFalse();
        status.Surface.Width.ShouldBe(status.PillTargetWidth, 0.5);
        status.Body.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Panel_Is_As_Wide_As_The_Pill_When_That_Is_Wider_And_Capped_By_The_Parent()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.NeedsInput, "Needs your approval to run a very long command line");
        f.Open(status, ArcMotionLevel.Off, width: 900);
        status.Surface.Width.ShouldBeGreaterThan(384);
        var pill = status.PillTargetWidth;
        status.Expand();
        f.Pump(0.1);
        status.PanelWidth.ShouldBe(pill, 0.01);
        status.Collapse();
        f.Pump(0.1);

        f.Window.Width = 340;
        f.Pump(0.1);
        Dispatcher.UIThread.RunJobs();
        status.Expand();
        f.Pump(0.1);
        status.PanelWidth.ShouldBe(340, 1);
    }

    [AvaloniaFact]
    public void Align_Start_And_End_Keep_The_Panel_On_The_Pill_Edge()
    {
        foreach (var align in new[] { AgentStatusPanelAlignment.Start, AgentStatusPanelAlignment.End })
        {
            using var f = new Fixture();
            var status = Pill(AgentStatusRunState.Working, "Reading");
            status.PanelAlignment = align;
            status.HorizontalAlignment = HorizontalAlignment.Center;
            f.Open(status, ArcMotionLevel.Off);
            f.Pump(0.1);
            status.Expand();
            f.Pump(0.2);
            var layer = status.Surface.GetVisualAncestors().OfType<OverlayLayer>().First();
            var slot = status.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Slot");
            var origin = slot.TranslatePoint(default, layer)!.Value;
            var left = Canvas.GetLeft(status.Surface);
            if (align == AgentStatusPanelAlignment.Start)
            {
                left.ShouldBe(origin.X, 0.5);
            }
            else
            {
                (left + status.Surface.Width).ShouldBe(origin.X + slot.Bounds.Width, 1);
            }
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Panel_And_Returns_Focus_To_The_Pill_With_The_Keyboard_Ring()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        f.Open(status, ArcMotionLevel.Off);
        status.Head.Focus(NavigationMethod.Tab);
        f.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        status.IsExpanded.ShouldBeTrue();
        status.Head.IsFocused.ShouldBeTrue("focus stays on the pill when the panel moves to the overlay");
        status.PrimaryButton.Focus(NavigationMethod.Tab);
        status.PrimaryButton.IsFocused.ShouldBeTrue();
        f.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        status.IsExpanded.ShouldBeFalse();
        status.Head.IsFocused.ShouldBeTrue();
        status.Head.Classes.Contains(":focus-visible").ShouldBeTrue();
        f.Pump(0.1);
        status.IsFloating.ShouldBeFalse();
        status.Head.IsFocused.ShouldBeTrue("still focused after the surface moved back");
    }

    [AvaloniaFact]
    public void Enter_And_Space_Toggle_The_Panel()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Idle);
        f.Open(status, ArcMotionLevel.Off);
        status.Head.Focus(NavigationMethod.Tab);
        f.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        status.IsExpanded.ShouldBeTrue();
        f.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        status.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Press_Outside_Closes_The_Panel_Without_Moving_Focus_To_The_Pill()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        var other = new Button { Content = "Elsewhere", Width = 100, Height = 30, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        var host = new Grid { Children = { status, other } };
        f.Status = status;
        status.TimeProvider = f.Time;
        f.Window = TestHelpers.Show(host, ArcMotionLevel.Off);
        f.Pump(0.1);
        status.Expand();
        f.Pump(0.1);
        Click(f.Window, other);
        status.IsExpanded.ShouldBeFalse();
        status.Head.IsFocused.ShouldBeFalse();

        status.Expand();
        f.Pump(0.1);
        Click(f.Window, status.Body);
        status.IsExpanded.ShouldBeTrue("a press inside the panel keeps it open");
    }

    [AvaloniaFact]
    public void ExpandedChanged_Is_Raised_And_IsExpanded_Is_Two_Way()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        var seen = new List<bool>();
        status.ExpandedChanged += (_, e) => seen.Add(e.IsExpanded);
        f.Open(status, ArcMotionLevel.Off);
        status.Toggle();
        status.Collapse();
        status.Expand();
        seen.ShouldBe([true, false, true]);
        AgentStatus.IsExpandedProperty.GetMetadata(typeof(AgentStatus)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
    }

    [AvaloniaFact]
    public void One_Button_Changes_Role_So_Focus_Stays_On_It()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        f.Open(status, ArcMotionLevel.Off);
        status.Expand();
        f.Pump(0.1);
        var button = status.PrimaryButton;
        button.Focus(NavigationMethod.Tab);
        status.Status = AgentStatusRunState.Paused;
        status.PrimaryButton.ShouldBeSameAs(button);
        AutomationProperties.GetName(button).ShouldBe("Resume");
        button.IsFocused.ShouldBeTrue();
        status.Status = AgentStatusRunState.NeedsInput;
        AutomationProperties.GetName(button).ShouldBe("Approve");
        button.IsFocused.ShouldBeTrue();
        status.Status = AgentStatusRunState.Working;
        AutomationProperties.GetName(button).ShouldBe("Pause");
        button.IsFocused.ShouldBeTrue();
        var stop = status.SecondaryButton;
        stop.Focus(NavigationMethod.Tab);
        status.Status = AgentStatusRunState.NeedsInput;
        status.SecondaryButton.ShouldBeSameAs(stop);
        AutomationProperties.GetName(stop).ShouldBe("Deny");
        stop.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void When_The_Focused_Button_Disappears_Focus_Goes_To_The_Pill()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        f.Open(status, ArcMotionLevel.Off);
        status.Expand();
        f.Pump(0.1);
        status.PrimaryButton.Focus(NavigationMethod.Tab);
        status.Status = AgentStatusRunState.Done;
        status.PrimaryButton.IsVisible.ShouldBeFalse();
        status.Head.IsFocused.ShouldBeTrue();
        status.Head.Classes.Contains(":focus-visible").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Simulation_Drives_The_Pill_And_Receives_Its_Buttons()
    {
        using var f = new Fixture();
        var sim = new AgentRunSimulation(AgentStatusSamples.Coding, f.Time);
        var status = new AgentStatus { Simulation = sim };
        f.Open(status);
        f.AdvanceTime(900 + 3000);
        status.Expand();
        f.Pump(1.2);
        status.DisplayState.ShouldBe(AgentStatusRunState.Working);
        status.Invoke(AgentStatusActions.Pause);
        sim.Status.ShouldBe(AgentStatusRunState.Paused);
        status.DisplayState.ShouldBe(AgentStatusRunState.Paused);
        AutomationProperties.GetName(status.PrimaryButton).ShouldBe("Resume");
        status.Invoke(AgentStatusActions.Resume);
        f.AdvanceTime(8_300);
        status.DisplayState.ShouldBe(AgentStatusRunState.NeedsInput);
        AutomationProperties.GetName(status.PrimaryButton).ShouldBe("Approve");
        status.Invoke(AgentStatusActions.Approve);
        status.DisplayState.ShouldBe(AgentStatusRunState.Working);
        AutomationProperties.GetName(status.PrimaryButton).ShouldBe("Pause");
        f.AdvanceTime(1_440);
        status.DisplayState.ShouldBe(AgentStatusRunState.Done);
        f.Face.LabelText!.ShouldContain("5 files touched");
        status.CounterText.Text.ShouldBe("6 of 6 steps");
        f.AdvanceTime(4_000);
        status.DisplayState.ShouldBe(AgentStatusRunState.Idle);
        status.CounterText.Text.ShouldBe("6 of 6 steps", "the panel keeps the last run's steps");
        status.StepList.RowCount.ShouldBe(5);
    }

    [AvaloniaFact]
    public void Open_Run_Confirms_With_A_Check_For_A_Moment()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Idle);
        var raised = 0;
        status.OpenRunRequested += (_, _) => raised++;
        f.Open(status, ArcMotionLevel.Off);
        status.Expand();
        f.Pump(0.1);
        AutomationProperties.GetName(status.OpenRunButton).ShouldBe("Open run");
        status.Invoke(AgentStatusActions.OpenRun);
        raised.ShouldBe(1);
        AutomationProperties.GetName(status.OpenRunButton).ShouldBe("Opened");
        f.AdvanceTime(1_599);
        AutomationProperties.GetName(status.OpenRunButton).ShouldBe("Opened");
        f.AdvanceTime(1);
        AutomationProperties.GetName(status.OpenRunButton).ShouldBe("Open run");
    }

    [AvaloniaFact]
    public void The_Panel_Lists_The_Latest_Steps_With_Times_And_Fills_The_Counter()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Running tests", started: f.Time.GetUtcNow());
        status.Steps.Add(new AgentStatusStep("a", "Read 12 files", AgentStatusStepState.Done) { Duration = TimeSpan.FromMilliseconds(1917) });
        status.Steps.Add(new AgentStatusStep("b", "Running tests", AgentStatusStepState.Active) { StartedAt = f.Time.GetUtcNow() });
        status.Steps.Add(new AgentStatusStep("c", "Push the branch", AgentStatusStepState.Pending));
        f.Open(status, ArcMotionLevel.Off);
        status.Expand();
        f.Pump(0.1);
        status.StepList.RowCount.ShouldBe(2);
        status.StepList.LabelOf(0).ShouldBe("Read 12 files");
        status.StepList.TimeOf(0).ShouldBe("1.9s");
        status.StepList.TimeOf(1).ShouldBe("0s");
        f.AdvanceTime(7_300);
        status.Tick();
        status.StepList.TimeOf(1).ShouldBe("7s");
        status.CounterText.Text.ShouldBe("Step 2 of 3");
        status.Segments.StateOf(2).ShouldBe(AgentStatusStepState.Pending);
        status.MaxSteps = 1;
        status.StepList.RowCount.ShouldBe(1);
        status.EmptyText.IsVisible.ShouldBeFalse();
        status.Steps.Clear();
        status.EmptyText.IsVisible.ShouldBeTrue();
        status.EmptyText.Text.ShouldBe("Nothing has run yet. Steps appear here once Atlas Scout 2 starts.");
    }

    [AvaloniaFact]
    public void Step_Rows_Slide_Up_When_The_Oldest_Leaves_And_New_Ones_Fade_In()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Working");
        status.MaxSteps = 3;
        for (var i = 0; i < 3; i++)
        {
            status.Steps.Add(new AgentStatusStep("s" + i, "Step " + i, AgentStatusStepState.Done) { Duration = TimeSpan.FromSeconds(1) });
        }
        f.Open(status);
        status.Expand();
        f.Pump(1.2);
        status.StepList.RowCount.ShouldBe(3);
        status.Steps.Add(new AgentStatusStep("s3", "Step 3", AgentStatusStepState.Active) { StartedAt = f.Time.GetUtcNow() });
        status.StepList.RowCount.ShouldBe(3);
        status.StepList.ExitingCount.ShouldBe(1);
        status.StepList.LabelOf(0).ShouldBe("Step 1");
        status.StepList.OpacityOf(2).ShouldBeLessThan(0.1);
        status.StepList.YOf(0).ShouldBeGreaterThan(status.StepList.TargetYOf(0), "the surviving rows slide up from where they were");
        f.Pump(0.6);
        status.StepList.ExitingCount.ShouldBe(0);
        status.StepList.OpacityOf(2).ShouldBe(1, 0.001);
        status.StepList.YOf(0).ShouldBe(status.StepList.TargetYOf(0), 0.1);
    }

    [AvaloniaFact]
    public void A_Narrow_Container_Makes_Compact_Icon_Buttons_And_Hides_The_Timer_While_They_Show()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.NeedsInput, "Needs your approval to run a command", started: f.Time.GetUtcNow());
        f.Open(status, ArcMotionLevel.Off, width: 340);
        f.Pump(0.1);
        Dispatcher.UIThread.RunJobs();
        status.IsCompact.ShouldBeTrue();
        status.Classes.Contains(":compact").ShouldBeTrue();
        status.InlineApproveButton.Width.ShouldBe(30);
        status.InlineDenyButton.Width.ShouldBe(30);
        AutomationProperties.GetName(status.InlineApproveButton).ShouldBe("Approve");
        f.Face.IsTimerVisible.ShouldBeFalse();
        status.Surface.Width.ShouldBeLessThanOrEqualTo(340);
        status.Expand();
        f.Pump(0.1);
        f.Face.IsTimerVisible.ShouldBeTrue("the timer is back when the buttons move into the panel");
        status.OpenRunButton.Width.ShouldBe(36);
        AutomationProperties.GetName(status.OpenRunButton).ShouldBe("Open run");
        status.Collapse();
        f.Pump(0.1);
        f.Window.Width = 700;
        f.Pump(0.1);
        Dispatcher.UIThread.RunJobs();
        status.IsCompact.ShouldBeFalse();
        double.IsNaN(status.InlineApproveButton.Width).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Pill_Is_A_Button_With_An_Expanded_State_And_A_Full_Name()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Checking specs", started: f.Time.GetUtcNow());
        status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, 8, 12);
        f.Open(status, ArcMotionLevel.Off);
        f.AdvanceTime(9_000);
        status.Tick();
        AutomationProperties.GetName(status.Head).ShouldBe("Atlas Scout 2: Checking specs 8 of 12, 0:09 elapsed");
        var peer = ControlAutomationPeer.CreatePeerForElement(status.Head);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        var expand = (IExpandCollapseProvider)peer;
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        status.Expand();
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        f.Pump(0.1);
        AutomationProperties.GetName(status.Body).ShouldBe("Atlas Scout 2 run details");
        var bar = ControlAutomationPeer.CreatePeerForElement(status.Segments);
        bar.GetAutomationControlType().ShouldBe(AutomationControlType.ProgressBar);
        bar.GetName().ShouldBe("Run progress");
        status.Status = AgentStatusRunState.Done;
        AutomationProperties.GetName(status.Head).ShouldStartWith("Atlas Scout 2: Done in ");
        AutomationProperties.GetName(status.Head)!.ShouldNotContain("elapsed");
    }

    [AvaloniaFact]
    public void The_Live_Region_Announces_Activities_And_Outcomes_Not_Counts_Or_Ticks()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, started: f.Time.GetUtcNow());
        status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, 1, 12);
        f.Open(status, ArcMotionLevel.Off);
        var history = status.Announcer!.History;
        history.Count.ShouldBe(0);
        for (var i = 2; i <= 6; i++)
        {
            status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, i, 12);
            f.AdvanceTime(1_000);
            status.Tick();
        }
        history.Count.ShouldBe(0);
        status.Activity = new AgentStatusActivity("Opening pages", ArcIcons.Search);
        history.ShouldBe(["Atlas Scout 2: Opening pages"]);
        status.Status = AgentStatusRunState.Paused;
        history[^1].ShouldBe("Atlas Scout 2: Paused");
        status.Notice = "Paused. The run holds where it is.";
        history[^1].ShouldBe("Atlas Scout 2: Paused. Paused. The run holds where it is.");
        status.Status = AgentStatusRunState.Working;
        status.Activity = new AgentStatusActivity("5 files touched");
        status.Status = AgentStatusRunState.Done;
        history[^1].ShouldStartWith("Atlas Scout 2: done in ");
        history[^1].ShouldEndWith(", 5 files touched");
        f.AdvanceTime(4_000);
        history[^1].ShouldBe("Atlas Scout 2: Ready");
    }

    [AvaloniaFact]
    public void Full_Motion_Spins_The_Ring_And_Reduced_Motion_Keeps_It_Still()
    {
        using var full = new Fixture();
        full.Open(Pill(AgentStatusRunState.Working, "Reading"));
        full.Pump(0.5);
        full.Face.IsLoopRunning.ShouldBeTrue();
        full.Face.SpinAngle.ShouldBeGreaterThan(0);
        var angle = full.Face.SpinAngle;
        full.Pump(0.9);
        full.Face.SpinAngle.ShouldNotBe(angle);
        full.Status.Status = AgentStatusRunState.NeedsInput;
        var frozen = full.Face.SpinAngle;
        full.Pump(0.5);
        full.Face.SpinAngle.ShouldBe(frozen, 0.001);
        full.Status.Status = AgentStatusRunState.Working;
        full.Pump(0.2);
        full.Face.SpinAngle.ShouldNotBe(frozen, "it continues from the frozen angle");

        using var reduced = new Fixture();
        reduced.Open(Pill(AgentStatusRunState.Working, "Reading"), ArcMotionLevel.Reduced);
        reduced.Pump(1.5);
        reduced.Face.SpinAngle.ShouldBe(0);
        reduced.Face.IsLoopRunning.ShouldBeFalse("no spin, no shimmer: nothing asks for frames");
        reduced.Status.Status = AgentStatusRunState.Paused;
        reduced.Face.LabelText.ShouldBe("Paused");
        reduced.Face.LeavingCount.ShouldBe(0, "no roll under reduced motion");
    }

    [AvaloniaFact]
    public void Done_Draws_The_Ring_And_The_Check_In_Full_Motion_But_Shows_Them_At_Once_When_Reduced()
    {
        using var full = new Fixture();
        full.Open(Pill(AgentStatusRunState.Working, "Reading"));
        full.Pump(0.2);
        full.Status.Status = AgentStatusRunState.Done;
        full.Pump(0.08);
        full.Face.ClosedProgress.ShouldBeGreaterThan(0);
        full.Face.ClosedProgress.ShouldBeLessThan(1);
        full.Face.GlyphDraw(AgentStatusGlyph.Check).ShouldBe(0, 0.001, "the check waits 120ms before it draws");
        full.Pump(0.6);
        full.Face.ClosedProgress.ShouldBe(1, 0.001);
        full.Face.GlyphDraw(AgentStatusGlyph.Check).ShouldBe(1, 0.001);
        full.Face.GlyphOpacity(AgentStatusGlyph.Mark).ShouldBe(0, 0.001);

        using var reduced = new Fixture();
        reduced.Open(Pill(AgentStatusRunState.Working, "Reading"), ArcMotionLevel.Reduced);
        reduced.Status.Status = AgentStatusRunState.Done;
        reduced.Face.ClosedProgress.ShouldBe(1);
        reduced.Face.GlyphDraw(AgentStatusGlyph.Check).ShouldBe(1);
        reduced.Pump(0.3);
        reduced.Face.GlyphOpacity(AgentStatusGlyph.Check).ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void Needs_Input_Pops_The_Dot_With_One_Ping()
    {
        using var f = new Fixture();
        f.Open(Pill(AgentStatusRunState.Working, "Reading"));
        f.Pump(0.2);
        f.Status.Status = AgentStatusRunState.NeedsInput;
        f.Face.Ping.Opacity.ShouldBe(0, 0.001);
        f.Pump(0.4);
        f.Face.Ping.Opacity.ShouldBeGreaterThan(0);
        f.Face.Ping.Scale.ShouldBeGreaterThan(1);
        f.Pump(1.2);
        f.Face.Ping.Opacity.ShouldBe(0, 0.001);
        f.Face.DotOpacity.ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void The_Pill_Width_Follows_The_Label_On_A_Spring_And_Fades_The_Edge_While_It_Grows()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading", started: f.Time.GetUtcNow());
        f.Open(status);
        f.Pump(1);
        var before = status.Surface.Width;
        status.Activity = new AgentStatusActivity("Reading all project files", ArcIcons.Search);
        f.Pump(0.1);
        status.Surface.Width.ShouldBeGreaterThan(before);
        status.Surface.Width.ShouldBeLessThan(status.PillTargetWidth);
        f.Face.CurrentFade.ShouldBeGreaterThan(0);
        f.Pump(1);
        status.Surface.Width.ShouldBe(status.PillTargetWidth, 0.5);
        f.Face.CurrentFade.ShouldBe(0, 0.001);
    }

    [AvaloniaFact]
    public void Off_Motion_Jumps_The_Width()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        f.Open(status, ArcMotionLevel.Off);
        status.Activity = new AgentStatusActivity("Reading all project files", ArcIcons.Search);
        status.Surface.Width.ShouldBe(status.PillTargetWidth, 0.01);
        f.Face.LeavingCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_Custom_Labels_Object_Rewords_Everything()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Idle);
        status.Labels = new AgentStatusLabels { Ready = "Bereit", Paused = "Pausiert", DoneIn = (t, o) => $"Fertig in {t}" };
        f.Open(status, ArcMotionLevel.Off);
        f.Face.LabelText.ShouldBe("Bereit");
        status.Status = AgentStatusRunState.Paused;
        f.Face.LabelText.ShouldBe("Pausiert");
    }

    [AvaloniaFact]
    public void Detaching_A_Floating_Panel_Removes_It_From_The_Overlay()
    {
        using var f = new Fixture();
        var status = Pill(AgentStatusRunState.Working, "Reading");
        var host = new Border { Child = status };
        f.Status = status;
        status.TimeProvider = f.Time;
        f.Window = TestHelpers.Show(host, ArcMotionLevel.Off);
        status.Expand();
        f.Pump(0.1);
        status.IsFloating.ShouldBeTrue();
        var layer = status.Surface.GetVisualAncestors().OfType<OverlayLayer>().First();
        host.Child = null;
        Dispatcher.UIThread.RunJobs();
        layer.Children.Contains(status.Surface).ShouldBeFalse();
        status.IsFloating.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Pill_Is_Collected_While_The_Hosts_Simulation_And_Steps_Live_On()
    {
        var time = new AgentStatusTestClock();
        var sim = new AgentRunSimulation(AgentStatusSamples.Coding, time);
        var kept = new AgentStatusStep("a", "Read 12 files", AgentStatusStepState.Done);
        var cleared = new AgentStatusStep("b", "Running tests", AgentStatusStepState.Active);
        var reference = PillOver(sim, kept, cleared);
        for (var i = 0; i < 4 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }
        reference.IsAlive.ShouldBeFalse();
        // The run and the steps go on without the pill.
        time.Advance(5_000);
        kept.Label = "Read 13 files";
        cleared.Label = "Ran tests";
        GC.KeepAlive(sim);
    }

    [AvaloniaFact]
    public void A_Pill_Put_Back_Follows_Its_Simulation_Again()
    {
        using var f = new Fixture();
        var sim = new AgentRunSimulation(AgentStatusSamples.Coding, f.Time);
        var status = new AgentStatus { Simulation = sim, TimeProvider = f.Time };
        var host = new Border { Child = status };
        f.Status = status;
        f.Window = TestHelpers.Show(host, ArcMotionLevel.Off);
        f.Pump(0.1);
        f.AdvanceTime(900 + 3000);
        status.DisplayState.ShouldBe(AgentStatusRunState.Working);
        host.Child = null;
        Dispatcher.UIThread.RunJobs();
        sim.Pause();
        host.Child = status;
        f.Pump(0.1);
        status.DisplayState.ShouldBe(AgentStatusRunState.Paused);
        sim.Resume();
        status.DisplayState.ShouldBe(AgentStatusRunState.Working);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference PillOver(AgentRunSimulation sim, AgentStatusStep kept, AgentStatusStep cleared)
    {
        var status = new AgentStatus { Simulation = sim };
        status.Steps.Add(cleared);
        var window = TestHelpers.Show(status);
        // A Clear reports no old items: the pill must still let go of the step it watched.
        status.Steps.Clear();
        status.Steps.Add(kept);
        Dispatcher.UIThread.RunJobs();
        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return new WeakReference(status);
    }
}

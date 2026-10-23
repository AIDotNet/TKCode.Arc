using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>
/// <see cref="ReleaseReadiness"/> on invented samples (Version 5.3, four checks with the first done, two owners): counts
/// and the gate, the release state machine (success, failure, retry, cancel, reset), events, focus after each step, the
/// keyboard, the compact paddings, framed and flat, screen reader names and the motion levels.
/// </summary>
public class ReleaseReadinessTests
{
    private static ReleaseCheck[] Samples() =>
    [
        new("Design signed off", "Final screens approved by product", isDone: true) { Key = "design" },
        new("Smoke tests green", "Build 5.3.0-beta.2 passed on preview") { Key = "smoke" },
        new("Release notes written", "Customer summary drafted and reviewed") { Key = "notes" },
        new("Support briefed", "Help desk knows what is changing") { Key = "support" },
    ];

    private sealed class Rig : IDisposable
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 640, Action<ReleaseReadiness>? configure = null,
            ManualFrameClock? clock = null)
        {
            Clock = clock;
            Gate = new ReleaseReadiness
            {
                Title = "Version 5.3",
                Subtitle = "Lumen mobile · goes live Tuesday",
                ChannelName = "#launches",
            };
            Gate.Owners.Add(new ProjectPerson("Rina Kato"));
            Gate.Owners.Add(new ProjectPerson("Samir Aziz"));
            Gate.Checks.AddRange(Samples());
            configure?.Invoke(Gate);
            Gate.CheckChanged += (_, e) => CheckEvents.Add(e);
            Gate.ReadyChanged += (_, e) => ReadyEvents.Add(e.IsReady);
            Gate.StateChanged += (_, e) => States.Add(e.NewState);
            Gate.Released += (_, e) => ReleasedEvents.Add(e);
            Gate.ReleaseFailed += (_, e) => FailedEvents.Add(e);
            Gate.ResetPerformed += (_, e) => ResetEvents.Add(e);
            Host = new Border { Width = width, Child = Gate, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 900;
            Window.Height = 900;
            Pump();
        }

        public ManualFrameClock? Clock { get; }

        public ReleaseReadiness Gate { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<ReleaseCheckChangedEventArgs> CheckEvents { get; } = new();

        public List<bool> ReadyEvents { get; } = new();

        public List<ReleaseReadinessState> States { get; } = new();

        public List<ReleasedEventArgs> ReleasedEvents { get; } = new();

        public List<ReleaseFailedEventArgs> FailedEvents { get; } = new();

        public List<ReleaseResetEventArgs> ResetEvents { get; } = new();

        public Button Release => TestHelpers.Descendant<Button>(Gate, ReleaseReadiness.ReleasePart);

        public Button ResetButton => TestHelpers.Descendant<Button>(Gate, ReleaseReadiness.ResetPart);

        public Switch Notify => TestHelpers.Descendant<Switch>(Gate, ReleaseReadiness.NotifyPart);

        public TextBlock Remaining => TestHelpers.Descendant<TextBlock>(Gate, ReleaseReadiness.RemainingPart);

        public TextBlock Count => TestHelpers.Descendant<TextBlock>(Gate, ReleaseReadiness.CountPart);

        public ProjectsProgressLine Bar => TestHelpers.Descendant<ProjectsProgressLine>(Gate, ReleaseReadiness.BarPart);

        public SpringSizeDecorator StatusHost => TestHelpers.Descendant<SpringSizeDecorator>(Gate, ReleaseReadiness.StatusHostPart);

        public ReleaseReadinessMessage Status => TestHelpers.Descendant<ReleaseReadinessMessage>(Gate, ReleaseReadiness.StatusPart);

        public ReleaseReadinessFooter Footer => TestHelpers.Descendant<ReleaseReadinessFooter>(Gate, ReleaseReadiness.FooterPart);

        public IReadOnlyList<ReleaseReadinessRow> Rows => Gate.Rows;

        public Checkbox Box(int index) => Rows[index].Checkbox!;

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Advance(TimeSpan time)
        {
            Clock!.Advance(time);
            Pump();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
        }

        public void TickAll()
        {
            foreach (var check in Gate.Checks)
            {
                check.IsDone = true;
            }
            Pump();
        }

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public void Dispose() => Window.Close();
    }

    #region Counts and the gate

    [AvaloniaFact]
    public void Counts_Texts_And_The_Bar_Follow_The_Required_Checks()
    {
        using var rig = new Rig();
        var gate = rig.Gate;
        gate.DoneCount.ShouldBe(1);
        gate.TotalCount.ShouldBe(4);
        gate.IsReady.ShouldBeFalse();
        gate.State.ShouldBe(ReleaseReadinessState.Collecting);
        rig.Remaining.Text.ShouldBe("3 to go");
        rig.Count.Text.ShouldBe("1 of 4");
        rig.Bar.Value.ShouldBe(1);
        rig.Bar.Maximum.ShouldBe(4);
        rig.Release.IsEnabled.ShouldBeFalse();
        rig.Rows.Count.ShouldBe(4);
        rig.Box(0).IsChecked.ShouldBe(true);

        gate.Checks[1].IsDone = true;
        gate.Checks[2].IsDone = true;
        rig.Pump();
        rig.Remaining.Text.ShouldBe("1 to go");
        rig.Count.Text.ShouldBe("3 of 4");
        rig.Box(2).IsChecked.ShouldBe(true);

        gate.Checks[3].IsDone = true;
        rig.Pump();
        rig.Remaining.Text.ShouldBe("All clear");
        gate.IsReady.ShouldBeTrue();
        gate.State.ShouldBe(ReleaseReadinessState.Ready);
        gate.Classes.ShouldContain(ReleaseReadiness.ReadyPseudoClass);
        rig.Release.IsEnabled.ShouldBeTrue();

        gate.Checks[0].IsDone = false;
        rig.Pump();
        gate.State.ShouldBe(ReleaseReadinessState.Collecting);
        rig.Release.IsEnabled.ShouldBeFalse();
        gate.Classes.ShouldNotContain(ReleaseReadiness.ReadyPseudoClass);
        rig.ReadyEvents.ShouldBe([true, false]);
    }

    [AvaloniaFact]
    public void Ticking_A_Box_Writes_The_Check_And_Dims_Its_Title()
    {
        using var rig = new Rig();
        var check = rig.Gate.Checks[1];
        rig.Box(1).IsChecked = true;
        rig.Pump();
        check.IsDone.ShouldBeTrue();
        rig.Rows[1].Classes.ShouldContain(ReleaseReadinessRow.DonePseudoClass);
        var e = rig.CheckEvents.ShouldHaveSingleItem();
        e.Check.ShouldBe(check);
        e.OldValue.ShouldBeFalse();
        e.NewValue.ShouldBeTrue();
        e.DoneCount.ShouldBe(2);
        e.TotalCount.ShouldBe(4);
        rig.Gate.Announcer!.Announcement.ShouldBe("2 to go");
    }

    [AvaloniaFact]
    public void Pressing_The_Label_Ticks_The_Check_But_The_Row_Padding_Does_Not()
    {
        using var rig = new Rig();
        var row = rig.Rows[1];
        var description = TestHelpers.Descendant<TextBlock>(row, ReleaseReadinessRow.DescriptionPart);
        var point = description.TranslatePoint(new Point(4, description.Bounds.Height / 2), rig.Window)!.Value;
        rig.Window.MouseDown(point, MouseButton.Left);
        rig.Window.MouseUp(point, MouseButton.Left);
        rig.Pump();
        rig.Gate.Checks[1].IsDone.ShouldBeTrue();

        var edge = row.TranslatePoint(new Point(row.Bounds.Width - 8, row.Bounds.Height / 2), rig.Window)!.Value;
        rig.Window.MouseDown(edge, MouseButton.Left);
        rig.Window.MouseUp(edge, MouseButton.Left);
        rig.Pump();
        rig.Gate.Checks[1].IsDone.ShouldBeTrue();

        // The release button answers the pointer once the gate is ready.
        rig.TickAll();
        var button = rig.Release.TranslatePoint(new Point(rig.Release.Bounds.Width / 2, rig.Release.Bounds.Height / 2), rig.Window)!.Value;
        rig.Window.MouseDown(button, MouseButton.Left);
        rig.Window.MouseUp(button, MouseButton.Left);
        rig.Pump();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Released);
    }

    [AvaloniaFact]
    public void Optional_Checks_Are_Counted_Out_Of_The_Gate()
    {
        ReleaseRequest? request = null;
        using var rig = new Rig(configure: g =>
        {
            g.Checks.Add(new ReleaseCheck("Intro call booked") { IsRequired = false });
            g.ReleaseAction = (r, _) =>
            {
                request = r;
                return Task.CompletedTask;
            };
        });
        rig.Gate.TotalCount.ShouldBe(4);
        foreach (var check in rig.Gate.Checks.Take(4))
        {
            check.IsDone = true;
        }
        rig.Pump();
        rig.Gate.IsReady.ShouldBeTrue();
        rig.Rows[4].Checkbox!.IsRequired.ShouldBeFalse();
        rig.Rows[0].Checkbox!.IsRequired.ShouldBeTrue();

        rig.Click(rig.Release);
        request.ShouldNotBeNull();
        request!.Checks.Count.ShouldBe(5);
        request.SkippedChecks.ShouldHaveSingleItem().Title.ShouldBe("Intro call booked");
        request.Notify.ShouldBeTrue();
        request.Attempt.ShouldBe(1);
    }

    [AvaloniaFact]
    public void An_Outside_List_Wins_And_Is_Followed()
    {
        var checks = new ObservableCollection<ReleaseCheck>(Samples().Take(2));
        var owners = new ObservableCollection<ProjectPerson> { new("Noor Haddad") };
        using var rig = new Rig(configure: g =>
        {
            g.ChecksSource = checks;
            g.OwnersSource = owners;
        });
        rig.Rows.Count.ShouldBe(2);
        rig.Gate.TotalCount.ShouldBe(2);
        checks.Add(new ReleaseCheck("Access granted"));
        rig.Pump();
        rig.Rows.Count.ShouldBe(3);
        rig.Gate.TotalCount.ShouldBe(3);
        rig.Rows[0].Classes.ShouldContain(ReleaseReadinessRow.FirstPseudoClass);
        rig.Rows[1].Classes.ShouldNotContain(ReleaseReadinessRow.FirstPseudoClass);

        var group = TestHelpers.Descendant<ReleaseReadinessOwners>(rig.Gate, ReleaseReadiness.OwnersPart);
        AutomationProperties.GetName(group).ShouldBe("Owners: Noor Haddad");
        owners.Add(new ProjectPerson("Tomás Ferreira"));
        rig.Pump();
        AutomationProperties.GetName(group).ShouldBe("Owners: Noor Haddad and Tomás Ferreira");
        owners.Clear();
        rig.Pump();
        group.IsVisible.ShouldBeFalse();
    }

    #endregion

    #region Release, failure, retry, cancel, reset

    [AvaloniaFact]
    public void A_Release_Locks_The_Checks_Then_Reports_Success()
    {
        var pending = new TaskCompletionSource();
        using var rig = new Rig(configure: g => g.ReleaseAction = (_, _) => pending.Task);
        rig.TickAll();
        rig.Click(rig.Release);
        var gate = rig.Gate;
        gate.State.ShouldBe(ReleaseReadinessState.Releasing);
        rig.Release.IsLoading.ShouldBeTrue();
        rig.Release.IsEnabled.ShouldBeTrue();
        rig.Box(1).IsEnabled.ShouldBeFalse();
        rig.Notify.IsEnabled.ShouldBeFalse();
        rig.Remaining.Text.ShouldBe("Rolling out");
        gate.Classes.ShouldContain(ReleaseReadiness.ReleasingPseudoClass);
        gate.Announcer!.Announcement.ShouldBe("Rolling out");

        pending.SetResult();
        rig.Pump();
        gate.State.ShouldBe(ReleaseReadinessState.Released);
        rig.Release.IsLoading.ShouldBeFalse();
        rig.Release.IsEnabled.ShouldBeFalse();
        AutomationProperties.GetName(rig.Release).ShouldBe("Rollout started");
        var label = rig.Release.Content.ShouldBeOfType<StackPanel>();
        label.Children[0].ShouldBeOfType<ArcGlyph>().Data.ShouldBe(ArcIcons.CircleCheck);
        rig.ResetButton.IsVisible.ShouldBeTrue();
        rig.Box(0).IsEnabled.ShouldBeFalse();
        rig.Remaining.Text.ShouldBe("Rolling out");
        gate.StatusText.ShouldBe("Version 5.3 is on its way. We posted in #launches.");
        rig.StatusHost.IsExpanded.ShouldBeTrue();
        rig.Status.IsError.ShouldBeFalse();
        gate.Announcer.Announcement.ShouldBe(gate.StatusText);
        rig.ReleasedEvents.ShouldHaveSingleItem().Notify.ShouldBeTrue();
        rig.States.ShouldBe([ReleaseReadinessState.Ready, ReleaseReadinessState.Releasing, ReleaseReadinessState.Released]);

        // Released is final until a reset: a second press does nothing.
        var again = gate.ReleaseAsync();
        again.IsCompleted.ShouldBeTrue();
        again.Result.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Without_Notify_The_Success_Line_Leaves_The_Channel_Out()
    {
        using var rig = new Rig();
        rig.Notify.IsChecked = false;
        rig.Pump();
        rig.Gate.IsNotifyEnabled.ShouldBeFalse();
        rig.TickAll();
        rig.Click(rig.Release);
        rig.Gate.StatusText.ShouldBe("Version 5.3 is on its way.");
        rig.ReleasedEvents.ShouldHaveSingleItem().Notify.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Failure_Shows_The_Error_And_The_Retry_Can_Succeed()
    {
        var attempts = 0;
        using var rig = new Rig(configure: g => g.ReleaseAction = (request, _) =>
        {
            attempts++;
            if (request.Attempt == 1)
            {
                throw new InvalidOperationException("The build is still uploading.");
            }
            return Task.CompletedTask;
        });
        rig.TickAll();
        rig.Click(rig.Release);
        var gate = rig.Gate;
        gate.State.ShouldBe(ReleaseReadinessState.Failed);
        gate.ErrorText.ShouldBe("The build is still uploading.");
        gate.StatusText.ShouldBe("The build is still uploading.");
        gate.Classes.ShouldContain(ReleaseReadiness.FailedPseudoClass);
        rig.Status.IsError.ShouldBeTrue();
        rig.Status.Classes.ShouldContain(ReleaseReadinessMessage.ErrorPseudoClass);
        gate.Alert!.Announcement.ShouldBe("The build is still uploading.");
        AutomationProperties.GetLiveSetting(gate.Alert).ShouldBe(AutomationLiveSetting.Assertive);
        rig.Release.IsEnabled.ShouldBeTrue();
        rig.Release.IsLoading.ShouldBeFalse();
        rig.Box(1).IsEnabled.ShouldBeTrue();
        rig.Notify.IsEnabled.ShouldBeTrue();
        rig.ResetButton.IsVisible.ShouldBeFalse();
        rig.Remaining.Text.ShouldBe("All clear");
        var failed = rig.FailedEvents.ShouldHaveSingleItem();
        failed.Exception.ShouldBeOfType<InvalidOperationException>();
        failed.Request.Attempt.ShouldBe(1);

        rig.Click(rig.Release);
        attempts.ShouldBe(2);
        gate.State.ShouldBe(ReleaseReadinessState.Released);
        gate.ErrorText.ShouldBeNull();
        rig.Status.IsError.ShouldBeFalse();
        rig.ReleasedEvents.ShouldHaveSingleItem().Request.Attempt.ShouldBe(2);
    }

    [AvaloniaFact]
    public void The_Error_Line_Comes_From_The_Formatter_Or_The_Fallback()
    {
        using var rig = new Rig(configure: g => g.ReleaseAction = (_, _) => throw new InvalidOperationException(" "));
        rig.TickAll();
        rig.Click(rig.Release);
        rig.Gate.ErrorText.ShouldBe("The rollout did not start. Try again.");

        rig.Gate.ErrorFormatter = e => "Formatted: " + e.GetType().Name;
        rig.Click(rig.Release);
        rig.Gate.ErrorText.ShouldBe("Formatted: InvalidOperationException");

        // Touching a check leaves the failed attempt behind.
        rig.Gate.Checks[2].IsDone = false;
        rig.Pump();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Collecting);
        rig.Gate.ErrorText.ShouldBeNull();
        rig.Gate.StatusText.ShouldBeNull();
        rig.StatusHost.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Cancel_Puts_The_Gate_Back_And_Ignores_A_Late_Result()
    {
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        using var rig = new Rig(configure: g => g.ReleaseAction = (_, t) =>
        {
            token = t;
            return pending.Task;
        });
        rig.TickAll();
        var task = rig.Gate.ReleaseAsync();
        rig.Pump();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Releasing);

        rig.Gate.CancelRelease();
        rig.Pump();
        token.IsCancellationRequested.ShouldBeTrue();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Ready);
        rig.Release.IsLoading.ShouldBeFalse();
        rig.Box(1).IsEnabled.ShouldBeTrue();

        pending.SetResult();
        rig.Pump();
        task.IsCompleted.ShouldBeTrue();
        task.Result.ShouldBeFalse();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Ready);
        rig.ReleasedEvents.ShouldBeEmpty();
        rig.FailedEvents.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Cancellation_From_The_Token_Is_Not_A_Failure()
    {
        using var rig = new Rig(configure: g => g.ReleaseAction = async (_, t) => await Task.Delay(Timeout.Infinite, t));
        rig.TickAll();
        var task = rig.Gate.ReleaseAsync();
        rig.Gate.CancelRelease();
        for (var i = 0; i < 20 && !task.IsCompleted; i++)
        {
            Thread.Sleep(5);
            rig.Pump();
        }
        task.IsCompleted.ShouldBeTrue();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Ready);
        rig.FailedEvents.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Reset_Puts_The_Checks_Back_And_Keeps_The_Switch()
    {
        using var rig = new Rig(configure: g => g.Checks[2].IsInitiallyDone = true);
        rig.Notify.IsChecked = false;
        rig.TickAll();
        rig.Click(rig.Release);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Released);
        rig.CheckEvents.Clear();

        rig.Click(rig.ResetButton);
        var gate = rig.Gate;
        // The first check was done when it was first shown; the third says so with IsInitiallyDone.
        gate.Checks.Select(c => c.IsDone).ShouldBe([true, false, true, false]);
        gate.IsNotifyEnabled.ShouldBeFalse();
        rig.Notify.IsChecked.ShouldBe(false);
        gate.State.ShouldBe(ReleaseReadinessState.Collecting);
        gate.StatusText.ShouldBeNull();
        rig.StatusHost.IsExpanded.ShouldBeFalse();
        rig.ResetButton.IsVisible.ShouldBeFalse();
        rig.Box(1).IsEnabled.ShouldBeTrue();
        rig.Notify.IsEnabled.ShouldBeTrue();
        rig.Remaining.Text.ShouldBe("2 to go");
        rig.Count.Text.ShouldBe("2 of 4");
        AutomationProperties.GetName(rig.Release).ShouldBe("Start rollout");
        rig.CheckEvents.Count.ShouldBe(2);
        rig.ResetEvents.ShouldHaveSingleItem().PreviousState.ShouldBe(ReleaseReadinessState.Released);
        gate.Announcer!.Announcement.ShouldBe("2 to go");

        // A second full run works like the first.
        rig.TickAll();
        rig.Click(rig.Release);
        gate.State.ShouldBe(ReleaseReadinessState.Released);
        rig.ReleasedEvents.Count.ShouldBe(2);
        rig.ReleasedEvents[1].Request.Attempt.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Release_Starting_Can_Stop_It_And_The_Command_Runs_First()
    {
        var order = new List<string>();
        var command = new Probe(order);
        using var rig = new Rig(configure: g =>
        {
            g.ReleaseCommand = command;
            g.ReleaseAction = (_, _) =>
            {
                order.Add("action");
                return Task.CompletedTask;
            };
        });
        rig.TickAll();
        var stop = true;
        rig.Gate.ReleaseStarting += (_, e) => e.Cancel = stop;
        rig.Click(rig.Release);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Ready);
        order.ShouldBeEmpty();

        stop = false;
        command.Allowed = false;
        rig.Click(rig.Release);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Ready);
        order.ShouldBeEmpty();

        command.Allowed = true;
        rig.Click(rig.Release);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Released);
        order.ShouldBe(["command", "action"]);
        command.Parameter.ShouldBeOfType<ReleaseRequest>().Attempt.ShouldBe(1);
    }

    private sealed class Probe(List<string> order) : ICommand
    {
        public bool Allowed { get; set; } = true;

        public object? Parameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => Allowed;

        public void Execute(object? parameter)
        {
            Parameter = parameter;
            order.Add("command");
        }
    }

    #endregion

    #region Focus and keyboard

    [AvaloniaFact]
    public void Focus_Moves_To_Reset_After_Success_And_To_The_First_Open_Check_After_Reset()
    {
        using var rig = new Rig();
        rig.TickAll();
        rig.Release.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Enter);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Released);
        rig.Focused.ShouldBe(rig.ResetButton);
        rig.ResetButton.Classes.ShouldContain(":focus-visible");

        rig.Key(PhysicalKey.Enter);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Collecting);
        rig.Focused.ShouldBe(rig.Box(1));
        rig.Box(1).Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Without_A_Reset_Button_The_Status_Line_Takes_The_Focus()
    {
        using var rig = new Rig(configure: g => g.IsResetVisible = false);
        rig.TickAll();
        rig.Release.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Space);
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Released);
        rig.ResetButton.IsVisible.ShouldBeFalse();
        rig.Focused.ShouldBe(rig.Status);
        rig.Status.IsTabStop.ShouldBeFalse();
        AutomationProperties.GetName(rig.Status).ShouldBe("Version 5.3 is on its way. We posted in #launches.");
    }

    [AvaloniaFact]
    public void After_A_Failure_The_Focus_Stays_On_The_Release_Button()
    {
        using var rig = new Rig(configure: g => g.ReleaseAction = async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Nope.");
        });
        rig.TickAll();
        rig.Release.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        for (var i = 0; i < 5 && rig.Gate.State == ReleaseReadinessState.Releasing; i++)
        {
            rig.Pump();
        }
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Failed);
        rig.Focused.ShouldBe(rig.Release);
    }

    [AvaloniaFact]
    public void A_Programmatic_Release_Leaves_Focus_Outside_Alone()
    {
        var outside = new TextBox();
        using var rig = new Rig();
        ((Border)rig.Gate.Parent!).Child = null;
        var stack = new StackPanel { Children = { outside, rig.Gate } };
        rig.Host.Child = stack;
        rig.Pump();
        rig.TickAll();
        outside.Focus();
        rig.Pump();
        _ = rig.Gate.ReleaseAsync();
        rig.Pump();
        rig.Gate.State.ShouldBe(ReleaseReadinessState.Released);
        rig.Focused.ShouldBe(outside);
        rig.Gate.Reset();
        rig.Pump();
        rig.Focused.ShouldBe(outside);
    }

    [AvaloniaFact]
    public void Space_Toggles_A_Check_But_Enter_Does_Not_And_The_Switch_Takes_Both()
    {
        using var rig = new Rig();
        var box = rig.Box(1);
        box.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Space);
        rig.Gate.Checks[1].IsDone.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Gate.Checks[1].IsDone.ShouldBeTrue();
        rig.Key(PhysicalKey.Space);
        rig.Gate.Checks[1].IsDone.ShouldBeFalse();

        rig.Notify.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Space);
        rig.Gate.IsNotifyEnabled.ShouldBeFalse();
        rig.Key(PhysicalKey.Enter);
        rig.Gate.IsNotifyEnabled.ShouldBeTrue();

        // From code the switch follows.
        rig.Gate.IsNotifyEnabled = false;
        rig.Pump();
        rig.Notify.IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public void The_Release_Button_Is_Not_A_Tab_Stop_Until_Ready()
    {
        using var rig = new Rig();
        rig.Release.IsEffectivelyEnabled.ShouldBeFalse();
        rig.Notify.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldNotBe(rig.Release);
        rig.TickAll();
        rig.Notify.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBe(rig.Release);
    }

    #endregion

    #region Layout and looks

    [AvaloniaFact]
    public void A_Framed_Card_Is_At_Most_560_Wide_And_Flat_Fills_The_Width()
    {
        using var rig = new Rig(width: 800);
        var card = TestHelpers.Descendant<Border>(rig.Gate, "PART_Card");
        card.Bounds.Width.ShouldBe(560, 0.5);
        card.BorderThickness.ShouldBe(new Thickness(1));
        card.CornerRadius.ShouldBe(new CornerRadius(16));
        TestHelpers.Descendant<Border>(rig.Gate, "PART_List").BorderThickness.Top.ShouldBe(1);
        TestHelpers.Descendant<Border>(rig.Gate, "PART_FooterFrame").BorderThickness.Top.ShouldBe(1);
        rig.Gate.Classes.ShouldContain(ReleaseReadiness.FramedPseudoClass);

        rig.Gate.IsFramed = false;
        rig.Pump();
        card.Bounds.Width.ShouldBe(800, 0.5);
        card.BorderThickness.ShouldBe(default);
        TestHelpers.ColorOf(card.Background).A.ShouldBe((byte)0);
        TestHelpers.Descendant<Border>(rig.Gate, "PART_List").BorderThickness.Top.ShouldBe(0);
        TestHelpers.Descendant<Border>(rig.Gate, "PART_FooterFrame").BorderThickness.Top.ShouldBe(0);
        // Rows keep their subtle dividers in both looks.
        rig.Rows[1].BorderThickness.Top.ShouldBe(1);
        rig.Rows[0].BorderThickness.Top.ShouldBe(0);
    }

    [AvaloniaFact]
    public void At_388_Or_Less_The_Side_Paddings_Tighten()
    {
        using var rig = new Rig(width: 389);
        var header = TestHelpers.Descendant<Grid>(rig.Gate, "PART_Header");
        rig.Gate.Classes.ShouldNotContain(ReleaseReadiness.CompactPseudoClass);
        header.Margin.Left.ShouldBe(20);
        rig.Rows[0].Padding.Left.ShouldBe(20);

        rig.Host.Width = 388;
        rig.Pump();
        rig.Gate.Classes.ShouldContain(ReleaseReadiness.CompactPseudoClass);
        header.Margin.ShouldBe(new Thickness(16, 16, 16, 12));
        TestHelpers.Descendant<StackPanel>(rig.Gate, "PART_Progress").Margin.ShouldBe(new Thickness(16, 0, 16, 16));
        TestHelpers.Descendant<Border>(rig.Gate, "PART_FooterFrame").Padding.ShouldBe(new Thickness(16, 12));
        rig.Status.Padding.ShouldBe(new Thickness(16, 0, 16, 14));
        rig.Rows[0].Padding.ShouldBe(new Thickness(16, 12));
        rig.Rows[0].Classes.ShouldContain(ReleaseReadinessRow.CompactPseudoClass);
    }

    [AvaloniaFact]
    public void A_Row_Puts_The_Hit_Area_Over_Its_Padding()
    {
        using var rig = new Rig();
        var row = rig.Rows[1];
        var box = row.Checkbox!;
        box.Margin.ShouldBe(new Thickness(-13, -12, 0, 0));
        var origin = box.TranslatePoint(default, row)!.Value;
        origin.X.ShouldBe(7, 0.01);
        origin.Y.ShouldBe(1, 0.01);
        // The label reaches the row's padding on the right.
        (box.Bounds.Right).ShouldBe(row.Bounds.Width - 20, 0.5);
    }

    [AvaloniaFact]
    public void The_Footer_Puts_The_Buttons_On_A_Second_Line_When_They_Do_Not_Fit()
    {
        var lead = new Border { Width = 160, Height = 44 };
        var trail = new Border { Width = 140, Height = 36 };
        var footer = new ReleaseReadinessFooter { Children = { lead, trail } };
        var host = new Border { Width = 400, Child = footer, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(host);
        try
        {
            window.Width = 600;
            Dispatcher.UIThread.RunJobs();
            footer.IsWrapped.ShouldBeFalse();
            footer.Bounds.Height.ShouldBe(44);
            trail.Bounds.ShouldBe(new Rect(400 - 140, 4, 140, 36));
            lead.Bounds.ShouldBe(new Rect(0, 0, 160, 44));

            // 160 + 16 + 140 = 316 does not fit in 315: the buttons take a line of their own, still at the end.
            host.Width = 315;
            Dispatcher.UIThread.RunJobs();
            footer.IsWrapped.ShouldBeTrue();
            footer.Bounds.Height.ShouldBe(44 + 12 + 36);
            trail.Bounds.ShouldBe(new Rect(315 - 140, 56, 140, 36));

            lead.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            footer.IsWrapped.ShouldBeFalse();
            trail.Bounds.X.ShouldBe(315 - 140);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Gate_Wraps_Its_Footer()
    {
        using var rig = new Rig(width: 220);
        rig.Footer.IsWrapped.ShouldBeTrue();
        var actions = TestHelpers.Descendant<StackPanel>(rig.Gate, "PART_Actions");
        var at = actions.TranslatePoint(default, rig.Footer)!.Value;
        at.Y.ShouldBe(rig.Notify.Bounds.Height + 12, 0.5);
        (at.X + actions.Bounds.Width).ShouldBe(rig.Footer.Bounds.Width, 0.5);
    }

    [AvaloniaFact]
    public void The_Bar_Turns_Green_When_Ready_And_The_Busy_Button_Dims()
    {
        var pending = new TaskCompletionSource();
        using var rig = new Rig(configure: g => g.ReleaseAction = (_, _) => pending.Task);
        var accent = ArcTokens.GetColor(rig.Gate, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Accent);
        var success = ArcTokens.GetColor(rig.Gate, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Success);
        var muted = ArcTokens.GetColor(rig.Gate, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceMuted);
        TestHelpers.ColorOf(rig.Bar.FillBrush).ShouldBe(accent);
        TestHelpers.ColorOf(rig.Bar.TrackBrush).ShouldBe(muted);
        rig.Bar.Bounds.Height.ShouldBe(4);
        rig.Bar.FillMode.ShouldBe(ProjectsProgressFill.Slide);
        rig.TickAll();
        TestHelpers.ColorOf(rig.Bar.FillBrush).ShouldBe(success);

        rig.Release.Opacity.ShouldBe(1);
        rig.Click(rig.Release);
        rig.Release.Opacity.ShouldBe(0.52, 0.001);
        pending.SetResult();
        rig.Pump();
        rig.Release.Opacity.ShouldBe(0.52, 0.001);
    }

    [AvaloniaFact]
    public void A_Row_Uses_The_Block_Type_And_Tints_On_Hover()
    {
        using var rig = new Rig();
        var row = rig.Rows[1];
        var title = TestHelpers.Descendant<TextBlock>(row, ReleaseReadinessRow.TitlePart);
        var description = TestHelpers.Descendant<TextBlock>(row, ReleaseReadinessRow.DescriptionPart);
        title.FontSize.ShouldBe(13);
        title.LineHeight.ShouldBe(18.2);
        title.FontWeight.ShouldBe(FontWeight.Medium);
        description.FontSize.ShouldBe(12);
        description.LineHeight.ShouldBe(16.8);
        description.Text.ShouldBe("Build 5.3.0-beta.2 passed on preview");
        TestHelpers.ColorOf(row.Background).A.ShouldBe((byte)0);

        rig.Pump();
        var point = row.TranslatePoint(new Point(row.Bounds.Width - 4, 4), rig.Window)!.Value;
        rig.Window.MouseMove(point);
        rig.Pump();
        row.IsPointerOver.ShouldBeTrue();
        TestHelpers.ColorOf(row.Background).A.ShouldBeGreaterThan((byte)0);
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void Parts_Have_Names_For_Screen_Readers()
    {
        using var rig = new Rig();
        var gate = rig.Gate;
        var owners = TestHelpers.Descendant<ReleaseReadinessOwners>(gate, ReleaseReadiness.OwnersPart);
        AutomationProperties.GetName(owners).ShouldBe("Owners: Rina Kato and Samir Aziz");
        ControlAutomationPeer.CreatePeerForElement(owners).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        var avatars = TestHelpers.Descendant<ProjectsAvatarStack>(gate, ReleaseReadiness.AvatarsPart);
        AutomationProperties.GetAccessibilityView(avatars).ShouldBe(AccessibilityView.Raw);
        avatars.Diameter.ShouldBe(28);
        avatars.Overlap.ShouldBe(6);
        avatars.Ring.ShouldBe(2);

        AutomationProperties.GetName(rig.Bar).ShouldBe("Readiness");
        var bar = ControlAutomationPeer.CreatePeerForElement(rig.Bar);
        bar.GetAutomationControlType().ShouldBe(AutomationControlType.ProgressBar);
        AutomationProperties.GetName(TestHelpers.Descendant<StackPanel>(gate, ReleaseReadiness.RowsPart)).ShouldBe("Release checks");

        AutomationProperties.GetName(rig.Box(1)).ShouldBe("Smoke tests green");
        AutomationProperties.GetHelpText(rig.Box(1)).ShouldBe("Build 5.3.0-beta.2 passed on preview");
        AutomationProperties.GetName(rig.Notify).ShouldBe("Notify #launches");
        rig.Notify.Content.ShouldBe("Notify #launches");
        AutomationProperties.GetName(rig.Release).ShouldBe("Start rollout");
        AutomationProperties.GetHeadingLevel(TestHelpers.Descendant<TextBlock>(gate, ReleaseReadiness.TitlePart)).ShouldBe(2);
        AutomationProperties.GetLiveSetting(gate.Announcer!).ShouldBe(AutomationLiveSetting.Polite);
        AutomationProperties.GetAccessibilityView(rig.Status).ShouldBe(AccessibilityView.Raw);

        gate.NotifyText = "Post in #launches";
        gate.OwnersLabel = "Release owners";
        gate.ChecksLabel = "Before the rollout";
        rig.Pump();
        AutomationProperties.GetName(rig.Notify).ShouldBe("Post in #launches");
        AutomationProperties.GetName(owners).ShouldBe("Release owners");
        AutomationProperties.GetName(TestHelpers.Descendant<StackPanel>(gate, ReleaseReadiness.RowsPart)).ShouldBe("Before the rollout");
    }

    [AvaloniaFact]
    public void Texts_Are_Properties()
    {
        using var rig = new Rig(configure: g =>
        {
            g.RemainingFormat = "{0} left";
            g.RemainingOneText = "one left";
            g.ReadyText = "Go";
            g.CountFormat = "{0}/{1}";
            g.ReleaseText = "Hand over";
            g.ReleasedText = "Handed over";
            g.ResetText = "Undo";
            g.ReleaseName = "Billing";
            g.SuccessNotifiedFormat = "{0} handed over; {1} knows.";
        });
        rig.Remaining.Text.ShouldBe("3 left");
        rig.Count.Text.ShouldBe("1/4");
        rig.Gate.Checks[1].IsDone = true;
        rig.Gate.Checks[2].IsDone = true;
        rig.Pump();
        rig.Remaining.Text.ShouldBe("one left");
        rig.TickAll();
        rig.Remaining.Text.ShouldBe("Go");
        AutomationProperties.GetName(rig.Release).ShouldBe("Hand over");
        rig.Click(rig.Release);
        AutomationProperties.GetName(rig.Release).ShouldBe("Handed over");
        rig.ResetButton.Content.ShouldBe("Undo");
        rig.Gate.StatusText.ShouldBe("Billing handed over; #launches knows.");
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void The_Bar_Springs_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full, clock: clock);
        rig.Bar.ShownFraction.ShouldBe(0.25, 0.001);
        rig.Gate.Checks[1].IsDone = true;
        rig.Pump();
        rig.Bar.ShownFraction.ShouldBe(0.25, 0.01);
        rig.Advance(TimeSpan.FromMilliseconds(100));
        rig.Bar.ShownFraction.ShouldBeInRange(0.3, 0.48);
        rig.Advance(TimeSpan.FromMilliseconds(700));
        rig.Bar.ShownFraction.ShouldBe(0.5, 0.003);
        // Critically damped: it never passes the target.
        rig.Gate.Checks[2].IsDone = true;
        var peak = 0.0;
        for (var i = 0; i < 60; i++)
        {
            rig.Advance(TimeSpan.FromMilliseconds(16));
            peak = Math.Max(peak, rig.Bar.ShownFraction);
        }
        peak.ShouldBeLessThanOrEqualTo(0.75 + 1e-6);
        // The counts swap in place, with nothing animated.
        rig.Remaining.Transitions.ShouldBeNull();
        rig.Count.Transitions.ShouldBeNull();
        rig.Remaining.RenderTransform.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Motion_Move_The_Bar_At_Once()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            using var rig = new Rig(level, clock: clock);
            rig.Gate.Checks[1].IsDone = true;
            rig.Pump();
            rig.Bar.ShownFraction.ShouldBe(0.5, 0.001, level.ToString());
        }
    }

    [AvaloniaFact]
    public void The_Status_Line_Opens_On_A_Spring_And_Fades_In()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full, clock: clock);
        rig.StatusHost.Bounds.Height.ShouldBe(0);
        rig.TickAll();
        rig.Click(rig.Release);
        rig.Advance(TimeSpan.FromMilliseconds(80));
        rig.StatusHost.Bounds.Height.ShouldBeInRange(1, 30);
        rig.Advance(TimeSpan.FromMilliseconds(900));
        rig.StatusHost.Bounds.Height.ShouldBe(rig.Status.DesiredSize.Height, 0.6);
        rig.Status.Transitions.ShouldNotBeNull();

        rig.Click(rig.ResetButton);
        rig.Advance(TimeSpan.FromMilliseconds(900));
        rig.StatusHost.Bounds.Height.ShouldBe(0, 0.5);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_The_Status_Line_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Reduced, clock: clock);
        rig.TickAll();
        rig.Click(rig.Release);
        rig.StatusHost.Bounds.Height.ShouldBeGreaterThan(20);
        rig.Status.Opacity.ShouldBe(1);
        rig.Status.Transitions.ShouldBeNull();
    }

    #endregion
}

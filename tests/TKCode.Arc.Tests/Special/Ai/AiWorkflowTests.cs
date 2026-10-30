using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The <see cref="AiWorkflow"/> card: a simulation (the gallery's support flow), test runs (simulated and through
/// <see cref="AiWorkflow.RunStep"/>), branches, failures and Retry, the add step menu and its keyboard model, removing steps,
/// the empty and loading states, autoplay, narrow layout, labels and the pure helpers.
/// </summary>
public class AiWorkflowTests
{
    private static (AiWorkflow Flow, Window Window, AgentPlanManualScheduler Clock) Open(AiWorkflow? flow = null, double width = 560)
    {
        flow ??= Simulated();
        flow.AutoPlay = false;
        var clock = new AgentPlanManualScheduler();
        flow.Scheduler = clock;
        flow.Width = width;
        flow.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = TestHelpers.Show(flow);
        window.Width = width + 80;
        window.Height = 1200;
        Pump();
        return (flow, window, clock);
    }

    /// <summary>A card playing the gallery's support flow.</summary>
    private static AiWorkflow Simulated() => new() { Simulation = AiWorkflowTestData.Simulation() };

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Advance(AgentPlanManualScheduler clock, double ms)
    {
        clock.Advance(TimeSpan.FromMilliseconds(ms));
        Pump();
    }

    private static void RunToEnd(AgentPlanManualScheduler clock)
    {
        for (var i = 0; i < 200 && clock.RunNext(); i++)
        {
            Pump();
        }
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static object? Focused(Window window) => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement();

    private static List<AiWorkflowStep> Linear(string? error = null) =>
    [
        new() { Id = "t", Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Mail, Title = "New email", Duration = TimeSpan.FromMilliseconds(100),
            Output = new AiWorkflowEmailOutput { From = "A", Subject = "S", Preview = "P" } },
        new() { Id = "a", Kind = AiWorkflowStepKind.Ai, App = AiWorkflowApp.Ai, Title = "Classify", Detail = "Label it", Activity = "Reading",
            Duration = TimeSpan.FromMilliseconds(200), Output = new AiWorkflowLabelOutput { Label = "Urgent", Confidence = 0.9, Reason = "one two three four five six seven" } },
        new() { Id = "c", Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Tracker, Title = "Create issue", Duration = TimeSpan.FromMilliseconds(700),
            Error = error, Output = new AiWorkflowRecordOutput { Id = "SUP-1", Title = "T" } }
    ];

    // Pure helpers ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(700, "700ms")]
    [InlineData(999.6, "1.0s")]
    [InlineData(1240, "1.2s")]
    [InlineData(65_000, "1m 05s")]
    public void Duration_Reads_Milliseconds_Tenths_Or_Minutes(double ms, string expected) =>
        Assert.Equal(expected, AiWorkflowFormat.Duration(TimeSpan.FromMilliseconds(ms)));

    [Fact]
    public void Filter_Leaves_Out_Triggers_And_A_Second_Branch_And_Matches_Every_Word()
    {
        var catalog = AiWorkflowTestData.Catalog;
        var all = AiWorkflowFormat.Filter(catalog, null, allowsBranch: true);
        Assert.DoesNotContain(all, i => i.Kind == AiWorkflowStepKind.Trigger);
        Assert.Contains(all, i => i.Kind == AiWorkflowStepKind.Branch);
        Assert.DoesNotContain(AiWorkflowFormat.Filter(catalog, null, allowsBranch: false), i => i.Kind == AiWorkflowStepKind.Branch);
        var issue = AiWorkflowFormat.Filter(catalog, "create TRACKER", allowsBranch: true);
        Assert.Single(issue);
        Assert.Equal("Create issue", issue[0].Title);
        Assert.Empty(AiWorkflowFormat.Filter(catalog, "nothing like this", allowsBranch: true));
    }

    [Fact]
    public void Add_Puts_Triggers_First_And_Other_Steps_At_The_End_Of_The_Visible_Path()
    {
        var steps = AiWorkflowTestData.Steps;
        var added = AiWorkflowFormat.Add(steps, new AiWorkflowStep { Id = "x", Title = "X" }, "other");
        Assert.Equal(3, added.Count);
        Assert.Equal("x", added[2].Paths[1].Steps[^1].Id);
        Assert.Equal(2, steps[2].Paths[1].Steps.Count); // the original is left alone
        var trigger = AiWorkflowFormat.Add(Linear(), new AiWorkflowStep { Id = "t2", Kind = AiWorkflowStepKind.Trigger }, null);
        Assert.Equal("t2", trigger[0].Id);
        var linear = AiWorkflowFormat.Add(Linear(), new AiWorkflowStep { Id = "y" }, null);
        Assert.Equal("y", linear[^1].Id);
    }

    [Fact]
    public void Remove_Finds_Steps_Inside_Paths_And_NewId_Avoids_Used_Ids()
    {
        var steps = AiWorkflowTestData.Steps;
        var removed = AiWorkflowFormat.Remove(steps, "issue");
        Assert.Single(removed[2].Paths[0].Steps);
        Assert.Equal("action-1", AiWorkflowFormat.NewId(steps, AiWorkflowStepKind.Action));
        steps.Add(new AiWorkflowStep { Id = "action-1" });
        Assert.Equal("action-2", AiWorkflowFormat.NewId(steps, AiWorkflowStepKind.Action));
    }

    [Fact]
    public void Next_Follows_The_Route_Into_A_Path()
    {
        var steps = AiWorkflowTestData.Steps;
        Assert.Equal("classify", AiWorkflowFormat.Next(steps, steps[0], null)!.Id);
        Assert.Equal("draft", AiWorkflowFormat.Next(steps, steps[2], "other")!.Id);
        Assert.Equal("issue", AiWorkflowFormat.Next(steps, steps[2].Paths[0].Steps[0], "urgent")!.Id);
        Assert.Null(AiWorkflowFormat.Next(steps, steps[2].Paths[0].Steps[1], "urgent"));
    }

    [Fact]
    public void App_Parse_Finds_Built_In_Apps_By_Id()
    {
        Assert.Same(AiWorkflowApp.Tracker, AiWorkflowApp.Parse("TRACKER"));
        var custom = AiWorkflowApp.Parse("Acme");
        Assert.Equal("Acme", custom.Name);
        Assert.Null(custom.Mark);
    }

    // The card --------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Simulation_Shows_Its_Flow_And_Says_It_Is_Simulated()
    {
        var (flow, window, _) = Open();
        try
        {
            Assert.True(flow.ShowsSimulation);
            Assert.Equal("Escalate urgent support email", flow.DisplayedTitle);
            Assert.Equal("Escalate urgent support email", AutomationProperties.GetName(flow));
            Assert.True(flow.SimulatedBadge!.IsVisible);
            Assert.NotNull(flow.RowOf("trigger"));
            Assert.NotNull(flow.RowOf("issue"));
            // The rail runs through the top-level rows and the shown path: trigger, classify, route, post, issue.
            Assert.Equal(["trigger", "classify", "route", "post", "issue"], flow.Rail!.Marks.Select(m => m.Row.Step.Id).ToArray());
            Assert.Equal("New email, Mail, Not run", AutomationProperties.GetName(flow.RowOf("trigger")!));
            Assert.Equal("Ready to test", flow.SummaryText!.Text);
            Assert.Equal(AiWorkflowMetrics.RowHeight, flow.RowOf("trigger")!.MarkCenterY * 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Steps_And_No_Simulation_The_Card_Shows_The_Empty_State()
    {
        var (flow, window, _) = Open(new AiWorkflow());
        try
        {
            Assert.False(flow.ShowsSimulation);
            Assert.Equal(string.Empty, flow.DisplayedTitle);
            Assert.True(flow.EmptyPanel!.IsVisible);
            Assert.False(flow.Rail!.IsVisible);
            Assert.False(flow.RunButton!.IsEnabled);
            Assert.False(flow.SimulatedBadge!.IsVisible);
            Assert.False(flow.AddButton!.IsVisible);
            Assert.Empty(flow.TriggerRows);
            Assert.Contains(":empty", flow.Classes);
            flow.AutoPlay = true;
            flow.SetOnScreen(true);
            Assert.False(flow.IsAutoPlayScheduled);
            flow.StartRun();
            Assert.False(flow.IsRunning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_On_Its_Own_While_On_Screen()
    {
        var (flow, window, clock) = Open();
        try
        {
            flow.AutoPlay = true;
            flow.SetOnScreen(true);
            Advance(clock, AiWorkflow.AutoPlayDelay.TotalMilliseconds);
            Assert.True(flow.IsRunning);
            Assert.Equal(AiWorkflowStepStatus.Running, flow.GetStepStatus("trigger"));
            Advance(clock, 6000);
            Assert.False(flow.IsRunning);
            Assert.Equal(AiWorkflowStepStatus.Done, flow.GetStepStatus("issue"));
            Assert.Equal(AiWorkflowStepStatus.Skipped, flow.GetStepStatus("draft"));
            Assert.Null(flow.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Own_Steps_Win_Over_The_Simulation()
    {
        var (flow, window, _) = Open(new AiWorkflow { Steps = Linear(), Simulation = AiWorkflowTestData.Simulation() });
        try
        {
            Assert.False(flow.ShowsSimulation);
            Assert.Equal(["t", "a", "c"], flow.Rail!.Marks.Select(m => m.Row.Step.Id).ToArray());
            Assert.Null(flow.RowOf("trigger"));
            // The simulation's name and catalog belong to its flow, not to the app's.
            Assert.Equal(string.Empty, flow.DisplayedTitle);
            Assert.False(flow.AddButton!.IsVisible);
            flow.Steps = null;
            Pump();
            Assert.True(flow.ShowsSimulation);
            Assert.NotNull(flow.RowOf("trigger"));
            Assert.Equal(AiWorkflowTestData.Title, flow.DisplayedTitle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Simulation_To_Null_Stops_The_Run_And_Shows_The_Empty_State()
    {
        var (flow, window, clock) = Open();
        try
        {
            flow.AutoPlay = true;
            flow.SetOnScreen(true);
            Advance(clock, AiWorkflow.AutoPlayDelay.TotalMilliseconds);
            Assert.True(flow.IsRunning);
            flow.Simulation = null;
            Pump();
            Assert.False(flow.IsRunning);
            Assert.False(flow.IsAutoPlayScheduled);
            Assert.True(flow.EmptyPanel!.IsVisible);
            Assert.Null(flow.RowOf("trigger"));
            Assert.Equal(string.Empty, flow.DisplayedTitle);
            Advance(clock, 10_000);
            Assert.False(flow.IsRunning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulated_Run_Goes_One_Step_At_A_Time_Takes_The_Route_And_Skips_The_Other_Path()
    {
        var (flow, window, clock) = Open();
        AiWorkflowRunCompletedEventArgs? done = null;
        flow.RunCompleted += (_, e) => done = e;
        try
        {
            flow.StartRun();
            Pump();
            Assert.True(flow.IsRunning);
            Assert.Equal(AiWorkflowStepStatus.Running, flow.GetStepStatus("trigger"));
            Assert.Equal(AiWorkflowStepStatus.Idle, flow.GetStepStatus("classify"));
            var trigger = flow.RowOf("trigger")!;
            Assert.True(trigger.DetailText.IsShimmering);
            Assert.Equal("Fetching the latest email", trigger.DetailText.Text);
            Assert.Equal("Stop", flow.RunButton!.Content is StackPanel p ? ((TextBlock)p.Children[1]).Text : null);

            Advance(clock, 420);
            Assert.Equal(AiWorkflowStepStatus.Done, flow.GetStepStatus("trigger"));
            Assert.True(trigger.OutputBox.IsExpanded);
            Assert.Equal("support@example.com", trigger.DetailText.Text);
            Assert.False(trigger.DetailText.IsShimmering);
            Assert.Equal("New email, Mail, Done in 420ms", AutomationProperties.GetName(trigger));

            // The gap, then the dot travels into the next step before it runs.
            Advance(clock, AiWorkflowRunner.Gap.TotalMilliseconds);
            Assert.Equal("classify", flow.Rail!.TravelingTo);
            Assert.Equal(AiWorkflowStepStatus.Idle, flow.GetStepStatus("classify"));
            Advance(clock, AiWorkflowRunner.TravelDuration.TotalMilliseconds);
            Assert.Equal(AiWorkflowStepStatus.Running, flow.GetStepStatus("classify"));
            Assert.Null(flow.Rail.TravelingTo);

            RunToEnd(clock);
            Assert.False(flow.IsRunning);
            Assert.Equal(AiWorkflowStepStatus.Done, flow.GetStepStatus("issue"));
            Assert.Equal(AiWorkflowStepStatus.Skipped, flow.GetStepStatus("draft"));
            Assert.Equal(AiWorkflowStepStatus.Skipped, flow.GetStepStatus("ticket"));
            Assert.Equal("urgent", flow.PathTabs.SelectedPathId);
            Assert.NotNull(done);
            Assert.Equal(AiWorkflowRunStatus.Passed, done!.Status);
            Assert.Null(done.FailedStepId);
            Assert.IsType<AiWorkflowRecordOutput>(done.Outputs["issue"]);
            Assert.Equal(5, done.Outputs.Count);
            Assert.StartsWith("Passed in ", flow.SummaryText!.Text);
            var said = flow.Announcer!.History;
            Assert.Equal("Test run started", said[0]);
            Assert.Contains(said, s => s.StartsWith("Route by label done in 180ms. Took the Urgent path", StringComparison.Ordinal));
            Assert.EndsWith("Simulated, nothing was sent.", said[^1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failing_Step_Shows_Its_Error_And_Retry_Resumes_From_It()
    {
        var (flow, window, clock) = Open(new AiWorkflow { Steps = Linear(error: "Token expired") });
        var results = new List<AiWorkflowRunCompletedEventArgs>();
        flow.RunCompleted += (_, e) => results.Add(e);
        try
        {
            flow.StartRun();
            RunToEnd(clock);
            Assert.Equal(AiWorkflowStepStatus.Failed, flow.GetStepStatus("c"));
            var row = flow.RowOf("c")!;
            Assert.True(row.ErrorBox.IsExpanded);
            Assert.True(row.RetryButton.IsEffectivelyVisible);
            Assert.Equal("Retry Create issue", AutomationProperties.GetName(row.RetryButton));
            Assert.Equal("Failed at Create issue", flow.SummaryText!.Text);
            Assert.Single(results);
            Assert.Equal(AiWorkflowRunStatus.Failed, results[0].Status);
            Assert.Equal("c", results[0].FailedStepId);
            Assert.Contains("Create issue failed: Token expired", flow.Announcer!.History);

            // Retry runs only the failed step again (earlier steps keep their results).
            row.RetryButton.Focus();
            Press(window, PhysicalKey.Enter);
            Assert.True(flow.IsRunning);
            Assert.Equal(AiWorkflowStepStatus.Done, flow.GetStepStatus("a"));
            Assert.Equal(AiWorkflowStepStatus.Running, flow.GetStepStatus("c"));
            RunToEnd(clock);
            Assert.Equal(AiWorkflowStepStatus.Done, flow.GetStepStatus("c"));
            Assert.Equal(2, results.Count);
            Assert.Equal(AiWorkflowRunStatus.Passed, results[1].Status);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RunStep_Runs_Each_Step_With_The_Previous_Outputs_And_A_Thrown_Error_Fails_It()
    {
        var calls = new List<(string Id, int Previous, int Attempt)>();
        var flow = new AiWorkflow
        {
            Steps = Linear(),
            RunStep = (step, context) =>
            {
                calls.Add((step.Id, context.Previous.Count, context.Attempt));
                if (step.Id == "c" && context.Attempt == 1)
                {
                    throw new InvalidOperationException("Quota reached");
                }
                return Task.FromResult<AiWorkflowOutput?>(new AiWorkflowTextOutput { Text = "ran " + step.Id });
            }
        };
        var (_, window, clock) = Open(flow);
        try
        {
            Assert.False(flow.SimulatedBadge!.IsVisible);
            flow.StartRun();
            RunToEnd(clock);
            Assert.Equal(["t", "a", "c"], calls.Select(c => c.Id).ToArray());
            Assert.Equal([0, 1, 2], calls.Select(c => c.Previous).ToArray());
            Assert.Equal(AiWorkflowStepStatus.Failed, flow.GetStepStatus("c"));
            Assert.Equal("Quota reached", flow.Runner.Peek("c")!.Error);
            flow.Retry();
            RunToEnd(clock);
            Assert.Equal(2, calls[^1].Attempt);
            Assert.Equal(AiWorkflowStepStatus.Done, flow.GetStepStatus("c"));
            Assert.Equal("ran c", ((AiWorkflowTextOutput)flow.GetStepOutput("c")!).Text);
            Assert.DoesNotContain(flow.Announcer!.History, s => s.Contains("Simulated", StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Cancels_The_Running_Step_And_Puts_It_Back()
    {
        CancellationToken token = default;
        var pending = new TaskCompletionSource<AiWorkflowOutput?>();
        var flow = new AiWorkflow
        {
            Steps = Linear(),
            RunStep = (_, context) =>
            {
                token = context.Cancellation;
                return pending.Task;
            }
        };
        var (_, window, _) = Open(flow);
        AiWorkflowRunCompletedEventArgs? done = null;
        flow.RunCompleted += (_, e) => done = e;
        try
        {
            flow.RunButton!.Focus();
            Press(window, PhysicalKey.Enter);
            Assert.True(flow.IsRunning);
            Press(window, PhysicalKey.Enter);
            Assert.False(flow.IsRunning);
            Assert.True(token.IsCancellationRequested);
            Assert.Equal(AiWorkflowStepStatus.Idle, flow.GetStepStatus("t"));
            Assert.Equal("Stopped", flow.SummaryText!.Text);
            Assert.Null(done);
            Assert.Equal("Test run stopped.", flow.Announcer!.History[^1]);
            pending.SetResult(null);
            Pump();
            Assert.Equal(AiWorkflowStepStatus.Idle, flow.GetStepStatus("t"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Path_Tabs_Are_A_Radio_Group_With_Roving_Focus()
    {
        var (flow, window, _) = Open();
        try
        {
            var tabs = flow.PathTabs.Tabs;
            Assert.Equal(2, tabs.Count);
            Assert.True(tabs[0].IsSelected);
            Assert.True(tabs[0].IsTabStop);
            Assert.False(tabs[1].IsTabStop);
            tabs[0].Focus();
            Press(window, PhysicalKey.ArrowRight);
            Assert.Equal("other", flow.PathTabs.SelectedPathId);
            Assert.Same(tabs[1], Focused(window));
            Assert.Equal(["trigger", "classify", "route", "draft", "ticket"], flow.Rail!.Marks.Select(m => m.Row.Step.Id).ToArray());
            Press(window, PhysicalKey.Home);
            Assert.Equal("urgent", flow.PathTabs.SelectedPathId);
            Press(window, PhysicalKey.End);
            Assert.Equal("other", flow.PathTabs.SelectedPathId);
            flow.SelectPath("urgent");
            Assert.True(tabs[0].IsSelected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Add_Menu_Filters_Moves_And_Adds_With_Enter_At_The_End_Of_The_Visible_Path()
    {
        var (flow, window, _) = Open();
        var changes = new List<AiWorkflowStepsChangedEventArgs>();
        flow.StepsChanged += (_, e) => changes.Add(e);
        try
        {
            flow.AddButton!.Focus();
            Press(window, PhysicalKey.Enter);
            var menu = flow.Menu!;
            Assert.True(menu.IsOpen);
            Pump();
            Assert.Same(menu.Search, Focused(window) is Visual c ? c.FindAncestorOfType<Input>(includeSelf: true) : null);
            // No triggers, and no second branch.
            Assert.DoesNotContain(menu.Rows, r => r.Item.Kind is AiWorkflowStepKind.Trigger or AiWorkflowStepKind.Branch);
            Assert.Equal(1, menu.Rows[0].Shortcut);
            Assert.Equal(0, menu.HighlightIndex);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(1, menu.HighlightIndex);
            Press(window, PhysicalKey.ArrowUp);
            Press(window, PhysicalKey.ArrowUp);
            Assert.Equal(menu.Rows.Count - 1, menu.HighlightIndex);

            menu.Search.Text = "tracker";
            Pump();
            Assert.Single(menu.Rows);
            Assert.Null(menu.Rows[0].Shortcut);
            Press(window, PhysicalKey.Enter);
            Assert.False(menu.IsOpen);
            Assert.Single(changes);
            Assert.Equal(AiWorkflowChangeKind.Added, changes[0].Kind);
            Assert.Equal("Create issue", changes[0].Step.Title);
            Assert.NotNull(flow.Steps);
            var urgent = flow.Steps![2].Paths[0].Steps;
            Assert.Equal(changes[0].Step.Id, urgent[^1].Id);
            Assert.NotNull(flow.RowOf(changes[0].Step.Id));
            Assert.Equal("Added Create issue", flow.Announcer!.History[^1]);
            // The edited flow is the simulation's: its name and catalog still apply.
            Assert.False(flow.ShowsSimulation);
            Assert.Equal(AiWorkflowTestData.Title, flow.DisplayedTitle);
            Assert.True(flow.AddButton.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Digit_Adds_The_Step_With_That_Shortcut_And_Escape_Returns_Focus()
    {
        var (flow, window, _) = Open(new AiWorkflow { Steps = Linear(), Catalog = AiWorkflowTestData.Catalog });
        try
        {
            flow.OpenAddMenu();
            Pump();
            var menu = flow.Menu!;
            var third = menu.Rows[2].Item;
            Press(window, PhysicalKey.Digit3);
            Assert.False(menu.IsOpen);
            Assert.Equal(third.Title, flow.Steps![^1].Title);
            Assert.Same(flow.AddButton, Focused(window));

            flow.OpenAddMenu();
            Pump();
            Assert.True(menu.IsOpen);
            Assert.Equal(string.Empty, menu.Search.Text);
            Press(window, PhysicalKey.Escape);
            Assert.False(menu.IsOpen);
            Assert.Same(flow.AddButton, Focused(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Remove_Takes_A_Step_Out_But_Never_The_Trigger()
    {
        var (flow, window, _) = Open(new AiWorkflow { Steps = Linear() });
        var changes = new List<AiWorkflowStepsChangedEventArgs>();
        flow.StepsChanged += (_, e) => changes.Add(e);
        try
        {
            var trigger = flow.RowOf("t")!;
            Assert.False(trigger.RemoveButton.IsVisible);
            var row = flow.RowOf("a")!;
            Assert.True(row.RemoveButton.IsVisible);
            Assert.Equal("Remove Classify", AutomationProperties.GetName(row.RemoveButton));
            row.RemoveButton.Focus();
            Press(window, PhysicalKey.Enter);
            Assert.Single(changes);
            Assert.Equal(AiWorkflowChangeKind.Removed, changes[0].Kind);
            Assert.Equal(["t", "c"], flow.Steps!.Select(s => s.Id).ToArray());
            Assert.Null(flow.RowOf("a"));
            Assert.Same(flow.RowOf("c")!.RemoveButton, Focused(window));
            Assert.False(flow.RemoveStep("t"));
            Assert.Equal("Removed Classify", flow.Announcer!.History[^1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Remove_Buttons_Are_Disabled_During_A_Run_And_Hidden_When_Not_Editable()
    {
        var (flow, window, _) = Open(new AiWorkflow { Steps = Linear() });
        try
        {
            flow.StartRun();
            Pump();
            Assert.False(flow.RowOf("a")!.RemoveButton.IsEnabled);
            Assert.False(flow.RemoveStep("a"));
            flow.StopRun();
            Pump();
            Assert.True(flow.RowOf("a")!.RemoveButton.IsEnabled);
            flow.IsEditable = false;
            Pump();
            Assert.False(flow.RowOf("a")!.RemoveButton.IsVisible);
            Assert.False(flow.AddButton!.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Empty_State_Lists_The_Triggers_And_Picking_One_Starts_The_Flow()
    {
        var (flow, window, _) = Open(new AiWorkflow { Steps = new List<AiWorkflowStep>(), Catalog = AiWorkflowTestData.Catalog });
        try
        {
            Assert.True(flow.EmptyPanel!.IsVisible);
            Assert.False(flow.Rail!.IsVisible);
            Assert.False(flow.RunButton!.IsEnabled);
            Assert.Equal(4, flow.TriggerRows.Count);
            Assert.All(flow.TriggerRows, r => Assert.Equal(AiWorkflowStepKind.Trigger, r.Item.Kind));
            flow.TriggerRows[1].Focus();
            Press(window, PhysicalKey.Enter);
            Assert.Single(flow.Steps!);
            Assert.Equal("On a schedule", flow.Steps![0].Title);
            Assert.False(flow.EmptyPanel.IsVisible);
            Assert.True(flow.RunButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_Three_Skeleton_Steps_And_Disables_The_Run()
    {
        var flow0 = Simulated();
        flow0.IsLoading = true;
        var (flow, window, _) = Open(flow0);
        try
        {
            Assert.True(flow.SkeletonPanel!.IsVisible);
            Assert.Equal(3, flow.SkeletonPanel.Children.Count);
            Assert.False(flow.Rail!.IsVisible);
            Assert.False(flow.RunButton!.IsEnabled);
            Assert.False(flow.AddButton!.IsVisible);
            Assert.Equal("Loading workflow", AutomationProperties.GetName(flow));
            flow.StartRun();
            Assert.False(flow.IsRunning);
            flow.IsLoading = false;
            Pump();
            Assert.True(flow.Rail.IsVisible);
            Assert.True(flow.RunButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_480px_Rows_Grow_And_The_Header_Wraps()
    {
        var (flow, window, _) = Open(width: 400);
        try
        {
            Assert.True(flow.IsNarrow);
            Assert.Equal(AiWorkflowMetrics.NarrowRowHeight / 2, flow.RowOf("trigger")!.MarkCenterY);
            Assert.Contains(":narrow", flow.Classes);
            flow.Width = 600;
            Pump();
            Assert.False(flow.IsNarrow);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Starts_On_Screen_Repeats_And_Stops_For_Good_After_A_Press()
    {
        var (flow, window, clock) = Open();
        try
        {
            flow.AutoPlay = true;
            flow.SetOnScreen(true);
            Assert.True(flow.IsAutoPlayScheduled);
            Advance(clock, AiWorkflow.AutoPlayDelay.TotalMilliseconds);
            Assert.True(flow.IsRunning);
            Advance(clock, 6000);
            // The run has passed; after a pause it plays again.
            Assert.False(flow.IsRunning);
            Assert.Equal(AiWorkflowRunStatus.Passed, flow.Runner.LastStatus);
            Assert.True(flow.IsAutoPlayScheduled);
            Advance(clock, AiWorkflow.AutoPlayPause.TotalMilliseconds);
            Assert.True(flow.IsRunning);

            // Off screen the autoplay run does not play unseen; it starts again once the card is back.
            flow.SetOnScreen(false);
            Assert.False(flow.IsAutoPlayScheduled);
            Assert.False(flow.IsRunning);
            flow.SetOnScreen(true);
            Assert.True(flow.IsAutoPlayScheduled);

            var inside = flow.TranslatePoint(new Point(40, 20), window)!.Value;
            window.MouseDown(inside, MouseButton.Left);
            window.MouseUp(inside, MouseButton.Left);
            Pump();
            Assert.True(flow.HasInteracted);
            Assert.False(flow.IsAutoPlayScheduled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Card()
    {
        var (flow, window, _) = Open(new AiWorkflow
        {
            Steps = Linear(),
            Labels = new AiWorkflowLabels { TestRun = "Probar", AddStep = "Añadir paso", Ready = "Listo", Remove = t => "Quitar " + t }
        });
        try
        {
            Assert.Equal("Probar", AutomationProperties.GetName(flow.RunButton!));
            Assert.Equal("Añadir paso", AutomationProperties.GetName(flow.AddButton!));
            Assert.Equal("Listo", flow.SummaryText!.Text);
            Assert.Equal("Quitar Classify", AutomationProperties.GetName(flow.RowOf("a")!.RemoveButton));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replacing_The_Steps_Updates_Rows_By_Id()
    {
        var (flow, window, _) = Open(new AiWorkflow { Steps = Linear() });
        try
        {
            var row = flow.RowOf("a");
            var renamed = Linear();
            renamed[1].Title = "Classify again";
            flow.Steps = renamed;
            Pump();
            Assert.Same(row, flow.RowOf("a"));
            Assert.Equal("Classify again", flow.RowOf("a")!.Step.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Full_Motion_The_Dot_Glides_Outputs_Open_On_A_Spring_And_Paths_Crossfade()
    {
        var frames = new ManualFrameClock();
        using var _ = frames.Install();
        var flow = new AiWorkflow
        {
            Simulation = AiWorkflowTestData.Simulation(), AutoPlay = false, Width = 560,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var clock = new AgentPlanManualScheduler();
        flow.Scheduler = clock;
        var window = TestHelpers.Show(flow, ArcMotionLevel.Full);
        window.Width = 640;
        window.Height = 1400;
        Pump();
        var frame = TimeSpan.FromMilliseconds(1000.0 / 60);
        void Run(double ms)
        {
            for (var i = 0; i < (int)Math.Round(ms / frame.TotalMilliseconds); i++)
            {
                frames.Advance(frame, frame);
                Pump();
            }
        }
        try
        {
            flow.StartRun();
            Pump();
            Advance(clock, 420);
            var trigger = flow.RowOf("trigger")!;
            Assert.True(trigger.OutputBox.IsExpanded);
            Run(50);
            Assert.True(trigger.OutputBox.IsMoving);
            var opening = trigger.OutputBox.DisplayedHeight;
            Run(500);
            Assert.False(trigger.OutputBox.IsMoving);
            Assert.True(trigger.OutputBox.DisplayedHeight > opening);

            // The gap, then the dot travels down the connector into the next step.
            Advance(clock, AiWorkflowRunner.Gap.TotalMilliseconds);
            Assert.Equal("classify", flow.Rail!.TravelingTo);
            Run(120);
            Assert.InRange(flow.Rail.TravelProgress, 0.05, 0.95);
            Run(200);
            Assert.Equal(1, flow.Rail.TravelProgress, 3);
            Advance(clock, AiWorkflowRunner.TravelDuration.TotalMilliseconds);
            Assert.Null(flow.Rail.TravelingTo);
            Assert.Equal(AiWorkflowStepStatus.Running, flow.GetStepStatus("classify"));

            // Switching paths crossfades the lists; the leaving one goes once it has faded.
            flow.StopRun();
            flow.SelectPath("other");
            Pump();
            Assert.NotNull(flow.PathSwap.Leaving);
            Run(400);
            Assert.Null(flow.PathSwap.Leaving);
            Assert.Equal("other", flow.PathTabs.SelectedPathId);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>
/// The invented fixture these tests play: a copy of the gallery's AiWorkflow demo data with invented model names, so
/// the tests do not depend on the gallery's content.
/// </summary>
internal static class AiWorkflowTestData
{
    public const string Title = "Escalate urgent support email";

    public const string Description = "Runs on every new email to support@example.com";

    /// <summary>
    /// The app of the model steps: Anthropic, whose Claude models label, draft and summarize here. The mark is a neutral
    /// sparkle in the accent; no brand mark is drawn.
    /// </summary>
    public static AiWorkflowApp Claude { get; } =
        new("vendor", "Vendor", ArcIcons.Sparkles, isAccent: true);

    private static string Classifier => "Model A";

    private static string Writer => "Model B";

    /// <summary>The sample flow, a fresh copy on every read (a demo may change its copy).</summary>
    public static List<AiWorkflowStep> Steps =>
    [
        new()
        {
            Id = "trigger",
            Kind = AiWorkflowStepKind.Trigger,
            App = AiWorkflowApp.Mail,
            Title = "New email",
            Detail = "support@example.com",
            Activity = "Fetching the latest email",
            Duration = TimeSpan.FromMilliseconds(420),
            Output = new AiWorkflowEmailOutput
            {
                From = "Dana Whitfield",
                Address = "dana@contoso.example",
                Subject = "Checkout fails with error 502 since this morning",
                Preview = "Hi team, since about 9:00 every checkout on our store ends in a 502 error. Customers are writing to us and we are losing orders. Can someone look at this today?"
            }
        },
        new()
        {
            Id = "classify",
            Kind = AiWorkflowStepKind.Ai,
            App = Claude,
            Title = "Classify with " + Classifier,
            Detail = "Label urgency and topic",
            Activity = "Reading the email and choosing a label",
            Duration = TimeSpan.FromMilliseconds(1240),
            Output = new AiWorkflowLabelOutput
            {
                Label = "Urgent",
                Confidence = 0.94,
                Reason = "Checkout fails for every customer since this morning, which stops orders, and the sender asks for a fix today."
            }
        },
        new()
        {
            Id = "route",
            Kind = AiWorkflowStepKind.Branch,
            App = AiWorkflowApp.Branch,
            Title = "Route by label",
            Detail = "If the label is Urgent",
            Activity = "Checking the label",
            Duration = TimeSpan.FromMilliseconds(180),
            Output = new AiWorkflowRouteOutput { PathId = "urgent", Note = "Label is Urgent" },
            Paths =
            [
                new()
                {
                    Id = "urgent",
                    Label = "Urgent",
                    Steps =
                    [
                        new()
                        {
                            Id = "post",
                            Kind = AiWorkflowStepKind.Action,
                            App = AiWorkflowApp.Chat,
                            Title = "Post message",
                            Detail = "#support",
                            Activity = "Posting to #support",
                            Duration = TimeSpan.FromMilliseconds(560),
                            Output = new AiWorkflowMessageOutput
                            {
                                Channel = "#support",
                                Author = "Workflow bot",
                                Text = "Urgent: checkout fails with error 502 for every customer since 9:00. Dana Whitfield (Contoso) is waiting for an update."
                            }
                        },
                        new()
                        {
                            Id = "issue",
                            Kind = AiWorkflowStepKind.Action,
                            App = AiWorkflowApp.Tracker,
                            Title = "Create issue",
                            Detail = "Support team",
                            Activity = "Creating the issue",
                            Duration = TimeSpan.FromMilliseconds(700),
                            Output = new AiWorkflowRecordOutput
                            {
                                Id = "SUP-1042",
                                Title = "Checkout fails with error 502",
                                Meta = "Urgent, assigned to On call"
                            }
                        }
                    ]
                },
                new()
                {
                    Id = "other",
                    Label = "Everything else",
                    Steps =
                    [
                        new()
                        {
                            Id = "draft",
                            Kind = AiWorkflowStepKind.Ai,
                            App = Claude,
                            Title = "Draft a reply",
                            Detail = Writer,
                            Activity = "Writing a reply",
                            Duration = TimeSpan.FromMilliseconds(980),
                            Output = new AiWorkflowTextOutput
                            {
                                Text = "Thanks for writing in. We have your message and someone from the team will get back to you within one business day."
                            }
                        },
                        new()
                        {
                            Id = "ticket",
                            Kind = AiWorkflowStepKind.Action,
                            App = AiWorkflowApp.Helpdesk,
                            Title = "Open ticket",
                            Detail = "Normal priority",
                            Activity = "Opening the ticket",
                            Duration = TimeSpan.FromMilliseconds(520),
                            Output = new AiWorkflowRecordOutput { Id = "#4821", Title = "Question from Dana Whitfield", Meta = "Normal priority" }
                        }
                    ]
                }
            ]
        }
    ];

    /// <summary>The add step catalog (four triggers, two model steps, six actions and a branch), a fresh copy on every read.</summary>
    public static List<AiWorkflowCatalogItem> Catalog =>
    [
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Mail, Title = "New email", Detail = "support@example.com",
            Activity = "Fetching the latest email", Duration = TimeSpan.FromMilliseconds(420),
            Output = new AiWorkflowEmailOutput
            {
                From = "Dana Whitfield", Address = "dana@contoso.example", Subject = "Checkout fails with error 502 since this morning",
                Preview = "Hi team, since about 9:00 every checkout on our store ends in a 502 error."
            }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Schedule, Title = "On a schedule", Detail = "Weekdays at 9:00",
            Activity = "Waiting for 9:00", Duration = TimeSpan.FromMilliseconds(300),
            Output = new AiWorkflowTextOutput { Text = "Started at 9:00 on Monday." }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Payments, Title = "Payment failed", Detail = "Any customer",
            Activity = "Fetching the latest event", Duration = TimeSpan.FromMilliseconds(380),
            Output = new AiWorkflowRecordOutput { Id = "evt_8f2k", Title = "Card declined for Fabrikam", Meta = "Sample amount 120.00" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Webhook, Title = "Webhook received", Detail = "POST /hooks/support",
            Activity = "Waiting for a request", Duration = TimeSpan.FromMilliseconds(260),
            Output = new AiWorkflowTextOutput { Text = "{ \"type\": \"ticket.created\", \"id\": 4821 }" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Ai, App = Claude, Title = "Summarize with " + Classifier, Detail = "Three bullet points",
            Activity = "Summarizing", Duration = TimeSpan.FromMilliseconds(1100),
            Output = new AiWorkflowTextOutput { Text = "Checkout fails with a 502 for every customer. It started around 9:00. The sender asks for a fix today." }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Ai, App = Claude, Title = "Extract fields with " + Writer, Detail = "Order number, company",
            Activity = "Extracting fields", Duration = TimeSpan.FromMilliseconds(860),
            Output = new AiWorkflowRecordOutput { Id = "fields", Title = "Company: Contoso, order: none given", Meta = "2 of 2 fields found" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Chat, Title = "Post message", Detail = "#support",
            Activity = "Posting to #support", Duration = TimeSpan.FromMilliseconds(560),
            Output = new AiWorkflowMessageOutput { Channel = "#support", Author = "Workflow bot", Text = "New email from Dana Whitfield: checkout fails with error 502." }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Tracker, Title = "Create issue", Detail = "Support team",
            Activity = "Creating the issue", Duration = TimeSpan.FromMilliseconds(700),
            Output = new AiWorkflowRecordOutput { Id = "SUP-1043", Title = "Checkout fails with error 502", Meta = "Assigned to On call" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Docs, Title = "Create page", Detail = "Incident notes",
            Activity = "Creating the page", Duration = TimeSpan.FromMilliseconds(640),
            Output = new AiWorkflowRecordOutput { Id = "Incident notes", Title = "Checkout 502, morning of the incident", Meta = "In Support / Incidents" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Sheets, Title = "Add row", Detail = "Escalations log",
            Activity = "Adding a row", Duration = TimeSpan.FromMilliseconds(480),
            Output = new AiWorkflowRecordOutput { Id = "Row 128", Title = "Contoso, Urgent, checkout", Meta = "Escalations log" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Crm, Title = "Update contact", Detail = "Add a note",
            Activity = "Updating Dana Whitfield", Duration = TimeSpan.FromMilliseconds(520),
            Output = new AiWorkflowRecordOutput { Id = "Dana Whitfield", Title = "Note added: checkout incident", Meta = "Contoso" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Repo, Title = "Open issue", Detail = "web-store",
            Activity = "Opening the issue", Duration = TimeSpan.FromMilliseconds(620),
            Output = new AiWorkflowRecordOutput { Id = "#982", Title = "502 on checkout", Meta = "web-store" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Branch, App = AiWorkflowApp.Branch, Title = "Branch", Detail = "Split by a condition",
            Activity = "Checking the condition", Duration = TimeSpan.FromMilliseconds(180),
            Output = new AiWorkflowRouteOutput { PathId = "yes", Note = "Condition met" },
            Paths = [new() { Id = "yes", Label = "Yes" }, new() { Id = "no", Label = "No" }]
        }
    ];

    /// <summary>A simulation of the sample flow with its catalog (the code-behind demos use it; the XAML ones spell it out).</summary>
    public static AiWorkflowSimulation Simulation() => new()
    {
        Title = Title,
        Description = Description,
        Steps = Steps,
        Catalog = Catalog,
    };
}

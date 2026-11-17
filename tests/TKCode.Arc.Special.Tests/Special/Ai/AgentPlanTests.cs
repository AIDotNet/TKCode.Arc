using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
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
/// The invented plan the agent plan tests simulate (the package ships none): a coding agent adding full-text search to a
/// help center. Six drafted steps, three suggestions, a step the agent inserts while running, a test step that fails once,
/// and a summary.
/// </summary>
internal static class AgentPlanTestScript
{
    public const string Goal = "Add full-text search to the help center";

    public const string Summary = "Search now covers all 31 article fields, with a disk fallback and 14 passing tests.";

    public static AgentModel Model { get; } = new("Lumen 2 Pro", ArcIcons.Sparkles);

    /// <summary>The six drafted steps with their scripts, in order.</summary>
    public static IReadOnlyList<AgentPlanScriptedStep> Steps { get; } =
    [
        new("inventory", "Inventory the article fields the help center stores",
            new(["Read 5 files", "Searched for articleSchema and draftSchema", "Found 31 article fields"], TimeSpan.FromSeconds(23))),
        new("index", "Build a search index from every published article",
            new(["Read jobs/publish.ts", "Created search/build-index.ts", "Edited 3 files"], TimeSpan.FromSeconds(46))),
        new("cache", "Cache the index in object storage for ten minutes",
            new(["Read lib/storage.ts", "Edited search/build-index.ts", "Ran 7 tests"], TimeSpan.FromSeconds(35))
            {
                Inserts = new("fallback", "Rebuild the index from disk when the cache is empty",
                    new(["Edited lib/storage.ts", "Ran 5 tests"], TimeSpan.FromSeconds(31))),
                InsertAfterActivity = 1
            }),
        new("rank", "Rank results by title match, then by freshness",
            new(["Edited search/rank.ts", "Ran 9 tests"], TimeSpan.FromSeconds(26))),
        new("tests", "Write tests for typos, stop words and empty queries",
            new(["Created search.test.ts", "Ran 14 tests"], TimeSpan.FromSeconds(67))
            {
                FailureReason = "3 of 14 tests failed: typo matching skips two-letter words",
                RetryActivities = ["Edited search/rank.ts", "Ran 14 tests, all passed"],
                RetryDuration = TimeSpan.FromSeconds(67)
            }),
        new("docs", "Describe the search syntax in the help center guide",
            new(["Read guide/overview.md", "Edited guide/search.md"], TimeSpan.FromSeconds(18)))
    ];

    /// <summary>The three steps Keep planning suggests, in order; each goes in before the last step.</summary>
    public static IReadOnlyList<AgentPlanScriptedStep> Suggestions { get; } =
    [
        new("highlight", "Highlight matched words in the result snippets",
            new(["Read search/snippet.ts", "Edited search/snippet.ts", "Ran 4 tests"], TimeSpan.FromSeconds(33))),
        new("zero-results", "Log searches that return no results",
            new(["Read lib/telemetry.ts", "Edited search/handler.ts"], TimeSpan.FromSeconds(21))),
        new("benchmark", "Benchmark search at 500 queries per second",
            new(["Ran a load script against staging for 60 seconds", "Measured 38 ms p99 at 500 queries per second"], TimeSpan.FromSeconds(69)))
    ];

    /// <summary>How the simulation runs a step it has no script for (one you added or renamed into a new id).</summary>
    public static AgentPlanStepScript GenericScript { get; } =
        new(["Read 2 files", "Edited 3 files", "Ran 5 tests"], TimeSpan.FromSeconds(30));

    /// <summary>A simulation of this plan on <paramref name="scheduler"/>.</summary>
    public static AgentPlanSimulation Simulation(IAgentPlanScheduler scheduler) => new(scheduler)
    {
        Goal = Goal,
        Model = Model,
        Steps = Steps,
        Suggestions = Suggestions,
        GenericScript = GenericScript,
        Summary = Summary
    };
}

/// <summary>The <see cref="AgentPlan"/> card: draft editing and its keyboard model, focus moves, controlled mode, the simulation, announcements.</summary>
public class AgentPlanTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static AgentPlan Controlled(AgentPlanStatus status = AgentPlanStatus.Draft, int count = 4)
    {
        var plan = new AgentPlan { Status = status, Goal = "Ship the report", Model = new AgentModel("Test model") };
        for (var i = 1; i <= count; i++)
        {
            plan.Steps.Add(new AgentPlanStep("s" + i, "Step title " + i));
        }
        return plan;
    }

    private static Window Open(AgentPlan plan, double width = 560, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        plan.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        plan.Width = width;
        var window = TestHelpers.Show(plan, level);
        window.Width = width + 80;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static object? Focused(Window window) => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement();

    private static IReadOnlyList<AgentPlanRow> Rows(AgentPlan plan) => plan.Rows.ToList();

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Titles(AgentPlan plan) => plan.Steps.Select(s => s.Title).ToArray();

    [AvaloniaFact]
    public void With_No_Data_And_No_Simulation_The_Plan_Is_An_Empty_Draft()
    {
        var plan = new AgentPlan();
        var window = Open(plan);
        try
        {
            Assert.Null(plan.Simulation);
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
            Assert.Empty(plan.Steps);
            Assert.Empty(Rows(plan));
            Assert.True(plan.Classes.Contains(":empty"));
            Assert.Equal(new AgentPlanLabels().EmptyPlan, plan.EmptyText.Text);
            Assert.Null(plan.EffectiveModel);
            Assert.Null(plan.EffectiveGoal);
            Assert.False(plan.ApproveButton.IsEnabled);
            var requested = 0;
            plan.KeepPlanningRequested += (_, _) => requested++;
            plan.KeepPlanning();
            Assert.Equal(1, requested);
            Assert.Empty(plan.Steps);
            Assert.False(plan.IsPlanning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Fills_An_Empty_Plan_With_Its_Draft()
    {
        var plan = new AgentPlan { Simulation = AgentPlanTestScript.Simulation(new AgentPlanManualScheduler()) };
        var window = Open(plan);
        try
        {
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
            Assert.Equal(6, plan.Steps.Count);
            Assert.Equal(6, Rows(plan).Count);
            Assert.Equal("Lumen 2 Pro plan: Add full-text search to the help center", AutomationProperties.GetName(plan));
            Assert.Equal("6", plan.HeadingCount.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Data_Wins_Over_The_Simulation()
    {
        var clock = new AgentPlanManualScheduler();
        var simulation = AgentPlanTestScript.Simulation(clock);
        var plan = Controlled();
        plan.Simulation = simulation;
        var window = Open(plan);
        try
        {
            Assert.False(simulation.IsAttached, "a plan with a status does not run the simulation");
            Assert.Equal(4, plan.Steps.Count);
            Assert.Equal("Test model plan: Ship the report", AutomationProperties.GetName(plan));
            plan.Approve();
            clock.Advance(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
            Assert.All(plan.Steps, s => Assert.Equal(AgentPlanStepState.Pending, s.State));

            // Without its own status the plan hands its steps to the simulation; its own goal and model still win.
            plan.Status = null;
            Assert.True(simulation.IsAttached);
            Assert.Equal(4, plan.Steps.Count);
            Assert.Equal("Test model plan: Ship the report", AutomationProperties.GetName(plan));
            plan.Goal = null;
            plan.Model = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Lumen 2 Pro plan: Add full-text search to the help center", AutomationProperties.GetName(plan));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_The_Simulation_Stops_It_And_Takes_Its_Draft_Away()
    {
        var clock = new AgentPlanManualScheduler();
        var simulation = AgentPlanTestScript.Simulation(clock);
        var plan = new AgentPlan { Simulation = simulation };
        var window = Open(plan);
        try
        {
            plan.Approve();
            clock.Advance(TimeSpan.FromMilliseconds(300));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AgentPlanStatus.Running, plan.CurrentStatus);
            plan.Simulation = null;
            Dispatcher.UIThread.RunJobs();
            Assert.False(simulation.IsAttached);
            Assert.False(simulation.IsWaiting);
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
            Assert.Empty(plan.Steps);
            Assert.Null(plan.EffectiveGoal);
            clock.Advance(TimeSpan.FromSeconds(30));
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(plan.Steps);
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Saves_A_Rename_Without_Leaving_The_Field_And_Reports_It()
    {
        var plan = Controlled();
        var changes = new List<AgentPlanChangeKind>();
        plan.Changed += (_, e) => changes.Add(e.Kind);
        var window = Open(plan);
        try
        {
            var row = Rows(plan)[1];
            row.TitleBox.Focus();
            row.TitleBox.Text = "Renamed";
            Press(window, PhysicalKey.Enter);
            Assert.Equal("Renamed", plan.Steps[1].Title);
            Assert.Same(row.TitleBox, Focused(window));
            Assert.Equal([AgentPlanChangeKind.Rename], changes);
            Assert.Equal("Step renamed to Renamed", plan.Announcer!.Announcement);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Undoes_The_Rename_Since_Focus_Or_The_Last_Save()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var box = Rows(plan)[0].TitleBox;
            box.Focus();
            box.Text = "First edit";
            Press(window, PhysicalKey.Enter);
            box.Text = "Second edit";
            Press(window, PhysicalKey.Escape);
            Assert.Equal("First edit", box.Text);
            Assert.Equal("First edit", plan.Steps[0].Title);
            Assert.Equal("First edit".Length, box.CaretIndex);
            Assert.Same(box, Focused(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Field_Saves_And_An_Emptied_Title_Removes_The_Step()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var rows = Rows(plan);
            rows[0].TitleBox.Focus();
            rows[0].TitleBox.Text = "Saved on blur";
            rows[1].TitleBox.Focus();
            Assert.Equal("Saved on blur", plan.Steps[0].Title);
            rows[1].TitleBox.Text = "";
            rows[2].TitleBox.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, plan.Steps.Count);
            Assert.Equal("Removed an empty step", plan.Announcer!.Announcement);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_In_An_Empty_Title_Removes_The_Step_And_Focuses_The_One_Above_At_Its_End()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var rows = Rows(plan);
            rows[2].TitleBox.Focus();
            rows[2].TitleBox.Text = "";
            Press(window, PhysicalKey.Backspace);
            Assert.Equal(3, plan.Steps.Count);
            Assert.DoesNotContain(plan.Steps, s => s.Id == "s3");
            Assert.Same(rows[1].TitleBox, Focused(window));
            Assert.Equal(rows[1].TitleBox.Text!.Length, rows[1].TitleBox.CaretIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_In_The_First_Empty_Title_Focuses_The_New_First_Row_At_Its_Start()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var rows = Rows(plan);
            rows[0].TitleBox.Focus();
            rows[0].TitleBox.Text = "";
            Press(window, PhysicalKey.Backspace);
            Assert.Equal("s2", plan.Steps[0].Id);
            Assert.Same(rows[1].TitleBox, Focused(window));
            Assert.Equal(0, rows[1].TitleBox.CaretIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alt_Arrows_Move_The_Step_Keep_The_Focus_And_The_Caret_And_Announce_The_Position()
    {
        var plan = Controlled();
        var moves = new List<AgentPlanChangeKind>();
        plan.Changed += (_, e) => moves.Add(e.Kind);
        var window = Open(plan);
        try
        {
            var row = Rows(plan)[1];
            row.TitleBox.Focus();
            row.TitleBox.CaretIndex = 5;
            Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Assert.Equal(["s1", "s3", "s2", "s4"], plan.Steps.Select(s => s.Id));
            Assert.Same(row.TitleBox, Focused(window));
            Assert.Equal(5, row.TitleBox.CaretIndex);
            Assert.Equal("Step 3", AutomationProperties.GetName(row.TitleBox));
            Assert.Equal("Step moved to position 3 of 4", plan.Announcer!.Announcement);
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            Assert.Equal("s2", plan.Steps[0].Id);
            // At the edge: nothing happens and nothing is announced.
            var before = plan.Announcer.History.Count;
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            Assert.Equal("s2", plan.Steps[0].Id);
            Assert.Equal(before, plan.Announcer.History.Count);
            Assert.All(moves, k => Assert.Equal(AgentPlanChangeKind.Move, k));
            Assert.Equal(3, moves.Count);
            // The row's visual order follows (Tab order).
            Assert.Equal(plan.Steps.Select(s => s.Id), Rows(plan).Select(r => r.Step.Id));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_On_The_Grip_Move_The_Step_And_Space_Enter_Home_End_Do_Nothing()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var row = Rows(plan)[0];
            row.Grip.Focus();
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal("s2", plan.Steps[0].Id);
            Assert.Equal("s1", plan.Steps[1].Id);
            Assert.Same(row.Grip, Focused(window));
            Assert.Equal("Move step 2: Step title 1", AutomationProperties.GetName(row.Grip));
            foreach (var key in new[] { PhysicalKey.Space, PhysicalKey.Enter, PhysicalKey.Home, PhysicalKey.End })
            {
                Press(window, key);
            }
            Assert.Equal("s2", plan.Steps[0].Id);
            Assert.Same(row.Grip, Focused(window));
            Press(window, PhysicalKey.ArrowUp);
            Assert.Equal("s1", plan.Steps[0].Id);
            Press(window, PhysicalKey.ArrowUp);
            Assert.Equal("s1", plan.Steps[0].Id);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Remove_Button_Hands_The_Focus_To_The_Neighbours_Button_And_Last_To_Add_Step()
    {
        var plan = Controlled(count: 3);
        var window = Open(plan);
        try
        {
            var rows = Rows(plan);
            rows[0].Remove.Focus();
            rows[0].Remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, plan.Steps.Count);
            Assert.Same(rows[1].Remove, Focused(window));
            Assert.Equal("Removed: Step title 1", plan.Announcer!.Announcement);
            // The last row hands it to Add step.
            rows[2].Remove.Focus();
            rows[2].Remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(plan.AddButton, Focused(window));
            rows[1].Remove.Focus();
            rows[1].Remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Same(plan.AddButton, Focused(window));
            Assert.Empty(plan.Steps);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Add_Step_Appends_An_Empty_Step_With_Its_Field_Focused()
    {
        var plan = Controlled(count: 2);
        var kinds = new List<AgentPlanChangeKind>();
        plan.Changed += (_, e) => kinds.Add(e.Kind);
        var window = Open(plan);
        try
        {
            plan.AddButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, plan.Steps.Count);
            Assert.Equal("", plan.Steps[2].Title);
            Assert.Same(Rows(plan)[2].TitleBox, Focused(window));
            Assert.Equal([AgentPlanChangeKind.Add], kinds);
            Assert.Equal("3", plan.HeadingCount.Text);
            Assert.Equal("Describe the step", Rows(plan)[2].TitleBox.PlaceholderText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Plan_Shows_The_Sentence_And_Disables_Approve_But_Not_Keep_Planning()
    {
        var plan = Controlled(count: 0);
        var window = Open(plan);
        try
        {
            Assert.True(plan.EmptyBlock.IsOpen);
            Assert.False(plan.ApproveButton.IsEnabled);
            Assert.True(plan.KeepButton.IsEnabled);
            Assert.Equal("0", plan.HeadingCount.Text);
            Assert.Contains(":empty", plan.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Single_Step_Reads_One_Step()
    {
        var plan = Controlled(count: 1);
        var window = Open(plan);
        try
        {
            Assert.Equal("1", plan.HeadingCount.Text);
            Assert.Equal("1 step", new AgentPlanLabels().StepCount(1));
            Assert.Equal("0 steps", new AgentPlanLabels().StepCount(0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Approve_Raises_Approved_With_Trimmed_Titles_And_No_Empty_Steps_And_Changes_No_State()
    {
        var plan = Controlled();
        plan.Steps[1].Title = "  Padded title  ";
        plan.Steps.Add(new AgentPlanStep("blank", "   "));
        IReadOnlyList<AgentPlanStep>? approved = null;
        plan.Approved += (_, e) => approved = e.Steps;
        var window = Open(plan);
        try
        {
            plan.ApproveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(approved);
            Assert.Equal(4, approved!.Count);
            Assert.Equal("Padded title", approved[1].Title);
            Assert.DoesNotContain(approved, s => s.Id == "blank");
            // Controlled: the host changes the status; the control did not.
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
            Assert.All(plan.Steps, s => Assert.Equal(AgentPlanStepState.Pending, s.State));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void When_The_Host_Starts_The_Run_Focus_Goes_To_Pause_And_The_Footer_Shows_The_Step()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            plan.ApproveButton.Focus();
            plan.Approve();
            plan.Status = AgentPlanStatus.Running;
            plan.Steps[0].State = AgentPlanStepState.Active;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(plan.PauseButton, Focused(window));
            Assert.Equal("Working on step 1", plan.StatusText.Content);
            Assert.Equal("Plan approved. Running 4 steps.", plan.Announcer!.History.First(h => h.StartsWith("Plan approved", StringComparison.Ordinal)));
            Assert.True(plan.ProgressLine!.IsShown);
            Assert.Equal("0 of 4 steps finished", AutomationProperties.GetHelpText(plan.ProgressLine));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failure_Moves_The_Focus_To_Retry_And_Retry_Or_Skip_Hand_It_Back_To_Pause()
    {
        var plan = Controlled(AgentPlanStatus.Running);
        plan.Steps[0].State = AgentPlanStepState.Done;
        plan.Steps[0].Duration = TimeSpan.FromSeconds(21);
        plan.Steps[1].State = AgentPlanStepState.Active;
        var actions = new List<AgentPlanStepActionKind>();
        plan.StepAction += (_, e) => actions.Add(e.Kind);
        var window = Open(plan);
        try
        {
            plan.PauseButton.Focus();
            plan.Steps[1].Error = "Two tests failed";
            plan.Steps[1].State = AgentPlanStepState.Failed;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var row = Rows(plan)[1];
            Assert.Same(row.Retry, Focused(window));
            Assert.Equal("Step 2 failed", plan.StatusText.Content);
            Assert.False(plan.PauseButton.IsVisible);
            Assert.Contains(":failed", plan.Classes);
            Assert.Equal("Step 2 failed: Two tests failed. Retry it or skip it.", plan.Announcer!.Announcement);
            row.Retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal([AgentPlanStepActionKind.Retry], actions);
            // Controlled: the step stays failed until the host updates it; then focus returns to Pause.
            Assert.Equal(AgentPlanStepState.Failed, plan.Steps[1].State);
            plan.Steps[1].Error = null;
            plan.Steps[1].State = AgentPlanStepState.Active;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(plan.PauseButton, Focused(window));
            Assert.Equal("Retrying: Step title 2", plan.Announcer.History.Last(h => h.StartsWith("Retrying", StringComparison.Ordinal)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_And_Resume_Report_And_The_Button_Keeps_The_Focus()
    {
        var plan = Controlled(AgentPlanStatus.Running);
        plan.Steps[0].State = AgentPlanStepState.Active;
        var pauses = new List<bool>();
        plan.PauseChanged += (_, e) => pauses.Add(e.IsPaused);
        var window = Open(plan);
        try
        {
            plan.PauseButton.Focus();
            plan.Pause();
            plan.Status = AgentPlanStatus.Paused;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(plan.PauseButton, Focused(window));
            Assert.Equal("Resume", AutomationProperties.GetName(plan.PauseButton));
            Assert.Equal("Paused at step 1", plan.StatusText.Content);
            plan.Resume();
            plan.Status = AgentPlanStatus.Running;
            Assert.Equal([true, false], pauses);
            Assert.Equal("Pause", AutomationProperties.GetName(plan.PauseButton));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Done_Shows_The_Total_And_The_Summary_And_Focus_Moves_To_Start_Over()
    {
        var plan = Controlled(AgentPlanStatus.Running, count: 2);
        plan.Summary = "All good.";
        plan.Steps[0].State = AgentPlanStepState.Done;
        plan.Steps[0].Duration = TimeSpan.FromSeconds(21);
        plan.Steps[1].State = AgentPlanStepState.Active;
        var window = Open(plan);
        try
        {
            plan.PauseButton.Focus();
            plan.Steps[1].Duration = TimeSpan.FromSeconds(42);
            plan.Steps[1].State = AgentPlanStepState.Done;
            plan.Status = AgentPlanStatus.Done;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Done in 1m 03s.", plan.DoneText.Prefix);
            Assert.Equal("All good.", plan.DoneText.Text);
            Assert.Same(plan.StartOverButton, Focused(window));
            Assert.Equal("Plan finished in 1m 03s. All good.", plan.Announcer!.Announcement);
            Assert.Equal(1.0, plan.Progress);
            Assert.Equal("2", plan.ProgressLine!.Value.ToString());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Start_Over_Raises_The_Event_And_Focus_Returns_To_Approve_When_The_Draft_Is_Back()
    {
        var plan = Controlled(AgentPlanStatus.Done, count: 1);
        plan.Steps[0].State = AgentPlanStepState.Done;
        var raised = 0;
        plan.StartOverRequested += (_, _) => raised++;
        var window = Open(plan);
        try
        {
            plan.StartOverButton.Focus();
            plan.StartOver();
            plan.Steps[0].State = AgentPlanStepState.Pending;
            plan.Status = AgentPlanStatus.Draft;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, raised);
            Assert.Same(plan.ApproveButton, Focused(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_The_Agent_Inserts_While_Running_Is_Announced_And_Counted()
    {
        var plan = Controlled(AgentPlanStatus.Running, count: 3);
        plan.Steps[0].State = AgentPlanStepState.Active;
        var window = Open(plan);
        try
        {
            plan.Steps.Insert(1, new AgentPlanStep("extra", "Extra step") { NoteKind = AgentPlanNoteKind.AddedByAgent });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("4", plan.HeadingCount.Text);
            Assert.Equal("New step from the agent: Extra step", plan.Announcer!.Announcement);
            Assert.Equal("Added by the agent", Rows(plan)[1].NoteText.Text);
            Assert.Equal(4, Rows(plan).Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Running_Rows_Announce_Their_State_Before_The_Title_And_Form_An_Ordered_List()
    {
        var plan = Controlled(AgentPlanStatus.Running, count: 3);
        plan.Steps[0].State = AgentPlanStepState.Done;
        plan.Steps[0].Duration = TimeSpan.FromSeconds(5);
        plan.Steps[1].State = AgentPlanStepState.Active;
        var window = Open(plan);
        try
        {
            var rows = Rows(plan);
            Assert.Equal("Done: Step title 1", AutomationProperties.GetName(rows[0]));
            Assert.Equal("In progress: Step title 2", AutomationProperties.GetName(rows[1]));
            Assert.Equal("Pending: Step title 3", AutomationProperties.GetName(rows[2]));
            Assert.Equal(2, AutomationProperties.GetPositionInSet(rows[1]));
            Assert.Equal(3, AutomationProperties.GetSizeOfSet(rows[1]));
            Assert.Equal("Plan steps", AutomationProperties.GetName(plan.List!));
            Assert.Equal("5s", rows[0].Time.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Titles_Are_Labelled_And_Described_By_The_Hint()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var row = Rows(plan)[0];
            Assert.Equal("Step 1", AutomationProperties.GetName(row.TitleBox));
            Assert.Equal("Remove step 1: Step title 1", AutomationProperties.GetName(row.Remove));
            var hint = new AgentPlanLabels().EditHint;
            Assert.Equal(hint, AutomationProperties.GetHelpText(row.TitleBox));
            Assert.Equal(hint, AutomationProperties.GetHelpText(row.Grip));
            Assert.Equal(hint, AutomationProperties.GetHelpText(row.Remove));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Remove_Button_Shows_On_Hover_Or_Focus_Only()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var row = Rows(plan)[0];
            Assert.False(row.IsRemoveRevealed);
            row.TitleBox.Focus();
            Assert.True(row.IsRemoveRevealed);
            Rows(plan)[1].TitleBox.Focus();
            Assert.False(row.IsRemoveRevealed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Container_Tightens_And_The_Draft_Buttons_Share_The_Row()
    {
        var plan = Controlled();
        var window = Open(plan, width: 380);
        try
        {
            Assert.Contains(":narrow", plan.Classes);
            Assert.Equal(2, plan.DraftRow.ColumnDefinitions.Count);
            Assert.Equal(Avalonia.Layout.HorizontalAlignment.Stretch, plan.KeepButton.HorizontalAlignment);
            Assert.True(Math.Abs(plan.KeepButton.Bounds.Width - plan.ApproveButton.Bounds.Width) < 2, "equal widths");
        }
        finally
        {
            window.Close();
        }
        var wide = Controlled();
        var window2 = Open(wide, width: 560);
        try
        {
            Assert.DoesNotContain(":narrow", wide.Classes);
            Assert.Equal(3, wide.DraftRow.ColumnDefinitions.Count);
        }
        finally
        {
            window2.Close();
        }
    }

    [AvaloniaFact]
    public void The_Draft_Title_Field_And_The_Running_Title_Occupy_The_Same_Box()
    {
        var plan = Controlled();
        var window = Open(plan);
        try
        {
            var row = Rows(plan)[1];
            var draftBox = row.TitleBox.TranslatePoint(new Point(8, 4), window);
            var heightBefore = row.Bounds.Height;
            plan.Status = AgentPlanStatus.Running;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var runningText = row.Title.TranslatePoint(new Point(0, 0), window);
            Assert.True(runningText.HasValue && draftBox.HasValue);
            Assert.Equal(draftBox!.Value.X, runningText!.Value.X, 0.5);
            Assert.Equal(draftBox.Value.Y, runningText.Value.Y, 0.5);
            Assert.Equal(heightBefore, row.Bounds.Height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Streamed_Suggestion_Becomes_An_Editable_Title_And_Reports_It()
    {
        var plan = Controlled();
        var kinds = new List<AgentPlanChangeKind>();
        plan.Changed += (_, e) => kinds.Add(e.Kind);
        var window = Open(plan);
        try
        {
            plan.StreamSuggestion(new AgentPlanStep("sug", "Highlight matched words in the result snippets"));
            Dispatcher.UIThread.RunJobs();
            var row = Rows(plan)[3];
            Assert.Equal("sug", row.Step.Id);
            Assert.Equal(AgentPlanNoteKind.Suggested, row.Step.NoteKind);
            Assert.False(row.IsStreaming);
            Assert.True(row.TitleBox.IsVisible);
            Assert.Equal("Highlight matched words in the result snippets", row.TitleBox.Text);
            Assert.Equal("Suggested", row.NoteText.Text);
            Assert.Equal([AgentPlanChangeKind.SuggestionStreamed], kinds);
            Assert.Equal("s4", plan.Steps[^1].Id);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keep_Planning_Raises_The_Request_And_Is_Ignored_While_Busy_Or_Exhausted()
    {
        var plan = Controlled();
        var requests = 0;
        plan.KeepPlanningRequested += (_, _) => requests++;
        var window = Open(plan);
        try
        {
            plan.KeepPlanning();
            Assert.Equal(1, requests);
            plan.IsPlanning = true;
            plan.KeepPlanning();
            plan.IsPlanning = false;
            plan.CanKeepPlanning = false;
            plan.KeepPlanning();
            Assert.Equal(1, requests);
            Assert.False(plan.KeepButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Drives_A_Plan_With_No_Status_Through_Approve_Failure_Skip_And_Done()
    {
        var clock = new AgentPlanManualScheduler();
        var plan = new AgentPlan { Simulation = AgentPlanTestScript.Simulation(clock) };
        var window = Open(plan);
        try
        {
            plan.ApproveButton.Focus();
            plan.Approve();
            Assert.Equal(AgentPlanStatus.Running, plan.CurrentStatus);
            Assert.Contains("This run is simulated.", plan.Announcer!.History.First(h => h.StartsWith("Plan approved", StringComparison.Ordinal)));
            clock.Advance(TimeSpan.FromMilliseconds(300));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AgentPlanStepState.Active, plan.Steps[0].State);
            Assert.Equal("Working on step 1", plan.StatusText.Content);
            Assert.Same(plan.PauseButton, Focused(window));
            for (var i = 0; i < 500 && plan.Steps.All(s => s.State != AgentPlanStepState.Failed); i++)
            {
                clock.RunNext();
            }
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(7, plan.Steps.Count);
            var failedRow = Rows(plan).Single(r => r.Step.State == AgentPlanStepState.Failed);
            Assert.Same(failedRow.Retry, Focused(window));
            Assert.Equal("Step 6 failed", plan.StatusText.Content);
            plan.Skip(failedRow.Step);
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(plan.PauseButton, Focused(window));
            while (plan.CurrentStatus != AgentPlanStatus.Done && clock.RunNext())
            {
            }
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Done in 2m 59s.", plan.DoneText.Prefix);
            Assert.Equal(AgentPlanTestScript.Summary, plan.DoneText.Text);
            plan.StartOver();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AgentPlanStatus.Draft, plan.CurrentStatus);
            Assert.Equal(6, plan.Steps.Count);
            Assert.Equal(6, Rows(plan).Count);
            Assert.Same(plan.ApproveButton, Focused(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Plan_Is_Collected_While_Its_Steps_Live_On()
    {
        var steps = new AvaloniaList<AgentPlanStep> { new("a", "Alpha"), new("b", "Beta") };
        var weak = TestHelpers.ShowAndClose(() => new AgentPlan { Status = AgentPlanStatus.Running, Steps = steps });
        TestHelpers.Collect(weak);
        Assert.False(weak.IsAlive);
        // The data still works without the plan.
        steps.Add(new AgentPlanStep("c", "Gamma"));
        steps[0].State = AgentPlanStepState.Done;
    }

    [AvaloniaFact]
    public void A_Plan_Is_Collected_While_Its_Simulation_Lives_On()
    {
        var clock = new AgentPlanManualScheduler();
        var simulation = AgentPlanTestScript.Simulation(clock);
        // Keep planning leaves a suggestion waiting on the simulation's timer when the plan goes away.
        var weak = TestHelpers.ShowAndClose(() => new AgentPlan { Simulation = simulation }, plan => plan.KeepPlanning());
        TestHelpers.Collect(weak);
        Assert.False(weak.IsAlive);
        Assert.False(simulation.IsAttached);
        clock.Advance(TimeSpan.FromSeconds(5));
    }

    [AvaloniaFact]
    public void A_Plan_Taken_Out_And_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var steps = new AvaloniaList<AgentPlanStep> { new("a", "Alpha"), new("b", "Beta") };
        var plan = new AgentPlan { Status = AgentPlanStatus.Draft, Steps = steps, Width = 560, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var host = new Border { Child = plan };
        var window = TestHelpers.Show(host);
        window.Width = 640;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal(2, Rows(plan).Count);
            var first = Rows(plan)[0];
            // Away and back with nothing changed: the same rows.
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            host.Child = plan;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(first, Rows(plan)[0]);
            // Changes made while away show once it is back.
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            steps.Add(new AgentPlanStep("c", "Gamma"));
            steps[0].Title = "Alpha two";
            host.Child = plan;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { "Alpha two", "Beta", "Gamma" }, Rows(plan).Select(r => r.TitleBox.Text ?? string.Empty));
            Assert.Same(first, Rows(plan)[0]);
            Assert.Equal("3", plan.HeadingCount.Text);
            // Moves and removals too.
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            steps.Move(2, 0);
            steps.RemoveAt(2);
            host.Child = plan;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { "Gamma", "Alpha two" }, Rows(plan).Select(r => r.TitleBox.Text ?? string.Empty));
            Assert.Same(first, Rows(plan)[1]);
            // And it follows changes made after it came back.
            steps.Add(new AgentPlanStep("d", "Delta"));
            steps[1].Title = "Alpha three";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { "Gamma", "Alpha three", "Delta" }, Rows(plan).Select(r => r.TitleBox.Text ?? string.Empty));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulated_Run_Waits_While_The_Plan_Is_Away_And_Carries_On_When_It_Is_Back()
    {
        var clock = new AgentPlanManualScheduler();
        var plan = new AgentPlan { Simulation = AgentPlanTestScript.Simulation(clock), Width = 560, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var host = new Border { Child = plan };
        var window = TestHelpers.Show(host);
        window.Width = 640;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        try
        {
            plan.Approve();
            clock.Advance(TimeSpan.FromMilliseconds(300));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AgentPlanStepState.Active, plan.Steps[0].State);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            // Nothing runs while the plan is away.
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Empty(plan.Steps[0].Activities);
            Assert.Equal(AgentPlanStatus.Running, plan.CurrentStatus);
            host.Child = plan;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(10));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AgentPlanStepState.Done, plan.Steps[0].State);
            Assert.NotEmpty(plan.Steps[0].Activities);
            Assert.Equal(plan.Steps.Count, Rows(plan).Count);
        }
        finally
        {
            window.Close();
        }
    }
}

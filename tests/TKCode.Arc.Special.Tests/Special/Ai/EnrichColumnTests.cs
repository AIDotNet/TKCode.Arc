using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>Manual clock for the enrich column: nothing waits until <see cref="Advance"/> moves past it.</summary>
internal sealed class EnrichColumnTestClock : IEnrichColumnClock
{
    private readonly List<(double Due, TaskCompletionSource Done, CancellationTokenRegistration Registration)> _waits = [];

    public double Now { get; private set; }

    public List<double> Requested { get; } = [];

    public Task Delay(TimeSpan delay, CancellationToken token)
    {
        Requested.Add(delay.TotalMilliseconds);
        var done = new TaskCompletionSource();
        var registration = token.Register(() => done.TrySetCanceled(token));
        _waits.Add((Now + delay.TotalMilliseconds, done, registration));
        return done.Task;
    }

    /// <summary>Moves the clock and completes every wait that comes due, in order, running the UI jobs after each.</summary>
    public void Advance(double milliseconds)
    {
        var end = Now + milliseconds;
        while (true)
        {
            var next = _waits.Where(w => w.Due <= end).OrderBy(w => w.Due).FirstOrDefault();
            if (next.Done is null)
            {
                break;
            }
            _waits.Remove(next);
            Now = Math.Max(Now, next.Due);
            next.Registration.Dispose();
            next.Done.TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
        Now = end;
        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// The enrich column: a simulation (the gallery's table), the empty frame, the run engine, cell states, events, the
/// editor, keyboard, labels and the responsive layout.
/// </summary>
public class EnrichColumnTests
{
    private static readonly EnrichColumnRow[] s_rows =
    [
        new("a", "Alder Works", "alder.example"),
        new("b", "Birch Systems", "birch.example"),
        new("c", "Cedar Labs", "cedar.example"),
    ];

    /// <summary>An enrich delegate whose cells finish when the test says so.</summary>
    private sealed class Pending
    {
        public Dictionary<string, TaskCompletionSource<EnrichColumnResult>> Calls { get; } = new();

        public Dictionary<string, EnrichColumnContext> Contexts { get; } = new();

        public Task<EnrichColumnResult> Enrich(EnrichColumnRow row, EnrichColumnContext context)
        {
            var tcs = new TaskCompletionSource<EnrichColumnResult>();
            context.CancellationToken.Register(() => tcs.TrySetCanceled());
            Calls[row.Id] = tcs;
            Contexts[row.Id] = context;
            return tcs.Task;
        }

        public void Finish(string id, string value)
        {
            Calls[id].SetResult(new EnrichColumnResult(value, new EnrichColumnSource(id + ".example", "https://" + id + ".example")));
            Dispatcher.UIThread.RunJobs();
        }

        public void Fail(string id, string reason)
        {
            Calls[id].SetException(new InvalidOperationException(reason));
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (EnrichColumn Column, Pending Pending, EnrichColumnTestClock Clock) Make(int concurrency = 2)
    {
        var pending = new Pending();
        var clock = new EnrichColumnTestClock();
        var column = new EnrichColumn
        {
            Rows = s_rows,
            Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round"),
            Enrich = pending.Enrich,
            Concurrency = concurrency,
            Clock = clock,
        };
        return (column, pending, clock);
    }

    private static List<EnrichColumnRowView> RowViews(Control root) =>
        root.GetVisualDescendants().OfType<EnrichColumnRowView>().Where(v => v.Item is not null).ToList();

    [AvaloniaFact]
    public void A_Simulation_Shows_Its_Table_And_Runs_When_Seen()
    {
        var column = new EnrichColumn { Simulation = EnrichColumnTestData.Simulation() };
        var clock = new EnrichColumnTestClock();
        column.Clock = clock;
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            Dispatcher.UIThread.RunJobs();
            column.DisplayedRows.Count.ShouldBe(7);
            column.DisplayedTitle.ShouldBe("Target accounts");
            column.EffectiveColumn.Name.ShouldBe("Headcount and funding");
            column.EffectiveModel!.Name.ShouldBe(EnrichColumnTestData.Model.Name);
            column.Title.ShouldBeNull();
            column.MetaText!.Text.ShouldBe("7 rows · sample data");
            column.IsSimulated.ShouldBeTrue();
            column.EffectiveAutoRun.ShouldBeTrue();
            TestHelpers.PumpUntil(() => column.IsRunning, 3000).ShouldBeTrue();
            column.Announcer!.Announcement.ShouldBe("Enriching 7 rows. Values are simulated; nothing is sent.");
            clock.Advance(20000);
            column.IsRunning.ShouldBeFalse();
            var (done, failed, total) = column.Counts();
            (done, failed, total).ShouldBe((6, 1, 7));
            column.GetCell(EnrichColumnTestData.FailingId).Error.ShouldBe("No source found");
            column.ProgressText!.Text.ShouldBe("6 of 7 enriched");
            column.Announcer.Announcement.ShouldBe("Done. 6 of 7 rows enriched, 1 failed.");

            column.RunRow(EnrichColumnTestData.FailingId);
            Dispatcher.UIThread.RunJobs();
            column.GetCell(EnrichColumnTestData.FailingId).Status.ShouldBe(EnrichColumnCellStatus.Running);
            column.GetCell(EnrichColumnTestData.FailingId).Activity.ShouldBe("Searching for Quarry Labs");
            clock.Advance(5000);
            var retried = column.GetCell(EnrichColumnTestData.FailingId);
            retried.Status.ShouldBe(EnrichColumnCellStatus.Done);
            retried.Attempt.ShouldBe(2);
            retried.Source!.Href.ShouldBe("https://quarrylabs.example/team");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Enrich_Does_Not_Auto_Run_And_Workers_Start_140ms_Apart()
    {
        var (column, pending, clock) = Make();
        var window = TestHelpers.Show(column);
        try
        {
            TestHelpers.PumpUntil(() => false, 100);
            column.EffectiveAutoRun.ShouldBeFalse();
            column.IsRunning.ShouldBeFalse();
            column.MetaText!.Text.ShouldBe("3 rows");
            column.GetCell("a").Status.ShouldBe(EnrichColumnCellStatus.Idle);

            column.Run();
            column.IsRunning.ShouldBeTrue();
            column.GetCell("a").Status.ShouldBe(EnrichColumnCellStatus.Running);
            column.GetCell("b").Status.ShouldBe(EnrichColumnCellStatus.Queued);
            column.GetCell("c").Status.ShouldBe(EnrichColumnCellStatus.Queued);
            clock.Requested.ShouldContain(140);
            pending.Calls.Keys.ShouldBe(["a"]);

            clock.Advance(140);
            column.GetCell("b").Status.ShouldBe(EnrichColumnCellStatus.Running);
            column.GetCell("c").Status.ShouldBe(EnrichColumnCellStatus.Queued);
            pending.Contexts["a"].Prompt.ShouldBe("Find the headcount and latest funding round");
            pending.Contexts["a"].Attempt.ShouldBe(1);

            pending.Finish("a", "12 people");
            column.GetCell("a").Status.ShouldBe(EnrichColumnCellStatus.Done);
            column.GetCell("a").Value.ShouldBe("12 people");
            // The free worker takes the next row at once.
            column.GetCell("c").Status.ShouldBe(EnrichColumnCellStatus.Running);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Concurrency_One_Runs_Rows_In_Order()
    {
        var (column, pending, _) = Make(concurrency: 1);
        var window = TestHelpers.Show(column);
        try
        {
            column.Run();
            pending.Calls.Count.ShouldBe(1);
            pending.Finish("a", "1");
            pending.Calls.Count.ShouldBe(2);
            column.GetCell("c").Status.ShouldBe(EnrichColumnCellStatus.Queued);
            pending.Finish("b", "2");
            pending.Finish("c", "3");
            column.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Events_Report_Every_Change_And_The_Settled_Run()
    {
        var (column, pending, clock) = Make();
        var changes = new List<(string Id, EnrichColumnCellStatus Status)>();
        EnrichColumnRunCompletedEventArgs? completed = null;
        column.CellChanged += (_, e) => changes.Add((e.Row.Id, e.Cell.Status));
        column.RunCompleted += (_, e) => completed = e;
        var window = TestHelpers.Show(column);
        try
        {
            column.Run();
            clock.Advance(140);
            pending.Finish("a", "1");
            pending.Fail("b", "No source found");
            pending.Finish("c", "3");
            changes.Where(c => c.Id == "a").Select(c => c.Status)
                .ShouldBe([EnrichColumnCellStatus.Queued, EnrichColumnCellStatus.Running, EnrichColumnCellStatus.Done]);
            changes.Where(c => c.Id == "b").Select(c => c.Status).Last().ShouldBe(EnrichColumnCellStatus.Failed);
            completed.ShouldNotBeNull();
            completed!.WasStopped.ShouldBeFalse();
            completed.Cells.Count.ShouldBe(3);
            completed.Cells["b"].Error.ShouldBe("No source found");
            column.Announcer!.Announcement.ShouldBe("Done. 2 of 3 rows enriched, 1 failed.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Cancels_Requests_And_Puts_Cells_Back()
    {
        var (column, pending, clock) = Make();
        column.InitialCells = new Dictionary<string, EnrichColumnCell> { ["a"] = EnrichColumnCell.Done("old value") };
        EnrichColumnRunCompletedEventArgs? completed = null;
        column.RunCompleted += (_, e) => completed = e;
        var window = TestHelpers.Show(column);
        try
        {
            column.GetCell("a").Value.ShouldBe("old value");
            column.Run();
            clock.Advance(140);
            var token = pending.Contexts["a"].CancellationToken;
            column.Stop();
            Dispatcher.UIThread.RunJobs();
            token.IsCancellationRequested.ShouldBeTrue();
            column.IsRunning.ShouldBeFalse();
            column.GetCell("a").Value.ShouldBe("old value");
            column.GetCell("a").Status.ShouldBe(EnrichColumnCellStatus.Done);
            column.GetCell("b").Status.ShouldBe(EnrichColumnCellStatus.Idle);
            column.GetCell("c").Status.ShouldBe(EnrichColumnCellStatus.Idle);
            completed!.WasStopped.ShouldBeTrue();
            column.Announcer!.Announcement.ShouldBe("Stopped. 1 of 3 rows enriched.");
            // A late answer from a cancelled request is ignored.
            pending.Calls["b"].TrySetResult(new EnrichColumnResult("late"));
            Dispatcher.UIThread.RunJobs();
            column.GetCell("b").Status.ShouldBe(EnrichColumnCellStatus.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Activity_Updates_The_Running_Label_And_Retry_Counts_Attempts()
    {
        var (column, pending, _) = Make(concurrency: 1);
        var window = TestHelpers.Show(column);
        try
        {
            column.RunRow("b");
            column.GetCell("b").Status.ShouldBe(EnrichColumnCellStatus.Running);
            column.GetCell("a").Status.ShouldBe(EnrichColumnCellStatus.Idle);
            pending.Contexts["b"].SetActivity("Reading birch.example");
            column.GetCell("b").Activity.ShouldBe("Reading birch.example");
            var view = RowViews(column).First(v => v.Item!.Row.Id == "b");
            view.CellContent.ShouldBeOfType<ToolCallText>().Text.ShouldBe("Reading birch.example");

            pending.Fail("b", "Rate limited");
            column.GetCell("b").Status.ShouldBe(EnrichColumnCellStatus.Failed);
            AutomationProperties.GetName(view.Action!).ShouldBe("Retry Birch Systems");
            column.RunRow("b");
            pending.Contexts["b"].Attempt.ShouldBe(2);
            pending.Finish("b", "40 people");
            column.GetCell("b").Attempt.ShouldBe(2);
            AutomationProperties.GetName(view.Action!).ShouldBe("Run Birch Systems");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Rows_And_No_Simulation_The_Column_Shows_The_Empty_Frame()
    {
        var column = new EnrichColumn { Clock = new EnrichColumnTestClock() };
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            TestHelpers.PumpUntil(() => false, 150);
            column.DisplayedRows.Count.ShouldBe(0);
            column.DisplayedTitle.ShouldBe(string.Empty);
            column.EffectiveColumn.Name.ShouldBe(string.Empty);
            column.EffectiveModel.ShouldBeNull();
            column.IsSimulated.ShouldBeFalse();
            column.EffectiveAutoRun.ShouldBeFalse();
            column.MetaText!.Text.ShouldBe("0 rows");
            column.RunButton!.IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(column, EnrichColumn.ModelNamePart).IsVisible.ShouldBeFalse();
            column.Run();
            column.IsRunning.ShouldBeFalse();
            RowViews(column).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Rows_Win_Over_The_Simulation_And_Need_A_Delegate_To_Run()
    {
        var column = new EnrichColumn { Rows = s_rows, Simulation = EnrichColumnTestData.Simulation(), Clock = new EnrichColumnTestClock() };
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            TestHelpers.PumpUntil(() => false, 150);
            column.DisplayedRows.Select(r => r.Id).ShouldBe(["a", "b", "c"]);
            column.DisplayedTitle.ShouldBe(string.Empty);
            column.EffectiveModel.ShouldBeNull();
            column.IsSimulated.ShouldBeFalse();
            column.IsRunning.ShouldBeFalse();
            column.MetaText!.Text.ShouldBe("3 rows");
            column.RunButton!.IsEnabled.ShouldBeFalse();
            column.Run();
            column.IsRunning.ShouldBeFalse();
            column.GetCell("a").Status.ShouldBe(EnrichColumnCellStatus.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Simulation_To_Null_Stops_The_Run_And_Empties_The_Table()
    {
        var clock = new EnrichColumnTestClock();
        var column = new EnrichColumn { Simulation = EnrichColumnTestData.Simulation(), Clock = clock };
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            TestHelpers.PumpUntil(() => column.IsRunning, 3000).ShouldBeTrue();
            clock.Advance(1000);
            column.Simulation = null;
            Dispatcher.UIThread.RunJobs();
            column.IsRunning.ShouldBeFalse();
            column.DisplayedRows.Count.ShouldBe(0);
            column.Cells.ShouldBeEmpty();
            column.RunButton!.IsEnabled.ShouldBeFalse();
            clock.Advance(20000);
            column.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Initial_Cells_Skip_Auto_Run()
    {
        var column = new EnrichColumn
        {
            Simulation = EnrichColumnTestData.Simulation(),
            InitialCells = new Dictionary<string, EnrichColumnCell> { ["lumen"] = EnrichColumnCell.Done("240 people") },
            Clock = new EnrichColumnTestClock(),
        };
        var window = TestHelpers.Show(column);
        try
        {
            TestHelpers.PumpUntil(() => false, 150);
            column.IsRunning.ShouldBeFalse();
            column.GetCell("lumen").Value.ShouldBe("240 people");
            column.RunButton!.Content.ShouldNotBeNull();
            AutomationProperties.GetName(column.RunButton).ShouldBe("Re-run");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_Button_Runs_Stops_And_Reruns()
    {
        var (column, pending, clock) = Make();
        var window = TestHelpers.Show(column);
        try
        {
            AutomationProperties.GetName(column.RunButton!).ShouldBe("Run column");
            column.RunButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            column.IsRunning.ShouldBeTrue();
            AutomationProperties.GetName(column.RunButton).ShouldBe("Stop");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            column.IsRunning.ShouldBeFalse();
            column.Run();
            clock.Advance(140);
            pending.Finish("a", "v");
            pending.Finish("b", "v");
            pending.Finish("c", "v");
            column.IsRunning.ShouldBeFalse();
            AutomationProperties.GetName(column.RunButton).ShouldBe("Re-run");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editor_Opens_Escape_Cancels_And_Ctrl_Enter_Saves_And_Reruns()
    {
        var (column, pending, _) = Make();
        EnrichColumnDefinition? saved = null;
        column.ColumnChanged += (_, e) => saved = e.Column;
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            Dispatcher.UIThread.RunJobs();
            column.EditButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            column.IsEditing.ShouldBeTrue();
            column.Editor!.IsExpanded.ShouldBeTrue();
            column.NameInput!.Text.ShouldBe("Headcount and funding");
            column.NameInput.IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            column.IsEditing.ShouldBeFalse();
            column.Editor.IsExpanded.ShouldBeFalse();
            column.EditButton.IsFocused.ShouldBeTrue();
            saved.ShouldBeNull();

            column.BeginEdit();
            column.NameInput.Text = "Tech stack";
            column.PromptInput!.Text = "List the main frameworks";
            column.PromptInput.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            column.IsEditing.ShouldBeFalse();
            saved!.Name.ShouldBe("Tech stack");
            column.Column!.Prompt.ShouldBe("List the main frameworks");
            column.IsRunning.ShouldBeTrue();
            pending.Contexts["a"].Prompt.ShouldBe("List the main frameworks");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Not_Editable_Hides_The_Edit_Control()
    {
        var (column, _, _) = Make();
        column.IsEditable = false;
        var window = TestHelpers.Show(column);
        try
        {
            column.EditButton!.IsVisible.ShouldBeFalse();
            column.BeginEdit();
            column.IsEditing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Row_Control_Runs_One_Row_From_The_Keyboard()
    {
        var (column, pending, _) = Make();
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            Dispatcher.UIThread.RunJobs();
            var view = RowViews(column).First(v => v.Item!.Row.Id == "c");
            AutomationProperties.GetName(view.Action!).ShouldBe("Run Cedar Labs");
            view.Action!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            pending.Calls.Keys.ShouldBe(["c"]);
            view.Action.IsVisible.ShouldBeFalse();
            column.Announcer!.Announcement.ShouldBe("Enriching 1 row.");
            pending.Finish("c", "9 people");
            view.CellContent.ShouldBeOfType<Grid>();
            view.Action.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(view).ShouldBe("Cedar Labs: 9 people");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Source_Request_Can_Be_Handled_By_The_App()
    {
        var (column, pending, _) = Make();
        EnrichColumnSourceRequestedEventArgs? request = null;
        column.SourceRequested += (_, e) =>
        {
            request = e;
            e.Handled = true;
        };
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            Dispatcher.UIThread.RunJobs();
            column.RunRow("a");
            pending.Finish("a", "12 people");
            var view = RowViews(column).First(v => v.Item!.Row.Id == "a");
            var link = view.CellContent!.GetVisualDescendants().OfType<EnrichColumnButton>().Single();
            AutomationProperties.GetName(link).ShouldBe("a.example, opens in a new tab");
            link.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            request!.Row.Id.ShouldBe("a");
            request.ValueSource.Href.ShouldBe("https://a.example");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Width_Sets_The_Density_Of_Rows()
    {
        EnrichColumn.DensityFor(760).ShouldBe(EnrichColumnDensity.Regular);
        EnrichColumn.DensityFor(560).ShouldBe(EnrichColumnDensity.Regular);
        EnrichColumn.DensityFor(559).ShouldBe(EnrichColumnDensity.Compact);
        EnrichColumn.DensityFor(439).ShouldBe(EnrichColumnDensity.Narrow);

        var (column, _, _) = Make();
        var window = TestHelpers.Show(column);
        try
        {
            window.Width = 720;
            Dispatcher.UIThread.RunJobs();
            var view = RowViews(column).First();
            EnrichColumn.GetDensity(view).ShouldBe(EnrichColumnDensity.Regular);
            view.NameText!.Inlines!.Count.ShouldBe(2);
            view.Bounds.Height.ShouldBe(44);

            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            EnrichColumn.GetDensity(view).ShouldBe(EnrichColumnDensity.Narrow);
            view.Classes.Contains(":narrow").ShouldBeTrue();
            view.NameText.Inlines!.Count.ShouldBe(1);
            view.Bounds.Height.ShouldBe(60);
            column.Bounds.Width.ShouldBe(400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Failure_Hides_The_Reason_Behind_The_Label()
    {
        var (column, pending, _) = Make();
        var window = TestHelpers.Show(column);
        try
        {
            column.RunRow("a");
            pending.Fail("a", "No source found");
            var view = RowViews(column).First(v => v.Item!.Row.Id == "a");
            var failed = view.CellContent.ShouldBeOfType<TextBlock>();
            failed.Text.ShouldBe("Failed");
            AutomationProperties.GetName(failed).ShouldBe("Failed, No source found");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Can_Change_Mid_Run_And_Workers_Use_The_Latest()
    {
        var (column, pending, _) = Make(concurrency: 1);
        var window = TestHelpers.Show(column);
        try
        {
            column.Run();
            column.Rows = [s_rows[0], s_rows[2]];
            Dispatcher.UIThread.RunJobs();
            pending.Finish("a", "1");
            // "b" left the table while queued, so the worker moves on to "c".
            pending.Calls.ContainsKey("b").ShouldBeFalse();
            pending.Calls.ContainsKey("c").ShouldBeTrue();
            column.Items.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Model_Null_Hides_The_Footer_Model_And_Labels_Reword()
    {
        var (column, _, _) = Make();
        column.Model = null;
        column.Note = "Q3 list";
        column.Labels = new EnrichColumnLabels { Rows = n => n + " comptes", RunColumn = "Lancer" };
        var window = TestHelpers.Show(column);
        try
        {
            TestHelpers.Descendant<TextBlock>(column, EnrichColumn.ModelNamePart).IsVisible.ShouldBeFalse();
            column.MetaText!.Text.ShouldBe("3 comptes · Q3 list");
            AutomationProperties.GetName(column.RunButton!).ShouldBe("Lancer");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Format_Counts()
    {
        var labels = EnrichColumnLabels.Default;
        labels.Rows(1).ShouldBe("1 row");
        labels.Rows(7).ShouldBe("7 rows");
        labels.Progress(5, 7).ShouldBe("5 of 7 enriched");
        labels.AnnounceFinished(7, 0, 7).ShouldBe("Done. 7 of 7 rows enriched.");
        EnrichColumnLogo.Monogram("  quarry labs").ShouldBe("Q");
        EnrichColumnLogo.Monogram(null).ShouldBe("?");
    }

    [AvaloniaFact]
    public void Status_Glyph_Swaps_And_Settles()
    {
        var glyph = new EnrichColumnStatusGlyph();
        var window = TestHelpers.Show(glyph, Motion.ArcMotionLevel.Full);
        try
        {
            glyph.Status = EnrichColumnCellStatus.Running;
            glyph.GlyphCount.ShouldBe(2);
            glyph.SetInstantly(EnrichColumnCellStatus.Done);
            glyph.GlyphCount.ShouldBe(1);
            glyph.Current.Draw.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>
/// The invented fixture these tests play: a copy of the gallery's EnrichColumn demo data with invented model names, so
/// the tests do not depend on the gallery's content.
/// </summary>
internal static class EnrichColumnTestData
{
    /// <summary>The table's name in the top bar.</summary>
    public const string Title = "Target accounts";

    /// <summary>The row whose first attempt fails.</summary>
    public const string FailingId = "quarry";

    /// <summary>The AI column.</summary>
    public static EnrichColumnDefinition Column { get; } =
        new("Headcount and funding", "Find the headcount and latest funding round");

    /// <summary>The footer's model: a real model that can call a web search tool.</summary>
    public static EnrichColumnModel Model { get; } = new("Model A", "web search");

    private static readonly (EnrichColumnRow Row, string Value, string Source, int Millis)[] s_data =
    [
        (new EnrichColumnRow("lumen", "Lumen Freight", "lumenfreight.example"), "240 people · Series B, $38M", "lumenfreight.example/about", 1500),
        (new EnrichColumnRow("harbor", "Harbor Analytics", "harboranalytics.example"), "85 people · Seed, $6M", "harboranalytics.example/news", 1200),
        (new EnrichColumnRow("kestrel", "Kestrel Robotics", "kestrel.example"), "410 people · Series C, $95M", "kestrel.example/press", 1800),
        (new EnrichColumnRow(FailingId, "Quarry Labs", "quarrylabs.example"), "32 people · Pre-seed, $1.5M", "quarrylabs.example/team", 1300),
        (new EnrichColumnRow("pinecrest", "Pinecrest Health", "pinecrest.example"), "1,200 people · Series D, $140M", "pinecrest.example/company", 1600),
        (new EnrichColumnRow("bluefin", "Bluefin Payments", "bluefin.example"), "560 people · Series C, $72M", "bluefin.example/blog", 1400),
        (new EnrichColumnRow("tidewater", "Tidewater Energy", "tidewater.example"), "150 people · Series A, $18M", "tidewater.example/investors", 1700),
    ];

    /// <summary>The seven rows.</summary>
    public static IList<EnrichColumnRow> Rows { get; } = s_data.Select(d => d.Row).ToArray();

    /// <summary>
    /// What each row settles into: "Searching for …" for 45% of its time, "Reading …" for the rest, then the value and
    /// its source. Quarry Labs fails its first attempt with "No source found".
    /// </summary>
    public static IList<EnrichColumnSimulationAnswer> Answers { get; } = s_data.Select(d => new EnrichColumnSimulationAnswer(
        d.Row.Id, d.Value, new EnrichColumnSource(d.Source, "https://" + d.Source))
    {
        Steps =
        [
            new EnrichColumnSimulationStep("Searching for " + d.Row.Name, TimeSpan.FromMilliseconds(d.Millis * 0.45)),
            new EnrichColumnSimulationStep("Reading " + d.Row.Domain, TimeSpan.FromMilliseconds(d.Millis * 0.55)),
        ],
        Error = d.Row.Id == FailingId ? "No source found" : null,
    }).ToArray();

    /// <summary>A new simulation of the table (tests and code use it; the XAML demos spell it out).</summary>
    public static EnrichColumnSimulation Simulation() => new()
    {
        Title = Title,
        Column = Column,
        Model = Model,
        Rows = Rows,
        Answers = Answers,
    };
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="EvalSuite"/> on its invented samples (14 cases, v12 against v13) and on small suites of its own: grading,
/// the summary and tiles, switching versions, filters, the open case, the keyboard, runs (queue, concurrency, events,
/// stop, errors) and the narrow layout. Every wait of a simulated run is released by hand.
/// </summary>
public class EvalSuiteTests
{
    /// <summary>Waits the test releases by hand.</summary>
    private sealed class Gate
    {
        private readonly List<TaskCompletionSource> _waits = new();

        public int Count => _waits.Count;

        public Task Wait(TimeSpan time, CancellationToken token)
        {
            var done = new TaskCompletionSource();
            token.Register(() => done.TrySetCanceled(token));
            _waits.Add(done);
            return done.Task;
        }

        /// <summary>Releases every wait so far and lets the suite go on.</summary>
        public void ReleaseAll()
        {
            var waits = _waits.ToArray();
            _waits.Clear();
            foreach (var wait in waits)
            {
                wait.TrySetResult();
            }
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (EvalSuite Suite, Window Window, Gate Gate) Show(double width = 900, Action<EvalSuite>? configure = null,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var gate = new Gate();
        var suite = new EvalSuite { Delay = gate.Wait };
        configure?.Invoke(suite);
        var window = TestHelpers.Show(suite, level);
        window.Width = width;
        window.Height = 1400;
        Dispatcher.UIThread.RunJobs();
        return (suite, window, gate);
    }

    private static void Click(Button? button)
    {
        button.ShouldNotBeNull();
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Shown(EvalSuite suite) => suite.List!.Rows.Select(r => r.Case.Id).ToArray();

    #region Grading

    [Fact]
    public void Grade_Applies_Each_Check_And_Gives_A_Reason()
    {
        var exact = new EvalCase { Id = "a", Expected = "A day pass costs €12.", Check = EvalCheckKind.Exact };
        EvalSuite.Grade(exact, "v", "  A day pass costs €12. ").Passed.ShouldBeTrue();
        var miss = EvalSuite.Grade(exact, "v", "a day pass costs €12.");
        miss.Passed.ShouldBeFalse();
        miss.Detail.ShouldBe("Differs from the expected text");

        var contains = new EvalCase { Id = "b", Expected = "30 days", Check = EvalCheckKind.Contains };
        EvalSuite.Grade(contains, "v", "Within 30 DAYS of a ride").Passed.ShouldBeTrue();
        EvalSuite.Grade(contains, "v", "Within a month").Detail.ShouldBe("Missing “30 days”");

        var regex = new EvalCase { Id = "c", Expected = "ride (ID|number)", Check = EvalCheckKind.Regex };
        var match = EvalSuite.Grade(regex, "v", "Send me the ride number please");
        match.Passed.ShouldBeTrue();
        match.Detail.ShouldBe("Matches the pattern at “ride number”");
        EvalSuite.Grade(new EvalCase { Expected = "(", Check = EvalCheckKind.Regex }, "v", "x").Detail.ShouldBe("The pattern is not valid");

        var rubric = new EvalCase { Id = "d", Expected = "Short", Check = EvalCheckKind.Rubric, Threshold = 0.7 };
        var pass = EvalSuite.Grade(rubric, "v", "ok", 0.7);
        pass.Passed.ShouldBeTrue();
        pass.Score.ShouldBe(0.7);
        pass.Detail.ShouldBe("Scored 0.70, needs 0.70");
        EvalSuite.Grade(rubric, "v", "ok", 0.69).Passed.ShouldBeFalse();
        var none = EvalSuite.Grade(rubric, "v", "ok");
        none.Passed.ShouldBeFalse();
        none.Detail.ShouldBe("No score from the grader");
        EvalSuite.Grade(contains, "v", "30 days").Score.ShouldBeNull();
    }

    [Fact]
    public void Compare_Names_The_Movement_Of_A_Case()
    {
        var pass = new EvalResult { Passed = true };
        var fail = new EvalResult { Passed = false };
        EvalSuite.Compare(pass, pass).ShouldBe(EvalCaseStatus.Passing);
        EvalSuite.Compare(fail, fail).ShouldBe(EvalCaseStatus.Failing);
        EvalSuite.Compare(pass, fail).ShouldBe(EvalCaseStatus.Regression);
        EvalSuite.Compare(fail, pass).ShouldBe(EvalCaseStatus.Fixed);
        EvalSuite.Compare(null, pass).ShouldBe(EvalCaseStatus.NotRun);
    }

    #endregion

    #region Summary and versions

    [AvaloniaFact]
    public void The_Samples_Compare_V12_And_V13()
    {
        var (suite, _, _) = Show();
        var s = suite.Summary;
        s.Total.ShouldBe(14);
        s.Compared.ShouldBe(14);
        s.BaselinePassed.ShouldBe(10);
        s.CandidatePassed.ShouldBe(11);
        s.Regressions.ShouldBe(2);
        s.Fixes.ShouldBe(3);
        s.StillFailing.ShouldBe(1);
        s.StillPassing.ShouldBe(8);
        s.CandidateFailing.ShouldBe(3);
        s.DeltaPoints.ShouldBe(100.0 / 14, 1e-9);
        suite.StatusOf("greeting").ShouldBe(EvalCaseStatus.Regression);
        suite.StatusOf("price").ShouldBe(EvalCaseStatus.Regression);
        suite.StatusOf("language").ShouldBe(EvalCaseStatus.Fixed);
        suite.StatusOf("apology").ShouldBe(EvalCaseStatus.Failing);
        suite.StatusOf("hours").ShouldBe(EvalCaseStatus.Passing);
        suite.Phase.ShouldBe(EvalRunPhase.Idle);

        // The shown change is the difference of the rounded rates, so 71% → 79% reads +8.
        suite.ShownRates().ShouldBe((71, 79, 8));
        suite.BaselineRateDigits!.Value.ShouldBe(71);
        suite.CandidateRateDigits!.Value.ShouldBe(79);
        suite.DeltaDigits!.Value.ShouldBe(8);
        suite.DeltaDigits.Prefix.ShouldBe("+");
        suite.DeltaNoteText!.Text.ShouldBe("2 regressions · 3 fixed");
        suite.Classes.ShouldContain(":up");
        suite.SubtitleText!.Text.ShouldBe("14 cases · 14 cases compared");
        AutomationProperties.GetName(suite).ShouldBe("Larkway support replies");
        suite.Header!.Titles.ShouldBe(["Status", "Case", "Check", "v12", "v13"]);
        suite.BaselinePicker!.SelectedItem.ShouldBeOfType<EvalVersion>().Id.ShouldBe("v12");
        suite.CandidatePicker!.SelectedItem.ShouldBeOfType<EvalVersion>().Id.ShouldBe("v13");
    }

    [AvaloniaFact]
    public void Switching_The_Baseline_Uses_Stored_Results_Without_Running()
    {
        var (suite, _, _) = Show();
        suite.BaselineVersionId = "v11";
        Dispatcher.UIThread.RunJobs();
        suite.Summary.BaselinePassed.ShouldBe(7);
        suite.Summary.Fixes.ShouldBe(6);
        suite.Header!.Titles[3].ShouldBe("v11");

        // The choice edits the property.
        suite.CandidatePicker!.SelectedItem = suite.Versions!.First(v => v.Id == "v12");
        Dispatcher.UIThread.RunJobs();
        suite.CandidateVersionId.ShouldBe("v12");
        suite.Summary.CandidatePassed.ShouldBe(10);
    }

    [AvaloniaFact]
    public void A_Version_Without_Results_Shows_Not_Run()
    {
        var (suite, _, _) = Show(configure: s =>
        {
            s.Versions = [.. EvalSuiteSamples.Versions(), new EvalVersion { Id = "v14", Name = "v14" }];
            s.CandidateVersionId = "v14";
        });
        suite.Summary.Compared.ShouldBe(0);
        suite.AllRows.ShouldAllBe(r => r.Status == EvalCaseStatus.NotRun);
        suite.SubtitleText!.Text.ShouldBe("14 cases · Not run yet");
        suite.DeltaDigits!.Value.ShouldBe(0);
        suite.DeltaDigits.Prefix.ShouldBeNull();
    }

    #endregion

    #region Filters and the open case

    [AvaloniaFact]
    public void Filters_Show_Their_Cases_With_Counts()
    {
        var (suite, _, _) = Show();
        suite.FilterBar!.Options!.Select(o => o.Label).ShouldBe(["All 14", "Regressions 2", "Fixed 3", "Failing 3"]);
        Shown(suite).Length.ShouldBe(14);

        suite.Filter = EvalCaseFilter.Regressions;
        Dispatcher.UIThread.RunJobs();
        Shown(suite).ShouldBe(["greeting", "price"]);
        suite.FilterBar.SelectedKey.ShouldBe("regressions");

        suite.Filter = EvalCaseFilter.Failing;
        Dispatcher.UIThread.RunJobs();
        Shown(suite).ShouldBe(["greeting", "price", "apology"]);

        // The pills edit the property.
        suite.FilterBar.SelectedKey = "fixed";
        suite.FilterBar.RaiseEvent(new RoutedEventArgs(PillFilterBar.SelectionChangedEvent));
        Dispatcher.UIThread.RunJobs();
        suite.Filter.ShouldBe(EvalCaseFilter.Fixed);
        Shown(suite).ShouldBe(["language", "short", "lost"]);
    }

    [AvaloniaFact]
    public void An_Empty_Filter_Says_So()
    {
        var (suite, _, _) = Show(configure: s => s.BaselineVersionId = "v13");
        suite.Filter = EvalCaseFilter.Regressions;
        Dispatcher.UIThread.RunJobs();
        Shown(suite).ShouldBeEmpty();
        AutomationProperties.GetName(suite.List!).ShouldBe("No regression cases.");
    }

    [AvaloniaFact]
    public void Opening_A_Case_Shows_Both_Outputs_And_The_Diff()
    {
        var (suite, _, _) = Show();
        suite.DetailHost!.IsExpanded.ShouldBeFalse();
        suite.OpenCase("greeting");
        Dispatcher.UIThread.RunJobs();
        suite.SelectedCaseId.ShouldBe("greeting");
        suite.Classes.ShouldContain(":open");
        suite.DetailHost.IsExpanded.ShouldBeTrue();
        suite.DetailTitleText!.Text.ShouldBe("Greets by name");
        suite.BaselineOutputText!.Text!.ShouldStartWith("Hi Mara");
        suite.CandidateOutputText!.Text!.ShouldStartWith("Sorry about that.");
        suite.BaselineReasonText!.Text!.ShouldStartWith("Contains “Mara” · ");
        suite.CandidateReasonText!.Text!.ShouldStartWith("Missing “Mara” · ");
        suite.List!.SelectedCaseId.ShouldBe("greeting");
        suite.List.CursorRow!.Case.Id.ShouldBe("greeting");

        suite.DetailMode = EvalDetailMode.Diff;
        Dispatcher.UIThread.RunJobs();
        suite.Classes.ShouldContain(":diff");
        suite.DiffText!.Before.ShouldStartWith("Hi Mara");
        suite.DiffText.InsertedWords.ShouldBeGreaterThan(0);
        suite.DetailModeSwitch!.SelectedIndex.ShouldBe(1);

        Click(suite.CloseButton);
        suite.SelectedCaseId.ShouldBeNull();
        suite.DetailHost.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_List_Is_Driven_By_The_Keyboard()
    {
        var (suite, window, _) = Show();
        var list = suite.List!;
        list.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        AutomationProperties.GetName(list).ShouldBe("1 of 14: Refund window, Passing. Baseline Pass, candidate Pass");
        AutomationProperties.GetHelpText(list).ShouldBe("Cases, 14 shown");

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        AutomationProperties.GetName(list).ShouldBe("2 of 14: Greets by name, Regression. Baseline Pass, candidate Fail");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        suite.SelectedCaseId.ShouldBe("greeting");

        // With a case open, the arrows page through the cases.
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        suite.SelectedCaseId.ShouldBe("ride-id");

        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        list.CursorIndex.ShouldBe(13);
        suite.SelectedCaseId.ShouldBe("helmet");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        suite.SelectedCaseId.ShouldBeNull();

        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        list.CursorIndex.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        suite.SelectedCaseId.ShouldBe("refund");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        suite.SelectedCaseId.ShouldBeNull();
    }

    #endregion

    #region Runs

    [AvaloniaFact]
    public async Task A_Run_Queues_Runs_Two_At_A_Time_And_Reports_Each_Case()
    {
        var (suite, _, gate) = Show();
        var started = 0;
        var completed = new List<(string Id, EvalCaseStatus Status)>();
        EvalSuiteRunCompletedEventArgs? done = null;
        suite.RunStarted += (_, e) =>
        {
            started++;
            e.Cases.Count.ShouldBe(14);
            e.Baseline.Id.ShouldBe("v12");
        };
        suite.CaseCompleted += (_, e) => completed.Add((e.Case.Id, e.Status));
        suite.RunCompleted += (_, e) => done = e;

        Click(suite.RunButton);
        started.ShouldBe(1);
        suite.Phase.ShouldBe(EvalRunPhase.Running);
        suite.Classes.ShouldContain(":running");
        suite.RunButton!.Content.ShouldBe("Stop");
        suite.ProgressBar!.IsVisible.ShouldBeTrue();
        suite.BaselinePicker!.IsEnabled.ShouldBeFalse();
        suite.LastAnnouncement.ShouldBe("Running 14 cases on v12 and v13");
        // Two cases run, each on both versions: four waits.
        gate.Count.ShouldBe(4);
        suite.StatusOf("refund").ShouldBe(EvalCaseStatus.Running);
        suite.StatusOf("greeting").ShouldBe(EvalCaseStatus.Running);
        suite.StatusOf("ride-id").ShouldBe(EvalCaseStatus.Queued);
        // The two versions' old results are gone; v11's stay.
        suite.Results!.Count.ShouldBe(14);
        suite.Summary.Compared.ShouldBe(0);
        suite.SubtitleText!.Text.ShouldBe("14 cases · Running 0 of 14");

        gate.ReleaseAll();
        completed.Select(c => c.Id).ShouldBe(["refund", "greeting"]);
        completed[1].Status.ShouldBe(EvalCaseStatus.Regression);
        suite.FinishedCount.ShouldBe(2);
        suite.ProgressBar.Value.ShouldBe(2);
        suite.Results!.Count.ShouldBe(18);
        suite.StatusOf("greeting").ShouldBe(EvalCaseStatus.Regression);
        suite.StatusOf("ride-id").ShouldBe(EvalCaseStatus.Running);

        while (gate.Count > 0)
        {
            gate.ReleaseAll();
        }
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
        completed.Count.ShouldBe(14);
        suite.Phase.ShouldBe(EvalRunPhase.Completed);
        suite.RunButton.Content.ShouldBe("Run suite");
        suite.ProgressBar.IsVisible.ShouldBeFalse();
        done.ShouldNotBeNull();
        done!.WasStopped.ShouldBeFalse();
        done.Finished.ShouldBe(14);
        done.Summary.Regressions.ShouldBe(2);
        suite.Summary.CandidatePassed.ShouldBe(11);
        suite.Results!.Count.ShouldBe(42);
        suite.LastAnnouncement.ShouldBe("Run complete. v13 passed 11 of 14, +8 points. 2 regressions.");
        suite.SubtitleText.Text!.ShouldStartWith("14 cases · Ran 14 cases in ");
    }

    [AvaloniaFact]
    public void Stopping_Keeps_Finished_Cases_And_Leaves_The_Rest_Not_Run()
    {
        var (suite, _, gate) = Show(configure: s => s.MaxConcurrency = 1);
        EvalSuiteRunCompletedEventArgs? done = null;
        suite.RunCompleted += (_, e) => done = e;
        _ = suite.RunAsync();
        Dispatcher.UIThread.RunJobs();
        gate.Count.ShouldBe(2);
        gate.ReleaseAll();
        suite.FinishedCount.ShouldBe(1);

        Click(suite.RunButton); // Stop
        Dispatcher.UIThread.RunJobs();
        suite.Phase.ShouldBe(EvalRunPhase.Stopped);
        done.ShouldNotBeNull();
        done!.WasStopped.ShouldBeTrue();
        done.Finished.ShouldBe(1);
        suite.StatusOf("refund").ShouldBe(EvalCaseStatus.Passing);
        suite.StatusOf("greeting").ShouldBe(EvalCaseStatus.NotRun);
        suite.StatusOf("helmet").ShouldBe(EvalCaseStatus.NotRun);
        suite.Summary.Compared.ShouldBe(1);
        suite.SubtitleText!.Text.ShouldBe("14 cases · Stopped after 1 of 14");
        suite.LastAnnouncement.ShouldBe("Run stopped after 1 of 14 cases.");
    }

    [AvaloniaFact]
    public async Task Your_Runner_Runs_The_Cases_And_A_Failure_Becomes_An_Error()
    {
        var calls = new List<string>();
        var (suite, _, _) = Show(configure: s =>
        {
            s.Versions = [new EvalVersion { Id = "a", Name = "A" }, new EvalVersion { Id = "b", Name = "B" }];
            s.BaselineVersionId = "a";
            s.CandidateVersionId = "b";
            s.Cases = [new EvalCase { Id = "one", Title = "One", Expected = "yes" }, new EvalCase { Id = "two", Title = "Two", Expected = "yes" }];
            s.Results = [];
            s.RunCaseAsync = (evalCase, version, _) =>
            {
                calls.Add(evalCase.Id + version.Id);
                if (evalCase.Id == "two" && version.Id == "b")
                {
                    throw new InvalidOperationException("Model timed out");
                }
                return Task.FromResult(EvalSuite.Grade(evalCase, "ignored", "yes"));
            };
        });
        suite.IsSimulated.ShouldBeFalse();
        suite.SubtitleText!.Text.ShouldBe("2 cases · Not run yet");
        await suite.RunAsync();
        calls.ShouldBe(["onea", "oneb", "twoa", "twob"], ignoreOrder: true);
        suite.Phase.ShouldBe(EvalRunPhase.Completed);
        suite.StatusOf("one").ShouldBe(EvalCaseStatus.Passing);
        suite.StatusOf("two").ShouldBe(EvalCaseStatus.Regression);
        var error = suite.Results!.Single(r => r.CaseId == "two" && r.VersionId == "b");
        error.IsError.ShouldBeTrue();
        error.Detail.ShouldBe("Model timed out");
        // The suite stamps the ids of the case and the version it ran.
        suite.Results!.Single(r => r.CaseId == "one" && r.VersionId == "b").Passed.ShouldBeTrue();
        suite.OpenCase("two");
        Dispatcher.UIThread.RunJobs();
        suite.CandidateReasonText!.Text!.ShouldStartWith("Model timed out");
    }

    [AvaloniaFact]
    public void Changing_The_Versions_During_A_Run_Stops_It()
    {
        var (suite, _, _) = Show();
        _ = suite.RunAsync();
        Dispatcher.UIThread.RunJobs();
        suite.Phase.ShouldBe(EvalRunPhase.Running);
        suite.BaselineVersionId = "v11";
        Dispatcher.UIThread.RunJobs();
        suite.Phase.ShouldBe(EvalRunPhase.Stopped);
    }

    [AvaloniaFact]
    public void A_Finished_Case_Fades_In_Under_Full_Motion()
    {
        var (suite, _, gate) = Show(level: ArcMotionLevel.Full);
        // Read the reveal when the case lands: frames may finish the 240 ms fade before the test looks again.
        double? reveal = null;
        suite.CaseCompleted += (_, e) => reveal ??= suite.AllRows.First(r => r.Case.Id == e.Case.Id).Reveal;
        _ = suite.RunAsync();
        Dispatcher.UIThread.RunJobs();
        gate.ReleaseAll();
        reveal.ShouldBe(0);
        suite.Stop();
        Dispatcher.UIThread.RunJobs();
    }

    #endregion

    #region Layout

    [AvaloniaFact]
    public void A_Narrow_Suite_Stacks_And_Hides_The_Check_Column()
    {
        var (suite, _, _) = Show(width: 420);
        suite.Classes.ShouldContain(":compact");
        suite.List!.IsCompact.ShouldBeTrue();
        suite.TilesGrid!.ColumnDefinitions.Count.ShouldBe(1);
        suite.TilesGrid.RowDefinitions.Count.ShouldBe(3);
        suite.OutputsGrid!.ColumnDefinitions.Count.ShouldBe(1);
        var columns = suite.List.Columns;
        columns.CheckWidth.ShouldBe(0);
        columns.CaseWidth.ShouldBeGreaterThan(0);
        (columns.CandidateX + columns.VerdictWidth).ShouldBeLessThanOrEqualTo(suite.List.Bounds.Width + 0.5);

        var (wide, _, _) = Show(width: 900);
        wide.Classes.ShouldNotContain(":compact");
        wide.TilesGrid!.ColumnDefinitions.Count.ShouldBe(3);
        wide.List!.Columns.CheckWidth.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void The_List_Measures_One_Row_Per_Case()
    {
        var (suite, _, _) = Show();
        suite.List!.Bounds.Height.ShouldBe(14 * 56, 0.5);
        suite.Cases = [];
        Dispatcher.UIThread.RunJobs();
        AutomationProperties.GetName(suite.List).ShouldBe("This suite has no cases yet.");
        suite.RunButton!.IsEnabled.ShouldBeFalse();
    }

    #endregion
}

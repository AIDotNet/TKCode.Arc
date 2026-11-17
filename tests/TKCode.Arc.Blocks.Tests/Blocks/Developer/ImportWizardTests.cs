using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// <see cref="ImportWizard"/>: the session rules on the sample file (counts, clean-up, records, the skipped rows file),
/// then the card's flow (loading and its errors, the step bar, importing in batches, failures and retries, cancelling,
/// starting over, the compact layout).
/// </summary>
public class ImportWizardTests
{
    private sealed class Rig : IDisposable
    {
        public Rig(double width = 760, Action<ImportWizard>? configure = null)
        {
            Wizard = new ImportWizard { TimingScale = 0 };
            configure?.Invoke(Wizard);
            Wizard.StepChanged += (_, e) => Steps.Add(e.NewStep);
            Wizard.Completed += (_, e) => Completions.Add(e);
            Wizard.Failed += (_, e) => Failures.Add(e);
            Wizard.BatchImported += (_, e) => Batches.Add(e);
            Host = new Border { Width = width, Child = Wizard, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, ArcMotionLevel.Off);
            Window.Width = 1000;
            Window.Height = 1400;
            Pump();
        }

        public ImportWizard Wizard { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<ImportWizardStep> Steps { get; } = new();

        public List<ImportWizardCompletedEventArgs> Completions { get; } = new();

        public List<ImportWizardFailedEventArgs> Failures { get; } = new();

        public List<ImportWizardBatchEventArgs> Batches { get; } = new();

        public void Pump()
        {
            for (var i = 0; i < 4; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public void LoadSample()
        {
            Wizard.LoadCsv(Wizard.SampleCsv!, Wizard.SampleFileName).ShouldBeTrue();
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    private static ImportWizardSession SampleSession()
    {
        var session = new ImportWizardSession(ImportWizardDefaults.CreateContactTargets().ToArray(), ImportWizardDefaults.CreateSampleExistingKeys());
        session.Load("customers.csv", 100, ImportWizardCsv.Parse(ImportWizardDefaults.SampleCsv)).ShouldBeTrue();
        return session;
    }

    // ---- Session ----

    [Fact]
    public void The_sample_has_22_rows_in_8_columns_and_maps_7()
    {
        var session = SampleSession();
        session.Table.Rows.Count.ShouldBe(22);
        session.Columns.Count.ShouldBe(8);
        session.MappedCount.ShouldBe(7);
        session.Columns[7].IsIgnored.ShouldBeTrue();
        session.IsMappingValid.ShouldBeTrue();
    }

    [Fact]
    public void The_sample_review_has_nine_rows_to_fix_and_five_safe_fixes()
    {
        var session = SampleSession();
        session.BuildReview();
        session.Fields.Count.ShouldBe(7);
        session.TotalRows.ShouldBe(22);
        session.IssueRows.ShouldBe(9);
        session.ReadyRows.ShouldBe(13);
        session.FixableCells.ShouldBe(5);
        var records = session.BuildRecords();
        records.Count.ShouldBe(13);
        records.Count(r => r.IsUpdate).ShouldBe(4);
        session.SkippedRows.ShouldBe(9);
    }

    [Fact]
    public void Cleaning_up_leaves_four_rows_out_and_imports_14_new_and_4_updated()
    {
        var session = SampleSession();
        session.BuildReview();
        foreach (var (row, field) in session.FixableCellList())
        {
            session.CleanUp(row, field).ShouldBeTrue();
        }
        session.FixableCells.ShouldBe(0);
        session.IssueRows.ShouldBe(4);
        session.ReadyRows.ShouldBe(18);
        var records = session.BuildRecords();
        records.Count(r => !r.IsUpdate).ShouldBe(14);
        records.Count(r => r.IsUpdate).ShouldBe(4);
        // The fixes: trimmed lower-case emails, spelled-out countries, the exact plan name.
        records.ShouldContain(r => r.Values["email"] == "rafael.ortiz@kiln.example");
        records.ShouldContain(r => r.Values["email"] == "mei.tanaka@bramble.example");
        records.ShouldContain(r => r.Values["country"] == "United States" && r.Values["name"] == "Hannah Weiss");
        records.ShouldContain(r => r.Values["country"] == "Netherlands");
        records.ShouldContain(r => r.Values["plan"] == "Team" && r.Values["name"] == "Samuel Okafor");
    }

    [Fact]
    public void Editing_validates_again_and_leaving_out_counts_as_skipped()
    {
        var session = SampleSession();
        session.BuildReview();
        var nameField = session.Fields.ToList().IndexOf(0);
        var missingName = session.Rows.Single(r => r.Problems[nameField] is not null);
        session.Edit(missingName, nameField, "Chen Wei").ShouldBeTrue();
        missingName.HasProblems.ShouldBeFalse();
        missingName.Fixed[nameField].ShouldBeTrue();
        session.Edit(missingName, nameField, "  ").ShouldBeFalse();
        missingName.HasProblems.ShouldBeTrue();

        var clean = session.Rows.First(r => !r.HasProblems);
        session.Remove(clean);
        session.TotalRows.ShouldBe(21);
        session.ReadyRows.ShouldBe(12);
        session.SkippedRows.ShouldBe(10);
    }

    [Fact]
    public void The_review_keeps_its_edits_until_the_mapping_changes()
    {
        var session = SampleSession();
        session.BuildReview();
        var row = session.Rows[0];
        session.Edit(row, 0, "Renamed");
        session.BuildReview();
        session.Rows[0].Values[0].ShouldBe("Renamed");
        session.SetTarget(2, -1);
        session.BuildReview();
        session.Rows[0].Values[0].ShouldBe("Amara Osei");
        session.Fields.Count.ShouldBe(6);
    }

    [Fact]
    public void The_skipped_rows_file_lists_line_values_and_the_problem()
    {
        var session = SampleSession();
        session.BuildReview();
        var csv = session.SkippedRowsCsv();
        var lines = csv.TrimEnd('\n').Split('\n');
        lines[0].ShouldBe("Line,Name,Email,Company,Role,Phone,Country,Plan,Problem");
        lines.Length.ShouldBe(10);
        lines[1].ShouldStartWith("3,Kwame Asante,kwame.asante@@larkspur.example,");
        lines[1].ShouldEndWith(",Check the email format");
        ImportWizardCsv.Parse(csv).Rows.Count.ShouldBe(9);
    }

    [Fact]
    public void Mapping_needs_every_required_field_once()
    {
        var session = SampleSession();
        session.SetTarget(0, -1);
        session.FirstMissingRequired().ShouldBe(0);
        session.IsMappingValid.ShouldBeFalse();
        session.SetTarget(0, 0);
        session.SetTarget(2, 0);
        session.FirstDuplicateTarget().ShouldBe(0);
        session.IsDuplicate(0).ShouldBeTrue();
        session.IsMappingValid.ShouldBeFalse();
        session.Columns[2].IsChosen.ShouldBeTrue();
        session.Columns[2].Confidence.ShouldBe(100);
    }

    // ---- Card ----

    [AvaloniaFact]
    public void Loading_the_sample_shows_the_file_and_enables_the_next_step()
    {
        using var rig = new Rig();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Upload);
        var view = rig.Wizard.CurrentView.ShouldBeOfType<ImportWizardUploadView>();
        view.NextButton.IsEnabled.ShouldBeFalse();
        view.SampleButton.ShouldNotBeNull();
        view.DropZone.ShouldNotBeNull();

        rig.LoadSample();
        rig.Wizard.FileName.ShouldBe(ImportWizardDefaults.SampleFileName);
        rig.Wizard.RowCount.ShouldBe(22);
        view.NextButton.IsEnabled.ShouldBeTrue();
        view.SampleButton.ShouldBeNull();
        view.FileCard.ShouldNotBeNull();
        view.FileCard!.Facts.ShouldStartWith("22 rows · 8 columns · ");
        view.FileCard.Preview.ShouldNotBeNull();
        rig.Wizard.Announcer!.Announcement.ShouldBe("customers-q3.csv read, 22 rows.");
    }

    [AvaloniaTheory]
    [InlineData("one column\nvalue\n", "We couldn't find a header row")]
    [InlineData("a,b\n", "We couldn't find a header row")]
    [InlineData("a,b\n\"never closed,1\n", "A quoted value in that file never ends")]
    public void Files_that_cannot_be_mapped_show_an_error(string text, string message)
    {
        using var rig = new Rig();
        rig.Wizard.LoadCsv(text, "bad.csv").ShouldBeFalse();
        rig.Pump();
        rig.Wizard.UploadPhase.ShouldBe(ImportUploadPhase.Error);
        var view = rig.Wizard.CurrentView.ShouldBeOfType<ImportWizardUploadView>();
        view.ErrorLine.IsVisible.ShouldBeTrue();
        view.ErrorLine.Text!.ShouldStartWith(message);
        view.NextButton.IsEnabled.ShouldBeFalse();
        rig.Wizard.FileName.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_file_over_the_row_limit_is_refused()
    {
        using var rig = new Rig(configure: w => w.MaxRows = 10);
        var text = "a,b\n" + string.Join("\n", Enumerable.Range(0, 11).Select(i => $"{i},{i}"));
        rig.Wizard.LoadCsv(text, "big.csv").ShouldBeFalse();
        rig.Pump();
        rig.Wizard.UploadError.ShouldBe("That file has 11 rows. Split it into files of 10 rows or fewer.");
    }

    [AvaloniaFact]
    public void Removing_the_file_brings_the_drop_zone_back()
    {
        using var rig = new Rig();
        rig.LoadSample();
        var view = (ImportWizardUploadView)rig.Wizard.CurrentView!;
        view.FileCard!.RemoveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Wizard.FileName.ShouldBeNull();
        view.DropZone.ShouldNotBeNull();
        view.SampleButton.ShouldNotBeNull();
        view.NextButton.IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_step_bar_marks_done_steps_fills_their_lines_and_lets_them_jump_back()
    {
        using var rig = new Rig();
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Map);
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Pump();
        var stepper = rig.Wizard.Stepper!;
        stepper.Buttons.Select(b => b.State).ShouldBe([ImportStepState.Done, ImportStepState.Done, ImportStepState.Current, ImportStepState.Todo]);
        stepper.Buttons.Select(b => b.IsEnabled).ShouldBe([true, true, false, false]);
        stepper.Connectors.Select(c => c.Fill).ShouldBe([1.0, 1.0, 0.0]);
        Avalonia.Automation.AutomationProperties.GetName(stepper.Buttons[0]).ShouldBe("Upload, step 1 of 4, done");

        stepper.Buttons[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Upload);
        rig.Steps.ShouldBe([ImportWizardStep.Map, ImportWizardStep.Review, ImportWizardStep.Upload]);
        stepper.Connectors.Select(c => c.Fill).ShouldBe([0.0, 0.0, 0.0]);
        // Back on the upload step the file is still there.
        ((ImportWizardUploadView)rig.Wizard.CurrentView!).FileCard.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Importing_sends_the_ready_rows_in_batches_and_reports_the_totals()
    {
        var sent = new List<IReadOnlyList<ImportRecord>>();
        using var rig = new Rig(configure: w =>
        {
            w.BatchSize = 5;
            w.ImportBatchAction = (records, _) =>
            {
                sent.Add(records);
                return Task.CompletedTask;
            };
        });
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Map);
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        sent.Select(b => b.Count).ShouldBe([5, 5, 3]);
        rig.Batches.Select(b => (b.Batch, b.BatchCount, b.Imported)).ShouldBe([(1, 3, 5), (2, 3, 10), (3, 3, 13)]);
        var done = rig.Completions.ShouldHaveSingleItem();
        (done.Created, done.Updated, done.Skipped).ShouldBe((9, 4, 9));
        rig.Wizard.IsImportDone.ShouldBeTrue();
        rig.Wizard.Stepper!.Buttons.Select(b => b.State).ShouldAllBe(s => s == ImportStepState.Done);
        rig.Wizard.Stepper.Buttons.ShouldAllBe(b => !b.IsEnabled);
        var view = rig.Wizard.CurrentView.ShouldBeOfType<ImportWizardImportView>();
        var summary = view.Summary.ShouldNotBeNull();
        summary.Title.PlainText.ShouldBe("Imported 13 contacts");
        summary.Numbers.Select(n => n.PlainText).ShouldBe(["9", "4", "9"]);
    }

    [AvaloniaFact]
    public void The_completed_command_gets_the_totals()
    {
        ImportWizardCompletedEventArgs? received = null;
        using var rig = new Rig(configure: w => w.CompletedCommand = new RelayCommand(p => received = p as ImportWizardCompletedEventArgs));
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        received.ShouldNotBeNull().Created.ShouldBe(9);
    }

    [AvaloniaFact]
    public void A_failed_batch_stops_shows_the_message_and_can_be_retried()
    {
        var calls = 0;
        using var rig = new Rig(configure: w => w.ImportBatchAction = (_, _) =>
        {
            calls++;
            return calls == 2 ? Task.FromException(new InvalidOperationException("Server busy")) : Task.CompletedTask;
        });
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        rig.Wizard.RunPhase.ShouldBe(ImportRunPhase.Failed);
        rig.Wizard.RunError.ShouldBe("This batch failed: Server busy");
        rig.Failures.ShouldHaveSingleItem().Batch.ShouldBe(2);
        rig.Wizard.RunImported.ShouldBe(5);

        rig.Wizard.RetryImport();
        rig.Pump();
        rig.Completions.ShouldHaveSingleItem();
        calls.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Cancelling_goes_back_to_the_review_and_cancels_the_batch()
    {
        var gate = new TaskCompletionSource();
        CancellationToken seen = default;
        using var rig = new Rig(configure: w => w.ImportBatchAction = (_, token) =>
        {
            seen = token;
            return gate.Task;
        });
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Import);
        rig.Wizard.GoBack();
        rig.Pump();
        seen.IsCancellationRequested.ShouldBeTrue();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Review);
        gate.SetResult();
        rig.Pump();
        rig.Completions.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Starting_over_forgets_the_file_and_returns_to_the_upload_step()
    {
        using var rig = new Rig();
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        var summary = ((ImportWizardImportView)rig.Wizard.CurrentView!).Summary!;
        summary.RestartButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Upload);
        rig.Wizard.FileName.ShouldBeNull();
        rig.Wizard.Stepper!.Connectors.Select(c => c.Fill).ShouldBe([0.0, 0.0, 0.0]);
        ((ImportWizardUploadView)rig.Wizard.CurrentView!).DropZone.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Saving_the_left_out_rows_uses_the_action_and_relabels_the_button()
    {
        string? saved = null;
        using var rig = new Rig(configure: w => w.SaveSkippedRowsAction = (csv, _) =>
        {
            saved = csv;
            return Task.FromResult(true);
        });
        rig.LoadSample();
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        var summary = ((ImportWizardImportView)rig.Wizard.CurrentView!).Summary!;
        summary.DownloadButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        saved.ShouldNotBeNull().ShouldStartWith("Line,Name,Email");
        summary.DownloadButton.Text.ShouldBe("Downloaded");
    }

    [AvaloniaFact]
    public void A_narrow_card_turns_compact_and_shows_the_step_counter()
    {
        using var rig = new Rig(width: 400);
        rig.Pump();
        rig.Wizard.IsCompact.ShouldBeTrue();
        rig.Wizard.Classes.ShouldContain(ImportWizard.CompactPseudoClass);
        rig.Wizard.Stepper!.Counter.IsVisible.ShouldBeTrue();
        rig.Wizard.Stepper.Counter.PlainText.ShouldBe("Step 1 of 4");
        rig.Host.Width = 700;
        rig.Pump();
        rig.Wizard.IsCompact.ShouldBeFalse();
        rig.Wizard.Stepper.Counter.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Custom_targets_and_nouns_flow_into_the_copy()
    {
        IList<ImportTarget> targets =
        [
            new ImportTarget { Key = "sku", Label = "SKU", IsRequired = true, IsKey = true },
            new ImportTarget { Key = "title", Label = "Title" },
        ];
        using var rig = new Rig(configure: w =>
        {
            w.Targets = targets;
            w.Noun = "product";
            w.PluralNoun = "products";
            w.ExistingKeys = new HashSet<string> { "A-1" };
        });
        rig.Wizard.LoadCsv("SKU,Title\nA-1,Lamp\nB-2,Desk\n", "products.csv").ShouldBeTrue();
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Wizard.StartImport();
        rig.Pump();
        var done = rig.Completions.ShouldHaveSingleItem();
        (done.Created, done.Updated, done.Skipped).ShouldBe((1, 1, 0));
        ((ImportWizardImportView)rig.Wizard.CurrentView!).Summary!.Title.PlainText.ShouldBe("Imported 2 products");
    }

    private sealed class RelayCommand(Action<object?> run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run(parameter);
    }
}

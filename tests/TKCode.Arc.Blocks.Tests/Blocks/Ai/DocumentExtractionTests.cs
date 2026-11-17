using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The document extraction block: runs, stops, failures, edits, checks and Accept all.</summary>
public class DocumentExtractionTests
{
    [AvaloniaFact]
    public void Idle_Shows_The_Fields_To_Fill_And_No_Values()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        Assert.Equal(ExtractionState.Idle, x.State);
        Assert.Same(rig.Invoice, x.SelectedSample);
        Assert.Equal(AiRunStatusKind.Idle, x.StatusLine!.Kind);
        Assert.Equal("10 fields to fill from this document", x.StatusLine.Text);
        Assert.True(x.SummaryNote!.IsVisible);
        Assert.Equal("No values yet", x.SummaryNote.Text);
        Assert.False(x.AverageGroup!.IsVisible);
        Assert.False(x.FlagGroup!.IsVisible);
        Assert.False(x.AcceptAllButton!.IsEnabled);
        Assert.True(x.ActionButton!.IsEnabled);
        Assert.Equal(ButtonVariant.Primary, x.ActionButton.Variant);
        Assert.Equal("$0.00", x.Meta!.CostText);
        Assert.Equal("0.0 s", x.Meta.ElapsedText);
        Assert.Equal("Lumen Extract S", x.Meta.ModelName);
        Assert.Equal(0, x.FoundCount);
        Assert.Equal(10, x.FieldCount);
        Assert.All(x.Bars!.Items!, bar => Assert.Equal(ConfidenceBarState.Pending, bar.State));
        Assert.Equal(10, x.Bars.Items!.Count);
    }

    [AvaloniaFact]
    public void Extract_Is_Disabled_Without_An_Extractor()
    {
        using var rig = new DocumentExtractionRig(configure: x => x.Extractor = null);
        Assert.False(rig.Extraction.ActionButton!.IsEnabled);
        Assert.True(rig.Extraction.StartAsync().IsCompleted);
        Assert.Equal(ExtractionState.Idle, rig.Extraction.State);
    }

    [AvaloniaFact]
    public void A_Run_Reads_Then_Fills_The_Invoice_And_Ends_Done()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        var started = 0;
        var completed = new List<ExtractionState>();
        x.ExtractionStarted += (_, _) => started++;
        x.ExtractionCompleted += (_, e) => completed.Add(e.Result);
        var run = rig.Start();

        Assert.Equal(ExtractionState.Reading, x.State);
        Assert.Equal(1, started);
        Assert.Equal(AiRunStatusKind.Running, x.StatusLine!.Kind);
        Assert.Equal("Reading the document", x.StatusLine.Text);
        Assert.Equal("0 of 10", x.StatusLine.CountText);
        Assert.Equal("Reading", x.SummaryNote!.Text);
        Assert.Equal("Stop", AutomationName(x.ActionButton!));
        Assert.Equal(ButtonVariant.Secondary, x.ActionButton!.Variant);
        Assert.Equal("$0.0000", x.Meta!.CostText);
        Assert.Equal("Extracting 10 fields", x.LastRunAnnouncement);
        Assert.Equal("Rows appear here as they are found", x.TableOf("lineItems")!.ShownEmptyText);

        rig.Step(2);
        Assert.Equal(ExtractionState.Extracting, x.State);
        Assert.Equal("Extracting fields", x.StatusLine.Text);
        Assert.Equal("1 of 10", x.StatusLine.CountText);
        Assert.Equal("Oakridge Ceramics Ltd", rig.Row("vendor").Value!.Text);
        Assert.True(x.AverageGroup!.IsVisible);
        Assert.Equal("99%", x.AverageText!.Text);
        Assert.False(x.FlagGroup!.IsVisible);

        rig.RunToEnd();
        Assert.Equal(ExtractionState.Done, x.State);
        Assert.True(run.IsCompleted);
        Assert.Equal(new[] { ExtractionState.Done }, completed);
        Assert.Equal(25, x.FoundCount);
        Assert.Equal(25, x.FieldCount);
        Assert.Equal(10, rig.Store.FoundFieldCount);
        Assert.Equal(2, x.CheckCount);
        Assert.Equal(23.27 / 25, x.AverageConfidence, 6);
        Assert.Equal("93%", x.AverageText.Text);
        Assert.Equal(1180 * 0.12m / 1_000_000m + 210 * 0.60m / 1_000_000m, x.Cost);
        Assert.Equal("$0.0003", x.Meta.CostText);
        Assert.Equal(AiRunStatusKind.NeedsCheck, x.StatusLine.Kind);
        Assert.Equal("2 fields need a check", x.StatusLine.Text);
        Assert.True(x.FlagGroup.IsVisible);
        Assert.Equal("2 to check", x.FlagText!.Text);
        Assert.Equal("Found 10 of 10 fields, 2 to check", x.LastRunAnnouncement);
        Assert.Equal("Extract again", AutomationName(x.ActionButton));
        Assert.True(x.AcceptAllButton!.IsEnabled);
        Assert.Equal("4 rows", x.TableOf("lineItems")!.RowsText);
        Assert.Equal(25, x.Values.Count);
    }

    [AvaloniaFact]
    public void The_Output_Is_Typed_And_The_Bars_Follow_The_Values()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        rig.FullRun();
        var x = rig.Extraction;
        var output = x.Output.Values.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("2026-09-03", output["issueDate"]);
        Assert.Equal(2686.20m, output["total"]);
        Assert.Equal("NL70ZZTE0417263598", output["iban"]);
        Assert.Equal(4, ((System.Collections.IList)output["lineItems"]!).Count);
        Assert.Contains("\"vendor\": \"Oakridge Ceramics Ltd\"", x.JsonView!.Text);
        Assert.Equal(x.JsonView.Text, x.JsonCopy!.Value);
        Assert.Equal("Fits the schema and follows your edits", x.JsonNote!.Text);
        Assert.Contains("\"$schema\"", x.SchemaView!.Text);
        Assert.Equal("Sent to lumen-extract-s as the output format", x.SchemaNote!.Text);
        var bars = x.Bars!.Items!;
        Assert.Equal(ConfidenceBarState.Ok, bars[0].State);
        Assert.Equal(0.99, bars[0].Confidence!.Value, 6);
        Assert.Equal(ConfidenceBarState.Check, bars[3].State);
        Assert.Equal(0.94, bars[4].Confidence!.Value, 6);
        Assert.Equal(ConfidenceBarState.Check, bars[9].State);
    }

    [AvaloniaFact]
    public void The_Clock_Shows_Tenths_And_Is_Never_Negative()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        _ = rig.Start();
        rig.Time.Advance(TimeSpan.FromSeconds(-5));
        rig.Frames.Advance(DocumentExtractionRig.FrameStep, DocumentExtractionRig.FrameStep);
        rig.Pump();
        Assert.Equal(TimeSpan.Zero, x.Elapsed);
        Assert.Equal("0.0 s", x.Meta!.ElapsedText);
        rig.Time.Advance(TimeSpan.FromSeconds(5));
        rig.Advance(TimeSpan.FromMilliseconds(1240));
        Assert.Equal(TimeSpan.FromMilliseconds(1200), x.Elapsed);
        Assert.Equal("1.2 s", x.Meta.ElapsedText);
        rig.RunToEnd();
        var final = x.Elapsed;
        rig.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(final, x.Elapsed);
        Assert.Equal(0, final.Ticks % TimeSpan.FromMilliseconds(100).Ticks);
    }

    [AvaloniaFact]
    public void Events_Of_One_Turn_Are_One_Update()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        var counts = new List<int>();
        x.PropertyChanged += (_, e) =>
        {
            if (e.Property == DocumentExtraction.FoundCountProperty)
            {
                counts.Add(x.FoundCount);
            }
        };
        rig.FullRun();
        Assert.Equal(new[] { 25 }, counts);
        Assert.Equal(ExtractionState.Done, x.State);
    }

    [AvaloniaFact]
    public void Stop_Keeps_The_Values_And_Says_So()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        var completed = new List<ExtractionState>();
        x.ExtractionCompleted += (_, e) => completed.Add(e.Result);
        var run = rig.Start();
        rig.Step(6);
        Assert.Equal(4, rig.Store.FoundFieldCount);
        rig.Click(x.ActionButton!);
        Assert.Equal(ExtractionState.Stopped, x.State);
        Assert.True(run.IsCompleted);
        Assert.True(rig.Gate.SawCancel);
        Assert.Equal(new[] { ExtractionState.Stopped }, completed);
        Assert.Equal("Oakridge Ceramics Ltd", rig.Row("vendor").Value!.Text);
        Assert.Equal(4, x.FoundCount);
        Assert.Equal(AiRunStatusKind.Stopped, x.StatusLine!.Kind);
        Assert.Equal("Stopped after 4 of 10 fields", x.StatusLine.Text);
        Assert.Equal(new DocumentExtractionLabels().StoppedNote, x.StatusLine.DetailText);
        Assert.Equal("Extraction stopped", x.LastRunAnnouncement);
        Assert.Equal("Extract again", AutomationName(x.ActionButton!));
        Assert.True(x.AcceptAllButton!.IsEnabled);
        // Released waits of the cancelled stream change nothing.
        rig.Step(3);
        Assert.Equal(4, x.FoundCount);
    }

    [AvaloniaFact]
    public void Escape_Stops_A_Run()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        _ = rig.Start();
        rig.Step(3);
        x.ActionButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Escape);
        Assert.Equal(ExtractionState.Stopped, x.State);
        Assert.Equal(2, x.FoundCount);
    }

    [AvaloniaFact]
    public void A_Failure_Keeps_The_Values_And_Retry_Starts_Over()
    {
        using var rig = new DocumentExtractionRig(configure: x => x.Extractor = null);
        var x = rig.Extraction;
        x.SelectedSample = rig.Email;
        x.Extractor = rig.EmailExtractor;
        rig.EmailExtractor.FailAfter = 4;
        rig.Pump();
        Assert.Equal("12 fields to fill from this email", x.StatusLine!.Text);
        _ = rig.Start();
        rig.RunToEnd();

        Assert.Equal(ExtractionState.Failed, x.State);
        Assert.Equal("The model is busy right now. Nothing was charged, so you can try again.", x.ErrorMessage);
        Assert.Equal(4, x.FoundCount);
        Assert.Equal(AiRunStatusKind.Failed, x.StatusLine.Kind);
        Assert.Equal("Extraction failed after 4 of 12 fields", x.StatusLine.Text);
        Assert.Equal(x.ErrorMessage, x.StatusLine.DetailText);
        Assert.StartsWith("Extraction failed: The model is busy", x.LastRunAnnouncement);
        Assert.True(x.AverageGroup!.IsVisible);
        Assert.False(x.FlagGroup!.IsVisible);
        Assert.Equal("Retry", AutomationName(x.ActionButton!));
        Assert.Equal(ButtonVariant.Primary, x.ActionButton!.Variant);
        Assert.False(x.AcceptAllButton!.IsEnabled);

        rig.Click(x.ActionButton);
        Assert.Equal(ExtractionState.Reading, x.State);
        Assert.Equal(0, x.FoundCount);
        Assert.Null(x.ErrorMessage);
        rig.RunToEnd();
        Assert.Equal(ExtractionState.Done, x.State);
        Assert.Equal(12, rig.Store.FoundFieldCount);
        Assert.Equal(2, rig.EmailExtractor.Runs);
    }

    [AvaloniaFact]
    public void An_Error_Event_Fails_The_Run_With_Its_Message()
    {
        IReadOnlyList<ExtractionEvent> events =
        [
            new ExtractionValueEvent("vendor", "Oakridge Ceramics Ltd", "vendor", 0.99),
            new ExtractionErrorEvent("Rate limited, try again in a minute."),
            new ExtractionValueEvent("invoiceNumber", "OC-2026-1187", "invoiceNumber", 0.98)
        ];
        using var rig = new DocumentExtractionRig(instant: true, invoiceEvents: events);
        var x = rig.Extraction;
        rig.FullRun();
        Assert.Equal(ExtractionState.Failed, x.State);
        Assert.Equal("Rate limited, try again in a minute.", x.ErrorMessage);
        Assert.Equal(1, x.FoundCount);
        Assert.Equal("Extraction failed after 1 of 10 fields", x.StatusLine!.Text);
    }

    [AvaloniaFact]
    public void A_New_Run_Cancels_The_Old_One_And_Clears()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        _ = rig.Start();
        rig.Step(4);
        Assert.Equal(3, x.FoundCount);
        _ = rig.Start();
        Assert.True(rig.Gate.SawCancel);
        Assert.Equal(ExtractionState.Reading, x.State);
        Assert.Equal(0, x.FoundCount);
        Assert.Equal(2, rig.Extractor.Runs);
        Assert.Equal(string.Empty, rig.Row("vendor").Value!.Text);
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Stops_The_Run()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        var run = rig.Start();
        rig.Step(3);
        rig.Host.Child = null;
        rig.Pump();
        Assert.Equal(ExtractionState.Stopped, x.State);
        Assert.True(rig.Gate.SawCancel);
        Assert.True(run.IsCompleted);
        Assert.Equal(2, x.FoundCount);
    }

    [AvaloniaFact]
    public void Choosing_Another_Sample_Clears_And_Rebuilds_The_Form()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        x.SelectedPath = "vendor";
        rig.Pump();
        rig.Extraction.Viewer!.SelectedDocument = rig.Email.Document;
        rig.Pump();
        Assert.Same(rig.Email, x.SelectedSample);
        Assert.Same(rig.Email.Document, x.Viewer!.Document);
        Assert.Equal(ExtractionState.Idle, x.State);
        Assert.Null(x.SelectedPath);
        Assert.Equal(0, x.FoundCount);
        Assert.NotNull(x.RowOf("merchant"));
        Assert.Null(x.RowOf("vendor"));
        Assert.Equal("12 fields to fill from this email", x.StatusLine!.Text);
        Assert.Equal(12, x.Bars!.Items!.Count);
        Assert.Equal("$0.00", x.Meta!.CostText);
    }

    [AvaloniaFact]
    public void Extract_Runs_The_Extract_Command()
    {
        object? parameter = null;
        var command = new RelayCommand(p => parameter = p);
        using var rig = new DocumentExtractionRig(configure: x => x.ExtractCommand = command);
        rig.Click(rig.Extraction.ActionButton!);
        Assert.Equal(ExtractionState.Reading, rig.Extraction.State);
        Assert.Same(rig.Invoice.Document, parameter);
    }

    [AvaloniaFact]
    public void Editing_Makes_The_Value_Edited_And_Updates_The_Output()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var changes = new List<ExtractionValueChangedEventArgs>();
        x.ValueChanged += (_, e) => changes.Add(e);
        rig.Row("vendor").FocusInput(NavigationMethod.Tab);
        rig.Pump();
        rig.Retype("Oakridge Studio");
        var value = rig.Row("vendor").Value!;
        Assert.Equal(ExtractionValueStatus.Edited, value.Status);
        Assert.Equal("Oakridge Studio", value.Text);
        Assert.Contains(changes, c => c.Path == "vendor" && c.Origin == ExtractionOrigin.User && c.NewText == "Oakridge Studio");
        Assert.Equal("Oakridge Studio", x.Output.Values.First(p => p.Key == "vendor").Value);
        Assert.Contains("\"vendor\": \"Oakridge Studio\"", x.JsonView!.Text);
        Assert.Equal("Edited", rig.Row("vendor").ConfidenceAutomationText);
    }

    [AvaloniaFact]
    public void An_Invalid_Value_Is_Null_And_Blocks_Accept_All()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var accepting = 0;
        x.Accepting += (_, _) => accepting++;
        rig.Row("issueDate").FocusInput(NavigationMethod.Tab);
        rig.Pump();
        rig.Retype("3 Sep");
        Assert.Equal("Use YYYY-MM-DD", rig.Row("issueDate").Value!.Error);
        Assert.Null(x.Output.Values.First(p => p.Key == "issueDate").Value);
        Assert.Equal("1 value does not fit its type and is set to null", x.JsonNote!.Text);

        x.SelectedTab = ExtractionTab.Json;
        x.AcceptAllButton!.Focus();
        rig.Pump();
        rig.Click(x.AcceptAllButton);
        Assert.Equal(0, accepting);
        Assert.Equal(ExtractionState.Done, x.State);
        Assert.Equal(ExtractionTab.Fields, x.SelectedTab);
        Assert.Equal("issueDate", x.SelectedPath);
        Assert.True(rig.FocusIsIn(rig.Row("issueDate")));
        Assert.Equal("Fix Issue date first: its value does not fit the type", x.LastReviewAnnouncement);
    }

    [AvaloniaFact]
    public void Looks_Right_Checks_The_Value_And_Announces_It()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var checkedPaths = new List<string?>();
        x.ValueChecked += (_, e) => checkedPaths.Add(e.Path);
        var row = rig.Row("dueDate");
        Assert.True(row.IsReasonOpen);
        rig.Click(row.LooksRightButton!);
        Assert.Equal(ExtractionValueStatus.Checked, row.Value!.Status);
        Assert.Equal(new string?[] { "dueDate" }, checkedPaths);
        Assert.Equal(1, x.CheckCount);
        Assert.Equal("1 field needs a check", x.StatusLine!.Text);
        Assert.Equal("1 to check", x.FlagText!.Text);
        Assert.Equal("Due date marked as checked", x.LastReviewAnnouncement);

        x.FocusNextCheck();
        rig.Pump();
        rig.Click(rig.Row("iban").LooksRightButton!);
        Assert.Equal(0, x.CheckCount);
        Assert.Equal(AiRunStatusKind.Done, x.StatusLine.Kind);
        Assert.Equal("Found 10 of 10 fields in 0.0 s", x.StatusLine.Text);
        Assert.Equal("Ready", x.FlagText.Text);
    }

    [AvaloniaFact]
    public void Review_Focuses_The_First_Value_To_Check()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        rig.Click(x.StatusLine!.Action!);
        Assert.Equal("dueDate", x.SelectedPath);
        Assert.True(rig.FocusIsIn(rig.Row("dueDate")));

        x.SelectedPath = null;
        x.SelectedTab = ExtractionTab.Json;
        rig.Pump();
        x.FocusNextCheck();
        rig.Pump();
        Assert.Equal(ExtractionTab.Fields, x.SelectedTab);
        Assert.Equal("dueDate", x.SelectedPath);
        Assert.True(rig.FocusIsIn(rig.Row("dueDate")));
    }

    [AvaloniaFact]
    public void Accept_All_Waits_For_The_Deferral_Then_Accepts_Everything()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var save = new TaskCompletionSource();
        ExtractionOutput? saved = null;
        var accepted = 0;
        x.Accepting += (_, e) =>
        {
            saved = e.Output;
            e.Defer(save.Task);
        };
        x.Accepted += (_, _) => accepted++;
        rig.Click(x.AcceptAllButton!);
        Assert.Equal(ExtractionState.Accepting, x.State);
        Assert.True(x.AcceptAllButton!.IsLoading);
        Assert.Equal("Accepting", x.FlagText!.Text);
        Assert.Same(x.Output, saved);
        Assert.DoesNotContain(x.Values, v => v.Status == ExtractionValueStatus.Accepted);

        save.SetResult();
        rig.Pump();
        Assert.Equal(ExtractionState.Accepted, x.State);
        Assert.Equal(1, accepted);
        Assert.All(x.Values, v => Assert.Equal(ExtractionValueStatus.Accepted, v.Status));
        Assert.False(x.AcceptAllButton.IsLoading);
        Assert.False(x.AcceptAllButton.IsEnabled);
        Assert.Equal(ButtonVariant.Secondary, x.AcceptAllButton.Variant);
        Assert.Equal("Accepted", AutomationName(x.AcceptAllButton));
        Assert.Equal(AiRunStatusKind.Accepted, x.StatusLine!.Kind);
        Assert.Equal("All accepted", x.FlagText.Text);
        Assert.All(x.Bars!.Items!, bar => Assert.Equal(ConfidenceBarState.Accepted, bar.State));
        Assert.Equal("All fields accepted", x.LastReviewAnnouncement);
        Assert.Contains(DocumentExtraction.AcceptedPseudoClass, x.Classes);
    }

    [AvaloniaFact]
    public void A_Cancelled_Or_Failed_Save_Undoes_The_Accept()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var mode = 0;
        x.Accepting += (_, e) =>
        {
            if (mode == 0)
            {
                e.Cancel = true;
            }
            else
            {
                e.Defer(Task.FromException(new InvalidOperationException("The server said no.")));
            }
        };
        rig.Click(x.AcceptAllButton!);
        Assert.Equal(ExtractionState.Done, x.State);
        Assert.DoesNotContain(x.Values, v => v.Status == ExtractionValueStatus.Accepted);
        Assert.Equal("Could not save. The fields are not accepted.", x.LastReviewAnnouncement);
        Assert.True(x.AcceptAllButton!.IsEnabled);

        mode = 1;
        rig.Click(x.AcceptAllButton);
        rig.Pump();
        Assert.Equal(ExtractionState.Done, x.State);
        Assert.DoesNotContain(x.Values, v => v.Status == ExtractionValueStatus.Accepted);
    }

    [AvaloniaFact]
    public void The_Accept_Cascade_Steps_Every_35ms_At_Full_Motion()
    {
        using var rig = new DocumentExtractionRig(ArcMotionLevel.Full, instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        rig.Advance(TimeSpan.FromSeconds(1));
        _ = x.AcceptAllAsync();
        rig.Pump(1);
        Assert.Equal(ExtractionState.Accepting, x.State);
        Assert.True(rig.Row("vendor").IsAccepted);
        Assert.False(rig.Row("invoiceNumber").IsAccepted);
        Assert.Equal(ConfidenceBarState.Accepted, x.Bars!.Items![0].State);
        Assert.NotEqual(ConfidenceBarState.Accepted, x.Bars.Items[1].State);

        rig.Frame(3);
        Assert.True(rig.Row("invoiceNumber").IsAccepted);
        Assert.False(rig.Row("dueDate").IsAccepted);
        rig.Frame(6);
        Assert.True(rig.Row("dueDate").IsAccepted);
        Assert.True(x.TableOf("lineItems")!.IsAccepted);
        Assert.Equal(ExtractionState.Accepting, x.State);

        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(ExtractionState.Accepted, x.State);
        Assert.All(x.Values, v => Assert.Equal(ExtractionValueStatus.Accepted, v.Status));
        // The views follow the values again.
        Assert.False(rig.Row("vendor").IsAccepted);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Accepts_Every_Field_At_Once()
    {
        using var rig = new DocumentExtractionRig(ArcMotionLevel.Reduced, instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        _ = x.AcceptAllAsync();
        rig.Pump();
        Assert.Equal(ExtractionState.Accepted, x.State);
    }

    [AvaloniaFact]
    public void Editing_After_Accept_Returns_Only_That_Value()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        rig.Click(x.AcceptAllButton!);
        Assert.Equal(ExtractionState.Accepted, x.State);
        rig.Row("vendor").FocusInput(NavigationMethod.Tab);
        rig.Pump();
        rig.Type("!");
        Assert.Equal(ExtractionState.Done, x.State);
        Assert.Equal(ExtractionValueStatus.Edited, rig.Row("vendor").Value!.Status);
        Assert.Equal(ExtractionValueStatus.Accepted, rig.Row("invoiceNumber").Value!.Status);
        Assert.True(x.AcceptAllButton!.IsEnabled);
        Assert.Equal("Accept all", AutomationName(x.AcceptAllButton));
        Assert.Equal(AiRunStatusKind.Done, x.StatusLine!.Kind);
        Assert.Equal("Ready", x.FlagText!.Text);
        Assert.Equal(ConfidenceBarState.Ok, x.Bars!.Items![0].State);
    }

    [AvaloniaFact]
    public void The_Review_Threshold_Moves_What_Needs_A_Check()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        x.ReviewThreshold = 0.95;
        rig.Pump();
        Assert.Equal(16 + 2 + 0, x.CheckCount);
    }

    private static string? AutomationName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control);

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

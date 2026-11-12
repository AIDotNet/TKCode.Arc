using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the upload step shows.</summary>
internal enum ImportUploadPhase
{
    /// <summary>The drop zone.</summary>
    Empty,

    /// <summary>The file row with the reading line.</summary>
    Reading,

    /// <summary>The file row with the preview.</summary>
    Loaded,

    /// <summary>The drop zone with an error under it.</summary>
    Error
}

/// <summary>What the import step shows.</summary>
internal enum ImportRunPhase
{
    /// <summary>The batches are being sent.</summary>
    Running,

    /// <summary>A batch failed; the message and a retry show.</summary>
    Failed,

    /// <summary>The summary.</summary>
    Done
}

[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(StepperPart, typeof(ImportWizardStepper))]
[TemplatePart(DeckPart, typeof(ImportWizardDeck))]
[TemplatePart(AnnouncerPart, typeof(DeveloperAnnouncer))]
[TemplatePart(AlertAnnouncerPart, typeof(DeveloperAnnouncer))]
[PseudoClasses(CompactPseudoClass)]
public partial class ImportWizard
{
    internal const string FramePart = "PART_Frame";
    internal const string StepperPart = "PART_Stepper";
    internal const string DeckPart = "PART_Deck";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string AlertAnnouncerPart = "PART_AlertAnnouncer";

    /// <summary>The pseudo-class below <see cref="CompactWidth"/>.</summary>
    public const string CompactPseudoClass = ":compact";

    private ImportWizardStepper? _stepper;
    private ImportWizardDeck? _deck;
    private DeveloperAnnouncer? _announcer;
    private DeveloperAnnouncer? _alertAnnouncer;
    private ImportWizardSession? _session;
    private CancellationTokenSource? _reading;
    private CancellationTokenSource? _import;
    private bool _isCompact;

    static ImportWizard()
    {
        FocusableProperty.OverrideDefaultValue<ImportWizard>(false);
    }

    // ---- State the step views read (they are rebuilt on every visit) ----

    /// <summary>The session (created on first use, replaced when the targets change).</summary>
    internal ImportWizardSession Session => _session ??= CreateSession();

    /// <summary>What the upload step shows.</summary>
    internal ImportUploadPhase UploadPhase { get; private set; }

    /// <summary>The upload error, or null.</summary>
    internal string? UploadError { get; private set; }

    /// <summary>Raised when <see cref="UploadPhase"/> or <see cref="UploadError"/> changed.</summary>
    internal event Action? UploadChanged;

    /// <summary>What the import step shows.</summary>
    internal ImportRunPhase RunPhase { get; private set; }

    /// <summary>The rows of the running or finished import.</summary>
    internal IReadOnlyList<ImportRecord> RunRecords { get; private set; } = [];

    /// <summary>Rows imported so far.</summary>
    internal int RunImported { get; private set; }

    /// <summary>The batch being sent (from 1).</summary>
    internal int RunBatch { get; private set; }

    /// <summary>How many batches the import has.</summary>
    internal int RunBatchCount { get; private set; }

    /// <summary>The failed batch's message, or null.</summary>
    internal string? RunError { get; private set; }

    /// <summary>The finished import's totals.</summary>
    internal (int Created, int Updated, int Skipped) RunTotals { get; private set; }

    /// <summary>Raised when the import's progress or phase changed.</summary>
    internal event Action? RunChanged;

    /// <summary>True when the last import finished (the fourth step shows as done).</summary>
    internal bool IsImportDone => Step == ImportWizardStep.Import && RunPhase == ImportRunPhase.Done;

    /// <summary>True below <see cref="CompactWidth"/>.</summary>
    internal bool IsCompact => _isCompact;

    /// <summary>Raised when <see cref="IsCompact"/> changed.</summary>
    internal event Action? CompactChanged;

    /// <summary>
    /// Multiplies every wait of the flow (reading, matching cadence, clean-up cadence, simulated batches, holds). Tests set 0
    /// so the flow runs without waiting.
    /// </summary>
    internal double TimingScale { get; set; } = 1;

    /// <summary>The step view shown now (tests).</summary>
    internal ImportWizardStepView? CurrentView => _deck?.Current as ImportWizardStepView;

    /// <summary>The stepper (tests).</summary>
    internal ImportWizardStepper? Stepper => _stepper;

    /// <summary>The polite announcer (tests).</summary>
    internal DeveloperAnnouncer? Announcer => _announcer;

    private ImportWizardSession CreateSession()
    {
        var targets = Targets is { Count: > 0 } custom ? custom.ToArray() : ImportWizardDefaults.CreateContactTargets().ToArray();
        var existing = ExistingKeys ?? (Targets is null ? ImportWizardDefaults.CreateSampleExistingKeys() : null);
        var session = new ImportWizardSession(targets, existing);
        return session;
    }

    // ---- Template ----

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stepper is not null)
        {
            _stepper.StepInvoked -= OnStepInvoked;
        }
        _stepper = e.NameScope.Find<ImportWizardStepper>(StepperPart);
        _deck = e.NameScope.Find<ImportWizardDeck>(DeckPart);
        _announcer = e.NameScope.Find<DeveloperAnnouncer>(AnnouncerPart);
        _alertAnnouncer = e.NameScope.Find<DeveloperAnnouncer>(AlertAnnouncerPart);
        if (_stepper is not null)
        {
            _stepper.StepInvoked += OnStepInvoked;
        }
        UpdateStepper(animate: false);
        ShowStep(Step, forward: true, animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the window cancels the work in flight; the state stays so the card can come back.
        CancelReading();
        if (_import is not null && RunPhase == ImportRunPhase.Running)
        {
            CancelImport();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetsProperty || change.Property == ExistingKeysProperty)
        {
            Reset();
        }
        else if (change.Property == LabelsProperty || change.Property == NounProperty || change.Property == PluralNounProperty
                 || change.Property == SampleCsvProperty || change.Property == MaxRowsProperty)
        {
            _stepper?.Refresh();
            CurrentView?.Refresh();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateCompact(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        // One pixel of hysteresis so a width right at the threshold does not flip back and forth.
        var compact = _isCompact ? width < CompactWidth + 1 : width < CompactWidth;
        if (compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        _stepper?.SetCompact(compact);
        // Changing the layout from inside the arrange pass: let it finish first.
        Dispatcher.UIThread.Post(() => CompactChanged?.Invoke(), DispatcherPriority.Render);
    }

    // ---- Steps ----

    private void OnStepInvoked(ImportWizardStep step)
    {
        if (step < Step && CanGoTo(step))
        {
            GoTo(step);
        }
    }

    /// <summary>True when the step bar may jump to <paramref name="step"/> (a done step while nothing is importing).</summary>
    internal bool CanGoTo(ImportWizardStep step)
    {
        if (Step == ImportWizardStep.Import)
        {
            return false;
        }
        return step < Step;
    }

    /// <summary>Shows <paramref name="step"/>, sliding forward or back by the step order.</summary>
    internal void GoTo(ImportWizardStep step)
    {
        if (step == Step && CurrentView is not null)
        {
            return;
        }
        var old = Step;
        var forward = step > old;
        if (step == ImportWizardStep.Review)
        {
            Session.BuildReview();
        }
        Step = step;
        UpdateStepper(animate: true);
        ShowStep(step, forward, animate: true);
        RaiseEvent(new ImportWizardStepChangedEventArgs(StepChangedEvent, old, step));
    }

    /// <summary>Goes one step back (from the import step: cancels the import and returns to the review).</summary>
    public void GoBack()
    {
        switch (Step)
        {
            case ImportWizardStep.Map:
                GoTo(ImportWizardStep.Upload);
                break;
            case ImportWizardStep.Review:
                GoTo(ImportWizardStep.Map);
                break;
            case ImportWizardStep.Import when RunPhase != ImportRunPhase.Done:
                CancelImport();
                break;
        }
    }

    private void ShowStep(ImportWizardStep step, bool forward, bool animate)
    {
        if (_deck is null)
        {
            return;
        }
        ImportWizardStepView view = step switch
        {
            ImportWizardStep.Upload => new ImportWizardUploadView(this),
            ImportWizardStep.Map => new ImportWizardMapView(this),
            ImportWizardStep.Review => new ImportWizardReviewView(this),
            _ => new ImportWizardImportView(this),
        };
        var old = CurrentView;
        old?.OnLeaving();
        _deck.Show(view, forward, animate && IsLoaded);
        view.OnEntered(animate && IsLoaded);
    }

    internal void UpdateStepper(bool animate)
    {
        _stepper?.SetState(Step, IsImportDone, Step != ImportWizardStep.Import, animate && IsLoaded);
    }

    // ---- Upload ----

    /// <summary>
    /// Reads <paramref name="text"/> as a CSV file named <paramref name="fileName"/> (<paramref name="size"/> in bytes; null
    /// counts the text's UTF-8 bytes), shows it on the upload step and returns whether it can be mapped. Goes back to the
    /// upload step first when another step is shown.
    /// </summary>
    public bool LoadCsv(string text, string fileName, long? size = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fileName);
        return BeginLoad(text, fileName, size ?? ImportWizardDefaults.ByteSize(text), error: null);
    }

    /// <summary>Reads a file from storage (as the drop zone and the picker do) and shows it on the upload step.</summary>
    public async Task<bool> LoadFileAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string text;
        long size;
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            text = await reader.ReadToEndAsync();
            size = stream.CanSeek ? stream.Length : ImportWizardDefaults.ByteSize(text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return BeginLoad(string.Empty, file.Name, 0, DeveloperText.Format(Labels.FileOpenFailed, exception.Message));
        }
        return BeginLoad(text, file.Name, size, error: null);
    }

    /// <summary>Opens the window's file picker for a CSV file and loads the one chosen.</summary>
    internal async Task OpenPickerAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            return;
        }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Labels.PickerTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV") { Patterns = ["*.csv"], MimeTypes = ["text/csv"], AppleUniformTypeIdentifiers = ["public.comma-separated-values-text"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files.Count > 0)
        {
            await LoadFileAsync(files[0]);
        }
    }

    /// <summary>Loads <see cref="SampleCsv"/>.</summary>
    internal void LoadSample()
    {
        if (SampleCsv is { } sample)
        {
            LoadCsv(sample, SampleFileName);
        }
    }

    private bool BeginLoad(string text, string fileName, long size, string? error)
    {
        CancelReading();
        if (Step != ImportWizardStep.Upload)
        {
            CancelImport(navigate: false);
            GoTo(ImportWizardStep.Upload);
        }
        Session.Clear();
        FileName = null;
        RowCount = 0;
        var table = error is null ? ImportWizardCsv.Parse(text, MaxRows) : ImportCsvTable.None;
        error ??= ErrorOf(table);
        var reading = new CancellationTokenSource();
        _reading = reading;
        // The file row shows "Reading file" with the 2px line first (420ms ease-out), also for a file that will fail.
        PendingFileName = fileName;
        PendingFileSize = size;
        SetUpload(ImportUploadPhase.Reading, null);
        var wait = ArcMotion.IsFull(this) && IsLoaded ? Scale(ImportWizardMotion.Reading) : TimeSpan.Zero;
        if (wait <= TimeSpan.Zero)
        {
            return FinishLoad(table, fileName, size, error);
        }
        _ = FinishLoadLaterAsync(table, fileName, size, error, wait, reading.Token);
        return error is null;
    }

    /// <summary>The file being read (shown in the file row before the parse result is applied).</summary>
    internal string? PendingFileName { get; private set; }

    /// <summary>The size of the file being read.</summary>
    internal long PendingFileSize { get; private set; }

    private async Task FinishLoadLaterAsync(ImportCsvTable table, string fileName, long size, string? error, TimeSpan wait, CancellationToken token)
    {
        try
        {
            await Task.Delay(wait, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        FinishLoad(table, fileName, size, error);
    }

    private bool FinishLoad(ImportCsvTable table, string fileName, long size, string? error)
    {
        _reading = null;
        if (error is not null)
        {
            PendingFileName = null;
            SetUpload(ImportUploadPhase.Error, error);
            _alertAnnouncer?.Announce(error);
            return false;
        }
        Session.Load(fileName, size, table);
        FileName = fileName;
        RowCount = table.Rows.Count;
        SetUpload(ImportUploadPhase.Loaded, null);
        _announcer?.Announce(Labels.FileReadAnnouncement(fileName, table.Rows.Count));
        RaiseEvent(new ImportWizardFileLoadedEventArgs(FileLoadedEvent, fileName, table.Rows.Count, table.Headers));
        return true;
    }

    private string? ErrorOf(ImportCsvTable table)
    {
        return table.Problem switch
        {
            ImportCsvProblem.None when table.Headers.Count >= 2 => null,
            ImportCsvProblem.UnclosedQuote => Labels.FileUnclosedQuote,
            ImportCsvProblem.TooManyRows => Labels.FileTooLarge(table.TotalRows, MaxRows),
            _ => Labels.FileUnreadable,
        };
    }

    /// <summary>Removes the loaded file and shows the drop zone.</summary>
    internal void RemoveFile()
    {
        CancelReading();
        Session.Clear();
        FileName = null;
        RowCount = 0;
        PendingFileName = null;
        SetUpload(ImportUploadPhase.Empty, null);
        UpdateStepper(animate: true);
    }

    private void SetUpload(ImportUploadPhase phase, string? error)
    {
        UploadPhase = phase;
        UploadError = error;
        UploadChanged?.Invoke();
    }

    private void CancelReading()
    {
        _reading?.Cancel();
        _reading = null;
    }

    /// <summary>Starts over: no file, the upload step, every connector empty.</summary>
    public void Reset()
    {
        CancelReading();
        CancelImport(navigate: false);
        _session = null;
        FileName = null;
        RowCount = 0;
        PendingFileName = null;
        RunPhase = ImportRunPhase.Running;
        RunRecords = [];
        SetUpload(ImportUploadPhase.Empty, null);
        if (Step != ImportWizardStep.Upload)
        {
            GoTo(ImportWizardStep.Upload);
        }
        else
        {
            UpdateStepper(animate: true);
            ShowStep(ImportWizardStep.Upload, forward: false, animate: false);
        }
    }

    // ---- Import ----

    /// <summary>Starts importing the ready rows (from the review step).</summary>
    internal void StartImport()
    {
        CancelImport(navigate: false);
        var records = Session.BuildRecords();
        RunRecords = records;
        RunImported = 0;
        RunBatchCount = Math.Max(1, (int)Math.Ceiling(records.Count / (double)BatchSize));
        RunBatch = 1;
        RunError = null;
        RunPhase = ImportRunPhase.Running;
        RunTotals = default;
        GoTo(ImportWizardStep.Import);
        _ = RunImportAsync(0);
    }

    /// <summary>Retries the failed batch.</summary>
    internal void RetryImport()
    {
        if (RunPhase != ImportRunPhase.Failed)
        {
            return;
        }
        RunError = null;
        RunPhase = ImportRunPhase.Running;
        RunChanged?.Invoke();
        _ = RunImportAsync(RunBatch - 1);
    }

    private async Task RunImportAsync(int firstBatch)
    {
        var cancellation = new CancellationTokenSource();
        _import = cancellation;
        var token = cancellation.Token;
        var records = RunRecords;
        var size = BatchSize;
        var action = ImportBatchAction;
        RunChanged?.Invoke();
        for (var batch = firstBatch; batch < RunBatchCount; batch++)
        {
            RunBatch = batch + 1;
            RunChanged?.Invoke();
            var slice = records.Skip(batch * size).Take(size).ToArray();
            try
            {
                if (action is null)
                {
                    var wait = Scale(ArcMotion.IsFull(this) ? ImportWizardMotion.Batch : ImportWizardMotion.ReducedBatch);
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, token);
                    }
                }
                else
                {
                    await action(slice, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                RunError = DeveloperText.Format(Labels.BatchFailed, exception.Message);
                RunPhase = ImportRunPhase.Failed;
                RunChanged?.Invoke();
                _alertAnnouncer?.Announce(RunError);
                RaiseEvent(new ImportWizardFailedEventArgs(FailedEvent, exception, batch + 1));
                return;
            }
            if (token.IsCancellationRequested)
            {
                return;
            }
            RunImported = Math.Min(records.Count, (batch + 1) * size);
            RunChanged?.Invoke();
            RaiseEvent(new ImportWizardBatchEventArgs(BatchImportedEvent, batch + 1, RunBatchCount, slice, RunImported, records.Count));
        }
        var hold = ArcMotion.IsFull(this) ? Scale(ImportWizardMotion.DoneHold) : TimeSpan.Zero;
        if (hold > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(hold, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        _import = null;
        var updated = records.Count(r => r.IsUpdate);
        RunTotals = (records.Count - updated, updated, Session.SkippedRows);
        RunPhase = ImportRunPhase.Done;
        UpdateStepper(animate: true);
        RunChanged?.Invoke();
        _announcer?.Announce(Labels.DoneAnnouncement(RunTotals.Created, RunTotals.Updated, RunTotals.Skipped));
        var args = new ImportWizardCompletedEventArgs(CompletedEvent, RunTotals.Created, RunTotals.Updated, RunTotals.Skipped, records);
        RaiseEvent(args);
        if (CompletedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    /// <summary>Stops the import; by default goes back to the review step.</summary>
    internal void CancelImport(bool navigate = true)
    {
        _import?.Cancel();
        _import = null;
        if (navigate && Step == ImportWizardStep.Import && RunPhase != ImportRunPhase.Done)
        {
            GoTo(ImportWizardStep.Review);
        }
    }

    /// <summary>The CSV of the rows that are not imported (problems not fixed, or left out by hand).</summary>
    public string GetSkippedRowsCsv() => Session.SkippedRowsCsv();

    /// <summary>Saves <see cref="GetSkippedRowsCsv"/> with <see cref="SaveSkippedRowsAction"/> or the save dialog.</summary>
    internal async Task<bool> SaveSkippedRowsAsync()
    {
        var csv = GetSkippedRowsCsv();
        if (SaveSkippedRowsAction is { } action)
        {
            return await action(csv, CancellationToken.None);
        }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanSave)
        {
            return false;
        }
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = Labels.SkippedFileName,
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] }],
        });
        if (file is null)
        {
            return false;
        }
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(csv);
        return true;
    }

    // ---- Helpers for the views ----

    /// <summary>Replaces <c>{noun}</c> with the singular noun when <paramref name="count"/> is 1, else the plural.</summary>
    internal string WithNoun(string format, int count = 2) =>
        format.Replace("{noun}", count == 1 ? Noun : PluralNoun, StringComparison.Ordinal);

    /// <summary>Says <paramref name="text"/> politely.</summary>
    internal void Announce(string text) => _announcer?.Announce(text);

    /// <summary>Says <paramref name="text"/> at once (errors).</summary>
    internal void Alert(string text) => _alertAnnouncer?.Announce(text);

    /// <summary><paramref name="time"/> times <see cref="TimingScale"/>.</summary>
    internal TimeSpan Scale(TimeSpan time) => TimeSpan.FromTicks((long)(time.Ticks * Math.Max(0, TimingScale)));

    /// <summary>Waits <paramref name="time"/> × <see cref="TimingScale"/>; a zero wait completes at once.</summary>
    internal Task Delay(TimeSpan time, CancellationToken token)
    {
        var scaled = Scale(time);
        return scaled <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(scaled, token);
    }
}

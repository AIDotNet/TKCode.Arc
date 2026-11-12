using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A four step CSV import in one card: choose or drop a file and preview it, match every column to a field with a
/// confidence for each automatic match, fix the cells with problems in place (or let "Clean up" apply the safe fixes)
/// while the counts roll down, then import the good rows in batches and read the summary. Rows whose problems are not
/// fixed are left out, not blocking, so the rest can be imported now.
/// Describe the fields as <see cref="Targets"/> with their header names, validators and safe fixes; send each batch to
/// your server with <see cref="ImportBatchAction"/> and read the totals in <see cref="Completed"/>. With no action set the
/// import is simulated.
/// <code>
/// &lt;arc:ImportWizard BatchSize="200" Noun="contact" PluralNoun="contacts" /&gt;
/// </code>
/// </summary>
public partial class ImportWizard : TemplatedControl
{
    #region Properties

    /// <summary>
    /// The fields to import into, in the order the review table shows them. Null uses a contact schema (name, email,
    /// company, role, phone, country, plan). Changing it starts over.
    /// </summary>
    public static readonly StyledProperty<IList<ImportTarget>?> TargetsProperty =
        AvaloniaProperty.Register<ImportWizard, IList<ImportTarget>?>(nameof(Targets));

    /// <summary>
    /// Values of the key field (<see cref="ImportTarget.IsKey"/>) that already exist; matching rows count as updates.
    /// Compared trimmed. Null with the default <see cref="Targets"/> uses the sample's four existing emails.
    /// </summary>
    public static readonly StyledProperty<ISet<string>?> ExistingKeysProperty =
        AvaloniaProperty.Register<ImportWizard, ISet<string>?>(nameof(ExistingKeys));

    /// <summary>
    /// Imports one batch of rows: awaited for each batch with a token that Cancel, starting over and the card leaving the
    /// window cancel. An exception stops the import, shows the message with a retry and raises <see cref="Failed"/>.
    /// Null simulates the import (about 388ms per batch).
    /// </summary>
    public static readonly StyledProperty<Func<IReadOnlyList<ImportRecord>, CancellationToken, Task>?> ImportBatchActionProperty =
        AvaloniaProperty.Register<ImportWizard, Func<IReadOnlyList<ImportRecord>, CancellationToken, Task>?>(nameof(ImportBatchAction));

    /// <summary>Rows per batch (5; at least 1).</summary>
    public static readonly StyledProperty<int> BatchSizeProperty =
        AvaloniaProperty.Register<ImportWizard, int>(nameof(BatchSize), 5, coerce: (_, value) => Math.Max(1, value));

    /// <summary>The word for one record in the copy ("contact").</summary>
    public static readonly StyledProperty<string> NounProperty =
        AvaloniaProperty.Register<ImportWizard, string>(nameof(Noun), "contact");

    /// <summary>The word for several records in the copy ("contacts").</summary>
    public static readonly StyledProperty<string> PluralNounProperty =
        AvaloniaProperty.Register<ImportWizard, string>(nameof(PluralNoun), "contacts");

    /// <summary>The most data rows a file may have (5,000); a larger file is refused with a message.</summary>
    public static readonly StyledProperty<int> MaxRowsProperty =
        AvaloniaProperty.Register<ImportWizard, int>(nameof(MaxRows), 5000, coerce: (_, value) => Math.Max(1, value));

    /// <summary>The text "Try a sample file" loads; null hides the button. Defaults to a sample customer list.</summary>
    public static readonly StyledProperty<string?> SampleCsvProperty =
        AvaloniaProperty.Register<ImportWizard, string?>(nameof(SampleCsv), ImportWizardDefaults.SampleCsv);

    /// <summary>The file name shown for the sample file.</summary>
    public static readonly StyledProperty<string> SampleFileNameProperty =
        AvaloniaProperty.Register<ImportWizard, string>(nameof(SampleFileName), ImportWizardDefaults.SampleFileName);

    /// <summary>
    /// Saves the CSV of the rows that were left out: returns true when it was saved. Null asks for a place with the
    /// window's save dialog.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<bool>>?> SaveSkippedRowsActionProperty =
        AvaloniaProperty.Register<ImportWizard, Func<string, CancellationToken, Task<bool>>?>(nameof(SaveSkippedRowsAction));

    /// <summary>Below this width of the card the step bar shows only its marks and mapping rows stack (520).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ImportWizard, double>(nameof(CompactWidth), 520);

    /// <summary>Run with the <see cref="ImportWizardCompletedEventArgs"/> once every batch is done.</summary>
    public static readonly StyledProperty<ICommand?> CompletedCommandProperty =
        AvaloniaProperty.Register<ImportWizard, ICommand?>(nameof(CompletedCommand));

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<ImportWizardLabels> LabelsProperty =
        AvaloniaProperty.Register<ImportWizard, ImportWizardLabels>(nameof(Labels), new ImportWizardLabels());

    /// <summary>The step shown.</summary>
    public static readonly DirectProperty<ImportWizard, ImportWizardStep> StepProperty =
        AvaloniaProperty.RegisterDirect<ImportWizard, ImportWizardStep>(nameof(Step), o => o.Step);

    /// <summary>The loaded file's name; null before a file is loaded.</summary>
    public static readonly DirectProperty<ImportWizard, string?> FileNameProperty =
        AvaloniaProperty.RegisterDirect<ImportWizard, string?>(nameof(FileName), o => o.FileName);

    /// <summary>How many data rows the loaded file has (0 before one is loaded).</summary>
    public static readonly DirectProperty<ImportWizard, int> RowCountProperty =
        AvaloniaProperty.RegisterDirect<ImportWizard, int>(nameof(RowCount), o => o.RowCount);

    #endregion

    #region Events

    /// <summary>Raised when another step is shown.</summary>
    public static readonly RoutedEvent<ImportWizardStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<ImportWizard, ImportWizardStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a file was read and can be mapped.</summary>
    public static readonly RoutedEvent<ImportWizardFileLoadedEventArgs> FileLoadedEvent =
        RoutedEvent.Register<ImportWizard, ImportWizardFileLoadedEventArgs>(nameof(FileLoaded), RoutingStrategies.Bubble);

    /// <summary>Raised after each batch was imported.</summary>
    public static readonly RoutedEvent<ImportWizardBatchEventArgs> BatchImportedEvent =
        RoutedEvent.Register<ImportWizard, ImportWizardBatchEventArgs>(nameof(BatchImported), RoutingStrategies.Bubble);

    /// <summary>Raised once every batch is done, with the totals.</summary>
    public static readonly RoutedEvent<ImportWizardCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<ImportWizard, ImportWizardCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="ImportBatchAction"/> threw; the import can be tried again.</summary>
    public static readonly RoutedEvent<ImportWizardFailedEventArgs> FailedEvent =
        RoutedEvent.Register<ImportWizard, ImportWizardFailedEventArgs>(nameof(Failed), RoutingStrategies.Bubble);

    /// <summary>Raised when another step is shown.</summary>
    public event EventHandler<ImportWizardStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <summary>Raised when a file was read and can be mapped.</summary>
    public event EventHandler<ImportWizardFileLoadedEventArgs>? FileLoaded
    {
        add => AddHandler(FileLoadedEvent, value);
        remove => RemoveHandler(FileLoadedEvent, value);
    }

    /// <summary>Raised after each batch was imported.</summary>
    public event EventHandler<ImportWizardBatchEventArgs>? BatchImported
    {
        add => AddHandler(BatchImportedEvent, value);
        remove => RemoveHandler(BatchImportedEvent, value);
    }

    /// <summary>Raised once every batch is done, with the totals.</summary>
    public event EventHandler<ImportWizardCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <summary>Raised when <see cref="ImportBatchAction"/> threw; the import can be tried again.</summary>
    public event EventHandler<ImportWizardFailedEventArgs>? Failed
    {
        add => AddHandler(FailedEvent, value);
        remove => RemoveHandler(FailedEvent, value);
    }

    #endregion

    private ImportWizardStep _step;
    private string? _fileName;
    private int _rowCount;

    /// <inheritdoc cref="TargetsProperty"/>
    public IList<ImportTarget>? Targets { get => GetValue(TargetsProperty); set => SetValue(TargetsProperty, value); }

    /// <inheritdoc cref="ExistingKeysProperty"/>
    public ISet<string>? ExistingKeys { get => GetValue(ExistingKeysProperty); set => SetValue(ExistingKeysProperty, value); }

    /// <inheritdoc cref="ImportBatchActionProperty"/>
    public Func<IReadOnlyList<ImportRecord>, CancellationToken, Task>? ImportBatchAction
    {
        get => GetValue(ImportBatchActionProperty);
        set => SetValue(ImportBatchActionProperty, value);
    }

    /// <inheritdoc cref="BatchSizeProperty"/>
    public int BatchSize { get => GetValue(BatchSizeProperty); set => SetValue(BatchSizeProperty, value); }

    /// <inheritdoc cref="NounProperty"/>
    public string Noun { get => GetValue(NounProperty); set => SetValue(NounProperty, value); }

    /// <inheritdoc cref="PluralNounProperty"/>
    public string PluralNoun { get => GetValue(PluralNounProperty); set => SetValue(PluralNounProperty, value); }

    /// <inheritdoc cref="MaxRowsProperty"/>
    public int MaxRows { get => GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }

    /// <inheritdoc cref="SampleCsvProperty"/>
    public string? SampleCsv { get => GetValue(SampleCsvProperty); set => SetValue(SampleCsvProperty, value); }

    /// <inheritdoc cref="SampleFileNameProperty"/>
    public string SampleFileName { get => GetValue(SampleFileNameProperty); set => SetValue(SampleFileNameProperty, value); }

    /// <inheritdoc cref="SaveSkippedRowsActionProperty"/>
    public Func<string, CancellationToken, Task<bool>>? SaveSkippedRowsAction
    {
        get => GetValue(SaveSkippedRowsActionProperty);
        set => SetValue(SaveSkippedRowsActionProperty, value);
    }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="CompletedCommandProperty"/>
    public ICommand? CompletedCommand { get => GetValue(CompletedCommandProperty); set => SetValue(CompletedCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ImportWizardLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public ImportWizardStep Step { get => _step; private set => SetAndRaise(StepProperty, ref _step, value); }

    /// <inheritdoc cref="FileNameProperty"/>
    public string? FileName { get => _fileName; private set => SetAndRaise(FileNameProperty, ref _fileName, value); }

    /// <inheritdoc cref="RowCountProperty"/>
    public int RowCount { get => _rowCount; private set => SetAndRaise(RowCountProperty, ref _rowCount, value); }
}

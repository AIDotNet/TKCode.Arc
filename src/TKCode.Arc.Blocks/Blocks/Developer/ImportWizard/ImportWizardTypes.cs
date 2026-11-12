using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The four steps of <see cref="ImportWizard"/>.</summary>
public enum ImportWizardStep
{
    /// <summary>Choose or drop a CSV file and preview it.</summary>
    Upload,

    /// <summary>Match every column of the file to a target field.</summary>
    Map,

    /// <summary>Fix or leave out the rows with problems.</summary>
    Review,

    /// <summary>Send the rows in batches and read the summary.</summary>
    Import
}

/// <summary>
/// A field the rows are imported into: how columns are recognised (header names, synonyms, values), how its values are
/// checked and, when a safe correction exists, how they are cleaned up.
/// </summary>
public sealed class ImportTarget
{
    /// <summary>The key of the field in <see cref="ImportRecord.Values"/> ("email").</summary>
    public required string Key { get; init; }

    /// <summary>The name people see ("Email").</summary>
    public required string Label { get; init; }

    /// <summary>True when every import needs a column for this field; the mapping step blocks until one is chosen.</summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// True for the field that identifies an existing record (one at most; the first wins). Rows whose value is in
    /// <see cref="ImportWizard.ExistingKeys"/> count as updates.
    /// </summary>
    public bool IsKey { get; init; }

    /// <summary>
    /// Header names that mean this field exactly (compared without case, spaces, underscores and hyphens). The label and
    /// the key always count.
    /// </summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>Header names that usually mean this field (a weaker match than <see cref="Headers"/>).</summary>
    public IReadOnlyList<string> Synonyms { get; init; } = [];

    /// <summary>Checks one value: returns a short message when it is wrong, or null when it is fine.</summary>
    public Func<string, string?>? Validate { get; init; }

    /// <summary>
    /// Returns a safe cleaned-up version of a wrong value (trimmed, lower-cased, spelled out), or null when there is no
    /// safe fix. "Clean up" applies it; the result is validated again.
    /// </summary>
    public Func<string, string?>? Fix { get; init; }

    /// <summary>Recognises values of this field (an email, a phone number) when the header gives no hint.</summary>
    public Func<string, bool>? Sniff { get; init; }
}

/// <summary>One row sent to <see cref="ImportWizard.ImportBatchAction"/>.</summary>
/// <param name="RowNumber">The row's line in the file (the header is line 1, so data starts at 2).</param>
/// <param name="Values">The mapped values by <see cref="ImportTarget.Key"/>, after the fixes and edits.</param>
/// <param name="IsUpdate">True when the key field matches one of <see cref="ImportWizard.ExistingKeys"/>.</param>
public sealed record ImportRecord(int RowNumber, IReadOnlyDictionary<string, string> Values, bool IsUpdate);

/// <summary>Arguments of <see cref="ImportWizard.StepChanged"/>.</summary>
public sealed class ImportWizardStepChangedEventArgs : RoutedEventArgs
{
    public ImportWizardStepChangedEventArgs(RoutedEvent routedEvent, ImportWizardStep oldStep, ImportWizardStep newStep)
        : base(routedEvent)
    {
        OldStep = oldStep;
        NewStep = newStep;
    }

    /// <summary>The step that was shown.</summary>
    public ImportWizardStep OldStep { get; }

    /// <summary>The step that is shown now.</summary>
    public ImportWizardStep NewStep { get; }
}

/// <summary>Arguments of <see cref="ImportWizard.FileLoaded"/>.</summary>
public sealed class ImportWizardFileLoadedEventArgs : RoutedEventArgs
{
    public ImportWizardFileLoadedEventArgs(RoutedEvent routedEvent, string fileName, int rowCount, IReadOnlyList<string> headers)
        : base(routedEvent)
    {
        FileName = fileName;
        RowCount = rowCount;
        Headers = headers;
    }

    /// <summary>The file's name.</summary>
    public string FileName { get; }

    /// <summary>How many data rows it has.</summary>
    public int RowCount { get; }

    /// <summary>Its header row.</summary>
    public IReadOnlyList<string> Headers { get; }
}

/// <summary>Arguments of <see cref="ImportWizard.BatchImported"/>.</summary>
public sealed class ImportWizardBatchEventArgs : RoutedEventArgs
{
    public ImportWizardBatchEventArgs(RoutedEvent routedEvent, int batch, int batchCount, IReadOnlyList<ImportRecord> records, int imported, int total)
        : base(routedEvent)
    {
        Batch = batch;
        BatchCount = batchCount;
        Records = records;
        Imported = imported;
        Total = total;
    }

    /// <summary>The batch that finished, from 1.</summary>
    public int Batch { get; }

    /// <summary>How many batches the import has.</summary>
    public int BatchCount { get; }

    /// <summary>The rows of this batch.</summary>
    public IReadOnlyList<ImportRecord> Records { get; }

    /// <summary>How many rows are imported so far.</summary>
    public int Imported { get; }

    /// <summary>How many rows the import sends in all.</summary>
    public int Total { get; }
}

/// <summary>Arguments of <see cref="ImportWizard.Completed"/> (also the parameter of <see cref="ImportWizard.CompletedCommand"/>).</summary>
public sealed class ImportWizardCompletedEventArgs : RoutedEventArgs
{
    public ImportWizardCompletedEventArgs(RoutedEvent routedEvent, int created, int updated, int skipped, IReadOnlyList<ImportRecord> records)
        : base(routedEvent)
    {
        Created = created;
        Updated = updated;
        Skipped = skipped;
        Records = records;
    }

    /// <summary>Rows imported as new records.</summary>
    public int Created { get; }

    /// <summary>Rows that matched an existing key.</summary>
    public int Updated { get; }

    /// <summary>Rows left out: those with problems that were not fixed and those removed by hand.</summary>
    public int Skipped { get; }

    /// <summary>Every imported row.</summary>
    public IReadOnlyList<ImportRecord> Records { get; }
}

/// <summary>Arguments of <see cref="ImportWizard.Failed"/>.</summary>
public sealed class ImportWizardFailedEventArgs : RoutedEventArgs
{
    public ImportWizardFailedEventArgs(RoutedEvent routedEvent, Exception exception, int batch)
        : base(routedEvent)
    {
        Exception = exception;
        Batch = batch;
    }

    /// <summary>What <see cref="ImportWizard.ImportBatchAction"/> threw.</summary>
    public Exception Exception { get; }

    /// <summary>The batch that failed, from 1.</summary>
    public int Batch { get; }
}

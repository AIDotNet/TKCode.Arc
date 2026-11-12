namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ImportWizard"/> shows or announces, in English by default. Replace the instance to translate or
/// reword. Texts with numbers are functions that return a format string: <c>{0}</c> (and <c>{1}</c>) mark where the
/// numbers go, so they can roll in place and the word order can change; the count is passed so the words can agree with
/// it. <c>{noun}</c> is replaced by <see cref="ImportWizard.Noun"/> or <see cref="ImportWizard.PluralNoun"/>.
/// </summary>
public sealed class ImportWizardLabels
{
    // ---- Steps ----

    /// <summary>The names of the four steps in the step bar.</summary>
    public IReadOnlyList<string> StepNames { get; init; } = ["Upload", "Match fields", "Review", "Import"];

    /// <summary>The accessible name of the step bar.</summary>
    public string StepsName { get; init; } = "Import steps";

    /// <summary>A step button's accessible name: name, number, count and whether it is done.</summary>
    public Func<string, int, int, bool, string> StepButtonName { get; init; } =
        (name, number, count, done) => done ? $"{name}, step {number} of {count}, done" : $"{name}, step {number} of {count}";

    /// <summary>The compact step line, "{0}" being the step number and "{1}" the count.</summary>
    public string StepCounter { get; init; } = "Step {0} of {1}";

    // ---- Upload ----

    /// <summary>The upload step's heading.</summary>
    public string UploadTitle { get; init; } = "Choose a file";

    /// <summary>The line under it; "{0}" is the row limit.</summary>
    public string UploadHint { get; init; } = "CSV with column names in the first row, up to {0} rows.";

    /// <summary>The drop zone's title.</summary>
    public string DropTitle { get; init; } = "Drag a CSV here";

    /// <summary>The drop zone's title while a file is dragged over it.</summary>
    public string DropRelease { get; init; } = "Release to upload";

    /// <summary>The words before the browse link.</summary>
    public string DropOr { get; init; } = "or";

    /// <summary>The browse link.</summary>
    public string DropBrowse { get; init; } = "browse files";

    /// <summary>The drop zone's accessible name.</summary>
    public string DropName { get; init; } = "Choose a CSV file";

    /// <summary>The title of the file picker.</summary>
    public string PickerTitle { get; init; } = "Choose a CSV file";

    /// <summary>The sample file button.</summary>
    public string UseSample { get; init; } = "Try a sample file";

    /// <summary>The file row's line while the file is read.</summary>
    public string Reading { get; init; } = "Reading file";

    /// <summary>The file row's facts: rows, columns, size in KB.</summary>
    public Func<int, int, double, string> FileFacts { get; init; } = (rows, columns, kb) =>
        $"{rows:N0} {(rows == 1 ? "row" : "rows")} · {columns} {(columns == 1 ? "column" : "columns")} · {kb:0.0} KB";

    /// <summary>The remove button of the file row.</summary>
    public string RemoveFile { get; init; } = "Remove file";

    /// <summary>The accessible name of the preview table.</summary>
    public string PreviewName { get; init; } = "File preview";

    /// <summary>The line under the preview: rows shown, rows in all.</summary>
    public Func<int, int, string> PreviewFooter { get; init; } = (shown, total) =>
        shown >= total ? $"All {total:N0} {(total == 1 ? "row" : "rows")}" : $"First {shown} of {total:N0} rows";

    /// <summary>What an empty cell shows.</summary>
    public string Blank { get; init; } = "Blank";

    /// <summary>The error when the file has no header row with two columns or no data row.</summary>
    public string FileUnreadable { get; init; } =
        "We couldn't find a header row and data in that file. Save it as CSV with column names on the first line.";

    /// <summary>The error when a quoted field is never closed.</summary>
    public string FileUnclosedQuote { get; init; } = "A quoted value in that file never ends. Check the quotes and try again.";

    /// <summary>The error when the file has too many rows: rows in the file, the limit.</summary>
    public Func<int, int, string> FileTooLarge { get; init; } = (rows, limit) =>
        $"That file has {rows:N0} rows. Split it into files of {limit:N0} rows or fewer.";

    /// <summary>The error when the file could not be opened; "{0}" is the reason.</summary>
    public string FileOpenFailed { get; init; } = "That file could not be opened: {0}";

    /// <summary>The upload step's primary button.</summary>
    public string ToMapping { get; init; } = "Match fields";

    // ---- Map ----

    /// <summary>The mapping step's heading.</summary>
    public string MapTitle { get; init; } = "Match fields";

    /// <summary>The line under it, by mapped count: "{0}" mapped, "{1}" columns.</summary>
    public Func<int, string> MappedCount { get; init; } = _ => "{0} of {1} columns mapped";

    /// <summary>The choice that leaves a column out.</summary>
    public string DoNotImport { get; init; } = "Don't import";

    /// <summary>A required field's option: its label.</summary>
    public Func<string, string> RequiredOption { get; init; } = label => $"{label} (required)";

    /// <summary>The accessible name of a column's field select: the column name.</summary>
    public Func<string, string> FieldFor { get; init; } = column => $"Field for {column}";

    /// <summary>A column that is still being matched.</summary>
    public string Matching { get; init; } = "Matching";

    /// <summary>A column mapped by hand to a field the matcher did not propose.</summary>
    public string Chosen { get; init; } = "Chosen";

    /// <summary>A column that is not imported.</summary>
    public string Ignored { get; init; } = "Ignored";

    /// <summary>Two columns mapped to the same field.</summary>
    public string Duplicate { get; init; } = "Duplicate";

    /// <summary>The accessible text of a confidence: the percent.</summary>
    public Func<int, string> ConfidenceName { get; init; } = percent => $"{percent}% confidence";

    /// <summary>The alert when a required field has no column: the field's label in lower case.</summary>
    public Func<string, string> MissingRequired { get; init; } = field => $"Choose a column for {field}.";

    /// <summary>The alert when a field is chosen for two columns: the field's label.</summary>
    public Func<string, string> DuplicateField { get; init; } = field => $"{field} is chosen for two columns. Keep one.";

    /// <summary>The back button.</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The mapping step's primary button.</summary>
    public string ToReview { get; init; } = "Check rows";

    // ---- Review ----

    /// <summary>The review step's heading.</summary>
    public string ReviewTitle { get; init; } = "Review";

    /// <summary>The line under it, by rows to fix: "{0}" to fix, "{1}" ready.</summary>
    public Func<int, string> ReviewCount { get; init; } = issues => issues == 1 ? "{0} row to fix · {1} ready" : "{0} rows to fix · {1} ready";

    /// <summary>The accessible name of the row filter.</summary>
    public string FilterName { get; init; } = "Rows";

    /// <summary>The filter that shows the rows with problems.</summary>
    public string FilterIssues { get; init; } = "To fix";

    /// <summary>The filter that shows every row.</summary>
    public string FilterAll { get; init; } = "All";

    /// <summary>The clean-up button, by fixable values: "{0}" is the count.</summary>
    public Func<int, string> CleanUp { get; init; } = count => count == 1 ? "Clean up {0} value" : "Clean up {0} values";

    /// <summary>The accessible name of the rows table.</summary>
    public string TableName { get; init; } = "Rows to review";

    /// <summary>The line number column.</summary>
    public string LineHeader { get; init; } = "Line";

    /// <summary>The accessible name of the actions column.</summary>
    public string ActionsHeader { get; init; } = "Actions";

    /// <summary>A cell button's accessible name: field, line, problem (null when fine).</summary>
    public Func<string, int, string?, string> CellName { get; init; } = (field, line, problem) =>
        problem is null ? $"{field}, line {line}. Press Enter to edit" : $"{field}, line {line}: {problem}. Press Enter to edit";

    /// <summary>The cell editor's accessible name: field, line.</summary>
    public Func<string, int, string> EditorName { get; init; } = (field, line) => $"{field}, line {line}";

    /// <summary>The leave-out button of a row: its line.</summary>
    public Func<int, string> LeaveOut { get; init; } = line => $"Leave out line {line}";

    /// <summary>The tooltip of the leave-out button.</summary>
    public string LeaveOutTip { get; init; } = "Leave out this row";

    /// <summary>The review step's primary button, by rows to import: "{0}" rows, "{1}" left out, "{noun}" the noun.</summary>
    public Func<int, string> ImportButton { get; init; } = rows => "Import {0} {noun}";

    /// <summary>The second part of the primary button: "{1}" rows left out.</summary>
    public Func<int, string> ImportButtonSkip { get; init; } = skipped => " · {1} left out";

    // ---- Import ----

    /// <summary>The big count: "{0}" done of "{1}".</summary>
    public string Progress { get; init; } = "{0} of {1}";

    /// <summary>The line under the count.</summary>
    public string Importing { get; init; } = "Sending {noun}";

    /// <summary>The accessible name of the progress bar.</summary>
    public string ProgressName { get; init; } = "Import progress";

    /// <summary>The batch line: batch, batches.</summary>
    public Func<int, int, string> BatchLine { get; init; } = (batch, count) => $"Batch {batch} of {count}";

    /// <summary>The cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The error when a batch failed; "{0}" is the message.</summary>
    public string BatchFailed { get; init; } = "This batch failed: {0}";

    /// <summary>The retry button after a failed batch.</summary>
    public string Retry { get; init; } = "Try again";

    /// <summary>The summary title, by imported rows: "{0}" the count, "{noun}" the noun.</summary>
    public Func<int, string> Done { get; init; } = _ => "Imported {0} {noun}";

    /// <summary>The summary's new records label.</summary>
    public string Created { get; init; } = "New";

    /// <summary>The summary's updated records label.</summary>
    public string Updated { get; init; } = "Updated";

    /// <summary>The summary's skipped rows label.</summary>
    public string Skipped { get; init; } = "Left out";

    /// <summary>The download button of the skipped rows.</summary>
    public string DownloadSkipped { get; init; } = "Download left-out rows";

    /// <summary>The download button after the file was saved.</summary>
    public string Downloaded { get; init; } = "Downloaded";

    /// <summary>The suggested file name of the skipped rows.</summary>
    public string SkippedFileName { get; init; } = "left-out-rows.csv";

    /// <summary>The restart button.</summary>
    public string StartOver { get; init; } = "Start a new import";

    // ---- Announcements ----

    /// <summary>Said when a file was read: name, rows.</summary>
    public Func<string, int, string> FileReadAnnouncement { get; init; } = (name, rows) => $"{name} read, {rows:N0} {(rows == 1 ? "row" : "rows")}.";

    /// <summary>Said when every column has a result: mapped, columns.</summary>
    public Func<int, int, string> MappedAnnouncement { get; init; } = (mapped, columns) => $"{mapped} of {columns} columns mapped.";

    /// <summary>Said when the import finished: created, updated, skipped.</summary>
    public Func<int, int, int, string> DoneAnnouncement { get; init; } = (created, updated, skipped) =>
        $"Import finished: {created} new, {updated} updated, {skipped} left out.";
}

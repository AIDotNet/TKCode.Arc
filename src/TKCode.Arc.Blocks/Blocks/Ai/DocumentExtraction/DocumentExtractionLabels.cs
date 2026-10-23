using System;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="DocumentExtraction"/> shows or announces, in English by default. Replace the instance to translate
/// or reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class DocumentExtractionLabels
{
    // Viewer.

    /// <summary>The accessible name of the viewer section.</summary>
    public string Document { get; init; } = "Document";

    /// <summary>The canvas' name; arguments are the file name and the zoom in percent.</summary>
    public Func<string, int, string> CanvasName { get; init; } = (name, zoom) => $"{name}, zoom {zoom}%";

    /// <summary>The file button's name when there is more than one document; the argument is the file name.</summary>
    public Func<string, string> SwitchDocument { get; init; } = name => $"{name}, switch document";

    /// <summary>The page count; arguments are the page and the page count.</summary>
    public Func<int, int, string> PageOf { get; init; } = (page, count) => $"{page} / {count}";

    /// <summary>The page count's accessible name.</summary>
    public Func<int, int, string> PageOfName { get; init; } = (page, count) => $"Page {page} of {count}";

    /// <summary>The accessible name of the zoom group.</summary>
    public string Zoom { get; init; } = "Zoom";

    /// <summary>The zoom out button.</summary>
    public string ZoomOut { get; init; } = "Zoom out";

    /// <summary>The zoom in button.</summary>
    public string ZoomIn { get; init; } = "Zoom in";

    /// <summary>The percentage button's name; the argument is the zoom in percent.</summary>
    public Func<int, string> ZoomReset { get; init; } = percent => $"Zoom {percent}%, show at actual size";

    /// <summary>The fit-to-width button.</summary>
    public string Fit { get; init; } = "Fit";

    /// <summary>The fit button's tooltip.</summary>
    public string FitTip { get; init; } = "Fit to width";

    // Form pane.

    /// <summary>The accessible name of the tab list.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The Fields tab.</summary>
    public string FieldsTab { get; init; } = "Fields";

    /// <summary>The JSON tab.</summary>
    public string JsonTab { get; init; } = "JSON";

    /// <summary>The Schema tab.</summary>
    public string SchemaTab { get; init; } = "Schema";

    /// <summary>The Extract button.</summary>
    public string Extract { get; init; } = "Extract";

    /// <summary>The Stop button.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The button after a run.</summary>
    public string ExtractAgain { get; init; } = "Extract again";

    /// <summary>The short form of <see cref="ExtractAgain"/> in a narrow container.</summary>
    public string ExtractAgainShort { get; init; } = "Extract";

    /// <summary>The button after a failure.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The Review pill.</summary>
    public string Review { get; init; } = "Review";

    /// <summary>The pill that marks a low-confidence value as checked.</summary>
    public string LooksRight { get; init; } = "Looks right";

    /// <summary>The confidence column of an edited value.</summary>
    public string Edited { get; init; } = "Edited";

    /// <summary>The type names shown in the confidence column before a value arrives.</summary>
    public Func<ExtractionFieldType, string> TypeName { get; init; } = type => type switch
    {
        ExtractionFieldType.Text => "Text",
        ExtractionFieldType.Date => "Date",
        ExtractionFieldType.Money => "Amount",
        ExtractionFieldType.Currency => "Code",
        ExtractionFieldType.Iban => "IBAN",
        ExtractionFieldType.Integer => "Integer",
        _ => "Number"
    };

    /// <summary>The accessible text of the confidence column; arguments are the value's state and the percentage.</summary>
    public Func<ExtractionValueStatus, bool, int, string> ConfidenceName { get; init; } = (status, low, percent) => status switch
    {
        ExtractionValueStatus.Pending => "No value yet",
        ExtractionValueStatus.Edited => "Edited",
        ExtractionValueStatus.Checked => $"Checked, {percent} percent",
        ExtractionValueStatus.Accepted => $"Accepted, {percent} percent",
        _ => low ? $"Low confidence, {percent} percent, check this value" : $"Confidence {percent} percent"
    };

    /// <summary>The label of a table's row count.</summary>
    public Func<int, string> Rows { get; init; } = count => count == 1 ? "1 row" : $"{count} rows";

    /// <summary>The caption of a table before it has rows.</summary>
    public string Table { get; init; } = "Table";

    /// <summary>The empty table's text.</summary>
    public string NoRows { get; init; } = "No rows yet";

    /// <summary>The empty table's text while a run is going.</summary>
    public string RowsPending { get; init; } = "Rows appear here as they are found";

    /// <summary>A table cell's name; arguments are the column and the row number (from 1).</summary>
    public Func<string, int, string> CellName { get; init; } = (column, row) => $"{column}, row {row}";

    // Status line.

    /// <summary>The idle line; the argument is the number of fields.</summary>
    public Func<int, string> FieldsToFill { get; init; } = count => $"{count} fields to fill from this document";

    /// <summary>The line while reading.</summary>
    public string Reading { get; init; } = "Reading the document";

    /// <summary>The line while values arrive.</summary>
    public string Extracting { get; init; } = "Extracting fields";

    /// <summary>The count next to it; arguments are found and total.</summary>
    public Func<int, int, string> Count { get; init; } = (found, total) => $"{found} of {total}";

    /// <summary>Fields to check; the argument is how many.</summary>
    public Func<int, string> NeedsCheck { get; init; } = count => count == 1 ? "1 field needs a check" : $"{count} fields need a check";

    /// <summary>A finished run; arguments are found, total and the time text.</summary>
    public Func<int, int, string, string> Found { get; init; } = (found, total, time) => $"Found {found} of {total} fields in {time}";

    /// <summary>After Accept all.</summary>
    public string AcceptedStatus { get; init; } = "All fields accepted and saved";

    /// <summary>After a stop; arguments are found and total.</summary>
    public Func<int, int, string> Stopped { get; init; } = (found, total) => $"Stopped after {found} of {total} fields";

    /// <summary>The line under a stop.</summary>
    public string StoppedNote { get; init; } = "The values found so far are kept. Extract again to start over.";

    /// <summary>The title of a failure.</summary>
    public string Failed { get; init; } = "Extraction failed";

    // JSON and schema tabs.

    /// <summary>The Copy button.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The Copied state of the button.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The note over valid JSON.</summary>
    public string JsonValid { get; init; } = "Fits the schema and follows your edits";

    /// <summary>The note over JSON with invalid values; the argument is how many.</summary>
    public Func<int, string> JsonInvalid { get; init; } = count => count == 1 ? "1 value does not fit its type and is set to null" : $"{count} values do not fit their types and are set to null";

    /// <summary>The note over the schema; the argument is the model id.</summary>
    public Func<string, string> SchemaNote { get; init; } = id => $"Sent to {id} as the output format";

    /// <summary>The accessible name of the JSON block.</summary>
    public string JsonName { get; init; } = "Typed output as JSON";

    /// <summary>The accessible name of the schema block.</summary>
    public string SchemaName { get; init; } = "JSON schema";

    // Footer.

    /// <summary>The footer summary before any value.</summary>
    public string NoValues { get; init; } = "No values yet";

    /// <summary>The footer summary while reading.</summary>
    public string ReadingShort { get; init; } = "Reading";

    /// <summary>The average; the argument is the percentage.</summary>
    public Func<int, string> Average { get; init; } = percent => $"{percent}%";

    /// <summary>The word after the average.</summary>
    public string AverageWord { get; init; } = "average";

    /// <summary>Values to check in the footer; the argument is how many.</summary>
    public Func<int, string> ToCheck { get; init; } = count => $"{count} to check";

    /// <summary>The footer summary when nothing needs checking.</summary>
    public string Ready { get; init; } = "Ready";

    /// <summary>The footer summary while saving.</summary>
    public string Accepting { get; init; } = "Accepting";

    /// <summary>The footer summary after accepting.</summary>
    public string AllAccepted { get; init; } = "All accepted";

    /// <summary>The Accept all button.</summary>
    public string AcceptAll { get; init; } = "Accept all";

    /// <summary>The button after accepting.</summary>
    public string Accepted { get; init; } = "Accepted";

    // Announcements.

    /// <summary>Announced when a run starts; the argument is the number of fields.</summary>
    public Func<int, string> AnnounceStarted { get; init; } = count => $"Extracting {count} fields";

    /// <summary>Announced when a run completes; arguments are found, total and how many need a check.</summary>
    public Func<int, int, int, string> AnnounceDone { get; init; } = (found, total, check) =>
        check == 0 ? $"Found {found} of {total} fields" : $"Found {found} of {total} fields, {check} to check";

    /// <summary>Announced when a run is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Extraction stopped";

    /// <summary>Announced when a run fails; the argument is the error.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = error => $"Extraction failed: {error}";

    /// <summary>Announced when a value is marked checked; the argument is the field's label.</summary>
    public Func<string, string> AnnounceChecked { get; init; } = label => $"{label} marked as checked";

    /// <summary>Announced when everything is accepted.</summary>
    public string AnnounceAccepted { get; init; } = "All fields accepted";

    /// <summary>Announced when Accept all is blocked; the argument is the field's label.</summary>
    public Func<string, string> AnnounceInvalid { get; init; } = label => $"Fix {label} first: its value does not fit the type";

    /// <summary>Announced when Accept all is refused by the app.</summary>
    public string AnnounceAcceptFailed { get; init; } = "Could not save. The fields are not accepted.";

    // Type errors.

    /// <summary>What a date field expects.</summary>
    public string DateError { get; init; } = "Use YYYY-MM-DD";

    /// <summary>What an amount, integer or number field expects.</summary>
    public string NumberError { get; init; } = "Enter a number";

    /// <summary>What an IBAN field expects.</summary>
    public string IbanError { get; init; } = "Check the IBAN";

    /// <summary>What a currency field expects.</summary>
    public string CurrencyError { get; init; } = "Use a three-letter code";

    // Added by the assembly.

    /// <summary>The idle line for an email; the argument is the number of fields.</summary>
    public Func<int, string> FieldsToFillFromEmail { get; init; } = count => $"{count} fields to fill from this email";

    /// <summary>The title of a failure after some values arrived; arguments are found and total.</summary>
    public Func<int, int, string> FailedAfter { get; init; } = (found, total) => $"Extraction failed after {found} of {total} fields";

    /// <summary>A run's time, shown to the tenth of a second.</summary>
    public Func<TimeSpan, string> Seconds { get; init; } = time =>
        (Math.Floor(Math.Max(0, time.TotalMilliseconds) / 100) / 10).ToString("0.0", CultureInfo.InvariantCulture) + " s";

    /// <summary>The cost of a run, in dollars.</summary>
    public Func<decimal, string> Cost { get; init; } = cost => "$" + cost.ToString("0.0000", CultureInfo.InvariantCulture);

    /// <summary>The cost before any run.</summary>
    public string NoCost { get; init; } = "$0.00";
}

using System.Collections.Generic;
using System.Threading;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Where an extraction is.</summary>
public enum ExtractionState
{
    /// <summary>Nothing has run, or the values were cleared.</summary>
    Idle,

    /// <summary>The run has started and no value has arrived.</summary>
    Reading,

    /// <summary>Values are arriving.</summary>
    Extracting,

    /// <summary>The run ended with every value in.</summary>
    Done,

    /// <summary>The person stopped the run; the values found so far are kept.</summary>
    Stopped,

    /// <summary>The run failed; the values found so far are kept.</summary>
    Failed,

    /// <summary>Accept all is being saved.</summary>
    Accepting,

    /// <summary>Every field was accepted.</summary>
    Accepted
}

/// <summary>A tab of the form pane.</summary>
public enum ExtractionTab
{
    /// <summary>The editable fields.</summary>
    Fields,

    /// <summary>The typed output as JSON.</summary>
    Json,

    /// <summary>The JSON schema sent to the model.</summary>
    Schema
}

/// <summary>Where a value stands in review.</summary>
public enum ExtractionValueStatus
{
    /// <summary>No value yet.</summary>
    Pending,

    /// <summary>The model found it.</summary>
    Filled,

    /// <summary>A low-confidence value the person marked "Looks right".</summary>
    Checked,

    /// <summary>The person edited it.</summary>
    Edited,

    /// <summary>It was accepted.</summary>
    Accepted
}

/// <summary>Who changed a value.</summary>
public enum ExtractionOrigin
{
    /// <summary>The model.</summary>
    Model,

    /// <summary>The person.</summary>
    User
}

/// <summary>The model that does the extraction, for the footer and the cost.</summary>
public sealed class ExtractionModelInfo
{
    /// <summary>The name shown in the footer.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The id shown in the schema note.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The price of a million input tokens, in dollars.</summary>
    public decimal InputPricePerMillion { get; set; }

    /// <summary>The price of a million output tokens, in dollars.</summary>
    public decimal OutputPricePerMillion { get; set; }
}

/// <summary>An event of an extraction stream.</summary>
public abstract record ExtractionEvent;

/// <summary>A value was found.</summary>
/// <param name="Path">A field key, or <c>table.row.column</c> for a cell (rows are numbered from 0 and are created when their first cell arrives).</param>
/// <param name="Value">The value as written in the document ("1,400.00", "3 September 2026"); the control parses it by the field's type.</param>
/// <param name="SourceId">The id of the <see cref="DocumentSpan"/> it came from, if any.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Note">Why the model is unsure; shown under values below the review threshold.</param>
public sealed record ExtractionValueEvent(string Path, string Value, string? SourceId, double Confidence, string? Note = null) : ExtractionEvent;

/// <summary>Tokens used so far (running totals, not increments).</summary>
public sealed record ExtractionUsageEvent(int InputTokens, int OutputTokens) : ExtractionEvent;

/// <summary>The model could not finish; the same as throwing from the stream.</summary>
public sealed record ExtractionErrorEvent(string Message) : ExtractionEvent;

/// <summary>Reads a document with a model.</summary>
public interface IDocumentExtractor
{
    /// <summary>
    /// Produces the events of one extraction; it runs off the UI thread and the control consumes the events on it. The token
    /// is cancelled when the person stops the run, starts another, or the control leaves the visual tree.
    /// </summary>
    /// <param name="document">The document (see <c>ExtractionDocument.ToPlainText</c> for the text to send).</param>
    /// <param name="schema">What to find (see <c>ExtractionSchema.ToJsonSchema</c> for the output format to send).</param>
    /// <param name="cancellationToken">Cancelled when the run is no longer wanted.</param>
    IAsyncEnumerable<ExtractionEvent> ExtractAsync(ExtractionDocument document, ExtractionSchema schema, CancellationToken cancellationToken);
}

/// <summary>One value of the form: what the document says, how sure the model was and where the person has taken it.</summary>
public sealed class ExtractionValue : AvaloniaObject
{
    private string _text = string.Empty;
    private string? _sourceId;
    private double _confidence;
    private string? _note;
    private ExtractionValueStatus _status;
    private string? _error;
    private object? _typedValue;
    private bool _isLow;

    /// <summary>The text.</summary>
    public static readonly DirectProperty<ExtractionValue, string> TextProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, string>(nameof(Text), o => o.Text, (o, v) => o.Text = v);

    /// <summary>The id of the cited span.</summary>
    public static readonly DirectProperty<ExtractionValue, string?> SourceIdProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, string?>(nameof(SourceId), o => o.SourceId);

    /// <summary>The confidence, 0 to 1.</summary>
    public static readonly DirectProperty<ExtractionValue, double> ConfidenceProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, double>(nameof(Confidence), o => o.Confidence);

    /// <summary>The model's note.</summary>
    public static readonly DirectProperty<ExtractionValue, string?> NoteProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, string?>(nameof(Note), o => o.Note);

    /// <summary>The review status.</summary>
    public static readonly DirectProperty<ExtractionValue, ExtractionValueStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, ExtractionValueStatus>(nameof(Status), o => o.Status);

    /// <summary>Why the text does not fit the type; null when it does.</summary>
    public static readonly DirectProperty<ExtractionValue, string?> ErrorProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, string?>(nameof(Error), o => o.Error);

    /// <summary>The parsed value (date, number, text); null when invalid.</summary>
    public static readonly DirectProperty<ExtractionValue, object?> TypedValueProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, object?>(nameof(TypedValue), o => o.TypedValue);

    /// <summary>Whether the value is below the review threshold and not yet checked or edited.</summary>
    public static readonly DirectProperty<ExtractionValue, bool> NeedsCheckProperty =
        AvaloniaProperty.RegisterDirect<ExtractionValue, bool>(nameof(NeedsCheck), o => o.NeedsCheck);

    /// <summary>Creates a value for a path.</summary>
    /// <param name="path">The field key, or <c>table.row.column</c>.</param>
    public ExtractionValue(string path) => Path = path;

    /// <summary>The path.</summary>
    public string Path { get; }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => _text; set => SetAndRaise(TextProperty, ref _text, value); }

    /// <inheritdoc cref="SourceIdProperty"/>
    public string? SourceId { get => _sourceId; set => SetAndRaise(SourceIdProperty, ref _sourceId, value); }

    /// <inheritdoc cref="ConfidenceProperty"/>
    public double Confidence { get => _confidence; set => SetAndRaise(ConfidenceProperty, ref _confidence, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => _note; set => SetAndRaise(NoteProperty, ref _note, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public ExtractionValueStatus Status { get => _status; set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <inheritdoc cref="ErrorProperty"/>
    public string? Error { get => _error; set => SetAndRaise(ErrorProperty, ref _error, value); }

    /// <inheritdoc cref="TypedValueProperty"/>
    public object? TypedValue { get => _typedValue; set => SetAndRaise(TypedValueProperty, ref _typedValue, value); }

    /// <inheritdoc cref="NeedsCheckProperty"/>
    public bool NeedsCheck { get => _isLow; set => SetAndRaise(NeedsCheckProperty, ref _isLow, value); }

    /// <summary>Whether the text fits the type (an empty value is valid).</summary>
    public bool IsValid => Error is null;
}

/// <summary>The typed result: one entry per field in schema order; dates are ISO text, amounts and numbers are numbers, an invalid value is null, a table is a list of row dictionaries.</summary>
public sealed partial class ExtractionOutput
{
    /// <summary>Creates an output.</summary>
    /// <param name="values">Key and value of each field in order; a table's value is a list of <c>IReadOnlyDictionary&lt;string, object?&gt;</c>.</param>
    public ExtractionOutput(IReadOnlyList<KeyValuePair<string, object?>> values) => Values = values;

    /// <summary>The entries in order.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Values { get; }
}

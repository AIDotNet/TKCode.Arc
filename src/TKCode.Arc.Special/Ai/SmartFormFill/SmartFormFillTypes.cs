using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a <see cref="SmartFormFillField"/> holds; it picks the keyboard and, for a company, adds a logo slot.</summary>
public enum SmartFormFillFieldKind
{
    /// <summary>Plain text (a name, a job title, an address).</summary>
    Text,

    /// <summary>An email address.</summary>
    Email,

    /// <summary>A phone number.</summary>
    Phone,

    /// <summary>A web address.</summary>
    Url,

    /// <summary>A company name, with its logo (or a monogram) at the start of the input.</summary>
    Company
}

/// <summary>How much of a form row a <see cref="SmartFormFillField"/> takes.</summary>
public enum SmartFormFillSpan
{
    /// <summary>Half the row; two half fields share a row. Under 440px every field takes the full row.</summary>
    Half,

    /// <summary>The full row.</summary>
    Full
}

/// <summary>Where a <see cref="SmartFormFill"/> is in its work.</summary>
public enum SmartFormFillPhase
{
    /// <summary>Waiting for text; the form can be typed in.</summary>
    Idle,

    /// <summary>The pasted text is being read; Stop or Escape cancels and nothing changes.</summary>
    Reading,

    /// <summary>The read values are flowing into the fields one by one.</summary>
    Filling,

    /// <summary>A read has filled the form; low-confidence fields may ask for review and Undo can restore the old values.</summary>
    Filled,

    /// <summary>The form is being saved.</summary>
    Saving,

    /// <summary>The form was saved.</summary>
    Saved,

    /// <summary>A read or a save failed; the status says which.</summary>
    Failed
}

/// <summary>
/// One field of a <see cref="SmartFormFill"/>: its id (the key of values and of extraction results), its label, what it
/// holds, a placeholder and how much of the row it takes.
/// </summary>
public sealed class SmartFormFillField : AvaloniaObject
{
    /// <summary>The key of the field in values and extraction results ("email").</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<SmartFormFillField, string>(nameof(Id), string.Empty);

    /// <summary>The visible label; also the input's accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SmartFormFillField, string?>(nameof(Label));

    /// <summary>What the field holds: text, an email, a phone number, a web address or a company (which adds a logo slot).</summary>
    public static readonly StyledProperty<SmartFormFillFieldKind> KindProperty =
        AvaloniaProperty.Register<SmartFormFillField, SmartFormFillFieldKind>(nameof(Kind));

    /// <summary>The hint shown while the field is empty.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SmartFormFillField, string?>(nameof(Placeholder));

    /// <summary>Half a row (default) or the full row.</summary>
    public static readonly StyledProperty<SmartFormFillSpan> SpanProperty =
        AvaloniaProperty.Register<SmartFormFillField, SmartFormFillSpan>(nameof(Span));

    /// <summary>Creates an empty field (set <see cref="Id"/> and <see cref="Label"/>).</summary>
    public SmartFormFillField()
    {
    }

    /// <summary>Creates a field.</summary>
    public SmartFormFillField(string id, string label, SmartFormFillFieldKind kind = SmartFormFillFieldKind.Text,
        string? placeholder = null, SmartFormFillSpan span = SmartFormFillSpan.Half)
    {
        Id = id;
        Label = label;
        Kind = kind;
        Placeholder = placeholder;
        Span = span;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public SmartFormFillFieldKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="SpanProperty"/>
    public SmartFormFillSpan Span { get => GetValue(SpanProperty); set => SetValue(SpanProperty, value); }

    /// <summary>
    /// The contact form a <see cref="SmartFormFill"/> shows when it is given no fields: name, job title, company, email,
    /// phone and address.
    /// </summary>
    public static SmartFormFillField[] ContactFields() =>
    [
        new("name", "Name", SmartFormFillFieldKind.Text, "Full name"),
        new("title", "Job title", SmartFormFillFieldKind.Text, "Role"),
        new("company", "Company", SmartFormFillFieldKind.Company, "Company name", SmartFormFillSpan.Full),
        new("email", "Email", SmartFormFillFieldKind.Email, "name@company.com"),
        new("phone", "Phone", SmartFormFillFieldKind.Phone, "+1 555 000 0000"),
        new("address", "Address", SmartFormFillFieldKind.Text, "Street, city", SmartFormFillSpan.Full),
    ];
}

/// <summary>One value read from the pasted text: the value, how sure the reader is, the exact text it came from and why it is unsure.</summary>
public sealed class SmartFormFillValue
{
    /// <summary>Creates a value.</summary>
    /// <param name="value">The value to put in the field.</param>
    /// <param name="confidence">How sure the reader is, 0 to 1.</param>
    /// <param name="source">The exact substring of the pasted text the value came from; it lights up in the text.</param>
    /// <param name="note">Why the reader is unsure (shown under a field that needs review).</param>
    public SmartFormFillValue(string value, double confidence, string? source = null, string? note = null)
    {
        Value = value;
        Confidence = Math.Clamp(double.IsNaN(confidence) ? 0 : confidence, 0, 1);
        Source = source;
        Note = note;
    }

    /// <summary>The value to put in the field.</summary>
    public string Value { get; }

    /// <summary>How sure the reader is, 0 to 1. Below <see cref="SmartFormFill.ReviewThreshold"/> the field asks for review.</summary>
    public double Confidence { get; }

    /// <summary>The exact substring of the pasted text the value came from, or null; it powers the highlight.</summary>
    public string? Source { get; }

    /// <summary>Why the reader is unsure, or null; shown under a field that needs review.</summary>
    public string? Note { get; }
}

/// <summary>What one read of the pasted text found: a <see cref="SmartFormFillValue"/> per field id. Fields it leaves out keep their value.</summary>
public sealed class SmartFormFillResult : Dictionary<string, SmartFormFillValue>
{
    /// <summary>Creates an empty result.</summary>
    public SmartFormFillResult()
        : base(StringComparer.Ordinal)
    {
    }
}

/// <summary>A text behind the Try sample button: a short label and the text it pastes.</summary>
public sealed class SmartFormFillSample
{
    /// <summary>Creates a sample.</summary>
    public SmartFormFillSample(string label, string text)
    {
        Label = label;
        Text = text;
    }

    /// <summary>The sample's name ("Email signature").</summary>
    public string Label { get; }

    /// <summary>The text it pastes.</summary>
    public string Text { get; }

    /// <summary>
    /// What a simulated read (no <see cref="SmartFormFill.Extract"/>) returns for <see cref="Text"/>, checked by hand; null
    /// reads it with the stand-in rules.
    /// </summary>
    public SmartFormFillResult? Result { get; init; }
}

/// <summary>Arguments of <see cref="SmartFormFill.ValuesChanged"/>, <see cref="SmartFormFill.Undone"/> and <see cref="SmartFormFill.Submitted"/>: the form's values by field id.</summary>
public sealed class SmartFormFillValuesEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SmartFormFillValuesEventArgs(RoutedEvent routedEvent, IReadOnlyDictionary<string, string> values)
        : base(routedEvent)
    {
        Values = values;
    }

    /// <summary>The values by field id (a snapshot).</summary>
    public IReadOnlyDictionary<string, string> Values { get; }
}

/// <summary>Arguments of <see cref="SmartFormFill.Filled"/>: the read that has just been filled in.</summary>
public sealed class SmartFormFillFilledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SmartFormFillFilledEventArgs(RoutedEvent routedEvent, SmartFormFillResult result, int filledCount, int reviewCount)
        : base(routedEvent)
    {
        Result = result;
        FilledCount = filledCount;
        ReviewCount = reviewCount;
    }

    /// <summary>What the read found.</summary>
    public SmartFormFillResult Result { get; }

    /// <summary>How many fields it filled.</summary>
    public int FilledCount { get; }

    /// <summary>How many of them ask for review.</summary>
    public int ReviewCount { get; }
}

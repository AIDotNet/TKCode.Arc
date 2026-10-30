using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="SmartFormFill"/> shows or says, in English by default (the title, the description and the
/// submit button are properties of the control). Replace the whole object to localize or reword; counts and names are
/// format functions.
/// </summary>
public sealed class SmartFormFillLabels
{
    /// <summary>The English defaults.</summary>
    public static SmartFormFillLabels Default { get; } = new();

    /// <summary>The heading of the paste well.</summary>
    public string PasteHeading { get; init; } = "Paste text";

    /// <summary>The hint in the empty paste area.</summary>
    public string PastePlaceholder { get; init; } = "Paste an email signature, a forwarded message or notes";

    /// <summary>The accessible name of the paste area and of the pasted text view.</summary>
    public string PastedText { get; init; } = "Pasted text";

    /// <summary>The model chip beside the paste well, from the model's name: "Read by" and the name (empty without one).</summary>
    public Func<string, string> ReadBy { get; init; } = model => string.IsNullOrWhiteSpace(model) ? string.Empty : "Read by " + model;

    /// <summary>The button that pastes the next sample.</summary>
    public string TrySample { get; init; } = "Try sample";

    /// <summary>The name of Try sample, from the sample's label: "Try sample: Email signature".</summary>
    public Func<string, string> TrySampleNamed { get; init; } = sample => "Try sample: " + sample;

    /// <summary>The button that reads the text.</summary>
    public string Fill { get; init; } = "Fill";

    /// <summary>The button that stops a read.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The button that returns from the pasted text view to the paste area.</summary>
    public string EditText { get; init; } = "Edit text";

    /// <summary>The Undo button.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The badge of a field that asks for review.</summary>
    public string Review { get; init; } = "Review";

    /// <summary>The badge of a field the person checked.</summary>
    public string Checked { get; init; } = "Checked";

    /// <summary>The badge of a filled field the person changed.</summary>
    public string Edited { get; init; } = "Edited";

    /// <summary>The badge of a confident field, from the confidence 0 to 1: "94%".</summary>
    public Func<double, string> Confidence { get; init; } =
        confidence => Math.Round(confidence * 100).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>The name of a field's check button, from the field's label: "Mark Phone as checked".</summary>
    public Func<string, string> CheckField { get; init; } = field => "Mark " + field + " as checked";

    /// <summary>The input's description for a confident field, from the confidence: "Filled by AI, 94% confident".</summary>
    public Func<double, string> ConfidenceDescription { get; init; } =
        confidence => "Filled by AI, " + Math.Round(confidence * 100).ToString(CultureInfo.InvariantCulture) + "% confident";

    /// <summary>The input's description for a field that needs review, from the confidence and the reason (may be empty).</summary>
    public Func<double, string, string> ReviewDescription { get; init; } = (confidence, note) =>
        "Needs review, " + Math.Round(confidence * 100).ToString(CultureInfo.InvariantCulture) + "% confident"
        + (string.IsNullOrEmpty(note) ? string.Empty : ". " + note);

    /// <summary>The note under a field that needs review when the reader gave no reason.</summary>
    public string ReviewNote { get; init; } = "Low confidence. Check this value.";

    /// <summary>The status before anything was read.</summary>
    public string Ready { get; init; } = "Paste text and AI fills the form";

    /// <summary>The status while reading, from the model's name: "Reading with" and the name, or "Reading" without one.</summary>
    public Func<string, string> Reading { get; init; } = model => string.IsNullOrWhiteSpace(model) ? "Reading" : "Reading with " + model;

    /// <summary>The status while values flow in.</summary>
    public string Filling { get; init; } = "Filling the form";

    /// <summary>The status after a read, from the filled and the review counts: "Filled 6 fields, 1 to review".</summary>
    public Func<int, int, string> Filled { get; init; } = (filled, review) =>
        "Filled " + filled + (filled == 1 ? " field" : " fields") + (review > 0 ? ", " + review + " to review" : string.Empty);

    /// <summary>The status once every field that asked for review was checked or edited.</summary>
    public string AllChecked { get; init; } = "All fields checked";

    /// <summary>The status when a field was checked and others still ask for review, from the remaining count.</summary>
    public Func<string, int, string> CheckedField { get; init; } = (field, left) =>
        field + " checked, " + left + " left to review";

    /// <summary>The status when a read found nothing.</summary>
    public string NothingFound { get; init; } = "No contact details found in the text";

    /// <summary>The status after Stop or Escape during a read.</summary>
    public string Stopped { get; init; } = "Stopped. Nothing changed.";

    /// <summary>The status when the read failed.</summary>
    public string ReadFailed { get; init; } = "Couldn't read the text. Try again.";

    /// <summary>The status after Undo.</summary>
    public string Undone { get; init; } = "Restored the previous values";

    /// <summary>The status when Save is pressed while fields still need review, from their count.</summary>
    public Func<int, string> SaveBlocked { get; init; } = review =>
        "Check " + review + (review == 1 ? " field" : " fields") + " before saving";

    /// <summary>The status while saving.</summary>
    public string Saving { get; init; } = "Saving";

    /// <summary>The status after a save.</summary>
    public string Saved { get; init; } = "Contact saved";

    /// <summary>The status when the save failed.</summary>
    public string SaveFailed { get; init; } = "Couldn't save. Try again.";

    /// <summary>Added to what the live region says when the built-in reader ran: the read was simulated.</summary>
    public string Simulated { get; init; } = "Simulated";
}

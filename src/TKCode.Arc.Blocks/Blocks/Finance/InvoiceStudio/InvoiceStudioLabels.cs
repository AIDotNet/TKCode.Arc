using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="InvoiceStudio"/>, on the form and on the printed preview. Replace the whole object to
/// localize the block; entries with values are functions that receive already formatted text.
/// </summary>
public sealed class InvoiceStudioLabels
{
    /// <summary>Culture for amounts and dates (en-US by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The compact switch's form tab: "Edit".</summary>
    public string EditTab { get; init; } = "Edit";

    /// <summary>The compact switch's preview tab: "Preview".</summary>
    public string PreviewTab { get; init; } = "Preview";

    /// <summary>Label of the client select: "Bill to".</summary>
    public string BillTo { get; init; } = "Bill to";

    /// <summary>Placeholder of the client select: "Choose a client".</summary>
    public string ChooseClient { get; init; } = "Choose a client";

    /// <summary>Label of the invoice number field: "Invoice number".</summary>
    public string Number { get; init; } = "Invoice number";

    /// <summary>Label of the terms select: "Payment due".</summary>
    public string Terms { get; init; } = "Payment due";

    /// <summary>A terms option: days ⇒ "On receipt" or "In 30 days".</summary>
    public Func<int, string> TermsOption { get; init; } = days => days <= 0 ? "On receipt" : $"In {days} days";

    /// <summary>Column header: "Description".</summary>
    public string Description { get; init; } = "Description";

    /// <summary>Column header: "Qty".</summary>
    public string Quantity { get; init; } = "Qty";

    /// <summary>Column header: "Price".</summary>
    public string Price { get; init; } = "Price";

    /// <summary>Column header: "Tax".</summary>
    public string Tax { get; init; } = "Tax";

    /// <summary>Column header: "Amount".</summary>
    public string Amount { get; init; } = "Amount";

    /// <summary>Placeholder of a line's description: "What did you deliver?".</summary>
    public string DescriptionPlaceholder { get; init; } = "What did you deliver?";

    /// <summary>The add-line button: "Add line".</summary>
    public string AddLine { get; init; } = "Add line";

    /// <summary>Screen reader name of a line's remove button: (description or line number) ⇒ "Remove line 2".</summary>
    public Func<string, string> RemoveLine { get; init; } = what => $"Remove {what}";

    /// <summary>How a line without a description is named: number ⇒ "line 2".</summary>
    public Func<int, string> LineNumber { get; init; } = number => $"line {number}";

    /// <summary>A tax option: rate ⇒ "No tax" or "8%".</summary>
    public Func<decimal, string> TaxOption { get; init; } = rate => rate <= 0 ? "No tax" : (rate * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>Label of the discount: "Discount".</summary>
    public string Discount { get; init; } = "Discount";

    /// <summary>Label of the notes: "Notes".</summary>
    public string Notes { get; init; } = "Notes";

    /// <summary>Placeholder of the notes: "Payment details or a thank-you".</summary>
    public string NotesPlaceholder { get; init; } = "Payment details or a thank-you";

    /// <summary>Totals row: "Subtotal".</summary>
    public string Subtotal { get; init; } = "Subtotal";

    /// <summary>Totals row: "Total".</summary>
    public string Total { get; init; } = "Total";

    /// <summary>The send button: "Send invoice".</summary>
    public string Send { get; init; } = "Send invoice";

    /// <summary>The send button while sending: "Sending".</summary>
    public string Sending { get; init; } = "Sending";

    /// <summary>The send button once sent: "Sent".</summary>
    public string Sent { get; init; } = "Sent";

    /// <summary>The line after sending: (email or client name) ⇒ "Sent to ap@harborpine.example".</summary>
    public Func<string, string> SentTo { get; init; } = to => $"Sent to {to}";

    /// <summary>Error when no client is chosen: "Choose who the invoice is for.".</summary>
    public string NeedClient { get; init; } = "Choose who the invoice is for.";

    /// <summary>Error when no line can be billed: "Add a line with a description and an amount.".</summary>
    public string NeedLine { get; init; } = "Add a line with a description and an amount.";

    /// <summary>Error when the send handler fails: "The invoice could not be sent. Try again.".</summary>
    public string SendFailed { get; init; } = "The invoice could not be sent. Try again.";

    /// <summary>The preview's heading: "Invoice".</summary>
    public string InvoiceHeading { get; init; } = "Invoice";

    /// <summary>The preview's issue date label: "Issued".</summary>
    public string Issued { get; init; } = "Issued";

    /// <summary>The preview's due date label: "Due".</summary>
    public string Due { get; init; } = "Due";

    /// <summary>The preview's amount-due label: "Amount due".</summary>
    public string AmountDue { get; init; } = "Amount due";

    /// <summary>Screen reader name of the preview: "Invoice preview".</summary>
    public string PreviewName { get; init; } = "Invoice preview";

    /// <summary>A date on the preview: date ⇒ "Nov 9, 2026".</summary>
    public Func<DateTime, string> Date { get; init; } = date => date.ToString("MMM d, yyyy", CultureInfo.GetCultureInfo("en-US"));
}

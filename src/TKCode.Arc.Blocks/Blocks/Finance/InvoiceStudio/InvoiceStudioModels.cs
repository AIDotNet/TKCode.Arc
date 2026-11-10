using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Who an invoice of <see cref="InvoiceStudio"/> is billed to.</summary>
public sealed class InvoiceClient
{
    /// <summary>Creates an empty client (set the properties).</summary>
    public InvoiceClient()
    {
    }

    /// <summary>Creates a client with its name, email and postal address.</summary>
    public InvoiceClient(string name, string? email = null, string? address = null)
    {
        Name = name;
        Email = email;
        Address = address;
    }

    /// <summary>Stable identifier; defaults to the name when empty.</summary>
    public string? Id { get; set; }

    /// <summary>The client's name as printed ("Harbor &amp; Pine Studio").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Where the invoice is sent.</summary>
    public string? Email { get; set; }

    /// <summary>The postal address, one line per line break.</summary>
    public string? Address { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// One line of an invoice: what was sold, how many, at what unit price and tax rate. Edits made in the block write back to
/// these objects, and changes made in code reach the block and the preview at once.
/// </summary>
public class InvoiceLineItem : AvaloniaObject
{
    /// <summary>What the line is for ("Brand workshop, half day").</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<InvoiceLineItem, string?>(nameof(Description));

    /// <summary>How many units (hours, items); at least 0.</summary>
    public static readonly StyledProperty<decimal> QuantityProperty =
        AvaloniaProperty.Register<InvoiceLineItem, decimal>(nameof(Quantity), 1m, coerce: (_, v) => Math.Max(0, v));

    /// <summary>The price of one unit, before tax.</summary>
    public static readonly StyledProperty<decimal> UnitPriceProperty =
        AvaloniaProperty.Register<InvoiceLineItem, decimal>(nameof(UnitPrice), coerce: (_, v) => Math.Max(0, v));

    /// <summary>The tax rate of the line as a share (0.08 = 8%).</summary>
    public static readonly StyledProperty<decimal> TaxRateProperty =
        AvaloniaProperty.Register<InvoiceLineItem, decimal>(nameof(TaxRate), coerce: (_, v) => Math.Clamp(v, 0, 1));

    /// <summary>Creates an empty line (one unit, no price, no tax).</summary>
    public InvoiceLineItem()
    {
    }

    /// <summary>Creates a line.</summary>
    public InvoiceLineItem(string description, decimal quantity, decimal unitPrice, decimal taxRate = 0)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="QuantityProperty"/>
    public decimal Quantity { get => GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }

    /// <inheritdoc cref="UnitPriceProperty"/>
    public decimal UnitPrice { get => GetValue(UnitPriceProperty); set => SetValue(UnitPriceProperty, value); }

    /// <inheritdoc cref="TaxRateProperty"/>
    public decimal TaxRate { get => GetValue(TaxRateProperty); set => SetValue(TaxRateProperty, value); }

    /// <summary>Quantity times unit price, rounded to cents.</summary>
    public decimal Amount => InvoiceStudioMath.Round(Quantity * UnitPrice);
}

/// <summary>How the discount of an <see cref="InvoiceStudio"/> is given.</summary>
public enum InvoiceDiscountKind
{
    /// <summary>A share of the subtotal, in percent (0 to 100).</summary>
    Percent,

    /// <summary>A fixed amount, at most the subtotal.</summary>
    Amount
}

/// <summary>Where an <see cref="InvoiceStudio"/> invoice is.</summary>
public enum InvoiceStudioState
{
    /// <summary>Being edited.</summary>
    Draft,

    /// <summary>Waiting for the send handler.</summary>
    Sending,

    /// <summary>Sent; the next edit makes it a draft again.</summary>
    Sent
}

/// <summary>The sums of an invoice, each rounded to cents.</summary>
/// <param name="Subtotal">The sum of the line amounts.</param>
/// <param name="Discount">What the discount takes off the subtotal.</param>
/// <param name="Tax">The tax on the discounted lines.</param>
/// <param name="Total">Subtotal − discount + tax.</param>
public sealed record InvoiceTotals(decimal Subtotal, decimal Discount, decimal Tax, decimal Total);

/// <summary>One line as sent: a snapshot of an <see cref="InvoiceLineItem"/>.</summary>
/// <param name="Description">What the line is for.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="UnitPrice">The price of one unit.</param>
/// <param name="TaxRate">The tax rate as a share.</param>
/// <param name="Amount">Quantity times unit price.</param>
public sealed record InvoiceLineSnapshot(string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate, decimal Amount);

/// <summary>The invoice an <see cref="InvoiceStudio"/> sends: everything on the printed page.</summary>
/// <param name="Number">The invoice number.</param>
/// <param name="Client">Who it is billed to.</param>
/// <param name="IssueDate">The date of issue.</param>
/// <param name="DueDate">The date payment is due.</param>
/// <param name="Lines">The billable lines.</param>
/// <param name="Totals">The sums.</param>
/// <param name="Notes">The note printed under the totals.</param>
public sealed record InvoiceDraft(
    string Number,
    InvoiceClient Client,
    DateTime IssueDate,
    DateTime DueDate,
    IReadOnlyList<InvoiceLineSnapshot> Lines,
    InvoiceTotals Totals,
    string? Notes);

/// <summary>An <see cref="InvoiceStudio"/> invoice was sent.</summary>
public sealed class InvoiceSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public InvoiceSentEventArgs(RoutedEvent routedEvent, InvoiceDraft invoice) : base(routedEvent) => Invoice = invoice;

    /// <summary>The invoice that was sent.</summary>
    public InvoiceDraft Invoice { get; }
}

/// <summary>Pure arithmetic of <see cref="InvoiceStudio"/>.</summary>
internal static class InvoiceStudioMath
{
    /// <summary>Rounds to cents, halves away from zero.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The invoice sums. The discount comes off the subtotal before tax and is shared across the lines by their amounts, so
    /// each line is taxed on what is really charged for it.
    /// </summary>
    public static InvoiceTotals Compute(IEnumerable<InvoiceLineItem> lines, InvoiceDiscountKind kind, decimal discountValue)
    {
        var items = lines.Select(l => (Amount: l.Amount, l.TaxRate)).ToList();
        var subtotal = items.Sum(i => i.Amount);
        var discount = Discount(subtotal, kind, discountValue);
        var kept = subtotal > 0 ? (subtotal - discount) / subtotal : 0;
        var tax = items.Sum(i => Round(i.Amount * kept * i.TaxRate));
        return new InvoiceTotals(subtotal, discount, tax, subtotal - discount + tax);
    }

    /// <summary>What the discount takes off <paramref name="subtotal"/>: a percentage (0–100) or an amount, never more than the subtotal.</summary>
    public static decimal Discount(decimal subtotal, InvoiceDiscountKind kind, decimal value)
    {
        if (subtotal <= 0 || value <= 0)
        {
            return 0;
        }
        return kind == InvoiceDiscountKind.Percent
            ? Round(subtotal * Math.Min(value, 100) / 100)
            : Math.Min(Round(value), subtotal);
    }

    /// <summary>A line is billable when it has a description and a positive amount.</summary>
    public static bool IsBillable(InvoiceLineItem line) => !string.IsNullOrWhiteSpace(line.Description) && line.Amount > 0;
}

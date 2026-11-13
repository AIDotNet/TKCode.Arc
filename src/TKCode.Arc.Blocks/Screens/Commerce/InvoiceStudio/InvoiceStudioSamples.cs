namespace TKCode.Arc;

/// <summary>
/// Invented sample content for the <see cref="InvoiceStudio"/> preview: a small design studio, three clients, two lines and a
/// simulated mail service. Replace them with your own data; none of it is required.
/// </summary>
public static class InvoiceStudioSamples
{
    /// <summary>How long the sample mail service takes to send.</summary>
    public static readonly TimeSpan SendDelay = TimeSpan.FromMilliseconds(900);

    /// <summary>Three invented clients.</summary>
    public static IReadOnlyList<InvoiceClient> CreateClients() =>
    [
        new("Harbor & Pine Studio", "ap@harborpine.example", "12 Quay Street\nPort Wren, OR 97001") { Id = "harbor" },
        new("Lumen Field Bakery", "hello@lumenfield.example", "88 Orchard Lane\nMillbrook, VT 05401") { Id = "lumen" },
        new("Tallgrass Cycling Club", "treasurer@tallgrass.example", "3 Prairie Road\nKestrel, KS 66044") { Id = "tallgrass" },
    ];

    /// <summary>Fills <paramref name="studio"/> with the sample sender, clients, two lines, a 10% discount and a note.</summary>
    public static void Apply(InvoiceStudio studio)
    {
        ArgumentNullException.ThrowIfNull(studio);
        var clients = CreateClients();
        studio.SenderName = "Fernhill Design Co.";
        studio.SenderAddress = "41 Linden Row, Ashford\nbilling@fernhill.example";
        studio.Clients = clients;
        studio.SelectedClient = clients[0];
        studio.InvoiceNumber = "INV-0042";
        studio.PaymentTerms = 30;
        studio.DiscountKind = InvoiceDiscountKind.Percent;
        studio.DiscountValue = 10;
        studio.Notes = "Thank you for working with us. Bank transfer details are on the second page.";
        studio.Lines.Clear();
        studio.Lines.Add(new InvoiceLineItem("Brand workshop, half day", 1, 1800m, 0.08m));
        studio.Lines.Add(new InvoiceLineItem("Logo refinements", 6, 95m, 0.08m));
        studio.Lines.Add(new InvoiceLineItem("Printed swatch book", 2, 42.5m, 0.2m));
    }

    /// <summary>A simulated mail service: waits <see cref="SendDelay"/> and accepts the invoice.</summary>
    public static async Task<bool> SendAsync(InvoiceDraft invoice, CancellationToken cancellation)
    {
        await Task.Delay(SendDelay, cancellation);
        return true;
    }
}

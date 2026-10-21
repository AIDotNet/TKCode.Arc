using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BillingOverviewEventsDemo : UserControl
{
    private readonly List<string> _entries = new();

    public BillingOverviewEventsDemo()
    {
        InitializeComponent();
        Billing.ChangePlanRequested += (_, _) => Write("ChangePlanRequested");
        Billing.CancelPlanRequested += (_, _) => Write("CancelPlanRequested");
        Billing.UpdatePaymentMethodRequested += (_, _) => Write("UpdatePaymentMethodRequested");
        Billing.EditBillingEmailRequested += (_, _) => Write("EditBillingEmailRequested");
        Billing.InvoiceDownloadRequested += (_, e) => Write($"InvoiceDownloadRequested: {e.Invoice.Number}");
    }

    private void Write(string entry)
    {
        // Newest first, the last five events.
        _entries.Insert(0, $"{DateTime.Now:HH:mm:ss}  {entry}");
        if (_entries.Count > 5)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _entries);
    }
}

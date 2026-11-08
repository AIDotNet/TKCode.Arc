using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>The Billing page: the plan, the card on file and the invoices.</summary>
public sealed class BillingPageViewModel
{
    public BillingPageViewModel(IWorkspaceService workspace)
    {
        Plan = workspace.GetPlan();
        PaymentMethod = workspace.GetPaymentMethod();
        Invoices = workspace.GetInvoices().ToList();
        BillingEmail = workspace.BillingEmail;
    }

    public BillingPlan Plan { get; }

    public BillingPaymentMethod PaymentMethod { get; }

    public IList<BillingInvoice> Invoices { get; }

    public string BillingEmail { get; }
}

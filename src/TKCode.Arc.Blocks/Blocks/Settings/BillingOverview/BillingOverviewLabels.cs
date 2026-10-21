using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of a <see cref="BillingOverview"/>. Replace the whole object (with <c>init</c> properties) to reword
/// or localize the block; the view rebuilds its text when <see cref="BillingOverview.Labels"/> changes. Functions receive
/// values already formatted with <see cref="Culture"/>.
/// </summary>
public sealed class BillingOverviewLabels
{
    /// <summary>Culture for money and dates (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Small label above the plan name: "Current plan".</summary>
    public string CurrentPlan { get; init; } = "Current plan";

    /// <summary>Plan name shown when there is no plan: "No plan".</summary>
    public string NoPlan { get; init; } = "No plan";

    /// <summary>Line under "No plan": "Choose a plan to get started."</summary>
    public string NoPlanDescription { get; init; } = "Choose a plan to get started.";

    /// <summary>Badge of an active plan: "Active".</summary>
    public string Active { get; init; } = "Active";

    /// <summary>Badge of a plan in its trial: "Trial".</summary>
    public string Trialing { get; init; } = "Trial";

    /// <summary>Badge of a plan whose payment failed: "Past due".</summary>
    public string PastDue { get; init; } = "Past due";

    /// <summary>Badge of a canceled plan: "Canceled".</summary>
    public string Canceled { get; init; } = "Canceled";

    /// <summary>Seat count: n ⇒ "6 seats" ("1 seat").</summary>
    public Func<int, string> Seats { get; init; } = n => n == 1 ? "1 seat" : $"{n:N0} seats";

    /// <summary>Renewal of an active plan: date ⇒ "Renews on Nov 1, 2026".</summary>
    public Func<string, string> RenewsOn { get; init; } = date => $"Renews on {date}";

    /// <summary>End of a trial: date ⇒ "Trial ends on Nov 1, 2026".</summary>
    public Func<string, string> TrialEndsOn { get; init; } = date => $"Trial ends on {date}";

    /// <summary>A failed renewal: date ⇒ "Payment overdue since Oct 1, 2026".</summary>
    public Func<string, string> OverdueSince { get; init; } = date => $"Payment overdue since {date}";

    /// <summary>End of access of a canceled plan: date ⇒ "Ends on Nov 1, 2026".</summary>
    public Func<string, string> EndsOn { get; init; } = date => $"Ends on {date}";

    /// <summary>Separator between parts of a line: " · ".</summary>
    public string Separator { get; init; } = " · ";

    /// <summary>Period after a monthly price: "per month".</summary>
    public string PerMonth { get; init; } = "per month";

    /// <summary>Period after a yearly price: "per year".</summary>
    public string PerYear { get; init; } = "per year";

    /// <summary>The next charge: (amount, date) ⇒ "Next invoice $90.00 on Nov 1, 2026". The amount is emphasized.</summary>
    public Func<string, string, string> NextInvoice { get; init; } = (amount, date) => $"Next invoice {amount} on {date}";

    /// <summary>The next charge without a renewal date: amount ⇒ "Next invoice $90.00".</summary>
    public Func<string, string> NextInvoiceAmount { get; init; } = amount => $"Next invoice {amount}";

    /// <summary>The owed amount of a past-due plan: amount ⇒ "$90.00 is overdue".</summary>
    public Func<string, string> Overdue { get; init; } = amount => $"{amount} is overdue";

    /// <summary>Footer of a canceled plan: "No upcoming invoices".</summary>
    public string NoUpcomingInvoice { get; init; } = "No upcoming invoices";

    /// <summary>Button that raises <see cref="BillingOverview.ChangePlanRequested"/>: "Change plan".</summary>
    public string ChangePlan { get; init; } = "Change plan";

    /// <summary>The same button when there is no plan: "Choose a plan".</summary>
    public string ChoosePlan { get; init; } = "Choose a plan";

    /// <summary>Button that raises <see cref="BillingOverview.CancelPlanRequested"/>: "Cancel plan".</summary>
    public string CancelPlan { get; init; } = "Cancel plan";

    /// <summary>Title of the payment card: "Payment".</summary>
    public string Payment { get; init; } = "Payment";

    /// <summary>The card: (brand, last four) ⇒ "Mastercard ending in 5108".</summary>
    public Func<string, string, string> CardEnding { get; init; } = (brand, last4) => $"{brand} ending in {last4}";

    /// <summary>Expiry of the card: "04/2029" ⇒ "Expires 04/2029".</summary>
    public Func<string, string> Expires { get; init; } = date => $"Expires {date}";

    /// <summary>Expiry within 60 days, shown in the warning color: "11/2026" ⇒ "Expires soon · 11/2026".</summary>
    public Func<string, string> ExpiresSoon { get; init; } = date => $"Expires soon · {date}";

    /// <summary>A card past its expiry, shown in the danger color: "09/2026" ⇒ "Expired 09/2026".</summary>
    public Func<string, string> Expired { get; init; } = date => $"Expired {date}";

    /// <summary>Title of the card row without a payment method: "No payment method".</summary>
    public string NoPaymentMethod { get; init; } = "No payment method";

    /// <summary>Line under "No payment method": "Add a card to pay for your plan."</summary>
    public string NoPaymentMethodDescription { get; init; } = "Add a card to pay for your plan.";

    /// <summary>Button that raises <see cref="BillingOverview.UpdatePaymentMethodRequested"/>: "Update".</summary>
    public string UpdateCard { get; init; } = "Update";

    /// <summary>Accessible name of the update button: "Update payment method".</summary>
    public string UpdateCardName { get; init; } = "Update payment method";

    /// <summary>The same button without a payment method: "Add card".</summary>
    public string AddCard { get; init; } = "Add card";

    /// <summary>Header of the billing email row: "Billing email".</summary>
    public string BillingEmail { get; init; } = "Billing email";

    /// <summary>Description of the billing email row without an address: "Invoices are not emailed yet."</summary>
    public string NoBillingEmail { get; init; } = "Invoices are not emailed yet.";

    /// <summary>Button that raises <see cref="BillingOverview.EditBillingEmailRequested"/>: "Edit".</summary>
    public string EditEmail { get; init; } = "Edit";

    /// <summary>The same button without an address: "Add".</summary>
    public string AddEmail { get; init; } = "Add";

    /// <summary>Accessible name of the billing email button: "Edit billing email".</summary>
    public string EditEmailName { get; init; } = "Edit billing email";

    /// <summary>Title of the invoice card: "Invoices".</summary>
    public string Invoices { get; init; } = "Invoices";

    /// <summary>Column header: "Date".</summary>
    public string DateColumn { get; init; } = "Date";

    /// <summary>Column header: "Invoice".</summary>
    public string NumberColumn { get; init; } = "Invoice";

    /// <summary>Column header: "Amount".</summary>
    public string AmountColumn { get; init; } = "Amount";

    /// <summary>Column header: "Status".</summary>
    public string StatusColumn { get; init; } = "Status";

    /// <summary>Invoice status: "Paid".</summary>
    public string Paid { get; init; } = "Paid";

    /// <summary>Invoice status: "Open".</summary>
    public string Open { get; init; } = "Open";

    /// <summary>Invoice status: "Past due".</summary>
    public string InvoicePastDue { get; init; } = "Past due";

    /// <summary>Invoice status: "Refunded".</summary>
    public string Refunded { get; init; } = "Refunded";

    /// <summary>Invoice status: "Void".</summary>
    public string Void { get; init; } = "Void";

    /// <summary>Accessible name and tooltip of a download button: number ⇒ "Download LS-2026-010".</summary>
    public Func<string, string> Download { get; init; } = number => $"Download {number}";

    /// <summary>Button that reveals the older invoices: total ⇒ "Show all invoices (6)".</summary>
    public Func<int, string> ShowAll { get; init; } = count => $"Show all invoices ({count})";

    /// <summary>The same button once they are shown: "Show fewer".</summary>
    public string ShowFewer { get; init; } = "Show fewer";

    /// <summary>Empty invoice list: "No invoices yet".</summary>
    public string NoInvoices { get; init; } = "No invoices yet";

    /// <summary>Line under "No invoices yet": "Invoices appear here after your first payment."</summary>
    public string NoInvoicesHint { get; init; } = "Invoices appear here after your first payment.";
}

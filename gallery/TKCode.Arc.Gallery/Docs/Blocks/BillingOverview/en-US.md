---
title: Billing overview
summary: The current plan, next charge, payment method and invoice history on one page, with the actions next to each.
---

<Demo name="Overview" />

## When to use

`BillingOverview` is the billing page of an account or workspace settings area. It answers the three questions people
bring to billing (what am I paying, when is the next charge, what did I pay before) and places the actions that change
those answers next to the facts they change: changing the plan, updating the card, editing the billing email and
downloading an invoice.

- **Workspace billing in a SaaS admin console**: an owner opens Settings, then Billing, and sees that the Team plan
  renews on Nov 1 for 12 seats, which card will be charged and when the next invoice is due. The block answers that
  without a separate report.
- **Handing invoices to finance**: a finance lead needs last quarter's invoices for an expense report. The invoice table
  lists them newest first with a download button on each row, so nobody has to ask the vendor for a PDF.
- **A per-seat plan with a fixed price**: a team plan billed monthly or yearly, where the next charge is the plan price
  for the current seat count. The plan card shows the price per period, the seats and the amount of the next charge.
- **Trials and failed payments**: a trial shows when it ends and what the first charge will be; a plan whose last charge
  failed shows a danger badge, so the owner fixes the card before access is affected.
- **Billing actions that open your own flows**: plan changes, card updates and cancellations usually need confirmation,
  proration or a payment provider's form. The block only raises events, so each flow stays in your code.

## When not to use

- **Usage-based billing where this period's total depends on metered units**: use `UsageBilling`, which shows the spend
  so far, the projected total, the usage per meter and the spending limit.
- **Choosing or comparing plans on a pricing page**: use `BillingToggle` and `BillingPrice`. `BillingOverview` shows the
  plan a customer already has, not the options they can pick from.
- **Collecting card details**: the block shows the card on file and never takes card numbers. Open your payment
  provider's form in a `Dialog` from `UpdatePaymentMethodRequested`.
- **A long, searchable payment history**: use `SortableDataTable`, which sorts, filters and pages rows. The invoice
  table here shows a short, newest-first list with "Show all invoices" for the rest.
- **A cancellation with questions or a retention offer**: use `CancelFlow`, a multi-step flow that you can start from
  `CancelPlanRequested`. `BillingOverview` only raises the request.

## Usage

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding Card}"
                     BillingEmail="{Binding BillingEmail}"
                     Invoices="{Binding Invoices}"
                     VisibleInvoiceCount="6" />
```

`BillingOverview` derives from Avalonia's `TemplatedControl` and is composed of `SettingsGroup`, `SettingsRow`, `Badge`
and `Button`. It inherits their spacing, focus ring, hover states and motion, so a billing page built from it matches
the other settings blocks without extra styling. Set four data properties: `Plan`, `PaymentMethod`, `BillingEmail` and
`Invoices`. The three model classes (`BillingPlan`, `BillingPaymentMethod` and `BillingInvoice`) are plain objects.
They can be written in XAML, as in the previews, or created in code. They raise `PropertyChanged`, so a change to a
plan, card or invoice after it is shown updates the view without replacing the object.

`Invoices` starts as an empty list you can add to. Replace it or bind it to your own collection; changes to an
observable collection update the table. Invoices are always shown newest first, by `Date`.

### Anatomy

The page has three cards, separated by the 16 px `GroupGap`. From top to bottom:

1. **Plan card** (`Plan`): the eyebrow "Current plan", the plan name (`PlanNamePart`, `TypeXL`) and a status badge
   (`PlanStatusPart`). Below the name, a detail line with the seat count and the renewal date. On the right, the price
   (`PricePart`) in `TypeXL` with "per month" or "per year" in the secondary text color. The footer (`NextInvoicePart`)
   shows the next charge with the amount emphasized, then the "Cancel plan" and "Change plan" buttons.
2. **Payment card** (`PaymentMethod`, `BillingEmail`): the brand mark (`CardBrandPart`), a 36 by 24 px rounded box with
   the brand text in `TypeXS`, followed by "{Brand} ending in {Last4}" (`CardTitlePart`) and the expiry line
   (`CardExpiryPart`). The "Update" button (`UpdateCardPart`) sits on the right. Below a divider, the "Billing email" row
   (`EmailRowPart`) shows the address and the "Edit" button (`EditEmailPart`).
3. **Invoices card** (`Invoices`): the "Invoices" title and a column header with Date, Invoice, Amount and Status
   (`InvoiceHeaderPart`, 32 px high). Each row (`InvoiceRowsPart`) is 48 px high, with a hairline above it, the date,
   the invoice number, the amount right-aligned in tabular figures, a status badge and a 36 px download icon button.
4. **Folded rows and "Show all invoices"** (`MoreInvoicesPart`, `ShowAllPart`): the invoices beyond
   `VisibleInvoiceCount` sit in a folding part, and the footer button toggles it.
5. **Empty state** (`InvoicesEmptyPart`): with no invoices, a 40 px tile, "No invoices yet" and a hint line replace the
   table.

Below 560 px (`NarrowWidth`), the price moves under the plan name, the footers stack and the Invoice column hides. Card
rows under 440 px move their button under the text. The Status column keeps a 14 px gap instead of 28 px.

### Plan states

The plan's `Status` sets the badge, the detail line and the footer, so every state reads the same way.

| `Status` | Badge | Detail line | Footer |
|---|---|---|---|
| `Active` | Success | "6 seats · Renews on Nov 1, 2026" | Next charge, "Cancel plan", "Change plan" |
| `Trialing` | Accent | "Trial ends on Nov 1, 2026" | Next charge, "Cancel plan", "Change plan" |
| `PastDue` | Danger | "Payment overdue since Oct 1, 2026" | Next charge, "Cancel plan", "Change plan" |
| `Canceled` | Neutral | "Ends on Nov 1, 2026" | "Change plan" only; no "Cancel plan" |

With no plan (`Plan` is null), the card shows "No plan", "Choose a plan to get started." and a "Choose a plan" button
that raises `ChangePlanRequested`. A canceled plan keeps its object, so the owner still sees when access ends.
The payment card follows the same rule: with no `PaymentMethod`, "Add card" raises `UpdatePaymentMethodRequested`.

### Invoice states

| `Status` | Badge | Row treatment |
|---|---|---|
| `Paid` | Success | Normal |
| `Open` | Neutral | Normal; issued and not yet paid |
| `PastDue` | Danger | Normal; due and unpaid |
| `Refunded` | Neutral | Muted: badge and amount at 70 percent opacity |
| `Void` | Neutral | Muted: badge and amount at 70 percent opacity |

Muted rows stay in the table and keep their download button, because the customer still needs the document. Do not
remove a refunded invoice from `Invoices` to hide it; the history then no longer matches the bank statement.

### Trial and an expiring card

A trialing plan shows when the trial ends and what the first charge will be: set `Status` to `Trialing` and `RenewsOn`
to the trial end date.

The card expiry is checked against `Clock`. When the expiry month starts within 60 days, the expiry line turns to the
warning color and reads "Expires soon" with the date, and a card past its expiry month reads "Expired" in the danger
color. `IsPaymentMethodExpiringSoon` and `IsPaymentMethodExpired` report the same two states, so you can show a banner
in your own page or send a reminder email. The brand is shown as short text in a small frame (VISA, MC, AMEX), never
as a logo. Without invoices, the table is replaced by "No invoices yet".

<Demo name="Trial" />

"Today" comes from the CLR property `Clock`, a `TimeProvider` (the system clock by default). Set a fixed clock in tests:

```csharp
billing.Clock = TimeProvider.System; // or any TimeProvider that your test supplies
```

### Handling the actions

Every button raises a routed event and changes nothing by itself. The events are `ChangePlanRequested`,
`CancelPlanRequested`, `UpdatePaymentMethodRequested` and `EditBillingEmailRequested`, and
`InvoiceDownloadRequested`, whose `BillingInvoiceEventArgs.Invoice` is the invoice on that row. Open your own dialog or
flow from the event, call your API, and then update `Plan`, `PaymentMethod`, `BillingEmail` or `Invoices` so the block
shows the result. The block does not confirm, prorate or call a payment service.

<Demo name="Events" />

The Events demo writes each event to a log. The download handler is asynchronous, so the view model can save the file
in the background:

```csharp
billing.InvoiceDownloadRequested += async (_, e) =>
{
    var pdf = await invoices.DownloadAsync(e.Invoice.Number);
    await SaveAsync(pdf, $"{e.Invoice.Number}.pdf");
};

billing.CancelPlanRequested += (_, _) => cancelDialog.Open(); // for example a CancelFlow in a Dialog
```

`VisibleInvoiceCount` (default 4) sets how many invoices show before "Show all invoices (n)". The button unfolds the
rest and changes to "Show fewer". `IsInvoiceListExpanded` is the same state; bind it two-way to keep it in your view model.

### Data shape and performance

The block does not page, sort or search. Pass the invoices the page should show: the most recent ones for the overview,
and a link to the complete history. The table reuses its rows when the collection changes, so adding an invoice does
not rebuild the table. Give each invoice a unique `Number`, because it names the download button.

Money and dates use `Labels.Culture` (en-US by default), and each amount uses its invoice's `Currency` or the plan's.
Format amounts once, in the block.

### Binding to a view model

In an MVVM app, the view model owns the billing data and the flows, and the block displays them. The example below uses
CommunityToolkit.Mvvm. The app's own service (`IBillingService`) and its methods are placeholders for your API.

```csharp
public partial class BillingViewModel : ObservableObject
{
    private readonly IBillingService _billing;

    public BillingViewModel(IBillingService billing)
    {
        _billing = billing;
    }

    [ObservableProperty]
    private BillingPlan? _plan;

    [ObservableProperty]
    private BillingPaymentMethod? _paymentMethod;

    [ObservableProperty]
    private string? _billingEmail;

    [ObservableProperty]
    private decimal? _nextInvoiceAmount;

    [ObservableProperty]
    private bool _isInvoiceListExpanded;

    public ObservableCollection<BillingInvoice> Invoices { get; } = new();

    [RelayCommand]
    private async Task CancelPlanAsync()
    {
        await _billing.CancelAtPeriodEndAsync();
        Plan!.Status = BillingPlanStatus.Canceled;
    }

    [RelayCommand]
    private async Task DownloadInvoiceAsync(BillingInvoice invoice)
    {
        var pdf = await _billing.DownloadAsync(invoice.Number);
        await _files.SaveAsync(pdf, $"{invoice.Number}.pdf");
    }
}
```

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding PaymentMethod}"
                     BillingEmail="{Binding BillingEmail}"
                     NextInvoiceAmount="{Binding NextInvoiceAmount}"
                     Invoices="{Binding Invoices}"
                     IsInvoiceListExpanded="{Binding IsInvoiceListExpanded, Mode=TwoWay}"
                     VisibleInvoiceCount="6" />
```

The block does not expose commands, so connect its events to the view model in code-behind, calling the
`CancelPlanCommand` and `DownloadInvoiceCommand` the toolkit generates.

Two-way bindings: `IsInvoiceListExpanded` is the only property to bind `Mode=TwoWay`, because "Show all invoices"
writes it. The other inputs are one-way: change the model objects (for example `Plan.Status`) or replace the property,
and the view follows.

### Text and language

All built-in text lives in `BillingOverviewLabels`. Replace the whole object to reword or localize the block; the
texts that contain values are functions that receive the values already formatted with `Culture`. The defaults are
English, for example "Renews on Nov 1, 2026", "Show all invoices (6)" and "Download LS-2026-010".

```csharp
billing.Labels = new BillingOverviewLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    CurrentPlan = "Huidig abonnement",
    ChangePlan = "Abonnement wijzigen",
    RenewsOn = date => $"Wordt verlengd op {date}",
    ShowAll = count => $"Alle facturen tonen ({count})",
};
```

Replace the whole object rather than one field, because its properties are init-only. Keep the functions that take values,
since they set the word order. One object covers the whole block, from the plan card to the empty state.

### Example: Team plan billing page

A workspace owner opens billing on a Team plan. Two seats were added in October, so the plan now costs $288 per month
for 14 seats, and the footer shows the next invoice on Nov 1 at the same amount. The card is a Mastercard that expires
in 2029, so no warning shows. The invoice table shows three rows and a "Show all invoices (4)" button; one earlier
invoice was refunded and is muted. The owner can download any invoice, change the plan or update the card from here.

The footer's next charge follows `Price` unless `NextInvoiceAmount` is set, so this example leaves it null. Set it only
when the next charge differs from the plan price. The invoice descriptions are not visible; they become part of each
download button's accessible name.

![A team plan billing page with a plan card, a card on file, a billing email and three recent invoices](images/BillingOverview.png)

```xml
<StackPanel Width="680" Spacing="16">
    <arc:BillingOverview BillingEmail="accounts@northwind.dev"
                         VisibleInvoiceCount="3">
        <arc:BillingOverview.Plan>
            <arc:BillingPlan Name="Team" Price="288" Currency="USD" Seats="14"
                             Interval="Month" Status="Active" RenewsOn="2026-11-01" />
        </arc:BillingOverview.Plan>
        <arc:BillingOverview.PaymentMethod>
            <arc:BillingPaymentMethod Brand="Mastercard" Last4="5108"
                                      ExpiryMonth="4" ExpiryYear="2029" />
        </arc:BillingOverview.PaymentMethod>
        <arc:BillingOverview.Invoices>
            <arc:BillingInvoice Number="LS-2026-010" Date="2026-10-01" Amount="288"
                                Status="Paid" Description="Team plan, 14 seats" />
            <arc:BillingInvoice Number="LS-2026-009" Date="2026-09-01" Amount="240"
                                Status="Paid" Description="Team plan, 12 seats" />
            <arc:BillingInvoice Number="LS-2026-008" Date="2026-08-01" Amount="200"
                                Status="Paid" Description="Team plan, 10 seats" />
            <arc:BillingInvoice Number="LS-2026-007" Date="2026-07-01" Amount="200"
                                Status="Refunded" Description="Team plan, 10 seats" />
        </arc:BillingOverview.Invoices>
    </arc:BillingOverview>
</StackPanel>
```

### Content guidelines

Write amounts with the currency and dates with the month name, as in "$240.00" and "Nov 1, 2026". Name the plan as the
customer bought it, and use the same word as the rest of the product: "Plan", not "Subscription" if the app says "Plan".

- Button text is a verb and an object in sentence case: "Change plan", "Cancel plan", "Update payment method".
- Keep the status words short. One or two words fit a badge: "Active", "Trial", "Past due", "Canceled".
- Say what happens to access, not only the plan: "Ends on Nov 1, 2026" tells the owner when service stops.

| Instead of | Write |
|---|---|
| Subscription tier | Plan |
| Cancel subscription | Cancel plan |
| Payment failed | Past due |
| 12 Seats | 12 seats |
| Click here | Download LS-2026-010 (the row's download button) |
| Card expires soon, please update | Expires soon · Apr 2029 (the default line) |

### Best practices

**Do**

- Set `Status` and `RenewsOn` from your billing provider on every change, because the footer, the badge and the detail
  line all read them. A stale status tells the owner the wrong thing about their access.
- Set `NextInvoiceAmount` when the next charge differs from the plan price, such as after seats are added or a discount
  applies, so the footer does not show a price the owner will not pay.
- Open a confirmation dialog or a `CancelFlow` from `CancelPlanRequested`, and update `Plan` only after the server
  confirms the change. The block cannot undo a change that the server rejected.
- Keep the newest invoices in `Invoices` and link to the full history. A long list in the overview makes the most recent
  invoice harder to find.

**Avoid**

- Hiding refunded or void invoices. The owner and the accounting team compare this list with their statements, and a
  missing row looks like a missing payment.
- Formatting amounts in your view model and passing text. The block formats money with `Labels.Culture` and the
  invoice's currency; a second format leads to two different values on the same page.
- Setting `Plan` to null for a canceled subscription. The owner then sees "Choose a plan" and loses the end date.
- Collecting card numbers in a field next to the card on file. Card data belongs in the payment provider's hosted form,
  which keeps it out of your application.

### Common questions

**Why does the card say "Choose a plan" and not "Cancel plan"?** The block shows "No plan" when `Plan` is null, and then
the only action is "Choose a plan", which raises `ChangePlanRequested`. A canceled plan keeps its object with
`Status="Canceled"`, so "Cancel plan" is hidden for it and the card shows its end date.

**Why does "Show all invoices" not appear?** The button appears only when `Invoices` has more items than
`VisibleInvoiceCount`. Set `VisibleInvoiceCount` to a smaller number, or add more invoices, to see it.

**Does the block cancel the subscription or download the file?** No. It raises `CancelPlanRequested` and
`InvoiceDownloadRequested`, and your code calls the billing API, shows a confirmation and saves the file.

**Why does the expiry line show "Expires soon" in one environment but not another?** The check uses `Clock`, which
defaults to the system clock. Set `Clock` to a fixed `TimeProvider` in tests, and make sure the card's expiry month is
set the same way in every environment.

**How do I show a different currency for one invoice?** Set `BillingInvoice.Currency` on that invoice; the others keep the
plan's `Currency`.

## API reference

<!-- api: BillingOverview -->

`Clock` (`TimeProvider`, default `TimeProvider.System`) is a CLR property: the source of "today" for the expiry warning.

### BillingPlan

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | The plan's name, such as "Studio". |
| `Price` | `decimal` | The total charged each interval, all seats included. |
| `Currency` | `string` | ISO 4217 code of the price (default "USD"). |
| `Interval` | `BillingInterval` | `Month` or `Year`: how often the price is charged. |
| `Seats` | `int?` | Number of paid seats, or null for plans not priced per seat. |
| `Status` | `BillingPlanStatus` | `Active`, `Trialing`, `PastDue` or `Canceled`. |
| `RenewsOn` | `DateTime?` | The next renewal; the end of the trial while trialing, the end of access once canceled. |

### BillingPaymentMethod

| Property | Type | Description |
|---|---|---|
| `Brand` | `string` | The card network as text, such as "Visa" or "Mastercard". |
| `Last4` | `string` | The last four digits of the card number. |
| `ExpiryMonth` | `int` | The expiry month, 1 to 12. |
| `ExpiryYear` | `int` | The four-digit expiry year. |

### BillingInvoice

| Property | Type | Description |
|---|---|---|
| `Number` | `string` | The invoice number, such as "LS-2026-010". |
| `Date` | `DateTime` | The date the invoice was issued; the table sorts by it, newest first. |
| `Amount` | `decimal` | The invoice total. |
| `Currency` | `string?` | ISO 4217 code of the amount; null uses the plan's currency. |
| `Status` | `BillingInvoiceStatus` | `Paid`, `Open`, `PastDue`, `Refunded` or `Void`. |
| `Description` | `string?` | What the invoice was for; added to the row's accessible name. |

### BillingInvoiceEventArgs

| Property | Type | Description |
|---|---|---|
| `Invoice` | `BillingInvoice` | The invoice whose download button was pressed. |

### Enumerations

| Type | Members |
|---|---|
| `BillingInterval` | `Month`, `Year` |
| `BillingPlanStatus` | `Active` (success badge), `Trialing` (accent badge), `PastDue` (danger badge), `Canceled` (neutral badge) |
| `BillingInvoiceStatus` | `Paid` (success badge), `Open`, `PastDue` (danger badge), `Refunded` and `Void` (muted neutral badges) |

## Accessibility

- Every button has a name. The download buttons are icon-only and are named "Download LS-2026-010", which is also their
  tooltip; "Update" is named "Update payment method" and "Edit" is named "Edit billing email".
- Each invoice row reads as one line, such as "Oct 1, 2026, LS-2026-010, $90.00, Paid". The column header is hidden from
  assistive technology.
- Status badges read their text. The expiry warning is text and a glyph as well as color.
- The "Show all invoices" button's name follows its text. Folded invoices leave the tab order, so Tab never lands on a
  hidden download button.
- The block takes no focus of its own; Tab moves through its buttons in reading order.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the next or previous button in reading order |
| `Enter` / `Space` | Press the focused button, such as "Change plan" or a download button |

## Motion

- Older invoices unfold and fold with a height spring without bounce, and the chevron of "Show all invoices" turns on a
  snappy spring while its label swaps with a short blur.
- Badge and button labels that change in place swap with a blur, as in `Badge` and `Button`. The expiry line eases to the
  warning color.
- Reduced motion keeps the crossfades and makes the height and the chevron jump. Off places everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Older invoices fold or unfold | Height opens on a smooth spring without bounce | Height jumps to the end state | Immediate |
| "Show all invoices" toggles | The chevron turns on a snappy spring; the label swaps with a blur | The chevron jumps; the label crossfades | Immediate |
| A badge or button label changes | The text swaps with a short blur | Crossfade | Immediate |
| Expiry line becomes a warning | The text eases to the warning color | The color crossfades | Immediate |

## Related

- The other Settings blocks: `SettingsPage`, `SettingsCommand`, `SecuritySettings`, `UsageBilling` and `CancelFlow` (a
  natural answer to `CancelPlanRequested`).
- `SettingsGroup` and `SettingsRow`, the cards and rows the block is built from.
- `Badge` for the plan and invoice status, `Button` for the actions.
- `BillingToggle` and `BillingPrice` for choosing a plan, and `Dialog` for confirming changes.

| Component | Choose it when |
|---|---|
| `UsageBilling` | The charge depends on metered usage this period, with a spending limit |
| `CancelFlow` | The cancellation needs steps, reasons or a retention offer |
| `SortableDataTable` | The payment history is long and must be sorted, filtered or paged |
| `SettingsGroup` / `SettingsRow` | You build a settings card that is not a billing summary |
| `BillingToggle` / `BillingPrice` | The customer chooses between plans or billing periods |
| `Dialog` | A plan change, card update or download needs a confirmation or a form |

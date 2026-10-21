---
title: Billing overview
summary: The current plan, next charge, payment method and invoice history on one page, with the actions next to each.
---

<Demo name="Overview" />

## When to use

- The billing section of account settings, where people check what they pay, when they pay next and what they paid before.
- A page that leads to the billing tasks your app already has: changing or canceling the plan, updating the card,
  changing the billing email and downloading invoices.
- Subscriptions with a fixed price per period, with or without seats.

## When not to use

- Metered plans whose total depends on usage this period: use `UsageBilling`.
- Choosing a plan or comparing plans: use a pricing layout with `BillingToggle` and `BillingPrice`.
- Collecting card details: the block shows the card on file and asks the host to update it.
- A long, searchable payment history: use `SortableDataTable`.

## Usage

Set `Plan`, `PaymentMethod`, `BillingEmail` and `Invoices`. The models are plain classes, so they can be written in XAML
(as in the preview) or created in code. They raise `PropertyChanged`, so a change to a plan, card or invoice after it is
shown updates the view. `Invoices` starts as an empty list you can add to; replace it or bind it to show another
collection, and changes to an observable collection update the table. Invoices are shown newest first.

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding Card}"
                     BillingEmail="{Binding BillingEmail}"
                     Invoices="{Binding Invoices}"
                     VisibleInvoiceCount="6" />
```

The plan card shows the plan name with a status badge (Active in the success color, Trial in the accent color, Past due in
the danger color, Canceled neutral), the seats and the renewal date, and the price per month or per year. Its footer shows
the next charge: `NextInvoiceAmount`, or the plan's price when it is null. A canceled plan has no "Cancel plan" button,
and without a plan the card offers "Choose a plan". Money uses the plan's `Currency`; an invoice can set its own.

Below 560 px wide (`NarrowWidth`) the price moves under the plan name, the footers stack and the Invoice column hides.

### Trial and an expiring card

A trialing plan shows when the trial ends and what the first charge will be. When the card's expiry month starts within 60
days the expiry line turns to the warning color with "Expires soon", and a card past its expiry month reads "Expired" in
the danger color; `IsPaymentMethodExpiringSoon` and `IsPaymentMethodExpired` report the same. The brand is shown as short
text in a small frame (VISA, MC, AMEX), never as a logo. Without invoices the table is replaced by "No invoices yet".

<Demo name="Trial" />

"Today" comes from the CLR property `Clock`, a `TimeProvider` (the system clock by default). Set it to a fixed clock in
tests or previews:

```csharp
billing.Clock = TimeProvider.System; // or any TimeProvider, such as FakeTimeProvider in tests
```

### Handling the actions

Every button raises a routed event and changes nothing by itself: `ChangePlanRequested`, `CancelPlanRequested`,
`UpdatePaymentMethodRequested`, `EditBillingEmailRequested` and `InvoiceDownloadRequested`, whose arguments carry the
`Invoice`. Open your own dialogs or flows from them and update the models afterwards. Here the first three invoices
show, and the rest fold behind "Show all invoices".

<Demo name="Events" />

```csharp
billing.InvoiceDownloadRequested += async (_, e) =>
{
    var pdf = await invoices.DownloadAsync(e.Invoice.Number);
    await SaveAsync(pdf, $"{e.Invoice.Number}.pdf");
};

billing.CancelPlanRequested += (_, _) => cancelDialog.Open(); // for example a CancelFlow in a Dialog
```

`VisibleInvoiceCount` (default 4) sets how many invoices show before "Show all invoices (n)". The button unfolds the rest
and turns into "Show fewer"; `IsInvoiceListExpanded` is the same state, two-way bindable.

### Text and language

All built-in text lives in `BillingOverviewLabels`. Replace the whole object to reword or localize the block; texts with
values are functions that receive the values already formatted with `Culture`.

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

## Accessibility

- Every button has a name. The download buttons are icon-only and are named "Download LS-2026-010", which is also their
  tooltip; "Update" is named "Update payment method" and "Edit" is named "Edit billing email".
- Each invoice row reads as one line, such as "Oct 1, 2026, LS-2026-010, $90.00, Paid". The column header is hidden from
  assistive technology.
- Status badges read their text. The expiry warning is text and a glyph as well as color.
- The "Show all invoices" button's name follows its text. Folded invoices leave the tab order, so Tab never lands on a
  hidden download button.
- Tab moves through the plan actions, the payment buttons, the download buttons and "Show all invoices" in reading order.
  Buttons activate with Enter or Space and show the Arc focus ring.

## Motion

- Older invoices unfold and fold with a height spring without bounce, and the chevron of "Show all invoices" turns on a
  snappy spring while its label swaps with a short blur.
- Badge and button labels that change in place swap with a blur, as in `Badge` and `Button`. The expiry line eases to the
  warning color.
- Reduced motion keeps the crossfades and makes the height and the chevron jump. Off places everything at once.

## Related

- The other Settings blocks: `SettingsPage`, `SettingsCommand`, `SecuritySettings`, `UsageBilling` and `CancelFlow` (a
  natural answer to `CancelPlanRequested`).
- `SettingsGroup` and `SettingsRow`, the cards and rows the block is built from.
- `Badge` for the plan and invoice status, `Button` for the actions.
- `BillingToggle` and `BillingPrice` for choosing a plan, and `Dialog` for confirming changes.

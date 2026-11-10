---
title: Invoice studio
summary: An invoice editor with a live printed preview: a client and payment terms, line items with quantity, price and tax, a discount as a percentage or an amount, totals that follow every edit, and a checked send.
---

<Demo name="Preview" />

## When to use

`InvoiceStudio` is where a freelancer or a small business writes an invoice. The form asks for who it is for, the invoice
number and when payment is due, then the lines: what was delivered, how many, at what price and with which tax. A discount
can be a percentage or a fixed amount, and a note goes at the bottom. Next to the form, a sheet shows the invoice as it
will print, and both the form's totals and the sheet follow every keystroke. Send checks that the invoice can be sent and
hands it to your service.

- **Billing in a freelance or agency tool.**
- **Quotes and estimates** with the same structure (change the labels).
- **Back-office corrections** where the printed result matters.

## When not to use

- **Showing a finished invoice only**: render the sheet or a PDF; the form is not needed.
- **Recurring subscriptions**: use the Billing overview block.
- **Long catalogs of items**: let people pick items from a catalog and add them as lines from code.

## Usage

The block derives from Avalonia's `TemplatedControl`. Set the sender (`SenderName`, `SenderAddress`), the `Clients` to
choose from, and fill `Lines` with `InvoiceLineItem`s. `InvoiceStudioSamples.Apply` fills a block with the preview's
invented studio.

```xml
<arc:InvoiceStudio SenderName="Fernhill Design Co." InvoiceNumber="INV-0042" PaymentTerms="30">
    <arc:InvoiceLineItem Description="Brand workshop" Quantity="1" UnitPrice="1800" TaxRate="0.08" />
</arc:InvoiceStudio>
```

### Totals

The subtotal sums the line amounts (quantity times price, rounded to cents). The discount comes off the subtotal before
tax, either `DiscountValue` percent or that amount, never more than the subtotal. Tax is charged per line on what is left
of the line after its share of the discount, at the line's rate. `Totals` holds the four sums.

### Sending

Send calls `CreateDraft`: an invoice needs a client and at least one line with a description and an amount. If one is
missing, the reason appears under the button, is announced, and the focus moves to the client or the first line. A ready
invoice goes to `SendAsync` as an `InvoiceDraft` (only billable lines); while it runs the button shows a spinner, then
"Sent" with "Sent to …" under it, and `InvoiceSent` is raised. The next edit makes it a draft again.

```csharp
studio.SendAsync = async (invoice, cancellation) =>
    await billing.SendAsync(invoice.Client.Email!, invoice, cancellation);
```

### Narrow blocks

Below 900 px the form and the preview take turns behind an Edit and Preview switch; below 560 px each line puts its
quantity, price and tax under the description.

<Demo name="Narrow" />

## API reference

<!-- api: InvoiceStudio -->

<!-- api: InvoiceLineItem -->

<!-- api: InvoiceClient -->

<!-- api: InvoiceDraft -->

## Accessibility

- Every field has a label or an accessible name: a line's description, quantity, price and tax fields are named by their
  column, and its remove button "Remove line 2" or "Remove Logo refinements".
- After a line is removed the focus moves to the next line's remove button.
- The preview is named "Invoice preview"; its text is real text a screen reader can read.
- A polite live region announces a missing field and the sent invoice.

## Motion

- The form and the sheet change in place; numbers update without rolling so typing stays calm.
- The send button's label swaps with the button's own transition and shows a spinner while sending.
- **Reduced motion** and **Off** apply every change at once.

## Responsive behavior

- The block reads its own width: below 900 px the preview moves behind a switch, below 560 px lines take two rows.
- The sheet keeps paper and ink colors in both themes, like a printout.

## Related

- The Checkout summary block for an order summary with a pay button.
- `Input`, `NumberField`, `Select` and `Textarea`, the fields the form uses.

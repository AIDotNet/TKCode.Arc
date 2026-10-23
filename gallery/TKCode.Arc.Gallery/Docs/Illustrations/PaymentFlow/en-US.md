---
title: Payment flow
summary: A card payment from checkout through tokenization, authorization and the issuer's decision to settlement.
---

<Demo name="CardPayment" />

## When to use

`PaymentFlow` explains what happens between a customer pressing Pay and the merchant receiving the funds. It draws
the route of one card payment: checkout, the payment gateway that tokenizes the card, the card network, the issuing
bank that approves or declines, the response that travels back, and the settlement to the merchant account. It sits on
checkout help pages, payment documentation and onboarding screens, where the reader needs to know why a payment can be
approved or declined and when the money arrives.

- **Checkout help for a declined card**: a help article on the checkout page that explains a decline. The diagram
  shows that the issuing bank made the decision, so the customer knows to contact the bank or try another card.
- **Integration documentation for a payment provider**: a developer guide that shows the amount, currency and merchant
  as they are configured for the integration, and where the tokenization and authorization steps happen.
- **Merchant onboarding for settlement timing**: a page that explains when approved funds reach the merchant's payout
  account. The settlement path and the balance readout show the order of events without a table of timings.
- **Sales or support training**: a short explanation for support staff who answer "why was my card declined" questions,
  with the decline path shown in the danger color.
- **Product marketing on a pricing page**: a visual for a feature that handles card payments end to end, placed beside
  the description of the payment methods.

## When not to use

- **Entering a payment amount or card details**: use `MoneyInput` for an amount that a person types and a form for the
  card fields. `PaymentFlow` only illustrates the flow; it has no input.
- **Plotting payment totals over time**: use `BarChart` or `LineChart` from the Charts category for revenue, volume or
  decline rates. The balance readout in the illustration counts the story, not your ledger.
- **The live status of a real payment**: use `Alert` for a payment that failed, `Badge` for its state and `Timeline` for
  the events of one transaction. An illustration that loops cannot report that a particular payment is still pending.
- **A list of past payments**: use `DataGrid` with the amounts, dates and decline codes. A single animated example does
  not replace a searchable history.
- **A multi-step checkout the customer completes**: use `Stepper` for the steps of the checkout itself. `PaymentFlow`
  shows what happens after the customer presses Pay and does not take input.

## Usage

The story plays one payment after another. The caption under the diagram describes the current step, and the merchant
balance grows with every approved payment. The control needs no data to run, and it uses the default amount of 42,
the merchant Northwind and the card ending in 4242.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PaymentFlow />
</UserControl>
```

`PaymentFlow` derives from `Illustration`, which derives from Avalonia's `TemplatedControl`. It takes focus, has a
`Label` for assistive technology, and uses the same frame, caption and playback button as the other system
illustrations. Each payment is a fixed sequence on the scene clock, so the same inputs always produce the same
story, and the decision the issuer makes is a value the control knows before the animation reaches it.

### Anatomy

1. **Customer** (`Labels.Customer`): the card at the top left. Its detail line shows the last four digits, for
   example "•••• 4242", and an amount badge above it shows the amount when a payment starts.
2. **Checkout** (`Labels.Checkout`): the merchant's checkout, with the merchant name (`MerchantName`) as its detail line.
   It turns to the accent color while it sends the card details, and to success or danger when the payment ends.
3. **Gateway** (`Labels.Gateway`, `Labels.GatewayDetail`): the payment gateway that tokenizes the card. A "Tokenized"
   badge with a lock icon sits above it while the gateway holds the token.
4. **Card network** (`Labels.Network`, `Labels.NetworkDetail`): the network that carries the authorization request.
5. **Issuing bank** (`Labels.Issuer`, `Labels.IssuerDetail`): the bank below the card network. Its busy ring turns on
   while it checks funds and risk, and a badge below it reads "Approved" or "Declined" with its icon.
6. **Merchant account** (`Labels.Merchant`, `Labels.MerchantDetail`): the payout account below the checkout. A balance
   readout beneath it (`Labels.Balance`) shows the amount in the currency, and a "+ amount" badge shows each settlement.
7. **Settlement line**: a dashed line from the gateway that turns left to the merchant account. It carries the funds
   only for approved payments.
8. **Caption line** and **play button**: the same controls as the other illustrations. The button is hidden in the
   still poster.

The scene is 844 by 270 units and scales to the width the control is given. The nodes are 150 by 52 units with the
12 px corner radius of the token, and the frame has 20 px of padding on the sides and top and 14 px at the bottom. A
request travels on the upper lane of each connection and the response on the lower lane, so the two never overlap.

### Amount, currency and merchant

`Amount` is a `decimal` and defaults to 42. `Currency` is an ISO code and defaults to USD; an empty value falls back to
USD. `MerchantName` names the merchant at checkout and `CardLast4` shows the last four digits of the card.

The illustration formats money for the currency. USD, EUR and GBP show their symbol with two decimals, JPY and KRW show
their symbol with no decimals, CNY and INR show their symbol with two decimals, and other codes show the ISO code and a
space before the amount, such as "CHF 42.00". Thousands are grouped, and the number always uses the invariant culture,
so "1,284.00" reads the same on every machine.

<Demo name="AmountAndMerchant" />

```xml
<arc:PaymentFlow Amount="129.90"
                 Currency="EUR"
                 MerchantName="Atelier"
                 CardLast4="0119"
                 DeclineEvery="0" />
```

Set `Amount` and `Currency` from the same order that the checkout charges, so the badge shown on the diagram matches
the amount the customer sees. The diagram does not convert currencies, and the balance readout is in the currency of
`Currency` even when the merchant account holds another one.

### Declines

`DeclineEvery` declines every N-th payment. The default is 4, so the fourth, eighth and twelfth payments of the story
are declined. Set it to 0 to approve every payment, and use a value of 1 to decline every payment. Negative values are
treated as 0.

```xml
<arc:PaymentFlow DeclineEvery="0" />
```

A declined payment follows the same route to the issuer. The issuer's badge reads "Declined" in the danger color, the
response travels back on the lower lane, and the caption explains the decline: "Declined: insufficient funds". The
settlement line does not light up, the merchant balance does not change, and the story stops at the step that
tells the customer to try another card.

### Labels

`Labels` takes a `PaymentFlowLabels` object with every visible word of the diagram: the node names and details, the
badges (`Approved`, `Declined`, `Paid`, `Payment failed`, `Tokenized`) and the step captions. The caption for the
payment's start is built from `Pay`, which takes the formatted amount and the merchant name, so a new label can use
both values.

```csharp
var payment = new PaymentFlow
{
    Amount = 19.00m,
    Labels = new PaymentFlowLabels
    {
        Merchant = "Shop",
        Issuer = "Issuing bank",
        Approved = "Approved",
        Declined = "Refused"
    }
};
```

Keep the labels short. The node cards hold about 16 characters on one line, and the badges sit above and below the
cards at the same width.

### Reacting to decisions

`PaymentCompleted` is raised when the issuer decides a payment. Its `PaymentCompletedEventArgs` has an `Approved`
flag and the `Amount` of the payment. `PaymentCount` gives the number of payments decided since the story started, and
`Balance` gives the merchant balance.

```csharp
payment.PaymentCompleted += (_, e) =>
    log.Text = $"{(e.Approved ? "Approved" : "Declined")} {e.Amount:0.00} · balance {payment.Balance:0.00}";
```

`Balance` rises when the settlement reaches the merchant, which is later in the story than the decision. Inside the
`PaymentCompleted` handler, `Balance` still shows the balance before this payment. Read it on a later step, or add the
amount yourself when `Approved` is true, if the log should show the new balance at the moment of the decision.

### Playback

`IsPlaying` plays and pauses the story, `Speed` scales it (0.25 to 4), and `Restart()` starts over from the first step
with the opening balance of 1,284 in the merchant account. `Step`, `StepCount` and `StepChanged` follow the steps of one
payment. The story has seven steps, and a declined payment stops at step 5, because the settlement step only runs for
approved payments.

| Step | Caption | What is shown |
|---|---|---|
| 0 | The customer pays $42.00 at Northwind (the formatted amount and merchant) | The amount badge above the customer |
| 1 | Card details are tokenized at the payment gateway | The request reaches checkout, then the gateway |
| 2 | The authorization request travels to the issuing bank | The gateway badge reads "Tokenized"; the request crosses the card network |
| 3 | The issuer checks funds and risk | The issuer's busy ring turns on |
| 4 | Approved: the response travels back, or Declined: insufficient funds | The issuer's badge and the response on the lower lane |
| 5 | The order is confirmed, or the customer is asked for another card | Checkout reads "Paid" or "Payment failed" |
| 6 | Funds settle to the merchant account (approved payments only) | The dashed settlement line and the balance readout |

```xml
<arc:PaymentFlow Speed="0.5" IsPlaying="False" />
```

A payment takes about 8.4 seconds of scene time, so the story at its default speed shows one payment in a little
under nine seconds. Stop the story with `IsPlaying` when the page explains a single payment and the animation would
compete with the text.

### Binding to a view model

In a checkout, the order owns the amount, the currency and the merchant, and the illustration only shows them. Bind
the values one way: the control never writes an amount or a currency back. `IsPlaying` is the only two-way property of
the illustration. The `PaymentCompleted` event is not a binding target, so subscribe to it in code-behind and forward
the decision to the view model.

```csharp
public partial class CheckoutViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _orderTotal = 129.90m;

    [ObservableProperty]
    private string _currency = "EUR";

    [ObservableProperty]
    private string _merchantName = "Atelier";

    [ObservableProperty]
    private string _cardLast4 = "0119";

    [ObservableProperty]
    private int _declinedCount;

    public void RecordDecision(bool approved)
    {
        if (!approved)
        {
            DeclinedCount++;
        }
    }
}
```

```xml
<arc:PaymentFlow Amount="{Binding OrderTotal}"
                 Currency="{Binding Currency}"
                 MerchantName="{Binding MerchantName}"
                 CardLast4="{Binding CardLast4}"
                 DeclineEvery="0" />
```

Subscribe to the event in code-behind, where the view model is available:

```csharp
public CheckoutView()
{
    InitializeComponent();
    Payment.PaymentCompleted += OnPaymentCompleted;
}

private void OnPaymentCompleted(object? sender, PaymentCompletedEventArgs e)
{
    ViewModel.RecordDecision(e.Approved);
}
```

Use the bindings for the values that the order already has, and keep the illustration's decline cycle for the
demonstration only. A checkout page that shows a real order should not show the animated decline cycle as if it were
the outcome of that order.

### Reduced motion

Under reduced motion (or Off) the illustration is a still frame of the approved path. Every connection is lit in the
success color, the issuer and checkout show success, the amount badge sits above the customer, and the balance readout
shows the opening balance plus the amount. The play button is hidden. Use the still frame on pages that explain the
approved path and do not need the sequence.

<Demo name="ReducedMotion" />

### Example: checkout help for a card payment

A help article on a checkout page for a design store. The order is 129.90 EUR at Atelier, the card ends in 0119, and
the illustration shows the approved path in the still frame, so the reader sees a normal payment. The text explains the
two decisions the issuer can make and the one place the reader can act, which is the card. The labels keep the
merchant's own wording where it helps, and the currency is the one the customer paid in.

![A card payment for 129.90 EUR at Atelier with the approved path and a Paid badge](images/CardCheckout.png)

```xml
<StackPanel Width="760" Spacing="16">
    <TextBlock Text="How your card payment is processed"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Your bank approves or declines every payment. A decline means the bank did not approve the amount, not that the store refused the card."
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:PaymentFlow Label="Card payment of 129.90 EUR at Atelier"
                     Amount="129.90"
                     Currency="EUR"
                     MerchantName="Atelier"
                     CardLast4="0119"
                     DeclineEvery="0">
        <arc:PaymentFlow.Labels>
            <arc:PaymentFlowLabels Issuer="Your bank"
                                   IssuerDetail="funds · risk"
                                   Merchant="Atelier"
                                   MerchantDetail="payout account"
                                   Approved="Approved"
                                   Declined="Declined"
                                   Paid="Paid" />
        </arc:PaymentFlow.Labels>
    </arc:PaymentFlow>
</StackPanel>
```

The screenshot is the still frame of the approved path, so it shows the badges and the balance readout at their final
values. The live control plays the full sequence unless the page sets reduced motion.

### Content guidelines

Name the parties the way the reader meets them on a statement or a receipt: the merchant, the card network and "your
bank" for the issuer. Use the amount with its currency and two decimals, and keep the decision words to what the bank
actually said. Write captions in the present tense and describe the state of the payment, not the company's process.

| Instead of | Write |
|---|---|
| Payment processed successfully | Approved |
| Your card has been rejected | Declined: insufficient funds |
| Transaction ID verified | Card details are tokenized at the payment gateway |
| Money sent to the merchant | Funds settle to the merchant account |
| 129 EUR | EUR 129.90 or 129.90 EUR, as the page's locale uses |
| Customer charged successfully | Paid |

Avoid the word "charged" for an authorization that has not settled, and do not say "refund" for a declined payment:
nothing was taken, so nothing is returned. Keep the labels short and the captions to one sentence.

### Best practices

**Do**

- Show the amount, currency and merchant that the order uses, so the diagram matches the page the customer is
  reading. A mismatched amount makes the help text read as a different payment.
- Explain on the page that the bank makes the decision. The diagram shows the issuer as the one that decides, so the
  reader does not blame the store.
- Keep the approved path on pages that describe normal payments, and use the decline cycle only in demonstrations, so
  a reader is not shown a decline for a normal order.
- Stop the story with `IsPlaying` when the page is about a single payment the reader has already made, because a loop
  in the same frame distracts from the receipt.

**Avoid**

- Presenting the sequence as the timing of a real settlement. The story takes about 8.4 seconds of scene time, and
  the settlement step shows only the order of events. Real settlement takes days and depends on the provider.
- Labelling the issuer as your company. The issuer is the customer's bank, and a diagram that names your company there
  states something you cannot know.
- Writing a count of declined payments from the illustration. `PaymentCount` and `Balance` count the story, so a page
  that shows them as sales figures reports numbers that have nothing to do with your ledger.
- Using the diagram as the only explanation of a decline. Give the customer the decline reason and the next step in
  text, because the diagram does not say which bank rule applied.

### Common questions

**Why does `Balance` not include the payment in the `PaymentCompleted` handler?** The decision happens before the
settlement reaches the merchant account, and the balance rises only when the settlement packet arrives. Read `Balance`
on a later step, or add the amount yourself after an approved decision.

**Can I make one order show a decline?** No. `DeclineEvery` counts payments in the story, and the control has no way
to receive a decision for a particular order. To explain a decline for a real order, use `Alert` or a message next to
the form that gives the bank's decline reason.

**How do I restart the story after a button press?** Call `Restart()`. It starts the first step again with the opening
balance of 1,284 and the decline counter reset, so the next payment is approved in the default cycle.

**Why does CHF show the code and not a symbol?** The illustration uses symbols for USD, EUR, GBP, JPY, CNY, INR and
KRW. Any other currency is shown with its ISO code and a space before the amount, which avoids guessing a symbol that
the customer might not recognize.

**Can I change the labels to another language?** Yes. Set `Labels` to a `PaymentFlowLabels` object with your own
strings. `Pay` and `Balance` are functions that take the formatted values, so keep the same number of parameters as the
defaults.

## API reference

<!-- api: PaymentFlow -->

<!-- api: PaymentFlowLabels -->

`PaymentCompleted` passes a `PaymentCompletedEventArgs` with these members.

| Member | Type | Meaning |
|---|---|---|
| `Approved` | `bool` | Whether the issuer approved the payment |
| `Amount` | `decimal` | The amount of the payment, as set in `Amount` when it was decided |

## Accessibility

- `Label` is the accessible name of the illustration. Name the flow in it, for example "Card payment for an order of
  129.90 EUR", so a screen reader user knows what the diagram is about before the caption is read.
- The illustration takes focus and shows a 2px focus ring around the frame when it is focused.
- The current caption is exposed as the help text, so the step's description is read when the control has focus.
- Caption changes are not announced, so a screen reader user does not hear the same payment repeat every few seconds.
- The decision is in text, not only in color: the issuer's badge reads "Approved" or "Declined", and the checkout
  reads "Paid" or "Payment failed". A reader who cannot see the colors still gets the outcome.
- Under reduced motion the still frame shows the approved path with its text badges and a summary caption.

### Keyboard

The illustration takes focus with Tab. Once it has focus, these keys control the story:

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or from the illustration |
| `Space` or `Enter` | Play or pause the story |
| `R` | Start the story over from the first step and the opening balance |

The illustration does not offer a way to choose the decision for a payment. To show a decline on demand, set
`DeclineEvery` or keep the default cycle, and explain the cycle in the surrounding text.

## Motion

- The amount rises above the customer, the card details travel to the gateway, and the request crosses the card
  network to the issuing bank.
- The issuer's decision shows as a green Approved or red Declined badge, and the response returns on the lower lane.
- An approved payment settles along the dashed line to the merchant account, and the balance readout increases.
- Reduced motion and Off show the approved path as a still frame with the Approved and Paid badges. The play button
  is hidden.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Amount and request travel | Animated packets on each route, with the amount badge above the customer | Still frame of the approved path | Still frame of the approved path |
| Gateway and issuer state | "Tokenized" badge pops in; the issuer's ring turns on while it checks | Shown in the final state | Shown in the final state |
| Decision | Badge pops in, response travels back on the lower lane | Approved badge and success route | Approved badge and success route |
| Settlement and balance | Dashed line runs to the merchant; the readout moves to the new balance | Readout shows the opening balance plus the amount | Readout shows the opening balance plus the amount |
| Play button | Visible; toggles playback | Hidden | Hidden |

## Related

- `DataFlow` for the pipeline that stores events after a payment is completed.
- `MultiRegionFailover` for keeping the payment service available when a region fails.
- `Stepper` for a fixed sequence of steps that the user moves through.

| Component | Choose it when |
|---|---|
| `MoneyInput` | The customer types an amount that must be parsed and formatted |
| `Alert` | You state that a payment failed, or that a service is down, in text |
| `Timeline` | You show the events of one real transaction in order, with their times |
| `DataGrid` | You list past payments with amounts, dates and decline codes |
| `BarChart` / `LineChart` | You plot revenue, volume or decline rates over time |
| `Stepper` | The user moves through the steps of a checkout |
| `MultiRegionFailover` | The story is about a region failing while the service stays available |

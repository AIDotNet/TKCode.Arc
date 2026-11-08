---
title: Seat manager
summary: A team seats card with an avatar row and a seat meter for members, invites and open seats, a stepper that rolls today's prorated charge and the new monthly total, a confirm step before any charge, invites you can resend or revoke with undo, and a full-team state that offers another seat.
---

<Demo name="Preview" />

## When to use

`SeatManager` is the seats section of a team billing page. It shows how many seats are paid for and who holds them, lets
an admin change the count with the price impact visible before anything is charged, and keeps pending invites (which
hold a seat too) in the same place.

- **Per-seat subscriptions.** The seat count is the subscription quantity. The stepper shows what adding seats costs
  today (prorated for the days left in the period) and what the recurring total becomes at renewal.
- **Seat limits at the moment they bite.** When every paid seat is taken, the notice line says so and offers
  "Add a seat", so an admin who just tried to invite someone can fix it in one tap.
- **Invites that reserve a seat.** Each pending invite holds a seat until it is accepted or revoked. Revoking frees the
  seat at once, with a short undo window, so lowering the count is possible right away.

## When not to use

- **Roles and permissions for many members.** Use a member table such as `Team members` and link to this card for seats.
- **Usage or credit billing.** Use `Usage billing`, `Usage pricing` or `Credit top-up` when the bill follows consumption.
- **Choosing between plans.** Use `Plan comparison` or `Pricing calculator`; this card works inside one plan.

## Usage

The block derives from Avalonia's `TemplatedControl`. Give it the plan, the seats paid for, the members, the invites and
the real billing period, so its proration matches your billing system. Then wire the three handlers to your backend.

```xml
<arc:SeatManager x:Name="Seats"
                 Workspace="Tidewater"
                 Purchased="12"
                 PeriodStart="2026-10-18"
                 PeriodEnd="2026-11-18"
                 ReceiptEmail="accounts@tidewater.example">
    <arc:SeatManager.Plan>
        <arc:SeatManagerPlan Name="Team" PricePerSeat="18" MaxSeats="25" />
    </arc:SeatManager.Plan>
    <arc:SeatManager.PaymentMethod>
        <arc:SeatManagerPaymentMethod Brand="Nova" Last4="1881" ProcessorName="Ledgerline" />
    </arc:SeatManager.PaymentMethod>
</arc:SeatManager>
```

```csharp
Seats.Members.Add(new SeatManagerMember { Id = "m1", Name = "Mira Okafor" });
Seats.Invites.Add(new SeatManagerInvite
{
    Id = "i1",
    Email = "theo.lindqvist@tidewater.example",
    InvitedBy = "Mira Okafor",
    Note = "3 days ago"
});

Seats.UpdateSeatsAsync = async (quote, cancellation) =>
{
    var response = await http.PostAsync("/api/billing/seats", JsonContent.Create(new { seats = quote.Target }), cancellation);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("The card was declined. Nothing was charged.");
    }
};
Seats.ResendInviteAsync = (invite, cancellation) => http.PostAsync($"/api/invites/{invite.Id}/resend", null, cancellation);
Seats.RevokeInviteAsync = (invite, cancellation) => http.DeleteAsync($"/api/invites/{invite.Id}", cancellation);
```

`SeatManagerSamples.Apply(block)` fills a block with the invented Tidewater team used on this page.

### Anatomy

- **Header**: the title, the plan's name as a badge, and a line with the workspace, the price of a seat and the renewal
  date.
- **Overview**: the first four members as avatars and "+N" for the rest; "12 of 12 seats in use"; the seat meter with one
  cell per seat; the legend (members, invited, open, and on the right the seats being added or removed); and the notice
  line.
- **Quote**: the seats paid for, the stepper (minus, the seat count, plus), the charge due today with its note, and the
  recurring total from the renewal date with the old total as its note.
- **Pending invites**: one row per invite with Resend and Revoke, or "No pending invites".
- **Footer**: the card the seats are billed to, or the change in words, and Update seats.

### The meter and the notice

The meter draws members in the accent, invited seats in a lighter accent tint, open seats as outlined cells, seats being
added as outlined accent cells at the end, and seats being removed as faint outlines. The notice line always says the
most useful thing: a limit you just pressed against, then seats being added, then seats being removed, then how many
seats are free, and finally that the team is full.

### Changing seats

The stepper is limited by the seats in use (members plus invites) and the plan's `MaxSeats`. Pressing past either limit
keeps the button usable and explains the limit in the notice line for 2.6 seconds. Removing seats is only possible down
to the seats in use, so removals always take open seats; they cost nothing today and apply at renewal.

```csharp
// Today's charge for added seats: delta × price × days left ÷ days in period, rounded to cents on the total.
var quote = SeatManagerQuote.Build(current: 12, target: 15, plan, periodStart, periodEnd, DateTime.Today);
// quote.DueToday == 38.32m, quote.NextTotal == 270m for the Team plan at $18.
```

`SeatManagerQuote.Build` is the same calculation the card uses, so a server can check the numbers. `QuoteChanged` fires
whenever the stepper changes the quote, if you want to show your billing system's own invoice preview next to it.

### Confirm, pay, done

Update seats opens a confirm step in the footer: the question, Back and Pay (Confirm when seats are only removed). The
stepper is locked meanwhile and Escape goes back. Pay runs `UpdateSeatsAsync` with a spinner on the button and a
shimmering line in the footer. When it succeeds, `Purchased` becomes the new count, `SeatsChanged` fires and the footer
says so for 2.8 seconds. When it throws, the exception's message stays in the footer with Try again.

<Demo name="Backend" />

### Resend and revoke

Resend shows a spinner on the button, then a check and "Sent". Revoke frees the seat immediately and turns the row into an
undo row for `UndoDuration` (3 seconds). Only when that window closes does `RevokeInviteAsync` run and the row collapse;
if the handler throws, the invite comes back where it was.

### States

<Demo name="States" />

### Narrow containers

<Demo name="Narrow" />

### Other words, currencies and motion

Every text is in `Labels` (a `SeatManagerLabels`); its `Culture` formats amounts and dates. The plan's `Currency` and
`Interval` drive the amounts and the "/mo" or "/yr" suffix. Without a `PaymentMethod` the footer shows no card.

<Demo name="Custom" />

### The demonstration loop

With no handlers set the card simulates its backend (900 ms charge, 660 ms resend) and plays a 12.7-second loop: three
seats added, paid, the first invite resent, the second revoked, and back to the start. The loop waits while the card is
off screen, stays silent for screen readers, and stops for good at the first pointer or key press inside the card. Set
`IsAutoPlayEnabled="False"` to turn it off, or `True` to keep it with handlers set.

### Best practices

- Pass the real billing period and "today" (`Now`) so the prorated amount matches the invoice.
- Throw from `UpdateSeatsAsync` with a message written for the person paying: it is shown as is.
- Keep `MemberCount` accurate when `Members` is only a sample for the avatars.
- Let the card stretch; it caps itself at 560 px and tightens below 460 px.

### Common questions

- **Why can't I go below the seats in use?** Removing a seat someone holds would lock them out. Revoke an invite first;
  the seat frees at once.
- **When is the invite really revoked?** When the undo window closes. Until then nothing is sent to your backend.
- **Does the card charge anything without handlers?** No. It simulates the charge and says so to screen readers.

## API reference

<!-- api: SeatManager -->

<!-- api: SeatManagerPlan -->

<!-- api: SeatManagerMember -->

<!-- api: SeatManagerInvite -->

<!-- api: SeatManagerPaymentMethod -->

<!-- api: SeatManagerQuote -->

<!-- api: SeatManagerPhase -->

<!-- api: SeatManagerLabels -->

## Accessibility

- The title is a level 2 heading. The avatar row is one image named "10 members".
- The seat meter is one image with a full description ("12 of 15 seats in use: 10 members, 2 invited, 3 open"); the
  legend repeats the counts as text, so color is never the only signal.
- The seat count is a range value with its minimum, maximum and value, and is read as "15 seats".
- Minus and plus stay focusable at a limit and explain it in the notice line instead of going silent.
- A polite live region announces the review, the update, resend, revoke, removal and undo. Without handlers it adds
  "Simulated, nothing was charged" (or sent). The demonstration loop announces nothing.
- Rolling numbers expose their real value; the digit strips are hidden from screen readers.
- Every invite action has its own name, such as "Revoke invite to theo.lindqvist@tidewater.example".
- **Tab** moves through the "Add a seat" action (when the team is full), minus, the seat count, plus, each invite's
  actions and the footer buttons. Update seats is skipped while there is nothing to update.
- On the seat count: **Arrow up** or **Arrow right** adds a seat, **Arrow down** or **Arrow left** removes one,
  **Page up** / **Page down** move by five, **Home** jumps to the seats in use and **End** to the plan's maximum.
- **Space** or **Enter** presses the focused button; minus and plus repeat while held.
- **Escape** leaves the confirm step.
- Focus moves to Pay when the confirm step opens, back to Update seats after Back, and to the seat count after a
  successful update. "Add a seat" hands focus to plus; a revoked invite's Undo takes the focus from Revoke and gives it
  back.

## Motion

- **Numbers** roll: each digit is a 0–9 strip that slides 240 ms on an ease-out by its own change, all at once. New
  places (and a "$" that shifts) fade in over 160 ms; the width never animates.
- **Seat cells** grow from zero and shrink to zero on a bounce-free 0.24 s spring while their neighbours make room; a
  cell that only changes kind recolours in 160 ms.
- **Text swaps** (notice, notes, footer, invite rows, Resend → Sent) cross-fade: old out in 100 ms, new in 160 ms with a
  2 px rise.
- **A revoked invite** fades in about 120 ms while its row closes on the same spring.
- Reduced motion and Off apply every change at once; the states, numbers and the loop are unchanged.

## Responsive behavior

- `SeatManager` reads its own width. It is at most 560 px wide; below 460 px (`:narrow`) the paddings go from 24 to
  20 px, and the notes beside the amounts and the legend's added or removed entry hide.
- Addresses, notes and the footer line end in an ellipsis; the stepper keeps its 120 px and amounts never wrap.

## Related

- `Usage billing` and `Billing overview`, the Settings blocks for the rest of a billing page.
- `Credit top-up` and `Checkout summary`, the other Pricing blocks with a confirm step.
- `Avatar group`, `Button` and `Text shimmer`, the pieces the card is built from.

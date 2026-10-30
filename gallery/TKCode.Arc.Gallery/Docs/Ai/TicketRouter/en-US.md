---
title: Ticket router
summary: Support messages arrive one at a time, an AI classifier labels intent, sentiment and confidence, and each ticket travels into a team lane with an assignee or waits in a review lane for one click.
---

<Demo name="Preview" />

## When to use

`TicketRouter` shows an AI classifier triaging inbound support messages. Messages from email, chat, a messaging
workspace and the help desk arrive one at a time. The classifier labels each one with an intent, a sentiment and a
confidence, a pulse runs along the connector to the target team, and the ticket travels into that team's lane, where
the next person in turn takes it. A ticket the classifier is unsure about waits in the review lane with its suggestion;
one click there sends it on.

- **Helpdesk and ops dashboards**: a live triage widget, with a human review lane for low confidence.
- **Showing how AI routing works**: what the classifier read, how sure it was and where the ticket went.
- **Landing pages and onboarding** for support products, with a `Simulation` playing an inbox you supply on a loop.

## When not to use

- **A full ticket inbox** with search and bulk actions: use a table or a list.
- **A single tool step in a chat transcript**: use `ToolCall`.
- **Generic multi-step agent progress**: use `AgentStatus` or `AgentPlan`.

## Usage

```xml
<arc:TicketRouter />
```

`TicketRouter` is a `TemplatedControl` in the Special package (register it with `UseArcSpecial()`). It fills its
container up to 840px; keep it in a content column of about 360 to 880px. With no tickets it rests on "No tickets yet"
in the default lanes (Billing, Engineering, Success and Needs review). Give it `Tickets` to route your own messages, or
a `Simulation` to play an inbox on its own.

### Simulation

`TicketRouterSimulation` is an inbox the router plays while `Tickets` is null: the classifier's `Model` name, the
`Lanes`, the `Tickets` in arrival order, each with the answer the simulated classifier gives in its `Sample`, and the
`Routed` tickets already in the lanes. It loops, waits while the card is off screen and pauses with `IsPaused`; its
announcements say the run is simulated and nothing was sent. Your own `Tickets`, `Lanes`, `InitialRouted` and `Model`
win over it, and setting `Simulation` to null starts over on the empty state.

```csharp
router.Simulation = new TicketRouterSimulation
{
    Model = "Your classifier",
    Tickets =
    [
        new TicketMessage("t-1", TicketChannel.Email, "Hannah Brooks", "Lumen Bakery", "Charged twice for the March invoice",
            "Our card shows two charges of $49 on the 3rd.")
        {
            Sample = new TicketClassification(TicketIntent.Billing, TicketSentiment.Negative, 0.94),
        },
    ],
};
```

The demos on this page take their inbox from `TicketRouterDemoData`: the people, companies and messages are invented,
and the classifier is named after a real model (checked on 2026-10-29).

### Anatomy

1. **Header**: the model mark on a tinted tile, the title, the model name, a status (Routing, Paused, All caught up)
   and the Pause / Resume button.
2. **Incoming ticket**: the channel glyph and name, the customer and company, when it arrived, the subject and the first
   words. Every line is one line high and ends with an ellipsis, so long subjects never change the card's height.
3. **Classifier strip**: "Classifying with Mistral Small 4" shimmers while the classifier reads, then the intent, the
   sentiment and the confidence (a short meter and the percentage) fade in, with the route target at the end: the team,
   or "Needs review · Suggested Billing".
4. **Connector**: a stem from the stage, a hairline across the lanes and a tick into each.
5. **Lanes**: each lane's name and count over its newest rows. A row shows the assignee's avatar (initials), the subject
   and the intent with the customer. A review row shows the confidence and the lane it goes to, with an arrow.

### Your classifier

Set `Classifier` to your model call. It gets the message and a cancellation token and returns a
`TicketClassification` (intent, sentiment, confidence from 0 to 1). The token is cancelled when the flow pauses, when
the card scrolls off screen and when it is unloaded; the call runs again when the flow resumes. Throw to show the error
state: "Couldn't classify" with Retry and Send to review.

<Demo name="YourClassifier" />

```csharp
router.Classifier = async (ticket, token) =>
{
    var reply = await http.PostAsJsonAsync("/api/classify", ticket, token);
    return (await reply.Content.ReadFromJsonAsync<TicketClassification>(token))!;
};
router.TicketRouted += (_, e) => helpdesk.Assign(e.Ticket.Message.Id, e.Ticket.LaneId, e.Ticket.Assignee);
router.ReviewConfirmed += (_, e) => helpdesk.Assign(e.Ticket.Message.Id, e.LaneId, e.Ticket.Assignee);
```

Without a classifier the card reads each message's `Sample`, and its announcements say the run is simulated and
nothing was sent. A message without a sample is read as an unsure question and goes to review.

### Lanes and routing

`Lanes` lists the lanes left to right. Each `TicketLane` has an id, a label, the assignees who take tickets in turn
(round robin), a `Count` of tickets routed earlier, and `IsReview` on the lane that collects low confidence tickets.
`Route` picks the lane id for a classification; the default, `TicketRouter.DefaultRoute`, sends billing and refunds to
`billing`, bugs to `engineering` and feature requests and questions to `success`. An id that matches no team lane falls
back to the first one. With no `Lanes` (and no simulation playing) the card uses the default lanes with those ids
(`TicketRouter.BillingLaneId`, `EngineeringLaneId`, `SuccessLaneId` and `ReviewLaneId`), named by the labels and
without assignees.

A ticket below `ReviewThreshold` (0.7 by default) lands in the review lane with the picked lane as its suggestion and no
assignee. Clicking it, or pressing Enter or Space on it, sends it to that lane with the next assignee there and raises
`ReviewConfirmed`. `ConfirmReview(ticket)` does the same from code.

### Your queue

`Tickets` takes your messages in order. Your own queue does not loop unless you set `Loop`, so it ends in
"All caught up"; append to an `ObservableCollection` and the flow picks up the new tickets. An empty queue shows
"No tickets yet". `InitialRouted` fills the lanes on first show (newest first), `Restart()` starts over, and
`RoutedTickets` keeps the newest 80 tickets.

### Pausing and speed

`IsPaused` (two-way) pauses the flow where it is and cancels a running classifier call; the Pause / Resume button sets
it and every change raises `PausedChanged`. Set `IsPaused="True"` in markup to start paused. `Speed` multiplies the
pace (1 is about two seconds per ticket). One frame loop runs the flow, and it waits while the card is off screen.

### Narrow containers

<Demo name="Narrow" />

Below 600px of content the four lanes fold into a 2 by 2 grid with at most two rows each (`MaxVisible` is 3 above), the
connector hides and the labels stack above the route target. Below 400px the confidence meter and the model name hide;
the percentage stays.

### Your own words

`Labels` holds every word the card shows or says, including the intents, sentiments and channels and every
announcement:

```csharp
router.Labels = new TicketRouterLabels
{
    Routing = "Tri en cours",
    Intent = intent => intent == TicketIntent.Billing ? "Facturation" : intent.ToString(),
    AnnounceRouted = (subject, lane, assignee) => $"{subject} : {lane}",
};
```

`Title`, `Model` and `ModelMark` (a 24 × 24 stroked glyph, the sparkle by default) set the header. Channels use neutral
glyphs (mail, chat bubble, message square, life buoy), never brand marks.

### Content guidelines

- Subjects are what the customer wrote; keep them as they are and let the card cut them.
- Keep lane names to one or two words ("Billing", "Needs review").
- Keep the threshold where a wrong route costs more than a short wait.

### Best practices

- Wire `Classifier` to your model and `TicketRouted` to your help desk; the card never sends anything itself.
- Use the review lane: low confidence is the point where a person should decide.
- Pass `CancellationToken` through to your HTTP call so a pause really stops it.

### Common questions

**Can the same message land twice?** When a simulation loops, yes; each landing is its own `RoutedTicket`.

**Does a ticket in the review lane count toward a team?** No. It counts in the review lane until someone confirms it.

## API reference

<!-- api: TicketRouter -->

<!-- api: TicketMessage -->

<!-- api: TicketClassification -->

<!-- api: TicketLane -->

<!-- api: RoutedTicket -->

<!-- api: TicketRouterLabels -->

<!-- api: TicketRouterSimulation -->

<!-- api: TicketRoutedEventArgs -->

<!-- api: TicketReviewConfirmedEventArgs -->

<!-- api: TicketClassificationFailedEventArgs -->

<!-- api: TicketRouterPausedChangedEventArgs -->

## Accessibility

- The card is named by its title. Each lane is named with its label and ticket count ("Billing, 14 tickets").
- A polite live region announces each routing ("routed to Billing, assigned to Andre Williams"), review tickets,
  failures, pauses and confirmations, and adds that the run is simulated when no classifier is set.
- Intent, sentiment and review status always pair a glyph with a word; confidence reads as a percentage, never as
  color alone.
- Review tickets are real buttons named with the subject, the confidence and the target lane. Team rows are not tab
  stops.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves between Pause, Retry and Send to review when shown, and the review tickets |
| Enter, Space | Pauses or resumes, retries a failed classification, sends it to review, or confirms a review ticket's lane |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A ticket arrives | The old card fades out over 100ms, the new one fades in over 160ms and rises 4px | Fades, no rise | At once |
| Classifying | The label shimmers | Still | Still |
| Labels | Fade in over 160ms with a 50ms stagger and rise 2px | Fade, no rise | At once |
| Connector | A pulse draws to the target lane in 240ms, then fades | None | None |
| Landing | The row travels from the stage into its lane on a position spring (0.32s, no bounce) | Appears in place | Appears in place |
| Rows enter and leave | Their height springs (0.24s, no bounce) | At once | At once |
| Landing highlight | The row lights with the accent tint, which fades over 900ms | Kept | None |
| Lane counts | Roll in reserved tabular width | At once | At once |

About two seconds per ticket at speed 1: arrive 220ms, classify, labels, the 240ms pulse, then the landing. The flow
pauses off screen. The card's height never changes while tickets flow.

## Related

- `AgentStatus` for the live status of one running agent.
- `ToolCall` for a single tool step in a transcript.
- `AgentPlan` for an agent's plan as a checklist.

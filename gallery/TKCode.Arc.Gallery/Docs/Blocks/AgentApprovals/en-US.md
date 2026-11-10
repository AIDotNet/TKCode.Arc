---
title: Agent approvals
summary: A queue of actions agents want to take that need a person's yes: each request with the agent's reason, the facts, its risk and an optional note, approved or denied, with an extra check for risky ones.
---

<Demo name="Preview" />

## When to use

`AgentApprovals` is where a person says yes or no to things AI agents want to do but should not do alone: send an
email outside the company, make a payment, deploy to production, delete records. The waiting requests are listed on
one side with their risk. The selected one shows in full on the other: what the agent wants to do, why, the facts of the
action (recipient, amount, environment), how risky it is and why, a field for a note back to the agent, and Deny and
Approve. A high-risk request asks the person to tick that they checked the details before Approve works. A decided
request leaves the queue and the next one opens.

- **Human in the loop for agents** that act on money, customers or production systems.
- **An approvals inbox** shared by a team.

## When not to use

- **Watching what an agent does.** Use `Agent run`.
- **Approvals between people** (expenses, leave). A form or a table fits better.
- **Confirming one action in place.** Use `Dialog` or `Confirm morph`.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no properties it shows sample requests and decides them
locally. In an app, give it your requests and send each decision to your backend:

```xml
<arc:AgentApprovals Requests="{Binding PendingApprovals}" />
```

```csharp
queue.DecisionHandler = (request, decision, note, cancellation) =>
    api.AnswerAsync(request.Id, decision == AgentApprovalDecision.Approved, note, cancellation);
queue.Decided += (_, e) => log.Add($"{e.Request.Title}: {e.Decision}");
```

### Requests and decisions

An `AgentApprovalRequest` has the agent, a kind (email, payment, deploy, data change, access, other) that picks its
glyph, a title, the agent's reason in `Context`, `Details` as label and value pairs, a `Risk` with a `RiskNote`, and when
it was asked. While `DecisionHandler` runs, Approve shows a spinner; if it throws, the request stays and the message
shows under the buttons. A decided request leaves the queue on screen but your collection is not changed: remove it there
when your backend confirms, or keep it for history. `Reset` puts every decided request back.

<Demo name="Backend" />

## API reference

<!-- api: AgentApprovals -->

<!-- api: AgentApprovalRequest -->

<!-- api: AgentApprovalDetail -->

<!-- api: AgentApprovalsLabels -->

## Accessibility

- The queue is a region named "Waiting for approval"; the list and the selected request have headings.
- **Tab** reaches the list rows, the note, the check (high risk) and the buttons. **Up** and **Down** move the selection
  through the list, **Home** and **End** jump to the ends; **Enter** or **Space** selects a row.
- Rows read as list items with their title, agent and risk, and report whether they are selected.
- After a decision a polite live region says what was decided, and focus moves to the next request.
- Risk is shown by a word, a glyph and a tint, never by color alone.

## Motion

- The risk panel's tint changes over 240ms when another request is selected; rows tint on hover and selection.
- Reduced motion and Off apply color changes at once.

## Responsive behavior

- The queue is at most 920 wide and follows its container below that.
- At 680 or less the list sits above the request.
- Titles in the list take two lines at most; details wrap.

## Related

- `Agent tasks` for the work the agents are doing.
- `Agent run` for one run in detail.
- `Dialog` for confirming one action in place.

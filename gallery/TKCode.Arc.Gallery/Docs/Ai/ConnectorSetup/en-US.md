---
title: Connector setup
summary: A "Connect your tools" card for an AI assistant: each app connects inline through redirect, authorize and a first sync with a progress hairline, then lists the scopes it was granted and offers Disconnect with an inline confirmation.
---

<Demo name="Preview" />

## When to use

`ConnectorSetup` is the card where people let an assistant into the apps their team uses. Each app is one row with its
mark, its name and one status line. Pressing Connect runs the whole handshake in that row: the sign-in opens, the person
approves, and the first sync counts what it read while a 2px hairline fills along the row. A connected app shows its
account and a Connected button that opens the scopes the assistant holds, with Disconnect and an inline confirmation.

- **An assistant's settings page** where people connect and review workspace apps.
- **A first-run checklist** that should show the OAuth handshake and the first sync without leaving the page.
- **Reviewing access**: which scopes the assistant holds in each app, and revoking them.

## When not to use

- **Browsing dozens of integrations by category**: use the `Integrations` block, a marketplace grid with filters.
- **Approving one action the agent wants to take**: use `ToolCall` with its approval, or a `Dialog`.
- **Showing a tool the agent is calling right now**: use `ToolCall`.

## Usage

Give the card your apps in `Connectors` and the ids that are already connected in `ConnectedIds`. Set `ConnectHandler`
to run your OAuth flow: report the steps through the context, return the account, scopes and sync count, and throw an
exception with a short human message ("Needs admin approval") to show it with Retry. The context's token is cancelled
when the person presses Cancel or the card leaves the window.

```csharp
setup.Connectors = apps;                  // ConnectorApp: Id, Name, Description, Kind or Glyph, Scopes, SyncCount, SyncNoun
setup.ConnectedIds = savedIds;
setup.ConnectHandler = async (app, context) =>
{
    var grant = await OpenOAuthPopupAsync(app.Id, context.CancellationToken);
    context.ReportAuthorizing();
    var approved = await grant.ResultAsync;
    var total = await StartFirstSyncAsync(app.Id, approved, synced => context.ReportSyncing(synced), context.CancellationToken);
    return new ConnectorConnectResult { Account = approved.Workspace, Scopes = approved.Scopes, Synced = total };
};
setup.DisconnectHandler = (app, token) => RevokeGrantAsync(app.Id, token);
setup.ConnectedIdsChanged += (_, e) => SaveConnectedApps(e.Ids);
```

Without handlers the flow is simulated, nothing is sent, and the live region says so. With no `Connectors` the card
lists its `Simulation`'s apps; with neither it lists no apps. `AssistantName` names the assistant in the texts; without
one they say "your assistant".

### Simulation

`ConnectorSetupSimulation` is a set of apps the card plays on its own: the `AssistantName`, the `Apps`, the ids already
`Connected`, and the scripted run's `Steps`. Once the card is on screen each `ConnectorSetupSimulationStep` waits its
`Pause`, then connects its app if it is idle or failed; a step with a `Failure` fails after approval, so the next one can
retry it. The run stops for good at the first pointer or key press inside the card. Your own `Connectors`,
`ConnectedIds` and `AssistantName` win over it, and setting `Simulation` to null stops the run and empties the list.

```csharp
setup.Simulation = new ConnectorSetupSimulation
{
    AssistantName = "Atlas",
    Apps = apps,                          // the same ConnectorApp records as Connectors
    Connected = ["code"],
    Steps =
    [
        new ConnectorSetupSimulationStep("chat", TimeSpan.FromMilliseconds(900)),
        new ConnectorSetupSimulationStep("crm", TimeSpan.FromMilliseconds(1200), "Needs admin approval"),
        new ConnectorSetupSimulationStep("crm", TimeSpan.FromMilliseconds(1800)),
    ],
};
```

The demos on this page list eight generic kinds of app from `ConnectorSetupDemoData`, with an invented company's
accounts and counts and an invented assistant.

### Your own flow

<Demo name="YourFlow" />

The handler reports `ReportAuthorizing()` once the sign-in waits for approval and `ReportSyncing(synced, total)` as the
first sync advances; the report calls may come from any thread. `ConnectedIds` is two-way and is written after each
connect or disconnect finishes, together with `ConnectedIdsChanged`. `AppConnected` carries the account, scopes and sync
count; `AppDisconnected` follows a revoke.

### Onboarding

<Demo name="Onboarding" />

`IsSearchable="False"` drops the search field for a short list. `Title`, `Description` and `Footer` replace the heading,
the line under it and the privacy note (`IsFooterVisible="False"` hides the note). `AssistantName` is used in the
description, the scopes heading, the confirmation and the footer.

### Marks

<Demo name="Marks" />

Apps are shown with neutral Lucide glyphs for their kind (`ConnectorMarkKind`: Chat, Code, Docs, Drive, Issues, Calendar,
Mail, Crm, Database, Analytics), never brand logos. Pass the app's own 24 × 24 stroked mark in `ConnectorApp.Glyph`; with
neither, the first letter of the name is shown. `ConnectorMark` draws the same tile anywhere else in your settings UI.

### Anatomy

1. **Header**: the heading, one line under it and the search field (it matches name, description, account and keywords).
2. **Row**: 60px tall. The mark, the name over one status line, and one 32px action: Connect, Cancel, Retry or Connected.
3. **Progress hairline**: 2px along the row's bottom edge while the app connects.
4. **Access details**: under a connected row, aligned with its text: "Atlas can" and the scopes, then Disconnect.
5. **Footer**: a lock and a short privacy note.

### States

| Phase | Status line | Action |
|---|---|---|
| `Idle` | The app's description | Connect |
| `Redirecting` | "Opening chat.example.com…" | Cancel |
| `Authorizing` | "Waiting for you to approve in Chat…" | Cancel |
| `Syncing` | "Syncing 1,240 messages", the count rolling | Cancel |
| `Connected` | "tailspin.chat · 1,240 messages synced" | Connected, opening the details |
| `Failed` | An alert and the reason | Retry |
| `Disconnecting` | "Disconnecting…" | Connected (disabled) |

`GetPhase(id)` reads where an app is; `StartConnect`, `CancelConnect`, `SetAccessOpen`, `Disconnect` and `StopAutoPlay`
drive the card from code.

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
setup.Labels = new ConnectorSetupLabels
{
    Title = "Verbinden Sie Ihre Tools",
    Connect = "Verbinden",
    Syncing = "Synchronisiere",
};
```

### Content guidelines

- Write scopes as plain-language capabilities ("Post replies as Atlas"), not raw OAuth scope strings.
- Keep a failure's message short and human ("Needs admin approval"); it shares one line with an alert icon.
- Give `SyncNoun` in the plural ("messages", "files").

### Best practices

- Keep it one card in a settings column; it is fluid up to 520px and holds at 360px.
- Pass the token to every call of your flow so Cancel really stops it.
- For hundreds of apps, filter on your side and pass the result as `Connectors`.

### Common questions

**What happens if the person leaves the page while an app connects?** The token is cancelled and the row returns to
rest.

**Does the card keep the connected list?** It writes `ConnectedIds` after each change; save it from
`ConnectedIdsChanged`.

## API reference

<!-- api: ConnectorSetup -->

<!-- api: ConnectorApp -->

<!-- api: ConnectorConnectResult -->

<!-- api: ConnectorConnectContext -->

<!-- api: ConnectorMark -->

<!-- api: ConnectorSetupLabels -->

<!-- api: ConnectorSetupSimulation -->

<!-- api: ConnectorSetupSimulationStep -->

## Accessibility

- The card is a labelled group with a real heading; the list is a list with one item per app, numbered among the apps
  the search shows.
- A polite live region announces each step: connecting, connected with the sync count, failed with the reason, cancelled
  and disconnected. With a simulated flow it adds "Simulated, nothing was sent."
- Status is never color alone: a failure pairs an alert icon with its message, and a connected app shows a check with the
  word Connected.
- The Connected button reports expanded or collapsed. Disconnect asks first; a press moves focus to the safe answer,
  Cancel, and the destructive button is only focused by the person.
- Content that is fading out leaves the accessibility tree and stops taking clicks at once.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the search field, each row's Connect, Cancel, Retry or Connected button, and the open details |
| Enter, Space | Connects, cancels, retries, or opens and closes a connected app's access details |
| Escape | Clears the search field, or backs out of the disconnect confirmation |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Status line or action changes | Old fades out over 100ms, new fades in over 160ms rising 2px, in one cell | At once | At once |
| Progress between steps | The hairline eases over 240ms | At once | At once |
| Progress while syncing | The hairline tracks the count linearly; the count rolls in tabular figures in a width reserved for its final value | Jumps with the count | Jumps with the count |
| Connect finishes | The hairline fills, then fades | At once | At once |
| Details open or close | Height springs (0.24s, no bounce), content fades | At once | At once |
| Chevron | Turns on a spring | At once | At once |
| Scripted run (simulation) | Starts once the card is on screen, stops at the first press | Same | Same |

Reduced motion keeps every state and announcement. The row is 60px tall in every state, so nothing below it moves while
an app connects.

## Related

- `ToolCall` for a tool the agent is calling right now, with approval in place.
- `AgentStatus` for the live status of a whole agent run.
- `ConfirmMorph` for a single destructive button that asks in place.

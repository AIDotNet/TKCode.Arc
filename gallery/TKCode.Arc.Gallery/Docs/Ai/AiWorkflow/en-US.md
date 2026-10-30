---
title: AI workflow
summary: An automation card where a trigger, an AI step, a branch and actions sit on one rail, a test run glides down it opening each step's output, and steps are added from a compact command menu.
---

<Demo name="Preview" />

## When to use

`AiWorkflow` shows a short automation that mixes an app trigger, a model step and app actions, and lets people test it in
place. Steps are compact 32px rows on one hairline rail. Test run moves one thing at a time: a small accent dot glides
down to the next step, the step shimmers while it runs, then it shows its duration and a preview of what it produced.
A branch picks one of its paths and marks the other one Skipped. Steps are added from a small command menu and removed
with a button on their row.

- **Automation and integrations pages** where people put together a trigger, an AI step and a few actions.
- **Onboarding and product tours** that show where an AI step fits between the apps a team already uses.
- **Reviewing a workflow**: what it does, and what a sample run produced at every step.

## When not to use

- **Large graphs** with many branches, loops or merges: use `WorkflowCanvas` from the Workflow package.
- **One tool call, or the live steps of an agent**: use `ToolCall` or `AgentStatus`.
- **The plan an agent wrote**: use `AgentPlan`.
- **Real products with autoplay on**: set `AutoPlay="False"`; the repeating run is for showcases.

## Usage

Give the card your flow in `Steps`. With no steps of its own it plays its `Simulation`, a flow you supply. With neither
it shows the empty state: the frame with Test run unavailable and "No steps yet", or the catalog's triggers to start
from when `Catalog` has some. The header says "Simulated" whenever no runner is wired.

```xml
<arc:AiWorkflow Title="Escalate urgent support email"
                Description="Runs on every new email to support@example.com"
                AutoPlay="False" />
```

### Steps

`Steps` is the flow, in order. Each `AiWorkflowStep` has an `Id`, a `Kind` (`Trigger`, `Ai`, `Action` or `Branch`), an
`App` (one of the built-in `AiWorkflowApp`s or your own name and mark), a `Title`, and optionally a `Detail`, the
`Activity` it shows while running, and the `Output`, `Duration` and `Error` a simulated run uses. A branch carries
`Paths` (each an `AiWorkflowBranchPath` with an id, a tab label and its own steps) and must be the last top-level step.

```csharp
flow.Steps =
[
    new AiWorkflowStep { Id = "mail", Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Mail, Title = "New email", Detail = "support@example.com" },
    new AiWorkflowStep { Id = "label", Kind = AiWorkflowStepKind.Ai, App = AiWorkflowApp.Ai, Title = "Classify with Claude Sonnet 5.5", Activity = "Reading the email" },
    new AiWorkflowStep { Id = "issue", Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Tracker, Title = "Create issue", Detail = "Support team" },
];
```

The card treats steps as values: put a new list in `Steps` to change them. An empty list shows the empty state, which
lists the catalog's triggers to start from.

The built-in apps are generic and use Lucide glyphs: `Mail`, `Ai` (a model step, drawn in the accent), `Chat`,
`Tracker`, `Docs`, `Sheets`, `Crm`, `Helpdesk`, `Repo`, `Payments`, `Schedule`, `Webhook` and `Branch`. For a real
product pass `new AiWorkflowApp("id", "Name", geometry)` with your own 24 × 24 stroked glyph, and name a model step's
app after the model's vendor (`isAccent: true` draws its mark in the accent).

### Simulation

`AiWorkflowSimulation` holds a flow to play while `Steps` is null: a `Title`, a `Description`, the `Steps` and the add
step `Catalog`. The card shows it and, with `AutoPlay`, test-runs it while it is on screen, repeating after a short pause
until someone presses or types inside it. Each step is simulated from its `Output`, `Duration` and `Error`, and nothing
is sent. Your own `Steps`, `Title`, `Description` and `Catalog` win over it. Adding or removing a step puts the edited
flow in `Steps`; setting `Simulation` to null stops the run and shows the empty state.

```csharp
flow.Simulation = new AiWorkflowSimulation
{
    Title = "Escalate urgent support email",
    Description = "Runs on every new email to support@example.com",
    Steps = SupportFlow.Steps(),     // List<AiWorkflowStep> with Output, Duration and Error
    Catalog = SupportFlow.Catalog(), // List<AiWorkflowCatalogItem>
};
```

The demos on this page build their flow in `AiWorkflowDemoData`; the model steps name real models from the vendors' pages
(checked on 2026-10-29), and every person, company and number is invented.

### Running steps for real

<Demo name="OwnRunner" />

Set `RunStep` to run each step against your test endpoint. It gets the step and an `AiWorkflowRunStepContext` with a
`Cancellation` token (cancelled on Stop, a new run, or when the card leaves the window), the `Previous` outputs by step id
and the `Attempt` number. Return one of the outputs, or throw to fail the step with the exception's message. A branch
returns an `AiWorkflowRouteOutput` to pick its path.

```csharp
flow.RunStep = async (step, context) =>
{
    using var response = await http.PostAsync($"/api/workflows/test/{step.Id}", null, context.Cancellation);
    response.EnsureSuccessStatusCode();
    return new AiWorkflowTextOutput { Text = await response.Content.ReadAsStringAsync(context.Cancellation) };
};
```

Without `RunStep` every step is simulated from its `Output` and `Duration`; a step with an `Error` fails on its first
attempt, so Retry can be shown. `RunCompleted` reports a passed run, or one that stopped on a failed step, with the
duration, every output and the failed step's id. `StartRun`, `StopRun` and `Retry` drive the run from code.

### Outputs

Each output renders inline in small muted text under its step, aligned with the title:

| Output | Shows |
|---|---|
| `AiWorkflowEmailOutput` | The sender and the subject on one line, then two lines of the body |
| `AiWorkflowLabelOutput` | A hairline label with a dot, the confidence in monospace, and a reason that streams in |
| `AiWorkflowRouteOutput` | Which path the branch took, with an optional note |
| `AiWorkflowMessageOutput` | The channel and author, then the message |
| `AiWorkflowRecordOutput` | A record key in monospace with its title and one more fact |
| `AiWorkflowTextOutput` | Free text, such as a drafted reply, streamed in |

### Adding and removing steps

Add step opens a compact command menu: a search field that filters by title, app or detail, and rows with the app's mark
and a 1 to 9 shortcut. A new step lands at the end of the flow, or at the end of the visible path when the flow ends in a
branch. Each step except the trigger has a remove button. Both put a new list in `Steps` (a two-way property) and raise
`StepsChanged` with the list, the kind of change and the step.

`Catalog` holds the menu's entries (`AiWorkflowCatalogItem`, a step without an id). Triggers appear only in the empty
state. A branch is offered only while the flow has none. An empty catalog hides the menu.

### Read only

<Demo name="ReadOnly" />

`IsEditable="False"` hides Add step and the remove buttons. Test run still works.

### Loading

`IsLoading` shows three skeleton steps while your flow loads. Test run and the menu are unavailable until it is false.

### Narrow

<Demo name="Narrow" />

The card fills its container up to 640px. Below 480px each row grows to 44px with the detail under the title, the
status slot narrows from 72px to 64px, the header wraps to two lines and the empty state's triggers drop their detail.

### Your own words

`Labels` holds every word the card shows or says, including the screen reader names and announcements. Replace the object
to localize or reword:

```csharp
flow.Labels = new AiWorkflowLabels
{
    TestRun = "Probar",
    AddStep = "Añadir paso",
    Remove = title => "Quitar " + title,
    Passed = duration => "Correcto en " + duration.TotalSeconds.ToString("0.0") + " s",
};
```

### Content guidelines

- Step titles start with a verb ("Create issue", "Post message"); the detail says where ("#support", "Support team").
- AI steps name the model in the title ("Classify with Claude Sonnet 5.5").
- Keep a flow short: one trigger, one or two AI steps, one branch at the end, a few actions per path.

### Best practices

- Wire `RunStep` to a test endpoint, return the outputs so the previews render, and throw to show a failure with Retry.
- Keep the step ids stable when you replace the list, so rows and run results stay in place.
- Turn `AutoPlay` off outside showcases and marketing pages.

### Common questions

**Why did the autoplay stop?** It stops for good after the first press or key inside the card, so it never runs under
someone's hands. It also waits while the card is off screen.

**Can a flow have two branches?** No. One branch per flow, as its last top-level step. For larger graphs use
`WorkflowCanvas`.

## API reference

<!-- api: AiWorkflow -->

<!-- api: AiWorkflowStep -->

<!-- api: AiWorkflowBranchPath -->

<!-- api: AiWorkflowCatalogItem -->

<!-- api: AiWorkflowSimulation -->

<!-- api: AiWorkflowApp -->

<!-- api: AiWorkflowOutput -->

<!-- api: AiWorkflowEmailOutput -->

<!-- api: AiWorkflowLabelOutput -->

<!-- api: AiWorkflowRouteOutput -->

<!-- api: AiWorkflowMessageOutput -->

<!-- api: AiWorkflowRecordOutput -->

<!-- api: AiWorkflowTextOutput -->

<!-- api: AiWorkflowRunStepContext -->

<!-- api: AiWorkflowStepsChangedEventArgs -->

<!-- api: AiWorkflowRunCompletedEventArgs -->

<!-- api: AiWorkflowLabels -->

## Accessibility

- The card is a group named by its title (or "Loading workflow"); the steps and each branch path are named lists.
- Every step's name carries its title, app and status ("Create issue, Tracker, Done in 700ms"). Status shows as an icon
  and a short label, never color alone.
- A polite live region announces when a run starts, each step's result, a failure with its message, and the outcome,
  adding "Simulated, nothing was sent." when no `RunStep` is wired. It also announces added and removed steps.
- The add step menu's search field keeps focus; its help text names the highlighted entry.
- Branch paths are a radio group with roving focus: only the selected tab is a tab stop.
- Remove buttons are named per step ("Remove Create issue"). With a mouse they show while their row is hovered or while
  they have keyboard focus; on touch screens they always show.
- Every word is in `AiWorkflowLabels`.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through Test run (or Stop), each step's remove button, the branch path tabs, Retry on a failed step, and Add step |
| Enter, Space | Starts or stops a run, removes a step, retries a failed step, or opens the add step menu |
| Arrow keys | Move between paths in the branch tabs (Home and End jump to the ends); in the open menu, Up and Down move the highlight |
| Type | Filters the add step menu by title, app or detail |
| Enter (menu) | Adds the highlighted step and closes the menu |
| 1 to 9 (menu) | With an empty search, adds the step with that shortcut |
| Escape | Closes the menu and returns focus to Add step |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| The run moves to the next step | A 5px accent dot glides down the connector in 240ms while the line behind it fills | The connector fills at once, no dot | At once |
| A step starts or ends | Its status crossfades (100ms out, 160ms in) in a fixed 72px slot; the detail crossfades to a shimmering activity line | Short fades, no shimmer | At once |
| An output, an error, an added or removed step | Opens or closes on its measured height on a 0.22s spring with no bounce, content fades | At once | At once |
| A reason or a draft | Streams five words every 34ms, each chunk fading in; the final layout is reserved from the first frame | Chunks appear without the fade | All at once |
| Switching paths | The lists crossfade (100ms out, 160ms in) while the height follows; the tab highlight slides on the same spring | 120ms crossfade, height at once | At once |
| The add step menu | Fades and settles in 160ms with no overshoot, leaves in 120ms; filtering animates the list to its new height | Fades | At once |
| Autoplay | Waits while the card is off screen and stops for good after the first press or key inside it | The same | The same |

The step rows keep their height and the status slot its width in every state, so nothing shifts while a run plays.

## Related

- `AgentPlan` for the plan an agent writes before it acts.
- `ToolCall` for a single tool call in an agent transcript.
- `AgentStatus` for the live status of a whole agent run.
- `WorkflowCanvas` for large free-form workflow graphs.

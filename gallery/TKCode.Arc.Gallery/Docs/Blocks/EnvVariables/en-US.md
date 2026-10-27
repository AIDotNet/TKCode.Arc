---
title: Env variables
summary: A project's environment variables as a matrix of switches per environment, with masked values, inline editing, .env import, a fingerprint comparison and a redeploy banner.
---

<Demo name="Preview" />

## When to use

`EnvVariables` is the settings page where a team keeps a project's configuration and secrets. Each row is one value and
the environments it applies to, so a key can have one value for development and staging and another for production. The
environments are columns of switches: turning a switch moves the row into or out of that environment, and a switch that
would give a key two values in the same environment is refused with a short note. Values stay hidden until someone
reveals one, and a value marked sensitive can be replaced but never shown again.

Every change marks its row and opens a banner that counts the changes and names the environments that still run the old
values. Pressing Redeploy calls your deployment; the banner confirms it, or turns red, keeps the changes and offers to try
again.

- **Project or service settings.** Keep the variables of an app, a worker or a site, with a column per environment.
- **Onboarding a project.** Paste an existing `.env` file and see, line by line, what will be added, replaced or skipped
  before anything changes.
- **Release checks.** Compare staging with production by fingerprint to find keys one side lacks or holds a different
  value for, and copy a missing value across.

## When not to use

- **Editing a single config file.** Use `CodeBlock` or a text editor when people work on the file itself, with comments
  and ordering that matter.
- **Thousands of keys.** The rows are not virtualized and every row animates; the block suits tens of keys, not a whole
  secrets store.
- **Secrets you must never send to the client.** The block only shows what you give it. Leave `Value` empty for
  sensitive rows, give a `Fingerprint` instead, and load a readable value only in `ValueRevealed`.
- **A list of unrelated settings.** For toggles and options that are not per environment, use a settings form with
  `Switch` and `Select`.

## Usage

### Environments and rows

`Environments` lists the columns in order; each `EnvEnvironment` has a `Key` that rows refer to and a `Name` people read.
Without any, the block uses Development, Preview and Production. Each `EnvVariable` has a `Key`, a `Value` and the keys of
the `Environments` it applies to; add `UpdatedAt` and `UpdatedBy` for the second line of the row. Give rows in any order:
the block sorts them by key (ordinal: upper-case letters before lower-case ones, and an underscore after the letters) and, for the same key, by
their first environment.

```xml
<arc:EnvVariables ProjectName="lumen-console" SampleDotenv="FEATURE_FLAGS_URL=https://flags.lumen.example">
    <arc:EnvVariables.Environments>
        <arc:EnvEnvironment Key="development" Name="Development" />
        <arc:EnvEnvironment Key="staging" Name="Staging" />
        <arc:EnvEnvironment Key="production" Name="Production" />
    </arc:EnvVariables.Environments>
</arc:EnvVariables>
```

```csharp
block.Variables.Add(new EnvVariable("CACHE_TTL_SECONDS", "30", "development", "staging"));
block.Variables.Add(new EnvVariable("CACHE_TTL_SECONDS", "300", "production"));
block.Variables.Add(new EnvVariable("PRIMARY_DB_DSN", null, "production") { IsSensitive = true, Fingerprint = "5e0c2a" });
```

Use `VariablesSource` for a collection you own; an `ObservableCollection` is followed, and so are changes to the rows'
properties and environment lists. The block edits that collection in place when people add, import or remove rows.

### Values, sensitive values and fingerprints

A value stays out of the screen and the accessibility tree until someone presses the eye; it is read at that moment and
dropped again when it hides. `ValueRevealed` is raised just before (fill `Value` there to load it on demand) and after it
hides. A sensitive row shows a lock and "Sensitive" instead of the eye and the copy button, and its editor starts empty:
a new value replaces the old one, and only removing the row and adding it again makes it readable.

The compare view shows a six-digit fingerprint instead of each value: by default the first six hex digits of the value's
SHA-256, computed outside the visual tree. Equal values give equal fingerprints. Set `Fingerprint` yourself for values you
do not load.

### Adding and importing

Add variable opens the single form: a key, a value, the environments (all but the last are chosen first; set
`DefaultAddEnvironments` to choose others) and the sensitive option. Errors appear after the first try and update as
people type: an empty key, a key that starts with a digit or has other characters than letters, digits and underscores,
a key already set in a chosen environment, an empty value, or no environment.

Import .env opens the paste form with `SampleDotenv`. Pasting several lines into the Key field switches to it too. The
preview under the text box shows each key line as new, replacing an existing value, or skipped (set again on a later line,
a bad key, no equals sign); comments, blank lines, `export` and quotes are handled. Importing turns the chosen
environments off on the rows they replace, removes a row left with none, and adds one row per line.

<Demo name="ImportDotenv" />

### Comparing environments

The compare view puts two environments side by side (`CompareLeft` and `CompareRight`, two-way; staging and production
here). Keys one side lacks get a Copy to button that adds that environment to the row holding the value, sensitive flag
included. `ShowMatching` (the Show identical switch) lists equal keys too. The search filters both views.

<Demo name="Compare" />

### Redeploying

`RedeployAction` receives an `EnvRedeployRequest` (the environments and the number of changes) and a cancellation token.
While it runs the button spins; changes made meanwhile wait for the next deployment. Throw to fail: the banner shows the
message, keeps every change and offers Retry. Without an action the block waits about 1.7 seconds, as a demo. Set
`Cancel` in `RedeployRequested` to stop it first, or call `RedeployAsync` and `CancelRedeploy` from code.

```csharp
// Your own call to the hosting service; here it only waits.
static Task StartDeployment(IReadOnlyList<string> environments, CancellationToken cancellation) =>
    Task.Delay(1500, cancellation);

block.RedeployAction = (request, cancellation) => StartDeployment(request.Environments, cancellation);
```

<Demo name="FailingRedeploy" />

### Narrow containers

The block reads its own width. Below 760 each row stacks: the key and the edit button, then the value, then one labelled
pill per environment, as many per line as fit at 120 or more; the compare table becomes one card per key. Below 560 the
side insets tighten, the header buttons share the width and the search and the view switch take a line each.

<Demo name="Compact" />

### Events and bound state

`ViewMode`, `SearchText`, `CompareLeft`, `CompareRight` and `ShowMatching` are two-way. `VariablesChanged` follows every
change with its reason and all rows; the specific events (`VariableAdded`, `VariableSaved`, `VariableRemoved`,
`VariablesImported`, `ScopeToggled`, `ScopeConflict`, `ValueCopied`) carry what changed. `PendingChangeCount`,
`PendingEnvironments` and `RedeployState` describe the banner. `IsReadOnly` keeps the block to reading and comparing, and
`IsFramed="False"` lays it flat on the page.

<Demo name="EventLog" />

### Texts

Every word is in `Labels` (`EnvVariablesLabels`), with formats as functions so the word order can change.

```csharp
block.Labels = new EnvVariablesLabels
{
    Title = "Variables d’environnement",
    AddButton = "Ajouter",
    ScopeName = (env, key) => $"{key} en {env}",
};
```

## API reference

<!-- api: EnvVariables -->

<!-- api: EnvVariable -->

<!-- api: EnvEnvironment -->

<!-- api: EnvRedeployRequest -->

<!-- api: EnvVariablesChangedEventArgs -->

## Accessibility

Keyboard:

- Tab moves through Import .env, Add variable, the banner's button, the search, the view switch, then each row: the eye
  (not on sensitive rows; the copy button comes before it while a value shows), one switch per environment and the edit
  button.
- Space or Enter turns a switch, reveals or hides a value, and opens or closes the editor.
- The view switch and the add panel's form switch move and choose with the arrow keys.
- In the editor and the add panel, Enter saves and Escape closes; the focus goes back to the button that opened them.
- Escape in the search clears it.

Screen readers:

- Each switch is a toggle named "<Environment> for <KEY>", in a group named "Environments for <KEY>"; the column header is
  not read. The eye is "Reveal <KEY>", the copy button "Copy <KEY>", the edit button "Edit <KEY>".
- A hidden value reads "Hidden value" and a sensitive one "Write-only value"; the bullets are not read. A compare cell
  reads the environment, "Fingerprint" and the digits.
- The refused-switch note is an assertive live region; changes are announced politely: "KEY now on in Staging",
  "KEY updated", "KEY created in Development and Staging", "KEY deleted from Production", "Imported 3 variables into …",
  "KEY now set in Staging", "Deployed Production", and a failed deployment with its message. Revealing a value and search
  results are not announced.
- The focus never falls back to the window: after saving it returns to the edit button, after adding or importing to the
  button that opened the panel, after removing to the next row's edit button, after Copy to the next Copy to button (or Show
  identical), after Clear search to the search box, and after the banner closes to the search box.

## Motion

- **Opening and closing**: the banner, the add panel, the editor, the conflict note and new rows grow on a smooth
  critically damped spring (about 700 ms) with the opacity on the same curve; they close, and removed or filtered rows
  collapse, in 180 ms on an accelerating curve. Switching between the single and the .env form is instant.
- **Saving, adding and importing**: the row glows in the accent tint for 1.6 seconds (held, then fading).
- **Switches**: the box fills in 120 ms and grows from 60% on a quick spring while the check mark draws.
- **Values**: the bullets fade up and out while the value rises in; the copy button pops in.
- **Banner**: the count and the lines swap with a short exit and a rising entry; on success the icon pops to a check and
  the tint eases to green, and the banner closes 2.8 seconds later. The conflict note closes after 3.8 seconds.
- **Views**: the old view fades out in 120 ms, then the new one rises 6 px and fades in over 240 ms while the height
  springs to fit.
- Reduced motion and Off apply every change at once, without the glow.

## Related

- API keys, Webhooks and Integrations, the other Developer blocks.
- `SearchField`, `SegmentedControl` and `Switch`, used for the toolbar and the compare controls.
- `CopyButton` for copying a single value elsewhere.
- `Alert` for a banner that is not tied to a list of changes.

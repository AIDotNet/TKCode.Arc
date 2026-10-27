---
title: Integrations
summary: A directory of apps to connect, with search, counted category filters, a connect pill that spins, connects or offers a retry, and a drawer for permissions, sync frequency and hold to disconnect.
---

<Demo name="Preview" />

## When to use

`Integrations` is the settings page where a workspace links the services it relies on. Each app is one row with its mark,
name, category and a one-line description, and a pill at the right that says where it stands. Pressing **Connect** runs
your authorization flow while the pill spins; when it succeeds the pill turns into a green **Connected** and a **Settings**
button slides in beside it. The name or the settings button opens a drawer with what the app may do, how often it syncs and
a button you hold to disconnect it.

- **An app marketplace or an add-ons page.** Search narrows the list as you type, the filters show how many apps each
  category has, and the order never changes when an app is connected, so people can find it where they left it.
- **Workspace or project settings.** The Connected filter shows what is linked today, and the drawer answers "what can
  this app see" before and after it is linked.
- **Onboarding.** A short list of recommended services with one press each to link them.

## When not to use

- **Hundreds of apps.** Rows are live elements that glide when the list changes; the block is designed for a few dozen.
  Use a paged or virtualized catalog for a large marketplace.
- **One service.** A single connect button with its own status line is clearer than a directory of one.
- **Settings that need a full page.** The drawer holds permissions, one frequency menu and the disconnect button. Link to
  a settings page when an app needs mappings, field choices or several steps.

## Usage

### Apps, categories and frequencies

Each `IntegrationItem` is a row. `Name` is its title everywhere, `Category` is the key of an `IntegrationCategory` (its
filter and the word after the name), `Description` is the line under it and the drawer's description. `Scopes` lists
what the app may do, each with a `Kind` that picks its icon (read, write, sync, admin). `SyncOptions` fill the drawer's
menu; leave it empty and the menu is gone.

```xml
<arc:Integrations Title="Connected apps" Subtitle="Link the services your workspace relies on.">
    <arc:Integrations.Categories>
        <arc:IntegrationCategory Key="files" Label="Files" />
        <arc:IntegrationCategory Key="insights" Label="Insights" />
    </arc:Integrations.Categories>
    <arc:Integrations.SyncOptions>
        <arc:IntegrationSyncOption Key="realtime" Label="Real-time" Hint="Updates land within seconds." Phrase="in real time" />
        <arc:IntegrationSyncOption Key="nightly" Label="Nightly" Hint="Runs every night at 03:00." Phrase="nightly" />
    </arc:Integrations.SyncOptions>
    <arc:IntegrationItem Key="vaultline" Name="Vaultline" Category="files"
                         Description="Mirror design files into the matching workspace board.">
        <arc:IntegrationScope Kind="Read" Text="Browse folders you share with the workspace" />
        <arc:IntegrationScope Kind="Write" Text="Add file links to boards and tasks" />
    </arc:IntegrationItem>
</arc:Integrations>
```

Bind `ItemsSource` instead of filling `Items` when the apps come from your own list; the block follows its changes.
`SelectedFilter` (`all`, `connected` or a category key) and `SearchText` are two-way, so a page can open on a filter or
keep the search in its address.

### Marks

`Logo` takes an image, a `Geometry` (filled with `MarkBrush`) or any control, drawn 20 by 20 without a plate. Without a
logo the block draws a generated mark: the first letter of the name, or `Monogram`, on a rounded tile in one of the chart
series colors, picked from the key so it stays the same between runs. The block ships no third-party logos.

### Connecting

`ConnectAction` is your authorization flow. It receives the app and the attempt number, and a token that is cancelled
when `CancelConnect` is called or the block leaves the window. Return `IntegrationConnectResult.Connected` with who
connected it (null means the person using the block: "Linked by you just now") or `IntegrationConnectResult.Failed` with
the reason; throwing fails too, and `ErrorFormatter` turns the exception into the drawer's line. A failed app shows
**Retry** on a red tint; pressing it runs the action again with the next attempt number.

<Demo name="Failing" />

```csharp
// authorize and revoke are your own calls to the service (here: delegates you provide).
Func<string, CancellationToken, Task<bool>> authorize = (key, token) => Task.FromResult(true);
Func<string, CancellationToken, Task> revoke = (key, token) => Task.CompletedTask;

directory.ConnectAction = async (request, cancellation) =>
{
    var approved = await authorize(request.Item.Key, cancellation);
    return approved
        ? IntegrationConnectResult.Connected()
        : IntegrationConnectResult.Failed($"{request.Item.Name} did not grant access. Try again.");
};
directory.DisconnectAction = (item, cancellation) => revoke(item.Key, cancellation);
```

Without a `ConnectAction` the block waits `SimulatedConnectDuration` (1.4 seconds) and succeeds, which is what the demos
on this page do. `ConnectRequested` can cancel a connect before it starts, `StatusChanged` reports every move between
idle, connecting, connected and failed, and `ConnectCommand` / `DisconnectCommand` run with the app as parameter.

### Narrow containers

The layout follows the block's own width, not the window's. Below 720 the search drops under the heading; at 520 the
side paddings tighten; at 480 and below the filters scroll sideways, rows put the pill beside a two-line description,
the category and the settings button are hidden, and the name opens the drawer.

<Demo name="Compact" />

### Categories only

Set `ShowConnectedFilter="False"` to offer only All and the categories, and leave `SyncOptions` empty when your apps have
no schedule: the drawer then shows the permissions and the disconnect button.

<Demo name="CategoriesOnly" />

### Events and bound state

`FilterChanged` and `SearchChanged` report the new visible count, `SyncFrequencyChanged` the old and new option keys,
and `ConfigurationOpened` / `ConfigurationClosed` the app of the drawer. `ConnectedCount`, `VisibleCount` and
`OpenItem` can be bound. `IsFramed="False"` lays the block flat on the page. Every word is in `Labels`
(`IntegrationLabels`), including the screen reader announcements.

<Demo name="EventLog" />

## API reference

<!-- api: Integrations -->

<!-- api: IntegrationItem -->

<!-- api: IntegrationCategory -->

<!-- api: IntegrationScope -->

<!-- api: IntegrationSyncOption -->

<!-- api: IntegrationStatusChangedEventArgs -->

<!-- api: IntegrationConnectRequestedEventArgs -->

<!-- api: IntegrationFilterChangedEventArgs -->

<!-- api: IntegrationSearchChangedEventArgs -->

<!-- api: IntegrationSyncFrequencyChangedEventArgs -->

## Accessibility

- The filters are radio buttons named "{filter}, {count}"; the list, the search field and every pill, name and button
  have accessible names ("Connect Pulsegrid", "Connecting Pulsegrid", "Settings for Chirp"), and the name's help text is
  the description.
- A connected pill is a status label: it is not focusable and is named "{app} connected".
- One polite live region announces connecting, connected, failed and disconnected apps, frequency changes and the
  number of apps after a filter or search change.
- States never rely on color alone: each pill has its own icon and word.

### Keyboard

| Key | Action |
| --- | --- |
| Tab | Moves through the search, the checked filter, then each row: its name, then its pill (idle, connecting or failed) or its settings button (connected). One stop per action. |
| Left / Up, Right / Down | In the filters: checks the previous or next filter, wrapping at the ends. |
| Home / End | In the filters: checks the first or last filter. |
| Enter / Space | Presses the focused pill, name or button. |
| Escape | Closes the drawer. |

After a connect the focus moves from the pill to the new settings button (or to the name when it is hidden); after a
connect in the drawer it moves to the sync frequency menu; after a disconnect it lands on the drawer's Connect pill. When
the drawer closes and the row that opened it has left the list, the focus goes to the heading. A row that leaves while
it holds the focus hands it to the next row.

## Motion

- Search and filters reflow the list: rows that leave fade into a 4px blur where they were (160ms), the others glide to
  their new place on one spring, new rows fade in out of a blur (240ms) and the list's height follows.
- The filter highlight glides between pills on the same spring; a changed count rolls up or down.
- The pill's label blur-swaps and its width follows on a spring; the check draws itself; the settings button slides in
  140ms after a connect and fades out after a disconnect. The drawer's settings open and close on a height spring.
- Reduced motion keeps 120ms fades and drops travel, blur and springs (leaving rows are removed at once); Off jumps.

## Related

- `Drawer` for the panel the settings open in.
- `HoldToConfirm` for the disconnect button.
- `SearchField` and `Select` for the search and the frequency menu.
- `EmptyState` for a list that is empty for other reasons.

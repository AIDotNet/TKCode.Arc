---
title: Settings command
summary: Find any setting from one search field and change it in place: toggles flip, choices cycle and links open, all from the keyboard.
---

<Demo name="Inline" />

## When to use

`SettingsCommand` puts the settings of an application behind one search field. People type a few letters, such as
"email" or "dark", and change the setting on the spot: no settings page to open, no section to find and no row to scroll
to. It sits beside the full settings page as a quick path to the same values, or on its own in a dialog that opens with
a shortcut. The palette holds the values and calls the app when one of them changes, so it fits settings that take
effect at once.

- **A settings palette opened with Ctrl+, (⌘, on a Mac)** in an analytics or project tool, where people change "Theme",
  "Compact tables" or "Default chart range" many times a day. A dialog with the palette removes the trip through the
  settings navigation.
- **Notification preferences in a team inbox**: "Mentions", "Product updates" and "Email digests" are switches or three
  named values. Each change applies at once, and the footer confirms it ("Mentions off"), so there is no save step to
  forget.
- **Privacy and presence switches in a collaboration tool**: "Show online status" and "Read receipts" take effect for the
  whole workspace. A notice in the footer confirms each change, so people see the result without opening a dialog.
- **Account actions next to the settings they relate to**: "Change email" and "Two-step verification" open other pages,
  and "Sign out everywhere" runs a command. Each action can show its shortcut in key caps, so one search finds all of
  them.
- **Admin consoles with many small switches**: a tenant console where a search for "single sign-on" finds the
  enforcement switch among rows grouped under Security, Users and Billing. Keep the list short enough to scan (see
  Size and performance).

## When not to use

- **Settings that need a form, validation or a save step**: use `SettingsPage` with `SettingsGroup` and `SettingsRow`,
  or a form built from `Input` and `Select`. The palette applies each value at once and has no place for a validation
  message or a Save button.
- **Choices with many values or free text**, such as a time zone, a country or a display name: use `Combobox` or
  `Select` for a long list, and `Input` for free text. A palette row shows its options in a short pill, so it only fits
  a handful of values.
- **Dates and times**, such as quiet hours or a report start date: use `TimePicker` or `DatePicker`. A palette row
  cannot show a calendar or a clock.
- **App commands that are not settings**, such as "New project" or "Export this view": use `CommandPalette`. It searches
  commands, while this palette searches the values people configure.
- **Searching records**, such as customers, invoices or messages: use `SearchField` or `ExpandingSearch`, with results
  from the data. The palette only searches the settings it was given.

## Usage

Write the settings inside the palette. A `SettingsCommandToggle` is on or off, a `SettingsCommandChoice` steps
through its `Options`, and a `SettingsCommandAction` runs a command. `Group` lists a setting under a title, and
`Keywords` adds other words that find it. Focus stays in the search field: the up and down keys move the highlight,
Enter acts on the highlighted row, and the left and right keys step a choice. The footer confirms each change.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SettingsCommand MaxWidth="560">
        <arc:SettingsCommandChoice Group="Appearance" Header="Theme" Options="Light, Dark, System" Keywords="dark light" />
        <arc:SettingsCommandToggle Group="Appearance" Header="Compact mode" Description="Tighter rows and smaller gaps." />
        <arc:SettingsCommandAction Group="Account" Header="Change email" IsNavigation="True" />
    </arc:SettingsCommand>
</UserControl>
```

`SettingsCommand` derives from Avalonia's `TemplatedControl`, so it takes the usual layout properties (`Width`,
`MaxWidth`, `Margin`) and styling. Its settings are an ordinary collection: `SettingsCommandItem` is the base class of
the three kinds, and the items are the content of the palette, so they can be written in XAML or added in code. The
palette is a framed panel by default, with its own surface, border, corner radius and floating shadow. Set `IsFramed`
to `False` when a surrounding surface already provides them.

### Anatomy

1. **Search row** (`Query`): 56 px high, with an 18 px search glyph in the muted text color, a borderless text box
   and the Esc key cap on the right. A divider sits under the row.
2. **Result list** (`ResultsMaxHeight`): 360 px by default, with an 8 px inset. Past that height the list scrolls, and
   its top and bottom edges fade over 20 px to show more content.
3. **Group title** (`Group`): extra small text in the muted text color, with 12, 10, 12 and 4 px of padding. Settings
   without a group appear under `Labels.Ungrouped`, and only when another setting has a group.
4. **Row** (`Header`, `Description`): 48 px high with a 12 px corner radius. The header is small body text with medium
   weight; the description is one line of extra small text in the secondary text color, cut with an ellipsis.
5. **Icon tile** (`Icon`): a 30 px tile with a 9 px corner radius and a 16 px glyph. Use an `ArcIcons` member for
   the glyph.
6. **Row control**: a switch for `SettingsCommandToggle`, a 28 px pill with two 24 px arrows and the current value for
   `SettingsCommandChoice`, and key caps or a right arrow for `SettingsCommandAction`.
7. **Active row highlight**: a muted surface with a 12 px corner radius that glides between rows.
8. **Footer**: 40 px high, with a divider above. Key hints sit on the left (up and down for Navigate, Enter for Toggle,
   Change or Open depending on the active row, and Esc for Close). The change notice sits on the right.

### Searching

The query splits on spaces. Each word must appear in the header, description, group or keywords, ignoring case, and it
may match part of a word. The results are ranked by how the header matches:

1. The header starts with the whole query.
2. A word in the header starts with the query.
3. The header contains the query.
4. Only the description, group or keywords match.

Settings with the same rank keep their declared order, and a group sorts by the rank of its best row. The matching part
of a header stays in the foreground while the rest of the header turns secondary, so people can see why a row matched.
When nothing matches, the palette shows `Labels.NoResults` with `Labels.NoResultsHint`, which suggests another word. An
empty query lists every setting in declared order, so write the settings in the order people expect to see them.

`Query` is two-way, so the app can prefill the search (for example from a deep link) or keep the last query. `Clear()`
empties it, which lists every setting again, and `FocusSearch()` moves the keyboard focus to the search field.

### Toggles, choices and actions

Pick the kind by what the setting does when it is changed:

- **`SettingsCommandToggle`** for a yes or no setting that applies at once, such as "Compact mode" or "Mentions".
  Enter or a click flips it, and the notice reads "Compact mode on" or "Compact mode off". A switch that needs a second
  step to confirm does not belong here.
- **`SettingsCommandChoice`** for three to five named values, such as "Light, Dark, System" or "Off, Daily, Weekly".
  Enter moves to the next option and wraps from the last to the first, while the arrows beside the value step without
  wrapping. Order the options so the usual next choice follows the default, because Enter moves one step at a time.
- **`SettingsCommandAction`** for a command or a link: "Change email", "Download your data", "Sign out everywhere".
  Enter or a click runs the `Command` and then raises `ItemInvoked`. Set `IsNavigation` to `True` for an action that
  opens another page, so the row shows an arrow and people expect a new screen.

Set `IsEnabled` to `False` for a setting that cannot be changed now, for example one that an admin has locked. The row
is dimmed and the keyboard skips it, but the row stays in the list, so people learn that the setting exists and why it
is unavailable. Use the `Description` to say why ("Turned off by your admin.").

### Binding to a view model

The values of a palette are styled properties. `IsChecked` and `SelectedOption` bind two-way: a change made in the
palette updates the view model, and a change made elsewhere in the app moves the row. The example below uses
CommunityToolkit.Mvvm (the `ObservableObject` base class, `[ObservableProperty]` and `[RelayCommand]` generators). The
app owns the state; the palette only edits it.

```csharp
public partial class WorkspaceSettingsViewModel : ObservableObject
{
    private readonly Action<string> _openPage;

    [ObservableProperty]
    private string? _theme = "System";

    [ObservableProperty]
    private bool _compactMode;

    [ObservableProperty]
    private bool _reduceMotion = true;

    [ObservableProperty]
    private string? _emailDigests = "Weekly";

    public WorkspaceSettingsViewModel(Action<string> openPage) => _openPage = openPage;

    [RelayCommand]
    private void ChangeEmail() => _openPage("account/email");
}
```

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandChoice Group="Appearance" Header="Theme"
                               Options="Light, Dark, System"
                               SelectedOption="{Binding Theme}" />
    <arc:SettingsCommandToggle Group="Appearance" Header="Compact mode"
                               IsChecked="{Binding CompactMode}" />
    <arc:SettingsCommandToggle Group="Appearance" Header="Reduce motion"
                               IsChecked="{Binding ReduceMotion}" />
    <arc:SettingsCommandChoice Group="Notifications" Header="Email digests"
                               Options="Off, Daily, Weekly"
                               SelectedOption="{Binding EmailDigests}" />
    <arc:SettingsCommandAction Group="Account" Header="Change email" IsNavigation="True"
                               Command="{Binding ChangeEmailCommand}" />
</arc:SettingsCommand>
```

Two-way: `IsChecked`, `SelectedOption` and `Query`. One-way: `Items`, `Labels` and `ResultsMaxHeight`, which the app sets
and the palette reads. Read-only: `ActiveItem`, which is the highlighted setting, or `null` when nothing is on show.
Use the events for side effects that a binding cannot express, such as saving to a server or logging an audit entry.
`SettingChanged` runs only for changes made in the palette, so a save in the handler does not run again when the app
changes a value itself. `ItemInvoked` runs after an action's `Command` has run. Both events are routed, so a handler can
sit on a parent element as well as on the palette.

<Demo name="Bound" />

```csharp
palette.SettingChanged += (_, e) => _settings.SaveAsync(e.Item.Header, e.Value);
palette.ItemInvoked += (_, e) => _telemetry.Track("settings.action", e.Item.Header);
```

### Focus and dismissal

A palette in a `Dialog` needs three things: `IsFramed` set to `False`, so the palette does not draw a second panel;
a negative margin as large as the dialog's padding, so the search row and the footer reach the dialog's edges; and a
rule for closing. The demo gives the palette a margin of -24 for the dialog's 24 px body padding. The code below closes the `Dialog` that hosts the palette.

Escape with an empty query raises `CloseRequested`, and Escape with a query clears the query first. Close the dialog in
the `CloseRequested` handler and set `Handled` to `True`, so the dialog does not handle the same key press again.

The dialog moves focus to its first control when it opens, which would take the keyboard away from the search field.
Post `FocusSearch()` on the UI thread once the dialog is open, and clear the query when the dialog opens so each
session starts from the full list.

<Demo name="InDialog" />

```csharp
private Dialog SettingsDialog = null!; // the Dialog with x:Name="SettingsDialog" in the demo

private void OnCloseRequested(object? sender, RoutedEventArgs e)
{
    e.Handled = true;
    SettingsDialog.Close();
}
```

The demo also opens the dialog with Ctrl+, (⌘, on a Mac), the usual shortcut for settings. The palette only shows
shortcuts, so the app registers the key gesture itself; see the next subsection.

### Shortcuts and actions

`Shortcut` shows an action's key combination in key caps. Write `mod` for the command key, which is ⌘ on a Mac and
Ctrl elsewhere, and combine it with `shift`, such as `mod+shift+e`. The palette only shows the shortcut, and the app
handles the key press, for example in a window-level key handler, so the shortcut works while the palette is closed as
well. `IsNavigation` adds an arrow for actions that open another page.

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandAction Header="Download your data" Shortcut="mod+shift+e" />
    <arc:SettingsCommandAction Header="Two-step verification" IsNavigation="True" />
    <arc:SettingsCommandToggle Header="Read receipts" Description="Turned off by your admin." IsEnabled="False" />
</arc:SettingsCommand>
```

### Text

The built-in text comes from `Labels`: strings for fixed text and functions for text with a value, such as the
no-results message. Replace the whole object to translate it, and keep each function's parameters in order.

```csharp
palette.Labels = new SettingsCommandLabels
{
    Placeholder = "Instellingen zoeken",
    NoResults = query => $"Geen instellingen voor “{query}”",
    TurnedOn = header => $"{header} aan",
    TurnedOff = header => $"{header} uit",
};
```

### Size and performance

The palette creates one row per setting and keeps it for the life of the item. Filtering changes only which rows show and
their order. Settings in the low hundreds are fine without virtualization; move a longer catalogue to a settings page.

Add or remove items in code with the `Items` collection, and the list updates at once.

### Example: Notification settings in a team inbox

A team inbox keeps its notification preferences in the same palette as its other settings. The query here is "email",
which shows how the ranking works: "Email digests" starts with the query, so it ranks first; "Change email" has a
header word that starts with it; "Product updates" matches only through its keywords, so it ranks last. Each group
sorts by its best row, which puts Notifications above Account. The matching text in the header stays in the foreground,
and the footer shows the keys that apply to the highlighted choice. The palette sits at 560 px, which fits a dialog or a
settings column without stretching the rows.

![A settings palette searching for email, ranked across two groups](images/EmailSettings.png)

```xml
<StackPanel Width="560">
    <arc:SettingsCommand Query="email"
                         ResultsMaxHeight="320">
        <arc:SettingsCommandChoice Group="Notifications"
                                   Header="Email digests"
                                   Icon="{x:Static arc:ArcIcons.Inbox}"
                                   Description="A summary of activity in your projects."
                                   Keywords="summary newsletter"
                                   Options="Off, Daily, Weekly"
                                   SelectedOption="Weekly" />
        <arc:SettingsCommandToggle Group="Notifications"
                                   Header="Mentions"
                                   Icon="{x:Static arc:ArcIcons.AtSign}"
                                   Description="When someone @mentions you in a comment."
                                   IsChecked="True" />
        <arc:SettingsCommandToggle Group="Notifications"
                                   Header="Product updates"
                                   Icon="{x:Static arc:ArcIcons.Bell}"
                                   Description="Occasional news about new features."
                                   Keywords="news announcements" />
        <arc:SettingsCommandAction Group="Account"
                                   Header="Change email"
                                   Icon="{x:Static arc:ArcIcons.Mail}"
                                   Description="noor@larkspur.co"
                                   Keywords="address login"
                                   IsNavigation="True" />
        <arc:SettingsCommandAction Group="Account"
                                   Header="Download your data"
                                   Icon="{x:Static arc:ArcIcons.Download}"
                                   Description="A copy of your files, comments and settings."
                                   Keywords="export archive backup"
                                   Shortcut="mod+shift+e" />
    </arc:SettingsCommand>
</StackPanel>
```

### Content guidelines

Write each header as the name of the setting, in sentence case, so it reads the same in the row, the search and the
notice. Keep the description to one short line, because the row trims it with an ellipsis. Name the choices with the
words people use on the settings page, and keep the same names in the notice that follows a change.

| Instead of | Write |
|---|---|
| Enable notifications for mentions | Mentions |
| Show a summary email every day | Email digests (option: Daily) |
| Toggle the compact view mode | Compact mode |
| Change your email address | Change email |
| Sign out of all devices | Sign out everywhere |
| Setting disabled by the organization administrator | Turned off by your admin. |

Use keywords for synonyms and the words people search for, such as "dark light" for Theme or "2fa security" for
Two-step verification. Do not repeat the header in the keywords, and do not add a keyword that names another setting.

### Best practices

**Do**

- Apply a change at once and confirm it in the footer. The notice is the only feedback in the palette, so it has to
  name the setting and its new value.
- Bind the values to the app's state and keep saving in the event handlers, so the palette and the rest of the app
  always show the same value.
- Keep choices to a few named values, and move anything longer to a page with a proper control.
- Register the open shortcut for each platform (`mod+,` in the demo) and show it next to the button that opens the
  palette, so people learn the shortcut from the screen they already use.
- Use `IsEnabled="False"` with a reason in the description for a locked setting, so people know it is not missing.

**Avoid**

- Settings that need a save step or validation. A palette change applies at once, so a rejected value would already be
  in effect; send those settings to `SettingsPage`.
- Actions that delete data or sign people out without a confirmation. Open a `Dialog` from the action's command, so the
  search does not skip the safeguard.
- Long lists of settings with the same description. The search cannot tell them apart, and the rows look alike.
- Changing the header of a setting for each state, such as "Turn on compact mode" and "Turn off compact mode". The row
  states the value; the header stays the same.
- Storing a value only in the palette. `SettingChanged` is raised for user changes only, so the app must keep the state
  itself.

### Common questions

**Why does the first Escape clear the query instead of closing?** Escape clears a non-empty query first, so a person can
dismiss a search without losing the palette. With an empty query, Escape raises `CloseRequested`. A dialog that hosts the
palette should close only in that handler.

**Why does the highlight stop when I press Down on the last row?** The highlight does not wrap: it stops at the first and
last enabled row. Disabled rows are skipped in both directions, so the highlight always lands on a setting that can be
changed.

**How do I react to a value that the app changes itself?** Bind the property, such as `IsChecked`, and the row follows
the change. `SettingChanged` is not raised for it, so use a `PropertyChanged` handler on the view model for changes that
must trigger a side effect.

## API reference

<!-- api: SettingsCommand -->
<!-- api: SettingsCommandToggle -->
<!-- api: SettingsCommandChoice -->
<!-- api: SettingsCommandAction -->

### SettingsCommandItem

The base class of the three kinds of setting. Each property is a styled property, so the palette follows changes.

| Property | Type | Description |
|---|---|---|
| `Header` | `string?` | The setting's name, shown in the row and searched first. |
| `Description` | `string?` | One line under the header, trimmed to a single line; also searched. |
| `Icon` | `Geometry?` | A 24 by 24 stroke icon, such as an `ArcIcons` member, shown in a tile before the header. |
| `Group` | `string?` | The group the row is listed under, such as "Appearance"; also searched. |
| `Keywords` | `string?` | Other words that find the setting, separated by spaces or commas. |
| `IsEnabled` | `bool` | Whether the setting can be changed. Disabled rows are dimmed and skipped by the keyboard. |

### SettingsCommandLabels

| Property | Type | Description |
|---|---|---|
| `Placeholder` | `string` | The search field's placeholder: "Search settings". |
| `SearchName` | `string` | The search field's accessible name: "Search settings". |
| `ResultsName` | `string` | The accessible name of the result list: "Settings". |
| `NoResults` | `Func<string, string>` | The message when nothing matches: "No settings match “theme”". |
| `NoResultsHint` | `string` | The hint under it: "Try another word, such as theme or email." |
| `Navigate`, `Toggle`, `Change`, `Open`, `Close` | `string` | The footer hints beside the keys. |
| `EscapeKey` | `string` | The Escape key cap: "Esc". |
| `TurnedOn`, `TurnedOff` | `Func<string, string>` | The notice after a toggle changes: "Compact mode on". |
| `ChangedTo` | `Func<string, string, string>` | The notice after a choice changes: "Theme: Dark". |
| `Ungrouped` | `string` | The group title of settings without a group, shown when other settings have one: "Settings". |
| `On`, `Off` | `string` | A toggle's state as assistive technology reads it. |
| `RowState` | `Func<string, string, string>` | A row's header and state: "Dark mode, on". |
| `PreviousOption`, `NextOption` | `string` | The names of the arrows beside a choice's value. |

### Event arguments

| Type | Property | Description |
|---|---|---|
| `SettingsCommandChangedEventArgs` | `Item` | The setting that changed. |
| `SettingsCommandChangedEventArgs` | `Value` | Its new value: a `bool` for a toggle, the option for a choice. |
| `SettingsCommandItemEventArgs` | `Item` | The action that was invoked. |

## Accessibility

- The search field is named "Search settings". Its help text follows the highlighted row, such as "Compact mode, off" or "Theme, Dark".
- The results are a list named "Settings", and each row is a list item named by its header.
- The keyboard stays in the search field. Up and down move the highlight and skip disabled rows, and Page Up and Page Down move five rows. The list does not wrap.
- Enter flips a toggle, moves a choice to its next option, or runs an action. Left and right step a choice without wrapping. Otherwise they move the caret.
- Escape clears the query. With an empty query it raises `CloseRequested`.
- The change notice is a polite live region, so screen readers announce "Compact mode on".
- The switches, the arrows beside a choice and the key caps cannot take focus. A click on any row acts like Enter, and the keyboard stays in the search field.

### Keyboard

| Keys | Action |
|---|---|
| `Down` / `Up` | Move the highlight to the next or previous enabled setting; it does not wrap |
| `PageDown` / `PageUp` | Move the highlight five settings |
| `Enter` | Flip a toggle, move a choice to its next option (after the last comes the first), or run an action |
| `Right` / `Left` | Step a choice to its next or previous option without wrapping; on other rows, move the caret |
| `Escape` | Clear a non-empty query; with an empty query, raise `CloseRequested` |
| Typing | Changes the query, and the list filters as you type |

A click on a row acts like Enter. Clicking a switch or a choice arrow changes that setting directly.

## Motion

- The highlight glides between rows on a snappy spring. When the results change it moves at once.
- The list scrolls smoothly to keep the highlighted row and its group title in view.
- A choice's value rolls up or down in the direction of the change, and switches slide their thumb.
- The change notice rises in with a soft blur and fades out after 2.5 seconds.
- Reduced motion fades the notice and the values and moves the highlight at once. Off shows every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The highlight moves between rows | Glides on a snappy spring | Moves at once | Moves at once |
| A choice's value changes | Rolls up or down in the direction of the change | Fades | Shows at once |
| The change notice appears | Rises in with a soft blur, then fades out after 2.5 seconds | Fades in and out | Shows at once |

## Related

- `SettingsPage` for a full settings page with section navigation and a save bar.
- `SecuritySettings`, `BillingOverview`, `UsageBilling` and `CancelFlow`, the other Settings blocks.
- `Switch`, the control in toggle rows, and `Dialog`, which hosts the palette behind a shortcut.
- `SearchField` and `Select` for a single search or choice inside a form.

| Component | Choose it when |
|---|---|
| `SettingsPage` | The settings are split into sections and saved together with a save bar |
| `CommandPalette` | People run app commands, not change configured values |
| `SearchField` | The text searches records or content, not settings |
| `Switch` | A single setting sits in a form or a row that needs no search |
| `Select` / `Combobox` | A form needs one value from a long list |

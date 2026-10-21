---
title: Settings command
summary: Find any setting from one search field and change it in place: toggles flip, choices cycle and links open, all from the keyboard.
---

<Demo name="Inline" />

## When to use

- A quick way to reach any setting from the keyboard, next to or instead of a full settings page.
- Settings that take effect at once: on or off switches, a few named values, and links to the pages that hold the rest.
- A settings palette opened with a shortcut, such as Ctrl+, or ⌘, in a dialog.

## When not to use

- Settings that need a form, validation or a save step: use a settings page with `SettingsGroup` and `SettingsRow`.
- Choices with many values or free text, such as a time zone or a display name: link to a page with an action instead.
- App commands that are not settings, such as "New project": use a command palette.

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

### Searching

Every word of the query must appear in the header, description, group or keywords; case does not matter. Rows whose
header starts with the query come first, then rows where a word of the header starts with it, then rows whose header
contains it, then the rest. Ties keep the declared order, and groups follow their best row. The matching part of a
header stays in the foreground while the rest turns secondary. When nothing matches, the palette says so and
suggests another word.

### In a dialog

Set `IsFramed` to `False` inside a `Dialog`, so the palette does not draw a second panel. The demo also gives it a
negative margin as large as the dialog's padding, so the search row and the footer reach the dialog's edges. Escape
with an empty query raises `CloseRequested`. Close the dialog there and mark the event handled, so the dialog does not handle the
same key again. The demo also opens the dialog with Ctrl+, (⌘, on a Mac), the usual shortcut for settings.

<Demo name="InDialog" />

```csharp
private void OnCloseRequested(object? sender, RoutedEventArgs e)
{
    e.Handled = true;
    SettingsDialog.Close();
}
```

### Bound values

`IsChecked` and `SelectedOption` bind two-way, and the rows follow when the app changes a value. `SettingChanged`
is raised only for changes made in the palette. Its `Value` is a `bool` for a toggle and the option for a choice.
Actions run their `Command` and then raise `ItemInvoked`.

<Demo name="Bound" />

```csharp
palette.SettingChanged += (_, e) => Save(e.Item.Header, e.Value);
palette.ItemInvoked += (_, e) => Navigate(e.Item.Header);
```

### Shortcuts and actions

`Shortcut` shows an action's key combination in key caps. Write `mod` for the command key, which is ⌘ on a Mac and
Ctrl elsewhere. The palette only shows the shortcut, and the app handles it. `IsNavigation` adds an arrow for actions
that open another page. Set `IsEnabled` to `False` to show a setting that cannot be changed now. The row is dimmed
and the keyboard skips it.

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandAction Header="Download your data" Shortcut="mod+shift+e" />
    <arc:SettingsCommandAction Header="Two-step verification" IsNavigation="True" />
    <arc:SettingsCommandToggle Header="Read receipts" Description="Turned off by your admin." IsEnabled="False" />
</arc:SettingsCommand>
```

### Text

The built-in text comes from `Labels`. Replace the whole object to translate it.

```csharp
palette.Labels = new SettingsCommandLabels
{
    Placeholder = "Instellingen zoeken",
    NoResults = query => $"Geen instellingen voor “{query}”",
    TurnedOn = header => $"{header} aan",
    TurnedOff = header => $"{header} uit",
};
```

## API reference

<!-- api: SettingsCommand -->
<!-- api: SettingsCommandToggle -->
<!-- api: SettingsCommandChoice -->
<!-- api: SettingsCommandAction -->

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

## Motion

- The highlight glides between rows on a snappy spring. When the results change it moves at once.
- The list scrolls smoothly to keep the highlighted row and its group title in view.
- A choice's value rolls up or down in the direction of the change, and switches slide their thumb.
- The change notice rises in with a soft blur and fades out after 2.5 seconds.
- Reduced motion fades the notice and the values and moves the highlight at once. Off shows every change at once.

## Related

- `SettingsPage` for a full settings page with section navigation and a save bar.
- `SecuritySettings`, `BillingOverview`, `UsageBilling` and `CancelFlow`, the other Settings blocks.
- `Switch`, the control in toggle rows, and `Dialog`, which hosts the palette behind a shortcut.
- `SearchField` and `Select` for a single search or choice inside a form.

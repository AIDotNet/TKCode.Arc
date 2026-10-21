---
title: Settings page
summary: A full settings page: section navigation with an account card, the chosen section beside it, and a save bar for unsaved changes.
---

<Demo name="Overview" />

## When to use

- The settings area of an app or workspace with a handful of sections, such as profile, notifications, security and billing.
- Sections whose changes are saved together, with a clear save and discard step.
- A layout that should keep working from a wide window down to a phone-width column.

## When not to use

- One short list of options: use `SettingsGroup` with `SettingsRow` on its own.
- Finding and changing a setting by name from anywhere in the app: use `SettingsCommand`.
- Navigation between unrelated pages of an app: use your app shell's sidebar.

## Usage

Write one `SettingsSection` per section. `Header`, `Icon` and `Badge` appear in the navigation, and the selected section
shows its header, `Description` and content on the right. The first enabled section is selected; set `SelectedIndex`
or call `Select(section)` to show another. `Title` sits above the navigation and `Footer` below it, for example an
account card.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SettingsPage Height="480">
        <arc:SettingsSection Header="Profile" Icon="{x:Static arc:ArcIcons.User}" Description="How you appear to others.">
            <arc:Input Label="Full name" Text="Noor Haddad" />
        </arc:SettingsSection>
        <arc:SettingsSection Header="Notifications" Icon="{x:Static arc:ArcIcons.Bell}" Badge="2">
            <arc:SettingsGroup Appearance="Plain">
                <arc:SettingsRow Header="Mentions" Description="When someone mentions you.">
                    <arc:Switch IsChecked="True" />
                </arc:SettingsRow>
            </arc:SettingsGroup>
        </arc:SettingsSection>
    </arc:SettingsPage>
</UserControl>
```

### Unsaved changes

Set `IsDirty` on a section while it has unsaved changes. Its navigation item shows a dot, and while it is selected a save
bar rises at the bottom of the page with Discard and Save. Save (or Ctrl+S, ⌘S on macOS) raises `SaveRequested`, and
Discard raises `DiscardRequested`. Both carry the `Section`. Set `IsDirty` back to `false` once the changes are saved or
reverted.

<Demo name="UnsavedChanges" />

```csharp
page.SaveRequested += async (_, e) =>
{
    await SaveProfileAsync();
    e.Section.IsDirty = false;
};
```

### Compact layout

Below `CompactWidth` (720 by default) the page becomes one column: the sections turn into a row of pills under the title
that scrolls sideways, the footer is hidden, and the content gets less padding. `IsCompact` reports the current layout.

<Demo name="Compact" />

### Settings groups and rows

`SettingsGroup` and `SettingsRow` build the content of a section, and work on their own anywhere. A row shows an
optional `Icon`, a `Header` and a `Description`, and the control that changes the setting as its content. Below
`StackWidth` (440 by default) the control moves under the text. When the control is a `Switch` or `Checkbox`, clicking
the text toggles it, and the control takes the header and description as its accessible name and help text.

A group draws a card by default, with an optional `Header`, `Description`, `HeaderActions` and a tinted `Footer` strip.
`Appearance="Plain"` drops the frame for sections that are separated by hairlines inside a page.

<Demo name="Rows" />

### Text

`Labels` holds the save bar text and the accessible names. Replace the whole object to translate it.

```csharp
page.Labels = new SettingsPageLabels
{
    UnsavedChanges = "Nicht gespeicherte Änderungen",
    Save = "Speichern",
    Discard = "Verwerfen",
};
```

## API reference

<!-- api: SettingsPage -->
<!-- api: SettingsSection -->
<!-- api: SettingsGroup -->
<!-- api: SettingsRow -->

## Accessibility

- Each navigation item is a tab item named after its section, followed by its badge and "has unsaved changes" when the
  section has them. The navigation is named "Settings sections".
- The navigation is one tab stop. Up and Down (Left and Right on a compact page) move to the previous or next enabled
  section and show it; Home and End go to the first and last. They do not wrap.
- Ctrl+S (⌘S) saves when the shown section has unsaved changes.
- Keyboard focus draws a 2px ring around the navigation item. Pointer focus does not.
- The note in the save bar is a polite live region.

## Motion

- The selection pill glides between navigation items on the snappy spring.
- The incoming section rises 6px and fades in (standard duration, enter easing) while the outgoing one fades out in place.
- The save bar rises into view and drops away.
- Reduced motion keeps the fades and drops the movement. With motion off, everything changes at once.

## Related

- `SettingsCommand`, `SecuritySettings`, `BillingOverview`, `UsageBilling` and `CancelFlow`: the other settings blocks,
  which fit inside a section.
- `Tabs`: switch between views of the same object.
- `Switch`, `Input`, `Select`, `BillingToggle`: the controls the sections in the examples are built from.

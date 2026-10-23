---
title: Settings page
summary: A full settings page: section navigation with an account card, the chosen section beside it, and a save bar for unsaved changes.
---

<Demo name="Overview" />

## When to use

`SettingsPage` is the settings area of an application or workspace. A column on the left lists the sections, such as
Profile, Notifications, Security and Billing, under the page title and above an account card. The chosen section fills
the rest of the page. Because the page knows which section has unsaved edits, it can show one save bar for that section,
so people always see whether their changes are pending.

- **Workspace settings in an admin console**: a Larkspur workspace with General, Members and Billing sections. An owner
  moves between them without losing the page, and each section holds the forms for its own job.
- **Account settings in a SaaS product**: Profile, Notifications, Security and Billing for one signed-in person. The
  sections match how people look for a setting, so they find "Two-step verification" under Security without searching.
- **A billing area with a save step**: the company name, billing email and VAT number are edited together and saved
  with one action. Discard restores the saved values, which a plain list of rows cannot offer.
- **Settings in a narrow window or a side panel**: below 720 px the page becomes one column, with the sections as a row
  of pills that scrolls sideways. The same page works in a split view or on a phone-width window.
- **Settings with more sections than a single form can hold**: the navigation keeps every section one click away, and
  it stays in place while a long section scrolls, because only the content column scrolls.

## When not to use

- **One short list of options**: use `SettingsGroup` with `SettingsRow` on its own, in a card or a dialog.
- **Finding and changing a setting by name from anywhere in the app**: use `SettingsCommand`, the search palette that
  changes values in place.
- **Navigation between the areas of an application**: use `Sidebar`. A settings page is one area with its own sections,
  not the shell of the app.
- **Views of the same record, such as Overview, Activity and Files**: use `Tabs`. Tabs switch views without a save step.
- **Ordered steps that must finish in sequence**, such as an onboarding or a plan change: use `Stepper`, which shows
  where the person is in the process.

## Usage

Write one `SettingsSection` per section. `Header`, `Icon` and `Badge` appear in the navigation, and the selected section
shows its header, `Description` and content on the right. The first enabled section is selected; set `SelectedIndex`
or call `Select(section)` to show another. `Title` sits above the navigation and `Footer` below it, for example an
account card.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`SettingsPage` derives from Avalonia's `SelectingItemsControl`, and its items are the sections. Selection therefore works
as in any selector: `SelectedIndex`, `SelectedItem` and `SelectionChanged` behave as usual, and `SelectedSection` returns
the shown section as a `SettingsSection`. The page builds the navigation for you, one item per section, and manages the
selection pill, the content area and the save bar, so the sections only describe their content.

### Anatomy

1. **Title** (`Title`, "Settings" by default): medium-weight large text at the top of the navigation column, with 24 px
   of space on the sides.
2. **Navigation column**: 248 px wide, with a background that is the surface color mixed with 2.5% of the foreground
   color, and a 1 px divider on its right edge.
3. **Navigation item** (`Header`, `Icon`, `Badge` of each section): 40 px high with a 2 px gap between items. The icon is
   18 px and the header is small text. An unselected item uses the secondary text color; the selected item uses the
   foreground color with medium weight.
4. **Badge** (`Badge`): a small `Badge` at the end of the item, such as a count of new messages.
5. **Unsaved dot**: a 6 px accent dot in the item when `IsDirty` is set on its section.
6. **Selection pill**: a raised surface with a 1 px subtle border and a resting shadow, with a 12 px corner radius. It
   glides between items.
7. **Footer** (`Footer`): under a divider at the bottom of the column, with 16 px and 14 px of padding. Hidden in the
   compact layout.
8. **Content area**: scrolls on its own, with 36 px of space at the sides and 30 px above. The section's header is
   extra large medium-weight text, its `Description` sits 4 px below in small secondary text, and the content starts
   24 px further down.
9. **Save bar**: a raised pill, centered at the bottom of the content area, with a floating shadow and 18, 5, 5, 5 px of
   padding. It holds the note "Unsaved changes", a ghost Discard button and a primary Save changes button. The content
   keeps 72 px of room at its bottom, so the last row is never hidden behind the bar.

### Unsaved changes

Set `IsDirty` on a section while it has unsaved changes. Its navigation item shows a dot, and while it is selected the save
bar rises at the bottom of the page with Discard and Save. Save (or Ctrl+S, ⌘S on macOS) raises `SaveRequested`, and
Discard raises `DiscardRequested`. Both carry the `Section`. Set `IsDirty` back to `false` once the changes are saved or
reverted.

Set `IsDirty` from the form, not from the act of opening the section: compare the current values with the saved ones, so
the dot disappears when a person types a value back to the original. The page does not stop people from switching
sections. A section that is still dirty keeps its dot in the navigation, and its save bar appears again when it is
selected.

<Demo name="UnsavedChanges" />

```csharp
page.SaveRequested += async (_, e) =>
{
    await SaveProfileAsync();
    e.Section.IsDirty = false;
};
```

### Layout and width

The page is two columns while it is at least `CompactWidth` wide (720 px by default). Below that width it becomes one
column: the title sits above a horizontal row of section pills that scrolls sideways, the footer is hidden, and the
content gets smaller padding (20 px at the sides, 22 px above and 24 px below). `IsCompact` reports the current layout,
and the page carries the `:compact` pseudo class, so a style can adjust the page for it. Choose the width by the room
the page has, not by the screen: a settings page in a 400 px side panel is compact even on a wide monitor.

<Demo name="Compact" />

### Binding to a view model

The page is a selector, so `SelectedSection` is the value to read, and `Select(section)` is the method to call. The
dirty state and the save step belong to the view model. In CommunityToolkit.Mvvm style, the view model compares the
workspace name with the saved value, exposes the result as `HasChanges`, and runs Save and Discard as commands:

```csharp
public partial class WorkspaceSettingsViewModel : ObservableObject
{
    private readonly Func<string, Task> _saveName;
    private string _savedName = "Larkspur Studio";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _workspaceName = "Larkspur Studio";

    public bool HasChanges => WorkspaceName != _savedName;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _saveName(WorkspaceName);
        _savedName = WorkspaceName;
        OnPropertyChanged(nameof(HasChanges));
    }

    public WorkspaceSettingsViewModel(Func<string, Task> saveName) => _saveName = saveName;

    [RelayCommand]
    private void Discard() => WorkspaceName = _savedName;
}
```

```xml
<arc:SettingsPage Title="Workspace">
    <arc:SettingsSection Header="General" Icon="{x:Static arc:ArcIcons.Settings}"
                         Description="Name and address of the workspace."
                         IsDirty="{Binding HasChanges}">
        <arc:Input Label="Workspace name" Text="{Binding WorkspaceName, Mode=TwoWay}" />
    </arc:SettingsSection>
</arc:SettingsPage>
```

The events connect the page's save bar to the commands in code-behind:

```csharp
Page.SaveRequested += (_, _) => ViewModel.SaveCommand.Execute(null);
Page.DiscardRequested += (_, _) => ViewModel.DiscardCommand.Execute(null);
```

Use two-way bindings for the values the sections edit, and one-way bindings for `IsDirty`, `Title` and `Footer`, which the
app sets. Read `SelectedSection` when the app needs the shown section, for example to pick the save action.

### Settings groups and rows

`SettingsGroup` and `SettingsRow` build the content of a section, and work on their own anywhere. A row shows an
optional `Icon`, a `Header` and a `Description`, and the control that changes the setting as its content. Below
`StackWidth` (440 by default) the control moves under the text. When the control is a `Switch` or `Checkbox`, clicking
the text toggles it, and the control takes the header and description as its accessible name and help text.

A group draws a card by default, with an optional `Header`, `Description`, `HeaderActions` and a tinted `Footer` strip.
`Appearance="Plain"` drops the frame for sections that are separated by hairlines inside a page. Inside a section, which
already has a title, prefer `Plain` so the page does not show cards inside a card.

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

### Example: Workspace settings in an admin console

An admin console for Larkspur Studio opens on Members, while General holds an unsaved workspace name. The dot on
General tells the owner that a change is pending, and the save bar stays hidden until General is selected, so the
Members section never offers to save someone else's edit. The page is 720 px wide, so it uses two columns, and the
footer shows the avatar of the person who is signed in. The Members badge shows the size of the workspace from the navigation.

![Workspace settings page on Members, with a dot on the General section that has unsaved changes](images/SettingsPage.png)

```xml
<arc:SettingsPage Width="720" Height="440" Title="Workspace" SelectedIndex="1">
    <arc:SettingsPage.Footer>
        <arc:Avatar DisplayName="Noor Haddad" />
    </arc:SettingsPage.Footer>
    <arc:SettingsSection Header="General" Icon="{x:Static arc:ArcIcons.Settings}"
                         Description="Name and address of the workspace." IsDirty="True">
        <StackPanel Spacing="16">
            <arc:Input Label="Workspace name" Text="Larkspur Studio Labs" />
            <arc:Input Label="Workspace URL" Text="larkspur" Prefix="arc.app/" IsReadOnly="True"
                       Description="Contact support to change the address." />
        </StackPanel>
    </arc:SettingsSection>
    <arc:SettingsSection Header="Members" Icon="{x:Static arc:ArcIcons.Users}" Badge="6"
                         Description="People with access to this workspace.">
        <arc:SettingsGroup Appearance="Plain">
            <arc:SettingsRow Header="Allow invites from members"
                             Description="Otherwise only owners can invite.">
                <arc:Switch IsChecked="True" />
            </arc:SettingsRow>
            <arc:SettingsRow Header="Require approval for new members"
                             Description="An owner confirms each new member before access starts.">
                <arc:Switch />
            </arc:SettingsRow>
        </arc:SettingsGroup>
    </arc:SettingsSection>
</arc:SettingsPage>
```

### Content guidelines

Name each section by the job it does, in sentence case, and keep the title short enough for the 248 px navigation column.
Write each section's `Description` as one sentence that says what the section changes. Use "Save changes" and "Discard"
on the save bar, so the actions read as the operation they perform.

| Instead of | Write |
|---|---|
| Misc | Advanced |
| Your account settings | Profile |
| You have unsaved changes!!! | Unsaved changes |
| Submit | Save changes |
| Revert | Discard |
| Change your billing info | Billing |

Keep the badge short: a count such as "2" or a word such as "New". Do not put a sentence in the badge; the navigation
item has no room for one.

### Best practices

**Do**

- Group the sections by the job a person has, such as Profile, Notifications, Security and Billing, so people know where
  to look before they read the navigation.
- Set `IsDirty` as soon as a value differs from the saved one, and clear it on save or discard, so the dot and the save
  bar always match the form.
- Use `Appearance="Plain"` for groups inside a section, because the section already draws the title and the page.
- Show a `Footer` with the account the settings belong to, so people can see whose workspace or profile they change.
- Keep one save model per page. Save each section with the bar, or save everything at once, so people can tell which
  changes are pending.

**Avoid**

- Saving some sections automatically and others with the save bar. People cannot tell which changes will be kept.
- Setting `IsDirty` on a section without editable fields. The save bar then appears with nothing to save.
- Putting destructive actions, such as deleting a workspace, behind the save bar. Open a `Dialog` from the action, so
  the save flow stays for edits.
- Long sections without a `Description`. The description tells people what the section changes before they read the
  forms.
- Using the page for a single list of options. A `SettingsGroup` in a card is lighter and needs no navigation.

### Common questions

**Why does the save bar not appear when I edit a field in another section?** The bar belongs to the selected section. A
section with unsaved changes keeps its dot in the navigation, and the bar appears when that section is selected.

**Does Ctrl+S save everything?** It raises `SaveRequested` for the selected section only, and only when that section is
dirty. Handle the event for each section you save, or check `e.Section` to choose the save action.

**Which section is shown first?** The first enabled section, unless `SelectedIndex` or `Select(section)` chooses another.
A disabled section is skipped in both the initial selection and the keyboard movement.

**How do I change the Save button text?** Replace the `Labels` object with a `SettingsPageLabels` whose `Save` property
holds the text you need. The other texts keep their defaults unless you set them too.

## API reference

<!-- api: SettingsPage -->
<!-- api: SettingsSection -->
<!-- api: SettingsGroup -->
<!-- api: SettingsRow -->

### SettingsPageLabels

| Property | Type | Description |
|---|---|---|
| `UnsavedChanges` | `string` | The note in the save bar: "Unsaved changes". |
| `Save` | `string` | The save bar's primary button: "Save changes". |
| `Discard` | `string` | The save bar's secondary button: "Discard". |
| `NavigationName` | `string` | The accessible name of the section navigation: "Settings sections". |
| `UnsavedSection` | `string` | The accessible text of the dot on a section with unsaved changes: "has unsaved changes". |

### Event arguments

| Type | Property | Description |
|---|---|---|
| `SettingsSectionEventArgs` | `Section` | The section whose changes are to be saved or discarded. |

## Accessibility

- Each navigation item is a tab item named after its section, followed by its badge and "has unsaved changes" when the
  section has them. The navigation is named "Settings sections".
- The navigation is one tab stop. Up and Down (Left and Right on a compact page) move to the previous or next enabled
  section and show it; Home and End go to the first and last. They do not wrap.
- Ctrl+S (⌘S) saves when the shown section has unsaved changes.
- Keyboard focus draws a 2px ring around the navigation item. Pointer focus does not.
- The note in the save bar is a polite live region.
- The selected item is announced as selected, so a screen reader user hears which section is shown after each move.

### Keyboard

| Keys | Action |
|---|---|
| `Up` / `Down` (`Left` / `Right` when compact) | Move focus to the previous or next enabled section and show it; does not wrap |
| `Home` / `End` | Move to the first or the last section |
| `Enter` / `Space` | Select the focused section |
| `Tab` | Leaves the navigation, which is a single stop on its focused or selected item, and moves into the content |
| `Ctrl+S` (`⌘S` on macOS) | Raise `SaveRequested` when the selected section is dirty |

## Motion

- The selection pill glides between navigation items on the snappy spring.
- The incoming section rises 6px and fades in (standard duration, enter easing) while the outgoing one fades out in place.
- The save bar rises into view and drops away.
- Reduced motion keeps the fades and drops the movement. With motion off, everything changes at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The selection moves to another section | The pill glides on a snappy spring | The pill moves at once | The pill moves at once |
| The shown section changes | The new section rises 6px and fades in; the old one fades out in place | The sections crossfade in place | The new section shows at once |
| The save bar appears or goes | Rises into view and drops away | Fades in and out | Shows or hides at once |

## Related

- `SettingsCommand`, `SecuritySettings`, `BillingOverview`, `UsageBilling` and `CancelFlow`: the other settings blocks,
  which fit inside a section.
- `Tabs`: switch between views of the same object.
- `Switch`, `Input`, `Select`, `BillingToggle`: the controls the sections in the examples are built from.

| Component | Choose it when |
|---|---|
| `SettingsGroup` / `SettingsRow` | One list of options is enough, or a section needs its content |
| `SettingsCommand` | People search for a setting by name and change it in place |
| `Sidebar` | The navigation moves between the areas of the application |
| `Tabs` | Views of one object switch without a save step |
| `Stepper` | Ordered steps must finish in sequence |
| `SecuritySettings` / `BillingOverview` | A section needs a complete security or billing layout |

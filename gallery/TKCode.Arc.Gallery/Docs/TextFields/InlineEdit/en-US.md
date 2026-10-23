---
title: Inline edit
summary: Rename in place: the text becomes a field without moving.
---

<Demo name="Rename" />

## When to use

`InlineEdit` shows a value as plain text and turns it into a field in the same place when the person chooses to edit it.
It fits the names and titles that appear on a page many times and change rarely: a project name in a header, a
dashboard title, a customer's display name on a CRM record. Nothing around the text moves when editing starts, so the
page keeps its layout, and the value saves on its own without a form around it.

- **Project and workspace names in a page header**: "Atlas redesign" at the top of a project page. People read it
  all day and rename it occasionally, so it should look like a title and become editable on activation.
- **Report and dashboard titles in an analytics app**: "Q3 revenue by region" on a dashboard. The title is saved as the
  person commits it, and a failed save returns the last saved title with a retry link.
- **Contact and account names in a CRM or billing screen**: a customer's company name or a contact's display name, where
  an edit is a single correction rather than a form submission.
- **Descriptions and short notes under a heading**: a one-line project summary, or a multiline description with
  `IsMultiline`, where the text wraps and Enter still saves.
- **Incident and ticket titles in an operations console**: a title that the on-call engineer corrects in place while the
  timeline stays where it is.

## When not to use

- **Several fields that save together**: a profile or a billing address is a form. Use `Input` or `Textarea` for each
  field and one save button, so people can review all of it before anything is saved.
- **A field that must always look editable**: a search box, an invoice number on an order that is being entered, or any
  value that people are expected to fill in. Use `Input`, which shows its frame and label at all times.
- **A long body the person writes**: a release note, a support reply or a policy text. Use `Textarea`, which holds a
  larger text and a visible frame while the person types.
- **Numbers and amounts that need parsing**: a price, a quantity or a percentage. Use `NumberField` or `MoneyInput`,
  which parse, format and step the value.
- **A value picked from a fixed list**: a status, an owner or a currency. Use `Select` or `Combobox`, which show the
  options and their state.

## Usage

The value is shown as text. Activating it turns it into a field of the same size. The value is saved when the
edit is committed.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:InlineEdit Label="Project name"
                    Value="Atlas redesign"
                    HeadingLevel="2" />
</UserControl>
```

`InlineEdit` derives from Avalonia's `TemplatedControl`. Its resting text is a button (`Enter` and `Space` start the
edit, so the text is reachable from the keyboard), and its editor is a `TextBox`, so caret, selection, IME composition
and the clipboard shortcuts work as they do in any text box. The control adds the save cycle on top: an optimistic
display of the new text, a saving indicator, a rollback on failure and the validation message.

### Anatomy

1. **Resting text**: the value in a button. The `Title` variant uses the extra-large type size (22 px) in medium
   weight on a 28 px line, with 3 px vertical and 8 px horizontal padding. The `Body` variant uses 14 px in the
   secondary text color on a 21 px line, with 5 px vertical and 8 px horizontal padding. The box has a leading margin
   of -9 px, so the text lines up with the content around it.
2. **Frame**: a box with the control radius. It stays transparent at rest, takes the muted surface color on hover
   while the text is at rest, and becomes a surface-colored box with the foreground-colored border while editing.
3. **Focus ring**: a 2 px ring at 22 px radius around the resting text when it has keyboard focus. While editing, the
   ring is 3 px and sits inside the frame.
4. **Pencil**: a 14 px muted glyph at the end of the text. It appears on hover with a fine pointer, and on keyboard focus.
   It is the visual cue that the text can be edited; touch input goes straight into the editor.
5. **Editor**: a `TextBox` with the same font metrics as the resting text, so the text does not shift when the field
   opens. Its padding adds 24 px at the trailing edge, and its minimum width is 5 em (110 px for `Title`, 70 px for
   `Body`), so the next character never waits for the box to grow.
6. **Action slot**: a reserved area at the trailing edge. While editing it holds the save button (a 28 px round primary
   button with the check glyph) and the cancel button (a 28 px outlined round button with the close glyph). While saving
   it shows a 14 px spinner; after a save it shows a check mark that draws itself; after a failed save it shows a
   danger glyph. A single line keeps 66 px free for the actions and a multiline value keeps 38 px, so the text never
   rewraps when the actions appear.
7. **Message row**: 12 px copy under the field. A validation message uses the danger color. A failed save uses the
   secondary text color and ends with the `RetryText` link, which reopens the field with the failed text.
8. **Status**: a hidden polite region that announces saving progress and the outcome.

### Variants and multiline

`Variant` sets the visual weight. `Title` is the default for names and headings; `Body` suits descriptions.
With `IsMultiline` set, the text wraps and the field grows in height. Enter still saves, and Shift+Enter adds a
line break.

<Demo name="Multiline" />

```xml
<arc:InlineEdit Label="Description"
                Variant="Body"
                IsMultiline="True"
                PlaceholderText="Add a description"
                Value="{Binding Description}" />
```

Use `Title` for text that names something and is read as a heading, and use `Body` for text that describes it. Keep
one variant for each kind of text on a page, so the names line up and the descriptions read as one group. Use
`IsMultiline` only for text that people write in paragraphs; a single-line name that wraps is a sign that it is too
long for the place it sits.

### Binding

`Value` is two-way. A new value from outside replaces the text while the person is not editing it.

```xml
<arc:InlineEdit Label="Project name"
                Value="{Binding ProjectName}"
                HeadingLevel="2" />
```

### Binding to a view model

In an MVVM app, the view model owns the saved value and the rules, and the control handles the editing. `Value` is
the two-way binding. `SaveHandler` and `Validator` are delegates, so they are assigned in the view's code-behind
with the view model's methods. Setting them in XAML is not possible for delegates.

```csharp
// The application's own service; it is not part of the control library.
public interface IProjectService
{
    Task RenameAsync(ProjectId id, string name);
}

public partial class ProjectHeaderViewModel : ObservableObject
{
    private readonly IProjectService _projects;
    private readonly ProjectId _projectId;

    public ProjectHeaderViewModel(IProjectService projects, ProjectId projectId)
    {
        _projects = projects;
        _projectId = projectId;
    }

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public string? ValidateName(string value)
        => value.Length switch
        {
            0 => "A project needs a name.",
            > 80 => "Use 80 characters or fewer.",
            _ => null
        };

    // The control sets Value to the saved text when this task completes, so the binding updates the property.
    public Task SaveNameAsync(string name) => _projects.RenameAsync(_projectId, name);
}
```

```csharp
// In the view's code-behind, after InitializeComponent and after the DataContext is set.
ProjectTitle.Validator = viewModel.ValidateName;
ProjectTitle.SaveHandler = viewModel.SaveNameAsync;
```

```xml
<StackPanel Spacing="8">
    <arc:InlineEdit x:Name="ProjectTitle"
                    Label="Project name"
                    Value="{Binding ProjectName, Mode=TwoWay}"
                    HeadingLevel="1" />
    <arc:InlineEdit Label="Description"
                    Variant="Body"
                    IsMultiline="True"
                    PlaceholderText="Add a description"
                    Value="{Binding Description, Mode=TwoWay}" />
</StackPanel>
```

- `Value` is two-way, so the saved name reaches the view model when the save lands. The control does not write the value
  before a save succeeds, so a failed save leaves the view model's property unchanged.
- A value from outside does not replace the text while the person is editing or while a save is running, so avoid
  pushing refreshed data into the view model during an edit.
- The code-behind names (`x:Name`) belong to the view. Keep the handlers in the view or in the view model; the control
  only calls them.

### Save and validation

`SaveHandler` persists the committed value. It runs while the field shows the saving state. A faulted task rolls
the text back and shows a retry link. `Validator` returns a message when the draft cannot be saved; the message
is shown under the field.

```csharp
title.Validator = value => value.Length == 0 ? "A project needs a name." : null;

title.SaveHandler = async next =>
{
    await projects.RenameAsync(projectId, next);
};
```

Handle `TextCommitted`, `Saved` and `SaveFailed` to react to the outcome.

```csharp
title.TextCommitted += (_, e) => log.Text = $"Committed “{e.NewValue}” (was “{e.OldValue}”)";
title.Saved += (_, e) => log.Text = $"Saved “{e.NewValue}”";
title.SaveFailed += (_, e) => log.Text = $"Save failed: {e.Error.Message}";
```

The save cycle has four steps, and the control exposes each one through `Phase` (`Idle`, `Saving`, `Saved`, `Failed`):

1. **Commit**: the text is trimmed (a single line also collapses inner whitespace), validated and, if it differs from
   the shown value, sent to `SaveHandler`. `TextCommitted` is raised with the previous and the new value. A commit
   that does not change the text closes the field without a save and without an event.
2. **Saving**: the new text shows at once at half opacity with a spinner. `Value` does not change yet. The field cannot
   be opened again until the save ends.
3. **Saved**: `Value` becomes the new text, `Saved` is raised and a check mark draws. The phase returns to `Idle` after
   about 1.8 seconds. Without a `SaveHandler`, the value is saved at once and the same events follow.
4. **Failed**: the shown text returns to the last saved value, `SaveFailed` is raised with the attempted text and the
   exception, and the frame flashes danger for a moment. The message row says why, using `FailedMessageFormat`, and
   offers `RetryText`. Choosing it reopens the field with the failed text and the caret at its end.

`ErrorMessage` holds the validation message while it applies. A validation failure keeps the field open with the
person's text, sets the invalid state and keeps focus in the field when the commit came from `Enter` or the save button.
The message clears when the text changes and validates again.

```csharp
// The server is the last word: its error shows as a validation message, and the next edit clears it.
title.SaveHandler = async next =>
{
    var result = await projects.RenameAsync(projectId, next);
    if (!result.IsSuccess)
    {
        throw new InvalidOperationException(result.Error);
    }
};
```

### Empty

A placeholder keeps the line until there is a value.

<Demo name="Empty" />

`PlaceholderText` appears in the muted color while `Value` is empty, and the field stays as tall as a filled one, so the
layout does not jump when a value is added. Use a placeholder that says what to add ("Add a subtitle"), not a generic
prompt.

### Saving and leaving the field

Enter saves, Escape cancels, and leaving the field saves. The rules are the same whichever way the person leaves:

- **Enter** commits. In a multiline field, Shift+Enter inserts a line break instead.
- **Escape** cancels: the field closes, the saved text returns and `EditCanceled` is raised.
- **Tab, or a click outside the field** commits, the same as Enter. Focus moving to the save or cancel button does not
  commit, because the buttons belong to the control.
- **Switching to another window** does not commit. The field stays open and commits when the person returns and leaves it.
- **A validation message** keeps the field open when the commit comes from a blur as well, so the person sees the message
  before the field closes.

The Save button and the Cancel button in the action slot give the same result as Enter and Escape. The code can do the
same with `BeginEdit`, `CommitEdit` and `CancelEdit`. `BeginEdit(caret)` opens the field with the caret at a position,
and `-1` (the default) selects all the text, ready to retype.

### Example: project page header

A project page for an analytics workspace. The project name is a `Title` heading, the description is a multiline `Body`
text, and a third line holds the subtitle, which is still empty and shows its placeholder. The text looks like the
page's own content, not like a form, so the header reads as a title until someone chooses to edit it. The metadata row
below the description uses plain text, because owner and dates are read-only here. The header keeps the same layout in
both states: the frame and the text share the same metrics, so the title does not move when it becomes a field.

![A project header with an inline title, a multiline description and an empty subtitle placeholder](images/ProjectHeader.png)

```xml
<StackPanel Width="620" Spacing="8">
    <arc:InlineEdit Label="Project name"
                    Value="Atlas redesign"
                    HeadingLevel="1" />
    <arc:InlineEdit Label="Subtitle"
                    Variant="Body"
                    PlaceholderText="Add a subtitle" />
    <arc:InlineEdit Label="Description"
                    Variant="Body"
                    IsMultiline="True"
                    PlaceholderText="Add a description"
                    Value="Rebuild the analytics home with clearer status, faster search and one place for every report." />
    <TextBlock Text="Owner Ada Lovelace · Created 3 Mar 2026 · Updated 2 hours ago"
               FontSize="12"
               Margin="0,8,0,0"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### Content guidelines

- **Label**: the name of the field in sentence case ("Project name", "Report title"). It is the accessible name, and it
  appears in the saving announcements ("Saving project name").
- **Placeholder**: an instruction that names the content to add ("Add a subtitle", "Add a description"). Do not use
  "Click to edit", because the text itself already signals that it can be changed.
- **Validation message**: say what is wrong and what is allowed, in the same terms as the field ("A project needs a
  name.", "Use 80 characters or fewer.").
- **Failed-save sentence**: the default format reads "Couldn’t save “{0}”, so the last saved {1} is back." Keep it when
  the default reads well; change `FailedMessageFormat` only to name the object more precisely.
- **Retry text**: a verb ("Try again" by default). Keep it short, because it sits at the end of the message row.

| Instead of | Write |
|---|---|
| Click to edit | Add a description |
| Name is required!!! | A project needs a name. |
| Error saving | Couldn’t save “Atlas”, so the last saved project name is back. |
| Enter text here | Add a subtitle |

### Best practices

**Do**

- Use `Title` for names and headings and `Body` for descriptions, and keep one variant per kind of text on a page, so
  the page reads in a consistent order.
- Set `HeadingLevel` on the title of a page or a section, so a screen reader user can jump to it by heading. Leave it
  at `0` for text that is not a heading.
- Validate the rule on the client with `Validator`, and show a server rejection through a failed `SaveHandler` with a
  clear message. The control holds the person's text while the field shows the error.
- Give each edit its own `Label`, so the announcements name the thing that was saved.

**Avoid**

- Using it for a form. Several fields that save together need `Input` or `Textarea`, where people can review every
  value before they save, and where a cancel discards all of them.
- Saving without a `SaveHandler`. Without one the value is saved at once, with no error path; add a handler whenever the
  change goes to a server, so failures roll back and the person knows.
- Long single-line names that wrap or truncate in the header. Keep titles short, or use `IsMultiline` for text that
  people write as paragraphs.
- Opening the field from code during a save. The control refuses to open while a save runs, so the call does nothing;
  check `Phase` before you open it.

### Common questions

**Why did my new name go back to the old one?** The save failed. `SaveHandler` threw an exception, the text rolled back
to the last saved value, and the message row offers "Try again". Handle `SaveFailed` to log `e.Error` and to tell the
person what went wrong.

**Why does the field stay open after I press Enter?** `Validator` returned a message. The field keeps the person's text,
marks itself invalid and shows the message. The message clears when the text changes and passes validation. A blur with a
validation message also keeps the field open.

**How do I open the field or commit it from code?** Call `BeginEdit()` to open it with all the text selected, or
`BeginEdit(caret)` to put the caret at a position. Call `CommitEdit()` to save and `CancelEdit()` to discard. Check
`IsEditing` first, because the calls do nothing when the field is not open.

**Does a refreshed value from the server overwrite what the person is typing?** No. A value from outside does not
replace the text while the person is editing or while a save runs. Hold the refresh until the edit has ended.

**Can I use it for a value with a fixed format, such as an amount or a date?** Not directly. The control holds a
string and saves what the person typed. Use `NumberField` or `MoneyInput` for amounts, and `DatePicker` for dates, so the
value is parsed before it reaches the view model.

## API reference

<!-- api: InlineEdit -->

## Accessibility

- The resting text is a button named with the label and the value, for example "Project name: Atlas redesign". With an
  empty value, the name uses the placeholder. Its help text is "Activate to edit."
- `HeadingLevel` reports the text as a heading to assistive technology. Use `1` to `3` for a page or section title, and
  `0` (the default) for text that is not a heading. Support for the heading level depends on the platform.
- The editor is named by `Label`. Its help text says what the keys do ("Enter saves, Escape cancels."), and while a
  validation message applies, the message replaces that help text.
- Validation messages and save errors are in a polite message row, and saving progress and the result are announced
  through a polite status region ("Saving project name", "Project name saved").
- The save and cancel buttons are named "Save project name" and "Cancel editing"; the retry link reads "Try again".
- Enter, the save button and Escape return focus to the resting text, so a keyboard user continues from the same place.
  A blur (Tab or a click elsewhere) leaves focus where the person moved it.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` or `Space` on the text | Opens the field with all of the text selected, ready to retype |
| `Enter` in the field | Commits and saves; with a validation message, the field stays open |
| `Shift+Enter` (multiline) | Adds a line break |
| `Escape` in the field | Cancels, restores the saved text and returns focus to the text |
| `Tab` in the field | Commits and saves; focus moves to the next control |
| `Left` / `Right`, `Home` / `End`, `Shift` selections | Move the caret and extend the selection, as in a text box |
| `Ctrl+A`, `Ctrl+C`, `Ctrl+X`, `Ctrl+V` (`Cmd` on macOS) | Select all, copy, cut and paste in the field |

## Motion

- The old value scrolls out and the new one rises in with a soft blur. A failed save rolls back the other way and
  briefly tints the frame.
- The width follows the draft while typing, and the box changes height smoothly when it is multiline.
- While saving, a small spinner turns. When the save lands, a check mark is drawn.
- Reduced motion cross-fades the text, draws the check at once and replaces the spinner with a static mark. Off does
  the same without the fade.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Value replaced from outside, or a save lands | The new text rises in from below with a soft blur; the old text moves up and fades | The text cross-fades | Immediate |
| Opening or closing the field | The frame's width and border spring into place; the text and actions switch with the frame | The text switches at once | Immediate |
| Typing changes the width | A quick spring without overshoot (about 0.16 s) | Immediate | Immediate |
| Saving | The spinner turns continuously and the text is at half opacity | A static mark replaces the spinner | Same as reduced |
| Save lands | The check mark draws over about 0.32 s after a short delay | The check is drawn at once | The check is drawn at once |
| Save fails | The old text drops back in from above; the frame washes with a danger tint that fades | The text cross-fades; the tint clears without a fade | Immediate |
| Hover and keyboard focus | The pencil fades in and the frame takes the hover tint | Immediate | Immediate |

## Related

- `Input` for a field that is always visible and saved with a form.
- `Textarea` for a longer body text inside a form.
- `ExpandingSearch` for a control that opens a search field and results.

| Component | Choose it when |
|---|---|
| `Input` | The field is always visible, has a label, and is saved with other fields |
| `Textarea` | The text is a long body that people write and review before saving |
| `NumberField` / `MoneyInput` | The value is a number or an amount that needs parsing and formatting |
| `Select` / `Combobox` | The value is one of a known list of options |
| `ExpandingSearch` | The person searches for a record or a page from a header |
| `InlineEdit` | A title or a name is renamed where it is shown, and saved on its own |

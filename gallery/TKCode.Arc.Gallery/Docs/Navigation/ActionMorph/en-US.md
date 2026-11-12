---
title: Action morph
summary: A corner button for the few entries people add most: it opens into a short list of actions, the chosen action reshapes the same surface into a small form, and a successful save shrinks it to the button with a check mark.
---

<Demo name="Preview" />

## When to use

`ActionMorph` lets people add a new entry, such as a to-do or a note, from a round button that stays in a corner of
the page. A press does not bring up a dialog. The button itself expands into a short list of actions, and choosing one
reshapes that same surface into a compact form with a title, a text field, an optional row of options and a submit
button. Once the entry is saved, the form shrinks back into the button, the button takes the success color and a check
mark is stroked onto it; shortly after, the plus comes back. Text typed into a form survives going back a level,
closing the surface or clicking somewhere else.

- **Adding something in passing**: a to-do, a note or an event, while the list it goes into stays in view.
- **Two to four kinds of new item** that people add many times a day and that should always be one press away.
- **Narrow screens**, where a form that grows out of a corner button takes less room than a dialog.

## When not to use

- **Entries with more than one text field**: put them in a `Dialog` or a `Drawer`.
- **Commands that take no text**: a `DropdownMenu` runs them straight from the list.
- **Saving what is already on screen with one button**: an `ActionButton` shows the busy and done states itself.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Panel>
        <!-- The page content goes first; the morph sits last, in a corner. -->
        <arc:ActionMorph HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="16">
            <arc:ActionMorphAction Label="To-do"
                                   Description="Something to finish"
                                   Glyph="{x:Static arc:ArcIcons.ListTodo}"
                                   Shortcut="d"
                                   Title="New to-do"
                                   Placeholder="What needs to get done?"
                                   ChoicesLabel="When"
                                   SubmitLabel="Add to-do"
                                   SuccessLabel="To-do added">
                <arc:ActionMorphAction.Choices>
                    <x:String>Today</x:String>
                    <x:String>This week</x:String>
                </arc:ActionMorphAction.Choices>
            </arc:ActionMorphAction>
            <arc:ActionMorphAction Label="Note"
                                   Glyph="{x:Static arc:ArcIcons.FileText}"
                                   Shortcut="n"
                                   Title="New note"
                                   IsMultiline="True"
                                   SubmitLabel="Save note"
                                   SuccessLabel="Note saved" />
        </arc:ActionMorph>
    </Panel>
</UserControl>
```

`ActionMorph` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. The morph does
not place itself. It takes only the 56 px button in the layout, and its menu and forms grow from the button's corner
over whatever is around it, so put it last in a panel, at a corner, with a margin.

Each `ActionMorphAction` is one menu row and the form it opens: `Label`, an optional `Description`, a `Glyph` and an
optional one-letter `Shortcut`, then the form's `Title`, `Placeholder`, `IsMultiline`, an optional `ChoicesLabel` with
`Choices` (shown as a radio group, `DefaultChoice` picked first), `SubmitLabel`, `SuccessLabel` and `EmptyHint`.
`Id` and `Tag` are yours to tell the actions apart. `Label` on the morph names the button and titles the menu.

### Saving what was submitted

`SubmitHandler` receives an `ActionMorphSubmission` (the action, the trimmed text and the picked option) with a
cancellation token. While its task runs, the submit button shows a spinner and the field cannot be edited; when it
throws, the form stays up, keeps the draft and shows `ErrorMessage`. Going back or closing the form cancels the token.
After a submission is accepted, `Submitted` is raised and the button shows the check for `ResetAfter` (1.6 s).

```csharp
morph.SubmitHandler = async (submission, token) =>
{
    await plans.SaveAsync(submission.Action.Id, submission.Text, submission.Choice, token);
};
morph.Submitted += (_, e) => list.Insert(0, e.Submission.Text);
```

An empty field is not submitted: the field is marked, gives a small shake and shows the action's `EmptyHint` (or the
morph's). Typing clears the message. Each action keeps its own draft until it is submitted.

### Opening it from code

`IsExpanded` is two-way. `Expand()`, `Collapse()`, `OpenComposer(action)` and `Back()` move between the faces, `View`
and `CurrentAction` tell which face shows, and `Expanded` and `Collapsed` are raised as the button opens and folds.

### The other corner

`Corner="Start"` is for a button in the bottom-left corner: the surface then grows up and to the right.

<Demo name="Corner" />

### Best practices

**Do**

- Offer two to four actions: the ones used most on that page.
- Give every action a letter shortcut and a short description.
- Let `SubmitHandler` await the actual save; when it throws, the form keeps the text and shows the error.

**Avoid**

- Forms that need more than a line or a few lines of text and one choice.
- Putting it where the open menu would cover the content people need while they type.

## API reference

<!-- api: ActionMorph -->

## Accessibility

- The button is named by `Label` and reports that it opens a menu and whether it is expanded. Once a submit succeeds,
  the action's `SuccessLabel` is appended to the button's name and read out through a polite status message.
- The open menu is a menu named by `Label`; its rows are menu items named by the action, with the description as help
  text and the shortcut as accelerator key.
- A form is a group named by its title. Its field is named by the title too; when a submit is empty or fails, the
  field is marked invalid and described by the message, which is announced politely. The options are a radio group
  named by `ChoicesLabel`.
- A face takes the focus as it appears (the menu's first row, or a form's field); when the surface shrinks to the
  button, the button gets it back.
- The keyboard ring shows around the button and the form's buttons and options after keyboard use only; the menu's
  highlight shows which row is active.

### Keyboard interactions

| Key | Action |
|---|---|
| Enter or Space on the button | Shows the menu with its first action focused. |
| Down / Up | Next or previous action; after the last comes the first again, and before the first the last. |
| Home / End | First or last action. |
| A shortcut letter, menu open | Goes straight to that action's form. |
| Enter in a single-line field | Sends the form. |
| Ctrl+Enter (⌘+Enter on macOS) in a multiline field | Sends the form; Enter alone starts a new line. |
| Arrow keys in the options | Pick the next or previous option; only the picked one is a tab stop. |
| Escape | One level up: a form returns to the menu with its action focused, the menu returns to the button. |

## Motion

- One surface carries all three faces. Its width, height and corners follow a spring: going deeper settles in about
  0.46 s, going back in about 0.38 s, both without overshoot. Its fill, edge and shadow change over 240 to 480 ms.
- A face comes in with a short fade from a 4 px blur. Between the button and a panel it grows from 90 % at the corner;
  between panels the deeper face slides in from the right while the old one is pushed left, and back the other way.
- Menu rows rise 8 px one after another, 35 ms apart, and the highlight slides to whichever row is active.
- While a submission saves, a spinner replaces the submit label. Once it is saved the form shrinks into the button,
  the button fills with the success color and its check mark is stroked in; when `ResetAfter` runs out, the check
  fades away and the plus rotates into place again.
- When a submit is empty or fails, the field gives one sideways nudge.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Surface size and corners | Spring | Jumps | Jumps |
| Faces | Fade, blur and scale or slide | 100–160 ms fade | Swap |
| Menu rows | Staggered rise | All at once | All at once |
| Press, shake | Scale 0.94, nudge | None | None |
| Check | Springs in and draws | Fades in | Shown |

## Responsive behavior

- Widths: 256 px for the menu and 320 px for a form, never more than the viewport less 32 px; a 320 px phone shows
  either one in full.
- The round button measures 56 px. Only a mouse or pen produces hover colors; touch input never does.
- The surface opens upward and away from the `Corner` side; leave that room free for the form.

## Related

- `OrbitMenu` fans a few actions out around a button without a form.
- `DropdownMenu` lists actions that run at once.
- `Dialog` and `Drawer` hold longer forms.

| Component | Choose it when |
|---|---|
| `ActionMorph` | Two to four create actions each need a line of input, right from a corner button |
| `OrbitMenu` | A few actions run at once from one button |
| `Dialog` | The task needs several fields or a confirmation |

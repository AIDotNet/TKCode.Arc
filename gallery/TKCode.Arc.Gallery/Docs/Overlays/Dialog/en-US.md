---
title: Dialog
summary: A focused surface for decisions that need attention.
---

<Demo name="ShortForm" />

## When to use

A `Dialog` is a centred, modal panel for a decision or a short task that must be finished before the person returns to
the page. It holds the question, the explanation and the buttons in one place, dims the page behind it and keeps focus
inside until the person answers. Use it where an interruption is intended, and keep it small enough that the
person can read the whole panel without scrolling.

- **Destructive confirmations** in an admin console: delete a project, revoke an API key or cancel a subscription.
  The dialog states the consequence ("This permanently deletes Atlas redesign and its 14 files") and puts the
  destructive action in the `Danger` variant next to Cancel.
- **Short forms** that fit one panel: rename a workspace, invite a teammate by email with a role, or set a payout
  reference. The fields are few, and the save or cancel decision is immediate.
- **Multi-step flows in one panel**: an onboarding step such as "Invite your team" moves through its steps by
  changing `Title` and `Description`, so the person keeps one surface and the copy crossfades in place.
- **Acknowledgements before continuing**: a billing or policy change that the person must read, such as a new
  price for the Team plan, shown before they can open the invoice page.
- **Prompts from code**: a bulk action on a table, such as "Archive 42 invoices", asks for confirmation through
  `Dialog.ConfirmAsync` and continues only when it returns `true`.

## When not to use

- **Long forms or record details**: use `Drawer`, which scrolls its body and keeps the page visible at its edge.
- **Mobile-first tasks that rest at a peek height**: use `BottomSheet`, which opens partly and expands on a drag.
- **Lightweight content that should not block the page**: use `Popover`, anchored to its trigger without a scrim.
- **A list of commands**: use `DropdownMenu` or `ContextMenu`, which keep the options next to the control they act on.
- **A message that stays with the page** until it is resolved: use `Alert`. A short result that should disappear on
  its own belongs in `Toast`.

## Usage

A `Dialog` derives from `ModalOverlay`, which itself is a `ContentControl`. Its `Content` is the body, `Title` and
`Description` form the header, `Actions` is the footer, and `IsOpen` shows or hides it. The control takes no space
where it is declared. While `IsOpen` is true, its panel is shown on the window's overlay layer above a blurred scrim,
so the dialog works from any position in the XAML tree and does not disturb the layout around it.

Declare a `Dialog` next to its trigger. It renders nothing in place and shows its panel while `IsOpen` is true.

### Declared in XAML

Put the body in the content and the buttons in `Actions`. A button with `arc:Dialog.Result` closes the dialog with
that value.

```xml
<arc:Dialog Title="Rename project"
            Description="Choose a short name your team will recognize."
            IsOpen="{Binding IsRenaming}">
    <arc:Input Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="Cancel" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="Save" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### Anatomy

1. **Title** (`Title`): the large type size at medium weight in the foreground color. A change while the dialog is
   open rises in with a soft blur; the old copy leaves.
2. **Description** (`Description`): the small type size in the secondary text color, 8 px under the title. It is
   hidden when empty.
3. **Close button** (`ShowCloseButton`, `CloseButtonName`): a 32 px button with a 1 px border and the control radius,
   aligned to the first line of the title. Its fill is the muted surface color at 50% opacity. It is named
   "Close dialog" by default.
4. **Header rule**: a 1 px line in the border color under the header.
5. **Body** (`Content`): 24 px padding on every side. The whole panel scrolls when it is taller than the window.
6. **Actions** (`Actions`): right aligned, 24 px below the body. It is hidden when `Actions` is null.
7. **Panel**: the raised surface color, a 1 px border, the surface radius and the floating shadow. Its width is the
   size preset (see Sizes), never wider than the window minus a 16 px gutter on each side.
8. **Scrim**: a near-black dim layer at 46% opacity in both appearances, with a 7 px background blur. The blur is turned off
   when the reduce-transparency setting is on.

### Confirmation from code

`Dialog.ConfirmAsync` builds the dialog, waits for a choice and returns `true` only for the confirm button. Escape,
the scrim and the close button return `false`. Set `isDanger` to `true` for a destructive action, so the confirm
button uses the `Danger` variant.

<Demo name="Confirmation" />

`Dialog.ShowAsync` takes custom content and a list of `DialogAction` values, and returns the result of the clicked
action, or `null` when the dialog is dismissed. Use it when the question needs a body that a confirmation cannot
express, such as a choice between three export formats.

```csharp
var choice = await Dialog.ShowAsync(this, "Export invoices", exportForm,
    "Choose a format. The file is generated on the server.",
    new DialogAction("Cancel", false),
    new DialogAction("Export", "csv"));
```

The companion record `DialogAction` describes one button of a code-built dialog.

| Member | Type | Meaning |
|---|---|---|
| `Label` | `string` | The button text |
| `Result` | `object?` | The value `ShowAsync` returns when this button is clicked |
| `Variant` | `ButtonVariant` | `Secondary` (the default) for dismissive actions; `Primary` or `Danger` for the main one |

### Step titles

Change `Title` and `Description` while the dialog is open. The new copy crossfades in place, so a step flow needs
no second dialog. Keep the step count in the body or in a small status line rather than the title, so the title can
change its wording without changing its length by much.

<Demo name="Steps" />

```csharp
dialog.Title = "Choose a role";
dialog.Description = "Editors can change content; viewers only read.";
```

### Sizes

`Size` sets the panel width: `Small` is 360 px, `Medium` is 440 px (the Arc default) and `Large` is 560 px. A panel
never exceeds the window minus a 16 px gutter on each side, so a narrow window shrinks every size to fit.

<Demo name="Sizes" />

```xml
<arc:Dialog Title="Workspace limits" Size="Large" IsOpen="{Binding ShowLimits}">
    <TextBlock Text="Limits apply per workspace." />
</arc:Dialog>
```

Use `Small` for a single question with one or two buttons, such as a delete confirmation. `Medium` is the default for
forms and settings and suits most short tasks. `Large` fits a form with several fields or a table of changes that
the person must review, such as the plan limits before a downgrade. Keep one size per kind of decision across the
application, so people learn where the buttons appear.

### Closing and results

`Closing` is raised before a user dismissal and can be cancelled. `Closed` reports the reason and the result.
A button with `arc:Dialog.Result` sets the result and closes the dialog with the reason `Action`. A button with a
`Command` and no result does not close the dialog by itself; the view model sets `IsOpen` to `false` when the work
succeeds, which keeps the dialog open while the save runs and reports the reason `Programmatic`.

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Reason == DismissReason.PressOutside && isSaving)
    {
        e.Cancel = true;
    }
};
dialog.Closed += (_, e) => status.Text = e.Result is true ? "Saved" : "Closed by " + e.Reason;
```

### Binding to a view model

`IsOpen` is two-way by default, so a dismissal by Escape, the scrim or the close button writes `false` back to the
view model. `Title` and `Description` are one-way: the view model owns the copy and the dialog displays it. Use
`Command` on the action buttons for work that can fail, and a result attribute only for answers that need no work.
Compiled bindings need `x:DataType` on the view; the examples leave it out for brevity.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public interface IProjectService
{
    Task RenameAsync(string name);
}

public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly IProjectService _projects;

    public ProjectSettingsViewModel(IProjectService projects) => _projects = projects;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [RelayCommand]
    private async Task SaveNameAsync()
    {
        IsSaving = true;
        try
        {
            await _projects.RenameAsync(ProjectName);
            IsRenaming = false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;
}
```

```xml
<arc:Dialog Title="Rename project"
            Description="Choose a short name your team will recognize."
            IsOpen="{Binding IsRenaming}">
    <arc:Input Label="Project name" Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary"
                        Content="Cancel"
                        Command="{Binding CancelRenameCommand}" />
            <arc:Button Content="Save name"
                        Command="{Binding SaveNameCommand}"
                        IsEnabled="{Binding !IsSaving}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

The dialog closes only when the view model sets `IsRenaming` to `false`. If the rename fails, the dialog stays open
with the typed name, and the view model can show the error in the input's `ErrorMessage`.

### Example: subscription cancellation in a billing console

A billing page offers "Cancel subscription" for the Team plan. The dialog states what happens on the next billing
date in plain words, and the buttons read "Keep plan" and "Cancel subscription". The keep action is the safe default
and sits on the left in the secondary variant; the destructive action is on the right in the danger variant. Both
buttons set a result, so the caller reads one value and does not need to know which button was used. Escape and the
close button dismiss the dialog without a result, so a handler that checks for `true` treats them like Keep plan.

```xml
<arc:Dialog Title="Cancel the Team plan?"
            Description="Acme Analytics keeps access until 31 October 2026. No refund is issued for the current period."
            Size="Small"
            IsOpen="{Binding IsCancelling}">
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary"
                        Content="Keep plan"
                        arc:Dialog.Result="{x:False}" />
            <arc:Button Variant="Danger"
                        Content="Cancel subscription"
                        arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### Content guidelines

A dialog asks one question, and its title is the question or the action. Write the title in sentence case. A
confirmation title ends with a question mark, and the description says what changes, with the object named and any
count as a digit. Button text is a verb and an object, so the person reads the outcome before they press it. Avoid
"OK", "Yes" and "No" on their own; they force the person to reread the title to know what they agree to.

| Instead of | Write |
|---|---|
| Are you sure? | Delete project? |
| OK | Delete project |
| Yes / No | Cancel subscription / Keep plan |
| Something went wrong | Could not save the name. Try again. |
| Click here to continue | Continue to billing |
| Delete 42 items? | Archive 42 invoices? |

### Best practices

**Do**

- Put the destructive action on the right in the `Danger` variant and the safe action on the left in the `Secondary`
  variant. People read left to right and the primary position is where they expect the commit.
- Name the object in the title and the button ("Delete project?" and "Delete project"). A reader who skims only the
  buttons still knows what they agree to.
- Use `Dialog.ConfirmAsync` for a yes or no question that needs no custom body, so the result is one `bool` and the
  call site stays short.
- Keep the close button and Escape working. They are the keyboard and assistive technology exits, and the close
  button is the only visible way out for a pointer user who does not know Escape.

**Avoid**

- Long forms in a dialog. The body scrolls, but a scrolling panel hides the actions below the fold; a `Drawer` keeps the
  page in view and the form in a longer panel.
- Disabling Escape with `CloseOnEscape` to prevent a loss of work. Handle `Closing` for the `Escape` reason and cancel
  it while the save runs, which keeps the keyboard exit available once the work finishes.
- Stacking a dialog on another dialog for a single follow-up question. Each layer adds a scrim and a focus trap, and
  only the topmost answers Escape, so the person has to close two panels to return.
- Closing the dialog from a command before the work succeeds. A failed call then leaves the person with no dialog and no
  error; set `IsOpen` to `false` only after the work succeeds.

### Focus and dismissal

Focus moves into the dialog when it opens, and the first focusable element receives it unless `InitialFocus` on the
presence is set. While the dialog is the topmost modal, Tab and Shift+Tab stay inside it. When it closes, focus returns
to the control that had it before the dialog opened, which is the button that started the flow.

Escape closes the dialog when `CloseOnEscape` is true (the default). A press on the scrim closes it when
`CloseOnOutsidePress` is true. Both reasons pass through `Closing`, so the application can cancel them while a save
runs. Set `IsModal` to `false` only when the page must stay usable beside the dialog; the scrim and focus trap are
then removed. Stacked dialogs are allowed: only the topmost one answers Escape, so pressing it closes the inner
dialog and leaves the outer one open.

### Common questions

**Why does Escape do nothing while my dialog is saving?** The application cancels the `Closing` event for the
`Escape` reason while the save runs. Set `CloseOnEscape` to `False` only when the dialog must never close from the
keyboard, because it removes the keyboard exit entirely.

**Why is the result null when the person closed the dialog?** `ShowAsync` and `ConfirmAsync` return `null` for Escape,
the scrim and the close button; they return the `Result` of the clicked action otherwise. Check for the value you
expect instead of comparing against `null`.

**Does a button with a `Command` close the dialog?** No. Only a button with `arc:Dialog.Result` closes it. For a
command that can fail, set `IsOpen` to `false` in the view model after the work succeeds.

**How do I make Enter press the primary button?** Set `IsDefault="True"` on the primary button in `Actions`. Enter in
the dialog then presses that button, as in any Avalonia window.

**Can I open a dialog from a control that is not in a window yet?** Use `ShowAsync(owner)` with a visual that is in a
window. The dialog finds the window through that owner and joins the logical tree while it is open.

## API reference

<!-- api: Dialog -->

## Accessibility

- The panel takes its name from `Title` and its help text from `Description`.
- The close button is named "Close dialog" by default. Change it with `CloseButtonName`.
- Focus moves into the dialog when it opens and Tab and Shift+Tab stay inside it. Focus returns to the control that opened
  the dialog when it closes.
- Escape closes the dialog and returns focus to the control that opened it, when `CloseOnEscape` is true.
- The dialog is a modal surface: the page behind it is not reachable by keyboard while it is open.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the next or previous control inside the dialog, wrapping at the ends |
| `Enter` or `Space` on a button | Press the focused button; a button with `arc:Dialog.Result` closes the dialog with that result |
| `Escape` | Close with reason `Escape`, unless `CloseOnEscape` is false or `Closing` cancels it |

## Motion

- The scrim fades in while the panel rises 8 px from a scale of 0.96 on a smooth spring. Closing is shorter: the panel
  moves 4 px and scales to 0.98, and both fade quickly. Closing starts from wherever the entrance is, so a dialog
  closed during its entrance does not jump.
- A changed title or description rises in with a soft blur.
- Reduced motion keeps only a short opacity fade. Off shows or removes the dialog at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening | Scrim fades in; the panel rises 8 px from 0.96 on a smooth spring while it fades in | Short opacity fade | Immediate |
| Closing | Panel moves 4 px and scales to 0.98 while both fade out quickly | Short opacity fade | Immediate |
| Title or description changes | The new copy rises in with a soft blur | Instant fade | Immediate |
| Close button press | The button scales to 0.96 and springs back on release | No scale | No scale |

## Related

- `Drawer` for long forms and detail panels that keep the page in view.
- `BottomSheet` for mobile-first tasks that rest at a peek height.
- `Popover` for interactive content that does not block the page.
- `Alert` for a message that stays in the page, and `Toast` for a short result.

| Component | Choose it when |
|---|---|
| `Drawer` | The form or the details are too long for one panel, or the page must stay visible |
| `BottomSheet` | The task is mobile-first and reads well at a peek height |
| `Popover` | The content is small, anchored to one control and does not need a scrim |
| `DropdownMenu` / `ContextMenu` | The content is a list of commands rather than a question |
| `Alert` | A message stays in the page until it is resolved |
| `Toast` | A short result, such as "Project renamed", should disappear on its own |

---
title: Dialog
summary: A focused surface for decisions that need attention.
---

## When to use

- Confirmations that must be answered before the user continues, such as deleting a project.
- Short forms that fit one panel, such as a rename or an invite.
- Step flows where the title and description change between steps.

## When not to use

- Long forms or record details: use `Drawer`.
- Mobile-first tasks that rest at a peek height: use `BottomSheet`.
- Lightweight content that should not block the page: use `Popover`.
- Lists of commands: use `DropdownMenu`.

## Usage

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

### Confirmation from code

`Dialog.ConfirmAsync` builds the dialog, waits for a choice and returns `true` only for the confirm button. Escape,
the scrim and the close button return `false`.

```csharp
var confirmed = await Dialog.ConfirmAsync(deleteButton, "Delete project?",
    "This permanently deletes Atlas redesign. This cannot be undone.", "Delete", isDanger: true);
status.Text = confirmed ? "Deleted" : "Kept";
```

`Dialog.ShowAsync` takes custom content and a list of `DialogAction` values, and returns the result of the
clicked action.

### Step titles

Change `Title` and `Description` while the dialog is open. The new copy crossfades in place, so a step flow needs
no second dialog.

```csharp
dialog.Title = "Choose a role";
dialog.Description = "Editors can change content; viewers only read.";
```

### Sizes

`Size` sets the panel width: `Small` is 360, `Medium` is 440 (the Arc default) and `Large` is 560. A panel never
exceeds the window minus a 16px gutter.

```xml
<arc:Dialog Title="Workspace limits" Size="Large" IsOpen="{Binding ShowLimits}">
    <TextBlock Text="Limits apply per workspace." />
</arc:Dialog>
```

### Closing and results

`Closing` is raised before a user dismissal and can be cancelled. `Closed` reports the reason and the result.

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

## API reference

<!-- api: Dialog -->

## Accessibility

- The panel takes its name from `Title` and its help text from `Description`.
- The close button is named "Close dialog" by default. Change it with `CloseButtonName`.
- Focus moves into the dialog when it opens and Tab and Shift+Tab stay inside it.
- Escape closes the dialog and returns focus to the control that opened it, unless `CloseOnEscape` is false.

## Motion

- The scrim fades in while the panel rises 8px from a scale of 0.96 on a smooth spring. Closing is shorter and
  starts from wherever the entrance is.
- A changed title or description rises in with a soft blur.
- Reduced motion keeps only a short opacity fade. Off shows or removes the dialog at once.

## Related

- `Drawer` for long forms and detail panels that keep the page in view.
- `BottomSheet` for mobile-first tasks that rest at a peek height.
- `Popover` for interactive content that does not block the page.
- `Alert` for a message that stays in the page, and `Toast` for a short result.

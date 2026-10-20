---
title: Checkbox
summary: A binary choice with a precise, legible state.
---

## When to use

- An independent choice that is confirmed with a submit button, such as accepting terms.
- A parent row that reflects some of its children with the indeterminate state.
- A choice the user can make more than one of within a set.

## When not to use

- A setting that takes effect at once: use `Switch`.
- A choice of exactly one option from a set: use `RadioGroup`.
- A frequently toggled filter shown as chips: use `ChipGroup`.

## Usage

The label is the content of the control. `Description` adds a line of help text under the label.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Checkbox Content="Accept terms and conditions"
                  Description="You agree to our Terms of Service and Privacy Policy." />
</UserControl>
```

### Required and invalid

`IsRequired` marks the field as required in a form. `IsInvalid` shows the error state, with the box border in the
danger color while the box is unchecked.

```xml
<arc:Checkbox Content="Accept terms and conditions"
              Description="Check this box to continue."
              IsRequired="True"
              IsInvalid="True" />
```

### Indeterminate parent

`CheckedState` is an enum with `Unchecked`, `Checked` and `Indeterminate`. `IsChecked` is the same value as a
nullable boolean, where `null` means indeterminate. Set the parent to indeterminate while some children are checked.

```xml
<arc:Checkbox Content="All channels" CheckedState="Indeterminate" />
```

```csharp
var all = new Checkbox { Content = "All channels" };
var children = new[]
{
    new Checkbox { Content = "Email", IsChecked = true },
    new Checkbox { Content = "Push notifications" },
    new Checkbox { Content = "SMS" }
};

void UpdateParent()
{
    var on = children.Count(c => c.IsChecked == true);
    all.CheckedState = on == 0 ? CheckboxState.Unchecked
        : on == children.Length ? CheckboxState.Checked
        : CheckboxState.Indeterminate;
}

foreach (var child in children)
{
    child.CheckedStateChanged += (_, _) => UpdateParent();
}
```

To make a click on the parent set every child, handle `CheckedStateChanged` on the parent and set each child's
`IsChecked` to the new state. Skip the indeterminate state in that handler.

### Three-state cycle

`IsThreeState="True"` makes a click cycle through unchecked, checked and indeterminate, as the Avalonia `CheckBox` does.

```xml
<arc:Checkbox Content="Three-state cycle" IsThreeState="True" />
```

### Events

`CheckedStateChanged` reports the old and the new state. `IsCheckedChanged` is raised for each change of `IsChecked`.

```csharp
checkbox.CheckedStateChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldState} -> {e.NewState}");
};
```

## API reference

<!-- api: Checkbox -->

## Accessibility

- The label is the checkbox's name. Without a label, set `AutomationProperties.Name`; the name falls back to "Checkbox".
- `Description` is the help text. Assistive technology reads it after the name.
- Space toggles the box. Enter does not, as in the WAI-ARIA checkbox pattern.
- The indeterminate state is reported as a mixed state. The drawn check and dash are not exposed separately.
- Keyboard focus draws a 2px outline offset by 2px. Pointer focus does not.

## Motion

- The fill grows in from a slightly smaller size when the box is checked, and fades out quickly when it is cleared.
- The check draws in. Switching to indeterminate morphs the check into a dash, and back again.
- Pressing the box scales it down slightly, and release springs it back.
- Reduced motion applies every state change at once and removes the press scale. Off does the same.

## Related

- `Switch` for a setting that takes effect at once.
- `RadioGroup` for choosing exactly one option from a set.
- `ChipGroup` for a set of filters the user toggles often.

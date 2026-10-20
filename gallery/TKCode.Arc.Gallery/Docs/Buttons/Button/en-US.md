---
title: Button
summary: A clear, responsive action with quiet secondary states.
---

## When to use

- Any single action on a page, form or dialog, such as Save, Continue or Cancel.
- Actions whose label changes in place, such as Save to Saved, while the width springs to fit.
- Short asynchronous work that needs a spinner while the button keeps its keyboard focus.

## When not to use

- A button that must show pending and success states of its own: use `ActionButton`.
- Two to five close alternatives for the same action: use `SplitButton`.
- A destructive action that needs a deliberate press: use `HoldToConfirm` from Gestures.
- A value that stays selected: use `SegmentedControl` from Toggles.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Content="Save changes" Variant="Primary" />
</UserControl>
```

### Variants

`Variant` sets the visual weight. Use one `Primary` action per surface. `Danger` marks destructive actions with a
danger outline.

```xml
<arc:Button Content="Save changes" Variant="Primary" />
<arc:Button Content="Cancel" Variant="Secondary" />
<arc:Button Content="Learn more" Variant="Ghost" />
<arc:Button Content="Delete project" Variant="Danger" />
```

### Sizes

`Size` sets the minimum height: `Small` is 36, `Medium` is 44 (the default) and `Large` is 50. The size does not
change with the window width.

```xml
<arc:Button Content="Small" Size="Small" Variant="Secondary" />
<arc:Button Content="Large" Size="Large" Variant="Secondary" />
```

### Loading and label morph

Set `IsLoading` to show a spinner. The button keeps keyboard focus, but clicks and key presses are ignored until
the work ends. Change `Content` to swap the label; the new text rises in and the width springs to fit.

```xml
<arc:Button Content="{Binding SaveLabel}"
            IsLoading="{Binding IsSaving}"
            Variant="Primary" />
```

```csharp
save.Click += async (_, _) =>
{
    save.IsLoading = true;
    await SaveAsync();
    save.IsLoading = false;
    save.Content = "Saved";
};
```

### Icon-only button

An icon-only button has no visible text, so give it an accessible name.

```xml
<arc:Button Size="Small" Variant="Ghost"
            AutomationProperties.Name="Close">
    <TextBlock Text="×" />
</arc:Button>
```

### Disabled

Set `IsEnabled` to `False`. Outlined variants keep their shape, so an unavailable action still reads as a button.

```xml
<arc:Button Content="Primary" IsEnabled="False" />
<arc:Button Content="Secondary" Variant="Secondary" IsEnabled="False" />
```

## API reference

<!-- api: Button -->

## Accessibility

- The button has the Button role. An icon-only button needs `AutomationProperties.Name`.
- While `IsLoading` is set, the button stays focusable and reports itself as busy and unavailable.
- Enter and Space activate the button. Clicks and key presses are swallowed while loading.
- Keyboard focus shows a 2px focus ring offset by 2px. Pointer focus draws no ring.

## Motion

- A press scales the content down slightly and a release springs it back. Narrow icon buttons scale a little more.
- Label changes rise in from a soft blur, and the width springs to the new label. The spinner turns continuously
  while loading.
- Reduced motion removes the press scale and the width spring. Labels and the spinner fade with a short crossfade,
  and the spinner stops turning. Off replaces the label and the state at once.

## Related

- `ActionButton` for an asynchronous submit that shows pending and success on the button.
- `SplitButton` for a primary action with a menu of nearby alternatives.
- `ButtonGroup` for several related actions joined into one surface.
- `CopyButton` for copying a value with a confirmation.

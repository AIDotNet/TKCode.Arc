---
title: Text shimmer
summary: Show ongoing work with a calm light across the words.
---

## When to use

- A status label while the app is busy, such as "Thinking" or "Generating summary".
- A status line that changes while the work moves on, such as "Reading 14 files" and "Drafting a plan".
- A label that settles into a finished state when the work ends.

## When not to use

- Placeholder content while a page loads: use `Skeleton`.
- Progress that can be measured: use `Progress`, which shows the amount done.
- A label that changes letter by letter without a sweep: use `TextMorph`.
- Long text or several lines: the sweep is for one short line only.

## Usage

The sweep runs while `IsActive` is `true`, which is the default. Set `IsActive` to `false` when the work is done. The
text then settles to a solid color.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextShimmer Text="Thinking" />
</UserControl>
```

### Changing the text

A new `Text` value rises in place while the line springs to its new width. Keep `IsActive` on while the steps change.

```csharp
summary.IsActive = true;
summary.Text = "Drafting a plan";
```

### Finishing

Set `IsActive` to `false` instead of removing the control. The sweep glides off and the text becomes solid, so the
line does not jump.

```csharp
summary.Text = "Summary ready";
summary.IsActive = false;
```

### Sweep speed

`SweepDuration` sets the time of one pass across the text. The default is 1.8 seconds.

```xml
<arc:TextShimmer Text="Indexing the workspace"
                 SweepDuration="0:0:3" />
```

### Size

The text uses the font properties of `TextElement`. A larger size works for a status heading.

```xml
<arc:TextShimmer Text="Generating summary" FontSize="22" />
```

## API reference

<!-- api: TextShimmer -->

## Accessibility

- The text is the control's name and stays readable at all times.
- The control reports a busy state while `IsActive` is `true`, through `AutomationProperties.ItemStatus`.
- The sweep is decorative. Nothing is read out letter by letter.
- The control does not announce its changes. Use a live region on a parent element to announce the finished state.
- The control is not focusable and has no keyboard interaction.

## Motion

- A soft band of the text color crosses a muted base. Each pass takes `SweepDuration`, then the band rests briefly
  before the next pass.
- The sweep runs only while the control is active, near the viewport and visible. It resumes where it stopped.
- A new label rises in from a soft blur.
- Setting `IsActive` to `false` glides the band off, and the text becomes solid.
- Reduced motion stops the sweep and swaps the label with a plain fade. Off swaps the label at once.

## Related

- `Skeleton` for placeholder content while a page loads.
- `Progress` for work that can be measured.
- `TextMorph` for a short label that changes state.

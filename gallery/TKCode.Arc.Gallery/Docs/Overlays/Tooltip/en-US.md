---
title: Tooltip
summary: Short supporting text for unfamiliar controls.
---

<Demo name="Sides" />

## When to use

`Tooltip` gives a short label to a control that does not explain itself on screen. It is attached to the control with
`arc:Tooltip.Tip`, so the trigger keeps its own layout and no extra element is added to the page. The label appears on
hover or keyboard focus, and it disappears as soon as the reader presses the control or moves away.

- **Icon-only buttons in a toolbar.** A document or order toolbar with a printer, a link or an archive icon needs a label
  such as "Print packing slip" so the reader knows what each icon does before pressing it.
- **Truncated text in a table or a card.** An order note or a product name cut off with an ellipsis can show its full
  text on hover, so the reader does not have to open the record to read it.
- **Short help beside a setting.** A short hint on a field or a toggle, such as "Refunds open after the payment settles",
  explains a rule without taking space from the form.
- **A disabled control that needs an explanation.** A disabled Refund button can explain why it is unavailable while the
  reader points at it.

## When not to use

- **Links, buttons with actions or inputs the reader must use:** use `Popover`. Tooltip content cannot be interacted
  with, so a link or a button inside it is never reachable.
- **A rich preview of a person or a link:** use `HoverCard`, which shows a profile or a summary with a layout for
  several facts.
- **Information that touch users must see:** show it on the page, not in a tooltip. Tooltips open on pointer hover and
  keyboard focus, so a touch user may never see the text.
- **Errors and validation messages:** use the field's own error row, such as the `ErrorMessage` of `Input`. A tooltip
  disappears on the next press and cannot be found by a reader who did not point at the field.
- **Explanations longer than a sentence or two:** use inline helper text or a `Popover` with a heading. The bubble is
  limited to 240 px wide and is meant for a label, not a paragraph.

## Usage

A tooltip is an attached property. Set `arc:Tooltip.Tip` on any control; the text appears on hover or keyboard focus.
The tooltip is not a child of the control, so it is safe to add to a button, a text block, a check box or an icon in a
template.

### Anatomy

1. **Trigger**: the control that carries `arc:Tooltip.Tip`. It keeps its own size and layout; the tooltip never changes
   it.
2. **Bubble**: a rounded box with the inverse of the page's text colors, so it reads as a dark label on a light page and
   as a light label on a dark page. It has a 1 px hairline border made by mixing 14% of the background into the
   foreground, which keeps its edge visible on both themes.
3. **Text**: the tip content in the small type size, regular weight. Strings and numbers wrap inside the bubble, whose
   maximum width is 240 px with 16 px of padding on each side and 12 px above and below the text.
4. **Gap**: the bubble sits 8 px away from the trigger on the side set by `Side`, and it rises 3 px into place when it
   opens.

### Basic

```xml
<arc:Button Content="Copy" arc:Tooltip.Tip="Copy link" />
```

Use a short verb phrase for an icon-only button, because the tooltip is the only visible label. A text button that
already says what it does needs no tooltip; a tooltip that repeats the button text adds delay and noise.

### Side

`arc:Tooltip.Side` sets the side of the trigger the bubble opens on: `Top` (the default), `Bottom`, `Left` or `Right`.
Choose the side that keeps the bubble clear of the content the reader is looking at.

```xml
<arc:Button Variant="Secondary" Content="Share" arc:Tooltip.Tip="Share with your team"
            arc:Tooltip.Side="Bottom" />
```

Use `Top` for controls in the middle of a page, `Bottom` for controls in a page header that sit against the top edge of
the window, and `Left` or `Right` for controls at the edge of a vertical toolbar, where a top tooltip would cover the
toolbar above it.

### Binding the text

`Tip` accepts any object. Bind it when the text comes from the view model, so a label can change with the state of
the application.

```xml
<arc:Button Content="Save" arc:Tooltip.Tip="{Binding SaveHint}" />
```

### Changing the text and the delay

Change the text in code while the tooltip is open. Strings crossfade and the bubble resizes to fit. Set
`ShowDelay` to change the wait before the first tooltip opens; the default is 250 ms, and a tooltip opened at once
while another one is warm ignores it.

<Demo name="LiveText" />

```csharp
Tooltip.SetTip(save, "Saved just now");
Tooltip.SetShowDelay(save, TimeSpan.FromMilliseconds(400));
```

Change the text only when the state the reader is looking at changes, such as a button that turns from "Save draft" into
"Saved just now" after a save. Do not change it every second; a tooltip that keeps rewriting itself is hard to read.

### Showing from code

`Tooltip.SetIsOpen` shows or hides the tooltip on an element at once, without `ShowDelay`. Use it to explain a control
that the reader has just changed, for example a refund button that becomes available after a payment settles. Call it
with `false` to hide the tooltip again. The tooltip still closes on its own when the pointer leaves, the control is
pressed or the focus moves away, so do not keep it open from code after the reader has moved on.

```csharp
Tooltip.SetIsOpen(refund, true);
```

### Binding to a view model

Bind the tip to a view model property when the explanation depends on data. A disabled control is a common case: the
tip explains why the action is unavailable, and it is shown while the pointer is over the control.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

public partial class OrderRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _canRefund;

    [ObservableProperty]
    private string _refundHint = "Refunds open after the payment settles.";
}
```

```xml
<arc:Button Content="Refund"
            IsEnabled="{Binding CanRefund}"
            arc:Tooltip.Tip="{Binding RefundHint}" />
```

A disabled control does not open a tooltip from hover or focus by itself; a tooltip on a disabled control opens only
while the pointer is over it. Keep the reason in the tip, so the reader learns what would enable the control. When
the reason changes (for example, "Refunds open after the payment settles" becomes "Refund requested on 2026-10-18"),
update the bound property and the tooltip follows.

### Queueing and timing

The first tooltip waits `ShowDelay` (250 ms by default) after the pointer enters the control or a Tab focus lands on
it. While any tooltip is open, and for 300 ms after the last one closed, the next tooltip opens at once with a fade
only. The shared timing lets a reader move along a toolbar and read each label without waiting at every button, and
the first wait keeps tooltips off the screen while the pointer simply crosses the window.

Focus by mouse does not open a tooltip; a click on a control only closes one. A press on the trigger closes the
tooltip at once, so it does not sit over a menu the press opened.

### Example: order row actions in a fulfilment console

A row in a fulfilment console with a truncated customer note and three actions. The note shows its full text in a
tooltip, because the row is too narrow to show it in full. The buttons carry text labels, so the tooltip is used only
where the label is not enough: the refund button is disabled, and its tip explains the rule that blocks it.

```xml
<StackPanel Width="560" Spacing="12">
    <Grid ColumnDefinitions="*,Auto,Auto,Auto" ColumnSpacing="8">
        <StackPanel Spacing="2" VerticalAlignment="Center">
            <TextBlock Text="ord_2026_0412 · Acme Analytics"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Leave the parcel with the concierge at the rear entrance. Ring twice."
                       MaxWidth="320"
                       TextTrimming="CharacterEllipsis"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}"
                       arc:Tooltip.Tip="Leave the parcel with the concierge at the rear entrance. Ring twice." />
        </StackPanel>
        <arc:Button Grid.Column="1" Variant="Ghost" Content="Print label"
                    VerticalAlignment="Center"
                    arc:Tooltip.Tip="Print the shipping label" />
        <arc:Button Grid.Column="2" Variant="Ghost" Content="Copy tracking"
                    VerticalAlignment="Center"
                    arc:Tooltip.Tip="Copy the tracking number" />
        <arc:Button Grid.Column="3" Variant="Secondary" Content="Refund"
                    VerticalAlignment="Center"
                    IsEnabled="False"
                    arc:Tooltip.Tip="Refunds open after the payment settles." />
    </Grid>
</StackPanel>
```

The note is truncated because the row is fixed in width and the full text would push the actions out of the row. The
tooltip repeats the full text rather than a summary, since the reader came to see the whole note. The disabled refund
button keeps its tip, because a disabled control gives no other clue about why it cannot be used.

### Content guidelines

- **Length**: one short phrase or sentence. The bubble is 240 px wide at most, so a tip longer than about 60 characters
  wraps onto several lines and is harder to scan.
- **Form**: a verb phrase for an action ("Copy link", "Print the shipping label"), or a noun phrase for a state ("Draft
  saved 2 minutes ago"). Use sentence case and no exclamation marks.
- **Period**: a single sentence can end with a period, and a phrase needs none. Pick one style per application and keep
  it.
- **Shortcuts**: include a keyboard shortcut only when the application defines it. Do not invent one for the tip.
- **Disabled controls**: say what would enable the control, not only that it is unavailable.

| Instead of | Write |
|---|---|
| Click this button to copy the link to your clipboard. | Copy link |
| Delete | Delete draft |
| Important!!! Read this first. | Expires in 24 hours |
| Not available | Refunds open after the payment settles. |
| Save | Save draft |

### Best practices

**Do**

- Give every icon-only control a visible tooltip and an `AutomationProperties.Name`. The tooltip is for sighted readers
  who point at the control; the name is what a screen reader announces, and it is not replaced by the tooltip.
- Keep the tip to the one thing the reader needs to decide. A label that names the action is more useful than a
  description of the whole screen.
- Use the same side for tooltips in the same toolbar, so the reader's eye learns where the label appears.
- Put the full text of a truncated label in the tip, so the reader can read it without opening the record.
- Use `Bottom` in a page header and `Left` or `Right` in a vertical toolbar, so the bubble does not cover the control the
  reader is pointing at.

**Avoid**

- Interactive content in a tip, such as a link or a button. Tooltip content cannot be reached with the keyboard, so the
  action is lost for anyone who does not use a pointer.
- A tooltip as the only explanation of a required field or an error. It disappears on the next press, so the reader may
  never see it; use the field's own helper or error row.
- Tips on every control of a page. A page covered in tooltips reads as noise; add them where the icon or the truncated
  text needs them.
- Relying on a tooltip on touch. Touch users may not see it, so put anything they must read on the page.
- Repeating the button text in the tip. "Copy" with the tip "Copy" adds a delay and no information.

### Common questions

**Why does the tooltip not show on a disabled button?** A disabled control does not open a tooltip from hover or focus
by itself. The tooltip opens only while the pointer is over the disabled control, so it can explain the reason without
the control taking focus.

**Why does the tooltip disappear when I click the button?** A press on the trigger closes the tooltip at once, so it does
not sit over the menu, dialog or panel the press opened.

**Can the tooltip be dismissed with Escape?** The tooltip does not handle Escape itself. It closes when the focus moves
away or the pointer leaves.

**Does a tooltip work on a touch screen?** Tooltips open on hover and keyboard focus, which touch screens do not
provide reliably. Keep any information that a touch user needs on the page.

**Can I show a tooltip on a control that is not focusable?** It opens on pointer hover, and `Tooltip.SetIsOpen` opens it
from code. A control that cannot take focus still needs a name for a screen reader, so set `AutomationProperties.Name`.

## API reference

<!-- api: Tooltip -->

The side of the trigger is set with `TooltipSide`, which has four members.

| Member | Meaning |
|---|---|
| `Top` | The bubble opens above the trigger (the default) |
| `Bottom` | The bubble opens below the trigger |
| `Left` | The bubble opens to the left of the trigger |
| `Right` | The bubble opens to the right of the trigger |

## Accessibility

- While the tooltip is open, its text is written to the trigger's help text.
- A tooltip is not a substitute for a name. An icon-only trigger still needs its own `AutomationProperties.Name`.
- A tooltip opens on hover and on keyboard focus. A Tab focus opens it after the delay, and moving the focus away closes
  it.
- The bubble is not focusable and does not take pointer input, so it never takes focus from the control it describes.
- The bubble contains no interactive elements, so keyboard users can read it without losing focus.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` onto the trigger | Opens the tooltip after `ShowDelay`, or at once while another tooltip is warm |
| `Tab` away from the trigger | Closes the tooltip |

## Motion

- The first tooltip waits 250ms, then rises 3px from its trigger while it scales in from 0.97.
- When the pointer moves to a neighbouring trigger, or the last tooltip closed less than 300ms ago, the next tooltip
  opens at once with a fade only.
- When the text changes while the tooltip is open, the new words rise in with a soft blur and the bubble springs to
  its new size.
- Closing is an instant fade with the bubble shrinking to 0.98.
- Reduced motion removes the movement and keeps a short fade. Off shows or removes the tooltip at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First tooltip opens | Waits for `ShowDelay`, then rises 3px and scales in from 0.97 | Short fade only | Shown at once |
| Next tooltip while warm | Opens at once with a fade only | Short fade only | Shown at once |
| Text changes while open | New words rise in with a soft blur; the bubble springs to its new size | Short fade only | Shown at once |
| Tooltip closes | Instant fade, the bubble shrinks to 0.98 | Short fade | Removed at once |

## Related

- `Popover` for interactive content anchored to a control.
- `HoverCard` for a rich, read-only preview of a person or a link.

| Component | Choose it when |
|---|---|
| `Popover` | The content has a link, a button or a form, and a click opens it |
| `HoverCard` | A read-only preview of a person or a link is wanted on hover |
| `Input` | A message about a field must stay on the page until the field is fixed |
| `Dialog` | The reader must read or confirm something before continuing |
